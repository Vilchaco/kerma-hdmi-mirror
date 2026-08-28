using System.Text;

namespace HdmiMirror;

internal sealed class ItemVentana
{
    public string Texto = "";
    public IntPtr Hwnd = IntPtr.Zero;

    public override string ToString() => Texto;
}

internal sealed class ItemMonitor
{
    public string Texto = "";
    public int Indice;

    public override string ToString() => Texto;
}

/// <summary>Opcion del desplegable "Origen de imagen" (Ajustes avanzados).</summary>
internal sealed class ItemOrigenAvz
{
    public string Texto = "";
    public bool EsDealer;          // true = capturar la ventana de la dealer app
    public int IndiceMonitor = -1; // si no, el monitor completo

    public override string ToString() => Texto;
}

/// <summary>
/// Panel de control, rediseño "2.0": tres zonas con jerarquia real.
///
///   1. ESTADO   - tarjeta con el estado en color, el boton Iniciar/Parar y
///                 chips de salud (captura, foco, autoarranque). Lo diario.
///   2. MESA     - una sola decision por mesa: cual es la dealer app (alimenta
///                 captura Y foco a la vez), en que pantalla esta el prompter,
///                 y donde mira el supervisor. Lo de instalacion.
///   3. AVANZADO - plegado por defecto: espejo, FPS, origen alternativo,
///                 checkboxes, quitar topmost, autoarranque, diagnostico.
/// </summary>
internal sealed class PanelControl : Form
{
    // ---- zona 1: estado ----
    private readonly Panel _tarjeta = new();
    private readonly Label _dot = new();
    private readonly Label _lblEstadoTitulo = new();
    private readonly Label _lblEstadoSub = new();
    private readonly Button _btnPrincipal = new();
    private readonly Label _chipCaptura = new();
    private readonly Label _chipFoco = new();
    private readonly Label _chipAuto = new();
    private Color _bordeTarjeta;

    // ---- zona 2: mesa ----
    private readonly ComboBox _cmbDealer = new();
    private readonly Button _btnRefrescar = new();
    private readonly ComboBox _cmbPrompter = new();
    private readonly Button _btnCristal = new();
    private readonly Button _btnPanelF = new();
    private bool _mirarPanel;   // false = cristal (clics directos), true = panel fisico (recolocados)

    // ---- zona 3: avanzado ----
    private readonly Button _btnAvanzado = new();
    private readonly Panel _pnlAvanzado = new();
    private readonly ComboBox _cmbOrigenAvz = new();
    private readonly ComboBox _cmbEspejo = new();
    private readonly NumericUpDown _numFps = new();
    private readonly CheckBox _chkDevolverFoco = new();
    private readonly CheckBox _chkOcultar = new();
    private readonly CheckBox _chkCursor = new();
    private readonly CheckBox _chkAutoarranque = new();
    private readonly Button _btnQuitarTopmost = new();
    private readonly Button _btnDiagnostico = new();

    private bool _ajustandoAutoarranque;
    private bool _ajustandoFoco;

    // ---- infraestructura ----
    private readonly System.Windows.Forms.Timer _autoInicio = new();
    private bool _esperandoAuto;
    private DateTime _avisoHasta = DateTime.MinValue;

    private readonly ToolTip _pistas = new();
    private readonly VigilanteFoco _vigilante = new();
    private readonly System.Windows.Forms.Timer _refresco = new();

    private VentanaEspejo _espejo;
    private Ajustes _ajustes = new();

    // ---- la reticula ----
    private const int Ancho = 470;
    private const int ColumnaEtiqueta = 122;
    private const int AnchoControl = Ancho - ColumnaEtiqueta;
    private const int AltoBoton = 32;

    // ---- paleta (aprobada en la propuesta de rediseño) ----
    private static readonly Color Indigo      = Color.FromArgb(42, 29, 99);      // #2A1D63
    private static readonly Color Tinta       = Color.FromArgb(26, 26, 31);
    private static readonly Color TintaSuave  = Color.FromArgb(85, 81, 106);
    private static readonly Color GrisTexto   = Color.FromArgb(139, 135, 160);
    private static readonly Color LineaCombo  = Color.FromArgb(201, 199, 212);

    private static readonly Color VerdeDot    = Color.FromArgb(30, 138, 94);
    private static readonly Color VerdeFondo  = Color.FromArgb(237, 248, 242);
    private static readonly Color VerdeBorde  = Color.FromArgb(191, 227, 210);
    private static readonly Color RojoTexto   = Color.FromArgb(178, 72, 61);
    private static readonly Color RojoFondo   = Color.FromArgb(251, 240, 238);
    private static readonly Color RojoBorde   = Color.FromArgb(239, 199, 193);
    private static readonly Color AmbarTexto  = Color.FromArgb(199, 126, 29);
    private static readonly Color AmbarFondo  = Color.FromArgb(248, 237, 219);
    private static readonly Color AmbarBorde  = Color.FromArgb(233, 212, 175);
    private static readonly Color GrisDot     = Color.FromArgb(155, 151, 171);
    private static readonly Color GrisFondo   = Color.FromArgb(246, 245, 249);
    private static readonly Color GrisBorde   = Color.FromArgb(226, 224, 234);

