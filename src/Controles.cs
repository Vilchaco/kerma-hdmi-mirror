namespace HdmiMirror;

/// <summary>
/// Desplegable oscuro. El ComboBox de WinForms no deja cambiar el color del
/// marco ni de la flecha (en estilo plano los pinta blancos), así que se dibujan
/// los elementos a mano y, después de cada repintado del sistema, se repasan el
/// marco y la flecha con los colores de marca. El comportamiento es el del
/// ComboBox normal.
/// </summary>
internal sealed class ComboOscuro : ComboBox
{
    private const int WM_PAINT = 0x000F;

    public ComboOscuro()
    {
        DropDownStyle = ComboBoxStyle.DropDownList;
        FlatStyle = FlatStyle.Flat;
        DrawMode = DrawMode.OwnerDrawFixed;
        ItemHeight = 22;
        BackColor = Marca.Superficie;
        ForeColor = Marca.Texto;
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool enLaCaja = (e.State & DrawItemState.ComboBoxEdit) != 0;
        bool marcado = !enLaCaja && (e.State & DrawItemState.Selected) != 0;
        Color fondo = marcado ? Marca.Superficie2Hover : Marca.Superficie;

        using (var b = new SolidBrush(fondo))
            e.Graphics.FillRectangle(b, e.Bounds);

        if (e.Index < 0 || e.Index >= Items.Count)
            return;

        var zona = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, zona, Marca.Texto, fondo,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);

        if (m.Msg == WM_PAINT && IsHandleCreated)
            RepasarMarco();
    }

    private void RepasarMarco()
    {
        using var g = Graphics.FromHwnd(Handle);
        var r = ClientRectangle;
        int ancho = SystemInformation.VerticalScrollBarWidth;
        var flecha = new Rectangle(r.Right - ancho - 1, r.Top + 1, ancho, r.Height - 2);

        using (var b = new SolidBrush(BackColor))
            g.FillRectangle(b, flecha);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float cx = flecha.Left + flecha.Width / 2f;
        float cy = flecha.Top + flecha.Height / 2f;
        using (var p = new Pen(Marca.Mut, 1.5f))
            g.DrawLines(p, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        using (var p = new Pen(Focused || DroppedDown ? Marca.Mut2 : Marca.Borde))
            g.DrawRectangle(p, 0, 0, r.Width - 1, r.Height - 1);
    }
}

/// <summary>
/// Chip de estado: un punto pequeño de color y el texto en gris. Así el estado
/// se lee de un vistazo sin rellenos verdes ni textos rojos.
/// </summary>
internal sealed class Chip : Label
{
    private const int Punto = 6;
    private const int HuecoPunto = 7;

    public Color ColorPunto { get; set; } = Marca.Mut2;

    public Chip()
    {
        AutoSize = true;
        Padding = new Padding(9, 3, 9, 4);
        BackColor = Marca.Superficie2;
        ForeColor = Marca.Mut;
    }

    public override Size GetPreferredSize(Size propuesto)
    {
        var s = base.GetPreferredSize(propuesto);
        return new Size(s.Width + Punto + HuecoPunto, s.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        int y = (Height - Punto) / 2;
        using (var b = new SolidBrush(ColorPunto))
            e.Graphics.FillEllipse(b, Padding.Left, y, Punto, Punto);

        int x = Padding.Left + Punto + HuecoPunto;
        var zona = new Rectangle(x, 0, Width - x - Padding.Right + 2, Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, zona, ForeColor, BackColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }
}

/// <summary>
/// Casilla oscura. La CheckBox plana de WinForms rellena la caja sin marcar de
/// un lila que parece marcada, así que se dibuja a mano: caja con borde de
/// marca y la marca de verificación en blanco. Clic, teclado y eventos son
/// los de la CheckBox normal.
/// </summary>
internal sealed class CasillaOscura : CheckBox
{
    private const int Caja = 15;
    private const int Hueco = 8;

    public CasillaOscura()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        AutoSize = true;
        ForeColor = Marca.Texto;
        Cursor = Cursors.Hand;
    }

    public override Size GetPreferredSize(Size propuesto)
    {
        var t = TextRenderer.MeasureText(Text, Font, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
        return new Size(Caja + Hueco + t.Width + 2, Math.Max(Caja, t.Height) + 4);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Marca.Fondo);

        int y = (Height - Caja) / 2;
        var caja = new Rectangle(0, y, Caja - 1, Caja - 1);

        using (var b = new SolidBrush(Checked ? Marca.Superficie2Hover : Marca.Superficie))
            g.FillRectangle(b, caja);
        using (var p = new Pen(Focused ? Marca.Mut2 : Marca.Borde))
            g.DrawRectangle(p, caja);

        if (Checked)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var p = new Pen(Marca.Texto, 1.8f);
            g.DrawLines(p, new[] { new PointF(3.5f, y + 7.5f), new PointF(6.3f, y + 10.3f), new PointF(11.3f, y + 4.5f) });
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
        }

        var zona = new Rectangle(Caja + Hueco, 0, Width - Caja - Hueco, Height);
        TextRenderer.DrawText(g, Text, Font, zona, Enabled ? ForeColor : Marca.Mut2,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
    }
}

/// <summary>
/// Selector numérico oscuro. Las flechas de subir y bajar son un control
/// interno que Windows pinta siempre en gris claro; se repintan con los
/// colores de marca después de cada repintado del sistema. El valor, los
/// límites y el teclado son los del NumericUpDown normal.
/// </summary>
internal sealed class NumeroOscuro : NumericUpDown
{
    private PintorFlechas _pintor;

    public NumeroOscuro()
    {
        BackColor = Marca.Superficie;
        ForeColor = Marca.Texto;
        BorderStyle = BorderStyle.FixedSingle;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (_pintor == null && Controls.Count > 0)
            _pintor = new PintorFlechas(Controls[0]);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var p = new Pen(Marca.Borde);
        e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
    }

    private sealed class PintorFlechas : NativeWindow
    {
        private const int WM_PAINT = 0x000F;
        private readonly Control _botones;

        public PintorFlechas(Control botones)
        {
            _botones = botones;
            if (botones.IsHandleCreated)
                AssignHandle(botones.Handle);
            botones.HandleCreated += (s, e) => { if (Handle == IntPtr.Zero) AssignHandle(_botones.Handle); };
            botones.HandleDestroyed += (s, e) => ReleaseHandle();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT)
                Pintar();
        }

        private void Pintar()
        {
            using var g = Graphics.FromHwnd(_botones.Handle);
            var r = _botones.ClientRectangle;

            using (var b = new SolidBrush(Marca.Superficie2))
                g.FillRectangle(b, r);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float cx = r.Width / 2f;
            float mitad = r.Height / 2f;
            using (var p = new Pen(Marca.Mut, 1.4f))
            {
                float a = mitad / 2f;
                g.DrawLines(p, new[] { new PointF(cx - 3, a + 1.5f), new PointF(cx, a - 1.5f), new PointF(cx + 3, a + 1.5f) });
                float b2 = mitad + mitad / 2f;
                g.DrawLines(p, new[] { new PointF(cx - 3, b2 - 1.5f), new PointF(cx, b2 + 1.5f), new PointF(cx + 3, b2 - 1.5f) });
            }

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            using (var l = new Pen(Marca.Borde))
                g.DrawLine(l, 0, 0, 0, r.Height);
        }
    }
}
