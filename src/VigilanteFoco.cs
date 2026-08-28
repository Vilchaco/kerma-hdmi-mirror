namespace HdmiMirror;

/// <summary>
/// Mantiene el foco de teclado en la dealer app (el escaner Datalogic teclea
/// el codigo de barras en la ventana que tenga el foco, como si fuera un
/// teclado; siempre que la dealer app pierda el foco, hay que devolverselo).
///
/// Antes esto era un sondeo cada 500 ms. Ahora es por eventos: Windows avisa
/// (SetWinEventHook) solo cuando la ventana en primer plano cambia, asi que
/// mientras nadie toca nada el coste es exactamente cero.
///
/// Y cuando el foco NO se puede recuperar (tipicamente porque la otra ventana
/// corre como administrador y la nuestra no), antes se reintentaba en bucle
/// cada 500 ms para siempre: restaurar ventana + AttachThreadInput + fallo,
/// dos veces por segundo, sin fin. Ese era el consumo visible del vigilante.
/// Ahora tras 4 fallos seguidos pasa a reintentar cada 10 segundos.
/// </summary>
internal sealed class VigilanteFoco : IDisposable
{
    // El delegate se guarda en un campo a proposito: si solo se pasara a la
    // API, el recolector de basura se lo llevaria y el hook moriria solo.
    private readonly Native.WinEventProc _callback;
    private IntPtr _hook = IntPtr.Zero;

    // Solo corre mientras hay un reintento pendiente tras un fallo.
    private readonly System.Windows.Forms.Timer _reintento = new();

    // Red de seguridad de baja frecuencia por si un evento se perdiera.
    // Una llamada trivial cada 5 s; solo actua si de verdad falta el foco.
    private readonly System.Windows.Forms.Timer _latido = new();

    private int _recuperaciones;
    private int _fallosSeguidos;

    /// <summary>Ventana que debe conservar el foco (la dealer app).</summary>
    public IntPtr Objetivo { get; set; } = IntPtr.Zero;

    /// <summary>Pausado por el usuario con Ctrl+Alt+F, para poder usar otras apps.</summary>
    public bool Pausado { get; set; }

    /// <summary>Cuantas veces ha tenido que devolver el foco. Util para diagnosticar.</summary>
    public int Recuperaciones => _recuperaciones;

    public bool Activo => _hook != IntPtr.Zero;

    /// <summary>true mientras esta en espera larga tras varios fallos seguidos.</summary>
    public bool EnEspera { get; private set; }

    public VigilanteFoco()
    {
        _callback = AlCambiarForeground;

        _reintento.Tick += (s, e) => { _reintento.Stop(); Recuperar(); };

        _latido.Interval = 5000;
        _latido.Tick += (s, e) =>
        {
            if (!Pausado && !EnEspera && Objetivo != IntPtr.Zero)
                Recuperar();
        };
    }

    public void Arrancar()
    {
        if (_hook != IntPtr.Zero)
            return;

        _recuperaciones = 0;
        _fallosSeguidos = 0;
        EnEspera = false;

        _hook = Native.SetWinEventHook(Native.EVENT_SYSTEM_FOREGROUND,
                                       Native.EVENT_SYSTEM_FOREGROUND,
                                       IntPtr.Zero, _callback, 0, 0,
                                       Native.WINEVENT_OUTOFCONTEXT);
        _latido.Start();
        Recuperar();
    }

    public void Parar()
    {
        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWinEvent(_hook);
            _hook = IntPtr.Zero;
        }