    private static readonly Color ChipNeutroF = Color.FromArgb(239, 237, 245);
    private static readonly Color ChipNeutroT = Color.FromArgb(85, 81, 106);
    private static readonly Color ChipOkF     = Color.FromArgb(225, 243, 234);
    private static readonly Color ChipOkT     = Color.FromArgb(20, 104, 74);
    private static readonly Color ChipMalF    = Color.FromArgb(249, 228, 225);
    private static readonly Color ChipMalT    = Color.FromArgb(147, 56, 46);
    private static readonly Color ChipAvisoF  = Color.FromArgb(248, 237, 219);
    private static readonly Color ChipAvisoT  = Color.FromArgb(138, 90, 16);

    public PanelControl(bool arranqueAutomatico = false)
    {
        Text = "HdmiMirror";

        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { /* sin icono no pasa nada */ }

        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(251, 251, 252);

        var raiz = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Location = new Point(0, 0),
            Padding = new Padding(24, 18, 24, 14)
        };

        raiz.Controls.Add(ConstruirTarjetaEstado());
        raiz.Controls.Add(ConstruirChips());
        raiz.Controls.Add(ConstruirMesa());
        raiz.Controls.Add(ConstruirAvanzadoToggle());
        raiz.Controls.Add(ConstruirAvanzado());
        raiz.Controls.Add(ConstruirPie());

        Controls.Add(raiz);

        _refresco.Interval = 1000;
        _refresco.Tick += (s, e) => RefrescarEstado();
        _refresco.Start();

        CargarListas();
        AplicarAjustes(Ajustes.Cargar());

        // Versiones anteriores registraban la app en el arranque de Windows;
        // ahora eso lo hace el lanzador escalonado de la mesa. Se borra lo que
        // esta app hubiera registrado para que no arranque dos veces.
        Autoarranque.LimpiarRegistroAntiguo();

        if (arranqueAutomatico || _ajustes.IniciarAlAbrir)
        {
            _esperandoAuto = true;
            _autoInicio.Interval = 3000;
            _autoInicio.Tick += (s, e) => IntentarInicioAutomatico();
            _autoInicio.Start();
            IntentarInicioAutomatico();
        }

