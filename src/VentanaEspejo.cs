namespace HdmiMirror;

public enum ModoClics
{
    /// <summary>
    /// La ventana deja pasar los clics tal cual. Correcto cuando el supervisor
    /// mira el CRISTAL: el reflejo re-invierte la imagen, asi que lo que ve esta
    /// en su sitio real y no hay nada que corregir.
    /// </summary>
    Directo,

    /// <summary>
    /// La ventana intercepta el clic, calcula donde esta de verdad el boton
    /// (al otro lado de la pantalla) e inyecta alli un clic real del sistema.
    /// Necesario cuando el supervisor mira el PANEL FISICO, donde la imagen se
    /// ve espejada y por tanto los botones estan en el lado contrario.
    /// </summary>
    EspejadoInyectado
}

/// <summary>
/// La ventana que se ve en el monitor del prompter.
///
/// Es una ventana normal salvo por cuatro detalles, y esos cuatro detalles son
/// justo lo que OBS no te deja hacer:
///   - WS_EX_TRANSPARENT   -> los clics la atraviesan y llegan a la dealer app.
///   - WS_EX_NOACTIVATE    -> nunca roba el foco del teclado.
///   - Topmost reafirmado  -> se mantiene por encima aunque la dealer app
///                            tambien sea always-on-top.
///   - WDA_EXCLUDEFROMCAPTURE -> es invisible para RustDesk / OBS / capturas,
///                            asi que los supervisores remotos ven la dealer app
///                            limpia y sin espejar, sin monitores virtuales.
/// </summary>
internal sealed class VentanaEspejo : Form
{
    private readonly Capturador _capturador = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private int _ticksDesdeTopmost;
    private RatonEspejado _raton;

    /// <summary>Se rellena si el hook de raton no se pudo instalar.</summary>
    public string ErrorRaton { get; private set; } = "";

    public ModoEspejo Modo { get; set; } = ModoEspejo.Horizontal;
    public FuenteCaptura Fuente { get; set; } = FuenteCaptura.Monitor;
    public Rectangle ZonaOrigen { get; set; }
    public IntPtr VentanaOrigen { get; set; } = IntPtr.Zero;

    /// <summary>
    /// Titulo de la ventana de origen. Si su handle muere (la dealer app se
    /// reinicio), se busca una ventana nueva con este mismo titulo y el espejo
    /// se re-engancha solo en vez de quedarse en blanco.
    /// </summary>
    public string TituloOrigen { get; set; } = "";

    public bool MostrarCursor { get; set; } = true;

    /// <summary>"" si el ultimo frame capturo bien; si no, el motivo.</summary>
    public string EstadoCaptura { get; private set; } = "";

    private int _ticksDesdeBusqueda;
    private string _errorPintado;

    private bool _clickThrough = true;
    private bool _ocultarDeCapturas = true;

    /// <summary>Como se tratan los clics del raton. Ver el enum ModoClics.</summary>
    public ModoClics Clics { get; set; } = ModoClics.Directo;

    public string UltimoError => _capturador.UltimoError;

    public VentanaEspejo(Screen monitorDestino, int fps)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Bounds = monitorDestino.Bounds;
        BackColor = Color.Black;
        TopMost = true;
        Text = "HdmiMirror - Salida";

        // Pintamos nosotros con GDI en cada tick; que WinForms no toque el fondo.
        SetStyle(ControlStyles.Opaque, true);
        SetStyle(ControlStyles.AllPaintingInWmPaint, true);
        SetStyle(ControlStyles.UserPaint, true);

