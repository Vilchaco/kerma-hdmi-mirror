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