        _reintento.Stop();
        _latido.Stop();
        EnEspera = false;
    }

    private void AlCambiarForeground(IntPtr hook, uint evento, IntPtr hwnd,
                                     int idObjeto, int idHijo, uint hilo, uint tiempo)
    {
        if (Pausado || Objetivo == IntPtr.Zero)
            return;

        if (hwnd == Objetivo || MismoProceso(hwnd, Objetivo))
        {
            // El foco esta en la dealer app (en su ventana principal o en
            // cualquier otra ventana suya): todo bien, borron y cuenta nueva.
            _fallosSeguidos = 0;
            EnEspera = false;
            _reintento.Stop();
            return;
        }

        Recuperar();
    }

    private void Recuperar()
    {
        if (Pausado || Objetivo == IntPtr.Zero)
            return;

        if (!Native.IsWindow(Objetivo))
        {
            // La dealer app se ha cerrado. Dejamos de insistir.
            Parar();
            return;
        }

        IntPtr frente = Native.GetForegroundWindow();

        if (frente == Objetivo || MismoProceso(frente, Objetivo))
        {
            _fallosSeguidos = 0;
            EnEspera = false;
            return;
        }

        // Mientras el usuario esta en el propio HdmiMirror (configurando el
        // panel), no se le arrebata el foco: seria imposible usarlo. En cuanto
        // pase a otra ventana o a la dealer app, el vigilante sigue.
        if (EsVentanaPropia(frente))
            return;

        DevolverFoco(Objetivo);

        frente = Native.GetForegroundWindow();
        if (frente == Objetivo || MismoProceso(frente, Objetivo))
        {
            _recuperaciones++;
            _fallosSeguidos = 0;
            EnEspera = false;
            return;
        }

        // No se pudo. Reintentar en bucle rapido seria quemar CPU para nada:
        // unos pocos intentos espaciados y despues espera larga.
        _fallosSeguidos++;
        EnEspera = _fallosSeguidos >= 4;
        _reintento.Interval = EnEspera ? 10000 : 1000;
        _reintento.Start();
    }

    /// <summary>
    /// true si ambas ventanas pertenecen al mismo proceso. Una app puede tener
    /// varias ventanas de primer nivel (tipico en ruleta: principal + numero
    /// ganador + rueda); todas cuentan como "la dealer app tiene el foco".
    /// </summary>
    private static bool MismoProceso(IntPtr a, IntPtr b)
    {
        if (a == IntPtr.Zero || b == IntPtr.Zero)
            return false;

        Native.GetWindowThreadProcessId(a, out uint pa);
        Native.GetWindowThreadProcessId(b, out uint pb);
        return pa != 0 && pa == pb;
    }

    private static bool EsVentanaPropia(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return false;

        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == (uint)Environment.ProcessId;
    }

    /// <summary>
    /// Windows no deja que un proceso en segundo plano le robe el foco a otro
    /// porque si no cualquier app te secuestraria el teclado. El rodeo estandar
    /// es engancharse temporalmente a la cola de entrada del hilo que tiene el
    /// foreground: mientras estas enganchado, el sistema te considera parte de
    /// esa cola y te deja hacer el cambio.
    /// </summary>
    public static void DevolverFoco(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd))
            return;

        if (Native.IsIconic(hwnd))
            Native.ShowWindow(hwnd, Native.SW_RESTORE);

        IntPtr actual = Native.GetForegroundWindow();

        uint hiloActual = Native.GetWindowThreadProcessId(actual, IntPtr.Zero);
        uint hiloDestino = Native.GetWindowThreadProcessId(hwnd, IntPtr.Zero);
        uint hiloPropio = Native.GetCurrentThreadId();

        bool enganchadoA = false;
        bool enganchadoB = false;

        try
        {
            if (hiloActual != 0 && hiloActual != hiloPropio)
                enganchadoA = Native.AttachThreadInput(hiloPropio, hiloActual, true);

            if (hiloDestino != 0 && hiloDestino != hiloPropio && hiloDestino != hiloActual)
                enganchadoB = Native.AttachThreadInput(hiloPropio, hiloDestino, true);

            Native.SetForegroundWindow(hwnd);
            Native.SetFocus(hwnd);
        }
        finally
        {
            if (enganchadoA) Native.AttachThreadInput(hiloPropio, hiloActual, false);
            if (enganchadoB) Native.AttachThreadInput(hiloPropio, hiloDestino, false);
        }
    }

    /// <summary>
    /// Le quita el always-on-top a la dealer app.
    ///
    /// Con el vigilante en marcha ya no lo necesita, y quitarselo evita que se
    /// pelee con la ventana espejo por estar arriba (que es lo que causaria
    /// parpadeo, porque las dos reafirmarian su posicion una y otra vez).
    /// </summary>
    public static void QuitarAlwaysOnTop(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd))
            return;

        Native.SetWindowPos(hwnd, Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
    }

    public void Dispose()
    {
        Parar();
        _reintento.Dispose();
        _latido.Dispose();
    }
}