        _timer.Interval = Math.Max(1, 1000 / Math.Max(1, fps));
        _timer.Tick += (s, e) => Render();
    }

    /// <summary>No debe activarse al mostrarse, para no robarle el foco a nadie.</summary>
    protected override bool ShowWithoutActivation => true;

    /// <summary>
    /// Los estilos se declaran AQUI y no solo con SetWindowLong despues de crear
    /// la ventana. WinForms recrea el handle por su cuenta en varias situaciones
    /// (TopMost, ShowInTaskbar, cambios de estilo...), y en cada recreacion se
    /// perderia todo lo aplicado a mano. CreateParams sobrevive a eso.
    /// </summary>
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED;

            if (DebeSerTransparente)
                cp.ExStyle |= Native.WS_EX_TRANSPARENT;

            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        AplicarEstilos();
        AplicarOcultarDeCapturas();
        ReafirmarTopmost();
        PrepararRaton();
        _timer.Start();
    }

    public bool ClickThrough
    {
        get => _clickThrough;
        set
        {
            _clickThrough = value;
            if (IsHandleCreated) AplicarEstilos();
        }
    }

    public bool OcultarDeCapturas
    {
        get => _ocultarDeCapturas;
        set
        {
            _ocultarDeCapturas = value;
            if (IsHandleCreated) AplicarOcultarDeCapturas();
        }
    }

    /// <summary>
    /// La ventana es click-through SIEMPRE, en los dos modos. En modo espejado
    /// los clics se recolocan antes con un hook de raton, no tocando esta ventana.
    /// Asi no hay ninguna carrera entre cambiar estilos e inyectar clics.
    /// </summary>
    private bool DebeSerTransparente => _clickThrough;

    private void AplicarEstilos()
    {
        AplicarTransparencia(DebeSerTransparente);
    }

    private void AplicarTransparencia(bool transparente)
    {
        int estilo = Native.GetWindowLongCompat(Handle, Native.GWL_EXSTYLE);

        estilo |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED;

        if (transparente)
            estilo |= Native.WS_EX_TRANSPARENT;
        else
            estilo &= ~Native.WS_EX_TRANSPARENT;

        Native.SetWindowLongCompat(Handle, Native.GWL_EXSTYLE, estilo);

        // Al activar WS_EX_LAYERED la ventana se queda invisible hasta que se le
        // fija una opacidad. 255 = totalmente opaca.
        Native.SetLayeredWindowAttributes(Handle, 0, 255, Native.LWA_ALPHA);
    }

    /// <summary>Instala el hook que recoloca los clics, si toca.</summary>
    private void PrepararRaton()
    {
        if (Clics != ModoClics.EspejadoInyectado)
            return;

        _raton = new RatonEspejado(Bounds, Modo);

        if (!_raton.Instalar())
        {
            _raton.Dispose();
            _raton = null;
            ErrorRaton = "No se pudo instalar el hook de raton. " +
                         "Prueba a ejecutar HdmiMirror como administrador.";
        }
    }

    private void AplicarOcultarDeCapturas()
    {
        uint afinidad = _ocultarDeCapturas ? Native.WDA_EXCLUDEFROMCAPTURE : Native.WDA_NONE;
        Native.SetWindowDisplayAffinity(Handle, afinidad);
    }

    /// <summary>
    /// Dentro de la banda "topmost" hay su propio orden, y gana la ultima ventana
    /// que reclama el sitio. Por eso hay que reafirmarlo periodicamente: asi la
    /// dealer app puede seguir siendo always-on-top sin taparnos.
    /// </summary>
    private void ReafirmarTopmost()
    {
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    private void Render()
    {
        if (!IsHandleCreated || IsDisposed)
            return;

        // Una vez por segundo aprox., no en cada frame: reafirmarlo 30 veces por
        // segundo puede provocar parpadeo si otra app hace lo mismo.
        if (++_ticksDesdeTopmost * _timer.Interval >= 1000)
        {
            _ticksDesdeTopmost = 0;
            ReafirmarTopmost();
        }

        if (Fuente == FuenteCaptura.Ventana)
            ReengancharSiHaceFalta();

        bool capturado = Fuente == FuenteCaptura.Ventana
            ? _capturador.CapturarVentana(VentanaOrigen, MostrarCursor)
            : _capturador.CapturarMonitor(ZonaOrigen, MostrarCursor);

        if (!capturado)
        {
            // Antes esto salia sin pintar nada y la pantalla se quedaba en
            // blanco sin explicacion. Ahora el motivo se pinta en la propia
            // pantalla (y el panel lo ensena via EstadoCaptura).
            EstadoCaptura = _capturador.UltimoError;
            PintarError(EstadoCaptura);
            return;
        }

        EstadoCaptura = "";
        _errorPintado = null;

        IntPtr dc = Native.GetDC(Handle);
        if (dc == IntPtr.Zero)
            return;

        try
        {
            _capturador.Presentar(dc, ClientSize.Width, ClientSize.Height, Modo);
        }
        finally
        {
            Native.ReleaseDC(Handle, dc);
        }
    }

    /// <summary>
    /// Si el handle de la ventana de origen ha muerto, busca (como mucho una
    /// vez por segundo) otra ventana con el mismo titulo y se re-engancha.
    /// Cubre el caso tipico de reiniciar la dealer app con el espejo en marcha.
    /// </summary>
    private void ReengancharSiHaceFalta()
    {
        if (Native.IsWindow(VentanaOrigen) || string.IsNullOrEmpty(TituloOrigen))
            return;

        if (++_ticksDesdeBusqueda * _timer.Interval < 1000)
            return;
        _ticksDesdeBusqueda = 0;

        IntPtr encontrada = IntPtr.Zero;
        Native.EnumWindows((hwnd, lp) =>
        {
            if (!Native.IsWindowVisible(hwnd))
                return true;

            int largo = Native.GetWindowTextLength(hwnd);
            if (largo <= 0)
                return true;

            var sb = new System.Text.StringBuilder(largo + 1);
            Native.GetWindowText(hwnd, sb, sb.Capacity);

            if (sb.ToString() == TituloOrigen)
            {
                encontrada = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        if (encontrada != IntPtr.Zero)
            VentanaOrigen = encontrada;
    }

    /// <summary>
    /// Fondo negro con el motivo del fallo. Solo repinta cuando el mensaje
    /// cambia, para no parpadear.
    /// </summary>
    private void PintarError(string mensaje)
    {
        if (mensaje == _errorPintado)
            return;
        _errorPintado = mensaje;

        try
        {
            using var g = CreateGraphics();
            g.Clear(Color.Black);

            using var fuente = new Font("Segoe UI", 16F);
            var zona = new RectangleF(60, 60, ClientSize.Width - 120, ClientSize.Height - 120);
            using var formato = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };

            g.DrawString("HdmiMirror — sin imagen\n\n" + mensaje +
                         "\n\nCtrl+Alt+M para el espejo · Ctrl+Alt+D diagnostico",
                         fuente, Brushes.White, zona, formato);
        }
        catch
        {
            // Pintar el aviso nunca debe tirar el bucle de render.
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        // A proposito vacio: el fondo lo pinta Render() entero cada tick.
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        _raton?.Dispose();
        _raton = null;
        _capturador.Dispose();
        base.OnFormClosed(e);
    }
}