        RefrescarEstado();
    }

    // =============================================================== zona 1

    private Control ConstruirTarjetaEstado()
    {
        _tarjeta.Size = new Size(Ancho, 66);
        _tarjeta.Margin = new Padding(0, 0, 0, 10);
        _tarjeta.Paint += (s, e) =>
            ControlPaint.DrawBorder(e.Graphics, _tarjeta.ClientRectangle, _bordeTarjeta, ButtonBorderStyle.Solid);

        _dot.AutoSize = false;
        _dot.SetBounds(14, 0, 22, 66);
        _dot.Text = "●";
        _dot.Font = new Font(Font.FontFamily, 12F);
        _dot.TextAlign = ContentAlignment.MiddleLeft;
        _dot.BackColor = Color.Transparent;

        _lblEstadoTitulo.AutoSize = false;
        _lblEstadoTitulo.SetBounds(38, 11, Ancho - 38 - 160, 22);
        _lblEstadoTitulo.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
        _lblEstadoTitulo.AutoEllipsis = true;
        _lblEstadoTitulo.BackColor = Color.Transparent;

        _lblEstadoSub.AutoSize = false;
        _lblEstadoSub.SetBounds(38, 34, Ancho - 38 - 160, 18);
        _lblEstadoSub.ForeColor = TintaSuave;
        _lblEstadoSub.AutoEllipsis = true;
        _lblEstadoSub.BackColor = Color.Transparent;

        _btnPrincipal.SetBounds(Ancho - 16 - 132, 16, 132, 34);
        _btnPrincipal.FlatStyle = FlatStyle.Flat;
        _btnPrincipal.Font = new Font(Font, FontStyle.Bold);
        _btnPrincipal.Click += (s, e) => AlternarEspejo();

        _tarjeta.Controls.Add(_dot);
        _tarjeta.Controls.Add(_lblEstadoTitulo);
        _tarjeta.Controls.Add(_lblEstadoSub);
        _tarjeta.Controls.Add(_btnPrincipal);

        return _tarjeta;
    }

    private Control ConstruirChips()
    {
        var fila = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            MinimumSize = new Size(Ancho, 0),
            Margin = new Padding(0, 0, 0, 14)
        };

        foreach (var chip in new[] { _chipCaptura, _chipFoco, _chipAuto })
        {
            chip.AutoSize = true;
            chip.Padding = new Padding(9, 3, 9, 4);
            chip.Margin = new Padding(0, 0, 6, 0);
            chip.Font = new Font(Font.FontFamily, 8F);
            fila.Controls.Add(chip);
        }

        return fila;
    }

    // =============================================================== zona 2

    private Control ConstruirMesa()
    {
        var t = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Margin = new Padding(0),
            GrowStyle = TableLayoutPanelGrowStyle.AddRows
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ColumnaEtiqueta));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AnchoControl));

        Cabecera(t, "MESA");

        // Dealer app + refrescar en la misma fila
        _cmbDealer.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbDealer.Width = AnchoControl - 36;
        _cmbDealer.FlatStyle = FlatStyle.System;
        _cmbDealer.SelectedIndexChanged += (s, e) => AlCambiarDealer();
        _pistas.SetToolTip(_cmbDealer,
            "La unica decision de la mesa: esta ventana se captura para el\n" +
            "espejo Y recibe el foco para el escaner. Una sola eleccion.");

        _btnRefrescar.Size = new Size(30, 27);
        _btnRefrescar.Text = "⟳";
        _btnRefrescar.FlatStyle = FlatStyle.System;
        _btnRefrescar.Margin = new Padding(6, 0, 0, 0);
        _btnRefrescar.Click += (s, e) => CargarListas();
        _pistas.SetToolTip(_btnRefrescar, "Volver a buscar ventanas y monitores");

        Fila(t, "Dealer app", Horizontal(_cmbDealer, _btnRefrescar));

        _cmbPrompter.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbPrompter.Width = AnchoControl;
        _cmbPrompter.FlatStyle = FlatStyle.System;
        Fila(t, "Prompter", _cmbPrompter);

        // segmentado cristal / panel fisico
        _btnCristal.Text = "El cristal";
        _btnPanelF.Text = "El panel fisico";
        foreach (var b in new[] { _btnCristal, _btnPanelF })
        {
            b.Size = new Size(AnchoControl / 2, 29);
            b.Margin = new Padding(0);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = LineaCombo;
            b.FlatAppearance.BorderSize = 1;
        }
        _btnCristal.Click += (s, e) => { _mirarPanel = false; PintarSegmento(); };
        _btnPanelF.Click += (s, e) => { _mirarPanel = true; PintarSegmento(); };
        _pistas.SetToolTip(_btnCristal, "El reflejo re-invierte la imagen: los clics pasan directos.");
        _pistas.SetToolTip(_btnPanelF, "En el panel todo se ve al reves: los clics se recolocan espejados.");
        PintarSegmento();

        Fila(t, "El supervisor mira", Horizontal(_btnCristal, _btnPanelF));

        return t;
    }

    private void PintarSegmento()
    {
        EstiloSegmento(_btnCristal, !_mirarPanel);
        EstiloSegmento(_btnPanelF, _mirarPanel);
    }

    private void EstiloSegmento(Button b, bool activo)
    {
        b.BackColor = activo ? Indigo : Color.White;
        b.ForeColor = activo ? Color.White : TintaSuave;
        b.Font = activo ? new Font(Font, FontStyle.Bold) : Font;
    }

    // =============================================================== zona 3

    private Control ConstruirAvanzadoToggle()
    {
        _btnAvanzado.Size = new Size(Ancho, 32);
        _btnAvanzado.Margin = new Padding(0, 14, 0, 0);
        _btnAvanzado.FlatStyle = FlatStyle.Flat;
        _btnAvanzado.FlatAppearance.BorderColor = GrisBorde;
        _btnAvanzado.BackColor = GrisFondo;
        _btnAvanzado.ForeColor = TintaSuave;
        _btnAvanzado.TextAlign = ContentAlignment.MiddleLeft;
        _btnAvanzado.Padding = new Padding(8, 0, 0, 0);
        _btnAvanzado.Text = "▼  Ajustes avanzados";
        _btnAvanzado.Click += (s, e) =>
        {
            _pnlAvanzado.Visible = !_pnlAvanzado.Visible;
            _btnAvanzado.Text = (_pnlAvanzado.Visible ? "▲" : "▼") + "  Ajustes avanzados";
        };
        return _btnAvanzado;
    }

    private Control ConstruirAvanzado()
    {
        _pnlAvanzado.AutoSize = true;
        _pnlAvanzado.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _pnlAvanzado.Visible = false;
        _pnlAvanzado.Margin = new Padding(0);

        var t = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Location = new Point(0, 0),
            Margin = new Padding(0),
            Padding = new Padding(10, 8, 0, 4),
            GrowStyle = TableLayoutPanelGrowStyle.AddRows
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ColumnaEtiqueta - 10));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AnchoControl));

        _cmbOrigenAvz.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbOrigenAvz.Width = AnchoControl;
        _cmbOrigenAvz.FlatStyle = FlatStyle.System;
        _pistas.SetToolTip(_cmbOrigenAvz,
            "Que se captura para el espejo. Por defecto, la ventana de la\n" +
            "dealer app (sigue funcionando aunque la tapen). Monitor completo\n" +
            "espeja todo lo que se vea en esa pantalla.");
        Fila(t, "Origen de imagen", _cmbOrigenAvz);

        _cmbEspejo.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbEspejo.Width = 150;
        _cmbEspejo.FlatStyle = FlatStyle.System;
        _cmbEspejo.Items.AddRange(new object[] { "Horizontal", "Vertical", "180 grados", "Sin espejo" });
        _cmbEspejo.SelectedIndex = 0;

        _numFps.Minimum = 5;
        _numFps.Maximum = 60;
        _numFps.Value = 20;
        _numFps.Width = 58;
        _numFps.Margin = new Padding(0);
        _pistas.SetToolTip(_numFps, "Si el raton va a tirones, baja a 10-15.");

        var lblFps = new Label
        {
            Text = "FPS",
            AutoSize = true,
            ForeColor = GrisTexto,
            Margin = new Padding(14, 6, 8, 0)
        };
        Fila(t, "Espejo", Horizontal(_cmbEspejo, lblFps, _numFps));

        _chkDevolverFoco.Text = "Devolver el foco a la dealer app (escaner)";
        _chkDevolverFoco.AutoSize = true;
        _chkDevolverFoco.Checked = true;
        _chkDevolverFoco.CheckedChanged += (s, e) => AlternarVigilante();

        _chkOcultar.Text = "Ocultar de capturas (RustDesk ve la app normal)";
        _chkOcultar.AutoSize = true;
        _chkOcultar.Checked = true;

        _chkCursor.Text = "Dibujar el cursor del raton";
        _chkCursor.AutoSize = true;
        _chkCursor.Checked = true;

        _chkAutoarranque.Text = "Iniciar el espejo al abrir la app";
        _chkAutoarranque.AutoSize = true;
        _chkAutoarranque.CheckedChanged += (s, e) => AlternarAutoarranque();
        _pistas.SetToolTip(_chkAutoarranque,
            "Cada vez que se abra HdmiMirror (por ejemplo desde el arranque\n" +
            "escalonado de la mesa), espera a que exista la dealer app e\n" +
            "inicia el espejo solo, sin clics. No toca el arranque de Windows:\n" +
            "quien lanza la app lo decide el lanzador de la mesa.");

        FilaAncha(t, _chkDevolverFoco, 8);
        FilaAncha(t, _chkOcultar, 2);
        FilaAncha(t, _chkCursor, 2);
        FilaAncha(t, _chkAutoarranque, 2);

        _btnQuitarTopmost.Text = "Quitar always-on-top";
        BotonSecundario(_btnQuitarTopmost, 168);
        _btnQuitarTopmost.Click += (s, e) => QuitarTopmost();
        _pistas.SetToolTip(_btnQuitarTopmost,
            "Con el foco garantizado, la dealer app ya no necesita estar\n" +
            "always-on-top; quitarselo evita peleas con el espejo.");

        _btnDiagnostico.Text = "Diagnostico";
        BotonSecundario(_btnDiagnostico, 116);
        _btnDiagnostico.Margin = new Padding(8, 0, 0, 0);
        _btnDiagnostico.Click += (s, e) => LanzarDiagnostico();

        FilaAncha(t, Horizontal(_btnQuitarTopmost, _btnDiagnostico), 10);

        _pnlAvanzado.Controls.Add(t);
        return _pnlAvanzado;
    }

    // ================================================================== pie

    private Control ConstruirPie()
    {
        var pie = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(Ancho, 0),
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 14, 0, 0)
        };
        pie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pie.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var atajos = new Label
        {
            Text = "Ctrl+Alt+M parar   ·   Ctrl+Alt+F foco   ·   Ctrl+Alt+D diagnostico",
            AutoSize = true,
            ForeColor = GrisTexto,
            Font = new Font(Font.FontFamily, 8F),
            Margin = new Padding(0, 2, 0, 0)
        };

        var version = new Label
        {
            // Del ensamblado (csproj <Version>), nunca escrita a mano aqui.
            Text = "v" + Application.ProductVersion.Split('+')[0],
            AutoSize = true,
            ForeColor = GrisTexto,
            Font = new Font(Font.FontFamily, 8F),
            Margin = new Padding(0, 2, 0, 0)
        };

        pie.Controls.Add(atajos, 0, 0);
        pie.Controls.Add(version, 1, 0);
        return pie;
    }

    // ------------------------------------------------------------ ayudantes

    private void Cabecera(TableLayoutPanel t, string texto)
    {
        int fila = t.RowCount;
        t.RowCount = fila + 1;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var l = new Label
        {
            Text = texto,
            AutoSize = true,
            Font = new Font(Font.FontFamily, 8F, FontStyle.Bold),
            ForeColor = GrisTexto,
            Margin = new Padding(0, 0, 0, 4)
        };

        t.Controls.Add(l, 0, fila);
        t.SetColumnSpan(l, 2);
    }

    private void Fila(TableLayoutPanel t, string etiqueta, Control control)
    {
        int fila = t.RowCount;
        t.RowCount = fila + 1;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var l = new Label
        {
            Text = etiqueta,
            AutoSize = true,
            ForeColor = Tinta,
            Margin = new Padding(0, 7, 10, 0)
        };
        t.Controls.Add(l, 0, fila);

        control.Margin = new Padding(0, 2, 0, 3);
        t.Controls.Add(control, 1, fila);
    }

    private void FilaAncha(TableLayoutPanel t, Control control, int margenSuperior)
    {
        int fila = t.RowCount;
        t.RowCount = fila + 1;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        control.Margin = new Padding(0, margenSuperior, 0, 2);
        t.Controls.Add(control, 0, fila);
        t.SetColumnSpan(control, 2);
    }

    private static FlowLayoutPanel Horizontal(params Control[] controles)
    {
        var f = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0)
        };
        f.Controls.AddRange(controles);
        return f;
    }

    private static void BotonSecundario(Button b, int ancho)
    {
        b.AutoSize = false;
        b.Size = new Size(ancho, AltoBoton - 4);
        b.FlatStyle = FlatStyle.System;
        b.Margin = new Padding(0);
    }

    private void PintarChip(Label chip, string texto, Color fondo, Color tinta)
    {
        chip.Text = texto;
        chip.BackColor = fondo;
        chip.ForeColor = tinta;
    }

    // ---------------------------------------------------------------- atajos

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Native.RegisterHotKey(Handle, Native.HOTKEY_PARAR, Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.M);
        Native.RegisterHotKey(Handle, Native.HOTKEY_FOCO,  Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.F);
        Native.RegisterHotKey(Handle, Native.HOTKEY_DIAG,  Native.MOD_CONTROL | Native.MOD_ALT, (uint)Keys.D);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();

            if (id == Native.HOTKEY_PARAR)
            {
                Parar();
                Show();
                WindowState = FormWindowState.Normal;
                Activate();
            }
            else if (id == Native.HOTKEY_DIAG)
            {
                LanzarDiagnostico();
            }
            else if (id == Native.HOTKEY_FOCO)
            {
                _vigilante.Pausado = !_vigilante.Pausado;
                RefrescarEstado();
            }
        }

        base.WndProc(ref m);
    }

    // --------------------------------------------------------------- listas

    private void CargarListas()
    {
        string dealerAnterior = (_cmbDealer.SelectedItem as ItemVentana)?.Texto ?? "";
        string origenAnterior = (_cmbOrigenAvz.SelectedItem as ItemOrigenAvz)?.Texto ?? "";

        _cmbDealer.Items.Clear();
        _cmbPrompter.Items.Clear();
        _cmbOrigenAvz.Items.Clear();

        _cmbDealer.Items.Add(new ItemVentana { Texto = "(ninguna)", Hwnd = IntPtr.Zero });
        _cmbOrigenAvz.Items.Add(new ItemOrigenAvz { Texto = "Ventana de la dealer app", EsDealer = true });

        var pantallas = Screen.AllScreens;
        for (int i = 0; i < pantallas.Length; i++)
        {
            var p = pantallas[i];
            string nombre = $"Monitor {i + 1} — {p.Bounds.Width}x{p.Bounds.Height}" +
                            (p.Primary ? " (principal)" : "");

            _cmbPrompter.Items.Add(new ItemMonitor { Texto = nombre, Indice = i });
            _cmbOrigenAvz.Items.Add(new ItemOrigenAvz { Texto = nombre, IndiceMonitor = i });
        }

        foreach (var v in ListarVentanas())
            _cmbDealer.Items.Add(v);

        SeleccionarPorTexto(_cmbDealer, dealerAnterior, 0);
        SeleccionarPorTexto(_cmbOrigenAvz, origenAnterior, 0);
        if (_cmbPrompter.Items.Count > 0 && _cmbPrompter.SelectedIndex < 0)
            _cmbPrompter.SelectedIndex = 0;
    }

    private static void SeleccionarPorTexto(ComboBox combo, string texto, int porDefecto)
    {
        if (!string.IsNullOrEmpty(texto))
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i].ToString() == texto)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
        }

        if (combo.Items.Count > porDefecto && combo.SelectedIndex < 0)
            combo.SelectedIndex = porDefecto;
    }

    private static bool SeleccionarExacto(ComboBox combo, string texto)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i].ToString() == texto)
            {
                combo.SelectedIndex = i;
                return true;
            }
        }

        return false;
    }

    private static List<ItemVentana> ListarVentanas()
    {
        var lista = new List<ItemVentana>();

        Native.EnumWindows((hwnd, lp) =>
        {
            if (!Native.IsWindowVisible(hwnd) || Native.IsIconic(hwnd))
                return true;

            int largo = Native.GetWindowTextLength(hwnd);
            if (largo <= 0)
                return true;

            var sb = new StringBuilder(largo + 1);
            Native.GetWindowText(hwnd, sb, sb.Capacity);
            string titulo = sb.ToString();

            if (string.IsNullOrWhiteSpace(titulo) || titulo.StartsWith("HdmiMirror"))
                return true;

            lista.Add(new ItemVentana { Texto = titulo, Hwnd = hwnd });
            return true;
        }, IntPtr.Zero);

        return lista;
    }

    private static IntPtr BuscarPorTitulo(string titulo)
    {
        IntPtr encontrada = IntPtr.Zero;

        Native.EnumWindows((hwnd, lp) =>
        {
            if (!Native.IsWindowVisible(hwnd))
                return true;

            int largo = Native.GetWindowTextLength(hwnd);
            if (largo <= 0)
                return true;

            var sb = new StringBuilder(largo + 1);
            Native.GetWindowText(hwnd, sb, sb.Capacity);

            if (sb.ToString() == titulo && !titulo.StartsWith("HdmiMirror"))
            {
                encontrada = hwnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);

        return encontrada;
    }

    // ---------------------------------------------------------- dealer app

    private string TituloDealer()
    {
        var item = _cmbDealer.SelectedItem as ItemVentana;
        return item == null || item.Hwnd == IntPtr.Zero && item.Texto == "(ninguna)" ? "" : item.Texto;
    }

    /// <summary>
    /// Handle vivo de la dealer app. Si el guardado murio (la app se
    /// reinicio), se re-busca por titulo y se actualiza en el propio item.
    /// </summary>
    private IntPtr DealerHwnd()
    {
        var item = _cmbDealer.SelectedItem as ItemVentana;
        if (item == null || string.IsNullOrEmpty(TituloDealer()))
            return IntPtr.Zero;

        if (!Native.IsWindow(item.Hwnd))
        {
            IntPtr nueva = BuscarPorTitulo(item.Texto);
            if (nueva != IntPtr.Zero)
                item.Hwnd = nueva;
        }

        return Native.IsWindow(item.Hwnd) ? item.Hwnd : IntPtr.Zero;
    }

    private void AlCambiarDealer()
    {
        // El vigilante sigue a la dealer app elegida sin pasos extra.
        IntPtr hwnd = DealerHwnd();
        if (hwnd != IntPtr.Zero)
            _vigilante.Objetivo = hwnd;
    }

    // -------------------------------------------------------------- ajustes

    private void AplicarAjustes(Ajustes a)
    {
        _ajustes = a;

        // dealer: el titulo guardado del foco manda; configs viejas solo
        // tenian "Ventana: X" en el origen, asi que se deriva de ahi.
        string dealer = a.TituloFoco;
        if ((string.IsNullOrEmpty(dealer) || dealer == "(ninguna)") &&
            a.TituloOrigen.StartsWith("Ventana: "))
            dealer = a.TituloOrigen.Substring("Ventana: ".Length);

        if (!string.IsNullOrEmpty(dealer) && dealer != "(ninguna)")
            SeleccionarExacto(_cmbDealer, dealer);

        if (a.MonitorSalida >= 0 && a.MonitorSalida < _cmbPrompter.Items.Count)
            _cmbPrompter.SelectedIndex = a.MonitorSalida;

        // origen avanzado: monitor guardado, o la dealer app (indice 0)
        if (a.MonitorOrigen >= 0 && a.MonitorOrigen + 1 < _cmbOrigenAvz.Items.Count)
            _cmbOrigenAvz.SelectedIndex = a.MonitorOrigen + 1;
        else
            _cmbOrigenAvz.SelectedIndex = 0;

        if (a.Espejo >= 0 && a.Espejo < _cmbEspejo.Items.Count)
            _cmbEspejo.SelectedIndex = a.Espejo;

        _numFps.Value = Math.Clamp(a.Fps, (int)_numFps.Minimum, (int)_numFps.Maximum);
        _chkOcultar.Checked = a.OcultarDeCapturas;
        _chkCursor.Checked = a.DibujarCursor;
        _mirarPanel = a.ClicsEspejados;
        PintarSegmento();

        _ajustandoAutoarranque = true;
        _chkAutoarranque.Checked = a.IniciarAlAbrir;
        _ajustandoAutoarranque = false;

        _ajustandoFoco = true;
        _chkDevolverFoco.Checked = a.VigilanteActivo;
        _ajustandoFoco = false;
        AlternarVigilante();
    }

    private void GuardarAjustes()
    {
        string dealer = TituloDealer();
        var origenAvz = _cmbOrigenAvz.SelectedItem as ItemOrigenAvz;
        bool origenEsDealer = origenAvz == null || origenAvz.EsDealer;

        _ajustes.TituloFoco = dealer;
        _ajustes.TituloOrigen = origenEsDealer && !string.IsNullOrEmpty(dealer)
            ? "Ventana: " + dealer
            : "";
        _ajustes.MonitorOrigen = origenEsDealer ? -1 : origenAvz.IndiceMonitor;
        _ajustes.MonitorSalida = _cmbPrompter.SelectedIndex;
        _ajustes.Espejo = _cmbEspejo.SelectedIndex;
        _ajustes.Fps = (int)_numFps.Value;
        _ajustes.OcultarDeCapturas = _chkOcultar.Checked;
        _ajustes.DibujarCursor = _chkCursor.Checked;
        _ajustes.ClicsEspejados = _mirarPanel;
        _ajustes.VigilanteActivo = _chkDevolverFoco.Checked;
        _ajustes.IniciarAlAbrir = _chkAutoarranque.Checked;

        _ajustes.Guardar();
    }

    // ---------------------------------------------------------------- estado

    private void RefrescarEstado()
    {
        // Mientras un aviso esta en pantalla, la tarjeta no se pisa.
        if (DateTime.Now < _avisoHasta)
            return;

        bool enMarcha = _espejo != null && !_espejo.IsDisposed;

        // el vigilante se re-arma solo si la dealer app reaparecio
        if (_chkDevolverFoco.Checked && !_vigilante.Activo)
        {
            IntPtr d = DealerHwnd();
            if (d != IntPtr.Zero)
            {
                _vigilante.Objetivo = d;
                _vigilante.Arrancar();
            }
        }

        // ---- tarjeta ----
        if (!enMarcha && _esperandoAuto)
        {
            PintarTarjeta(AmbarTexto, AmbarFondo, AmbarBorde,
                "Esperando a la dealer app...",
                "El espejo se iniciara solo en cuanto aparezca.");
        }
        else if (enMarcha && !string.IsNullOrEmpty(_espejo.EstadoCaptura))
        {
            PintarTarjeta(RojoTexto, RojoFondo, RojoBorde,
                "Espejo en marcha — sin imagen", _espejo.EstadoCaptura);
        }
        else if (enMarcha)
        {
            PintarTarjeta(VerdeDot, VerdeFondo, VerdeBorde,
                "Espejo en marcha", DescribirConfiguracion());
        }
        else
        {
            PintarTarjeta(GrisDot, GrisFondo, GrisBorde,
                "Espejo parado", "Listo para iniciar con la configuracion guardada.");
        }

        // ---- boton principal ----
        if (enMarcha)
        {
            _btnPrincipal.Text = "Parar";
            _btnPrincipal.BackColor = Color.White;
            _btnPrincipal.ForeColor = RojoTexto;
            _btnPrincipal.FlatAppearance.BorderColor = RojoBorde;
            _btnPrincipal.FlatAppearance.BorderSize = 1;
        }
        else
        {
            _btnPrincipal.Text = "Iniciar espejo";
            _btnPrincipal.BackColor = Indigo;
            _btnPrincipal.ForeColor = Color.White;
            _btnPrincipal.FlatAppearance.BorderSize = 0;
        }

        // ---- chips ----
        if (!enMarcha)
        {
            _chipCaptura.Visible = false;
        }
        else
        {
            _chipCaptura.Visible = true;
            if (string.IsNullOrEmpty(_espejo.EstadoCaptura))
                PintarChip(_chipCaptura, "Captura OK", ChipOkF, ChipOkT);
            else
                PintarChip(_chipCaptura, "Captura SIN IMAGEN", ChipMalF, ChipMalT);
        }

        if (!_chkDevolverFoco.Checked || string.IsNullOrEmpty(TituloDealer()))
            PintarChip(_chipFoco, "Foco desactivado", ChipNeutroF, ChipNeutroT);
        else if (_vigilante.Pausado)
            PintarChip(_chipFoco, "Foco PAUSADO (Ctrl+Alt+F)", ChipAvisoF, ChipAvisoT);
        else if (_vigilante.EnEspera)
            PintarChip(_chipFoco, "Foco: reintentando cada 10 s", ChipAvisoF, ChipAvisoT);
        else if (_vigilante.Recuperaciones == 0)
            PintarChip(_chipFoco, "Foco en reposo", ChipNeutroF, ChipNeutroT);
        else if (_vigilante.Recuperaciones == 1)
            PintarChip(_chipFoco, "Foco devuelto 1 vez", ChipOkF, ChipOkT);
        else
            PintarChip(_chipFoco, $"Foco devuelto {_vigilante.Recuperaciones} veces", ChipOkF, ChipOkT);

        if (_chkAutoarranque.Checked)
            PintarChip(_chipAuto, "Inicia solo al abrir la app", ChipNeutroF, ChipNeutroT);
        else
            PintarChip(_chipAuto, "Inicio manual", ChipNeutroF, ChipNeutroT);
    }

    private void PintarTarjeta(Color dot, Color fondo, Color borde, string titulo, string sub)
    {
        _dot.ForeColor = dot;
        _tarjeta.BackColor = fondo;
        _bordeTarjeta = borde;
        _lblEstadoTitulo.Text = titulo;
        _lblEstadoSub.Text = sub;
        _tarjeta.Invalidate();
    }

    private string DescribirConfiguracion()
    {
        var origenAvz = _cmbOrigenAvz.SelectedItem as ItemOrigenAvz;
        string origen = origenAvz != null && !origenAvz.EsDealer
            ? $"Monitor {origenAvz.IndiceMonitor + 1}"
            : (string.IsNullOrEmpty(TituloDealer()) ? "?" : TituloDealer());

        string salida = $"monitor {(_cmbPrompter.SelectedIndex >= 0 ? _cmbPrompter.SelectedIndex + 1 : 0)}";
        string clics = _mirarPanel ? "clics recolocados" : "clics directos";

        return $"{origen} → {salida} · {_cmbEspejo.Text.ToLowerInvariant()} · {clics}";
    }

    private void MostrarAviso(string texto)
    {
        PintarTarjeta(RojoTexto, RojoFondo, RojoBorde, "Atencion", texto);
        _avisoHasta = DateTime.Now.AddSeconds(6);
    }

    // -------------------------------------------------------------- acciones

    private ModoEspejo ModoElegido()
    {
        switch (_cmbEspejo.SelectedIndex)
        {
            case 0: return ModoEspejo.Horizontal;
            case 1: return ModoEspejo.Vertical;
            case 2: return ModoEspejo.Rotar180;
            default: return ModoEspejo.Ninguno;
        }
    }

    private IntPtr VentanaFocoElegida() => DealerHwnd();

    private void AlternarEspejo()
    {
        if (_espejo != null && !_espejo.IsDisposed)
            Parar();
        else
            Iniciar();
    }

    private void AlternarVigilante()
    {
        if (_ajustandoFoco)
            return;

        if (_chkDevolverFoco.Checked)
        {
            IntPtr objetivo = DealerHwnd();
            if (objetivo != IntPtr.Zero)
            {
                _vigilante.Objetivo = objetivo;
                _vigilante.Pausado = false;
                _vigilante.Arrancar();
                VigilanteFoco.DevolverFoco(objetivo);
            }
            // sin dealer elegida no molestamos: el re-armado automatico de
            // RefrescarEstado lo activara en cuanto la haya
        }
        else
        {
            _vigilante.Parar();
        }

        RefrescarEstado();
    }

    private void AlternarAutoarranque()
    {
        if (_ajustandoAutoarranque)
            return;

        GuardarAjustes();
    }

    private void QuitarTopmost()
    {
        IntPtr objetivo = DealerHwnd();
        if (objetivo == IntPtr.Zero)
        {
            MostrarAviso("Elige primero la dealer app.");
            return;
        }

        VigilanteFoco.QuitarAlwaysOnTop(objetivo);
    }

    private void Iniciar()
    {
        Parar();

        if (_cmbPrompter.SelectedItem is not ItemMonitor salida)
        {
            MostrarAviso("Elige la pantalla del prompter.");
            return;
        }

        var pantallas = Screen.AllScreens;
        if (salida.Indice >= pantallas.Length)
        {
            MostrarAviso("Ese monitor ya no existe. Pulsa el boton de refrescar.");
            return;
        }

        var origenAvz = _cmbOrigenAvz.SelectedItem as ItemOrigenAvz;
        bool origenEsDealer = origenAvz == null || origenAvz.EsDealer;

        IntPtr dealer = DealerHwnd();

        if (origenEsDealer && dealer == IntPtr.Zero)
        {
            MostrarAviso("Elige la dealer app (o un monitor en Ajustes avanzados).");
            return;
        }

        if (!origenEsDealer && origenAvz.IndiceMonitor >= pantallas.Length)
        {
            MostrarAviso("Ese monitor de origen ya no existe. Pulsa refrescar.");
            return;
        }

        bool mismoMonitor = !origenEsDealer && origenAvz.IndiceMonitor == salida.Indice;
        if (mismoMonitor && !_chkOcultar.Checked)
        {
            var r = MessageBox.Show(
                "Vas a capturar el mismo monitor en el que se va a mostrar el espejo, " +
                "pero tienes desactivado 'Ocultar de capturas'.\n\n" +
                "Lo mas probable es que salga efecto tunel (imagen dentro de imagen).\n\n" +
                "Quieres activarlo?",
                "Aviso", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);

            if (r == DialogResult.Cancel) return;
            if (r == DialogResult.Yes) _chkOcultar.Checked = true;
        }

        _espejo = new VentanaEspejo(pantallas[salida.Indice], (int)_numFps.Value)
        {
            Modo = ModoElegido(),
            Fuente = origenEsDealer ? FuenteCaptura.Ventana : FuenteCaptura.Monitor,
            ZonaOrigen = origenEsDealer ? Rectangle.Empty : pantallas[origenAvz.IndiceMonitor].Bounds,
            VentanaOrigen = dealer,
            TituloOrigen = origenEsDealer ? TituloDealer() : "",
            MostrarCursor = _chkCursor.Checked,
            ClickThrough = true,
            OcultarDeCapturas = _chkOcultar.Checked,
            Clics = _mirarPanel ? ModoClics.EspejadoInyectado : ModoClics.Directo
        };

        _espejo.Show();

        if (_chkDevolverFoco.Checked && dealer != IntPtr.Zero)
        {
            _vigilante.Objetivo = dealer;
            if (!_vigilante.Activo)
                _vigilante.Arrancar();
            VigilanteFoco.DevolverFoco(dealer);
        }

        GuardarAjustes();
        _avisoHasta = DateTime.MinValue;
        RefrescarEstado();

        if (!string.IsNullOrEmpty(_espejo.ErrorRaton))
            MostrarAviso(_espejo.ErrorRaton);
    }

    private void Parar()
    {
        if (_espejo != null)
        {
            if (!_espejo.IsDisposed)
                _espejo.Close();
            _espejo = null;
        }

        _avisoHasta = DateTime.MinValue;
        RefrescarEstado();
    }

    // -------------------------------------------------------- autoarranque

    /// <summary>
    /// Lanzada con /auto: la dealer app probablemente tambien esta arrancando,
    /// asi que se reintenta cada 3 segundos hasta que su ventana existe y
    /// entonces se inicia el espejo con la configuracion guardada.
    /// </summary>
    private void IntentarInicioAutomatico()
    {
        if (_espejo != null && !_espejo.IsDisposed)
        {
            _autoInicio.Stop();
            _esperandoAuto = false;
            return;
        }

        CargarListas();

        string dealer = _ajustes.TituloFoco;
        if ((string.IsNullOrEmpty(dealer) || dealer == "(ninguna)") &&
            _ajustes.TituloOrigen.StartsWith("Ventana: "))
            dealer = _ajustes.TituloOrigen.Substring("Ventana: ".Length);

        bool dealerOk = string.IsNullOrEmpty(dealer) || dealer == "(ninguna)"
                     || SeleccionarExacto(_cmbDealer, dealer);

        if (!dealerOk)
            return;   // aun no esta; el timer reintenta en 3 s

        _autoInicio.Stop();
        _esperandoAuto = false;

        _ajustandoFoco = true;
        _chkDevolverFoco.Checked = _ajustes.VigilanteActivo;
        _ajustandoFoco = false;
        AlternarVigilante();

        Iniciar();

        // El espejo tapa la pantalla igualmente; minimizados no aparecemos en
        // la vista de RustDesk ni robamos sitio en la barra de tareas.
        if (_espejo != null && !_espejo.IsDisposed)
            WindowState = FormWindowState.Minimized;
    }

    private void LanzarDiagnostico()
    {
        string informe = Diagnostico.Generar(_espejo, VentanaFocoElegida());

        Parar();

        string ruta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "HdmiMirror-diagnostico.txt");

        try
        {
            File.WriteAllText(ruta, informe);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ruta,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(informe + "\n\n(No se pudo guardar: " + ex.Message + ")", "Diagnostico");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        GuardarAjustes();

        Native.UnregisterHotKey(Handle, Native.HOTKEY_PARAR);
        Native.UnregisterHotKey(Handle, Native.HOTKEY_FOCO);
        Native.UnregisterHotKey(Handle, Native.HOTKEY_DIAG);

        _refresco.Stop();
        _autoInicio.Stop();
        _vigilante.Dispose();
        Parar();

        base.OnFormClosing(e);
    }
}
