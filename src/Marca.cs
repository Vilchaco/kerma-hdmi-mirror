using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace HdmiMirror;

/// <summary>
/// BRANDBOOK · tokens de marca de Kerma Games (Brandbook 2026, págs. 21 y 26).
/// Son los mismos valores que el bloque BRANDBOOK de Tech Inventory. Si la marca
/// cambia, se toca AQUÍ: el resto de la app solo usa estos nombres.
/// </summary>
internal static class Marca
{
    // ---- colores de marca ----
    public static readonly Color Fondo       = Hex(0x0A0712);  // Kerma Black Magic
    public static readonly Color Superficie  = Hex(0x110C1F);  // Kerma Jambolan: tarjeta, campos
    public static readonly Color Superficie2 = Hex(0x161029);  // secundario: botón neutro, activo
    public static readonly Color Borde       = Hex(0x353041);  // secundario: bordes de controles
    public static readonly Color Texto       = Color.White;
    public static readonly Color Acento      = Hex(0xEA7F2B);  // Kerma Honeybell: un solo elemento por pantalla
    public static readonly Color AcentoTinta = Fondo;          // texto sobre el acento

    // Grises derivados del #CFCED2 del brandbook, para que se lean sobre el fondo
    public static readonly Color Mut  = Mezcla(Hex(0xCFCED2), Fondo, 0.70);
    public static readonly Color Mut2 = Mezcla(Hex(0xCFCED2), Fondo, 0.45);

    // ---- colores del sistema (no son de marca; solo en puntos pequeños) ----
    public static readonly Color Ok    = Hex(0x5FB878);
    public static readonly Color Mal   = Hex(0xE05555);
    public static readonly Color Aviso = Hex(0xF4C95D);    // amarillo: el naranja es el acento

    // ---- hover de botones (derivados) ----
    public static readonly Color Superficie2Hover = Mezcla(Borde, Superficie2, 0.35);
    public static readonly Color AcentoHover      = Mezcla(Color.White, Acento, 0.12);

    private static Color Hex(int rgb) => Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);

    private static Color Mezcla(Color a, Color b, double pesoA) => Color.FromArgb(
        (int)Math.Round(a.R * pesoA + b.R * (1 - pesoA)),
        (int)Math.Round(a.G * pesoA + b.G * (1 - pesoA)),
        (int)Math.Round(a.B * pesoA + b.B * (1 - pesoA)));

    // ================================================================ letra
    // Sora es la letra principal y Orbitron solo va en la marca. No están
    // instaladas en los PCs de mesa, así que viajan dentro del exe (carpeta
    // Marca/, licencia OFL). Si algo falla al cargarlas, se usa Segoe UI.

    private static readonly PrivateFontCollection _coleccion = new();
    private static FontFamily _sora;
    private static FontFamily _soraSemi;
    private static FontFamily _orbitron;

    static Marca()
    {
        try
        {
            Cargar("Sora-Regular.ttf");
            Cargar("Sora-SemiBold.ttf");
            Cargar("Orbitron-Bold.ttf");

            foreach (var f in _coleccion.Families)
            {
                if (f.Name == "Sora") _sora = f;
                else if (f.Name == "Sora SemiBold") _soraSemi = f;
                else if (f.Name == "Orbitron") _orbitron = f;
            }
        }
        catch
        {
            // sin las fuentes de marca la app sigue funcionando con Segoe UI
        }
    }

    /// <summary>
    /// Hay que registrar la fuente dos veces: en GDI+ (para crear el Font) y en
    /// GDI (porque los controles de WinForms dibujan el texto con GDI y buscan
    /// la letra por su nombre). La memoria no se libera nunca a propósito: las
    /// dos copias registradas la usan mientras la app esté abierta.
    /// </summary>
    private static void Cargar(string archivo)
    {
        using var s = typeof(Marca).Assembly.GetManifestResourceStream("HdmiMirror.Marca." + archivo);
        if (s == null)
            return;

        var datos = new byte[s.Length];
        s.ReadExactly(datos);

        IntPtr memoria = Marshal.AllocCoTaskMem(datos.Length);
        Marshal.Copy(datos, 0, memoria, datos.Length);

        _coleccion.AddMemoryFont(memoria, datos.Length);
        uint instaladas = 0;
        AddFontMemResourceEx(memoria, (uint)datos.Length, IntPtr.Zero, ref instaladas);
    }

    public static Font Sora(float puntos) =>
        _sora != null ? new Font(_sora, puntos) : new Font("Segoe UI", puntos);

    public static Font SoraSemi(float puntos) =>
        _soraSemi != null ? new Font(_soraSemi, puntos) : new Font("Segoe UI Semibold", puntos);

    public static Font Orbitron(float puntos) =>
        _orbitron != null ? new Font(_orbitron, puntos, FontStyle.Bold) : new Font("Segoe UI", puntos, FontStyle.Bold);

    // ================================================================= logo

    public static Image Logo()
    {
        try
        {
            using var s = typeof(Marca).Assembly.GetManifestResourceStream("HdmiMirror.Marca.kerma-logo.png");
            if (s == null)
                return null;
            using var original = Image.FromStream(s);
            return new Bitmap(original);   // copia: Image.FromStream necesita el stream vivo
        }
        catch
        {
            return null;
        }
    }

    // ======================================================== barra de título

    /// <summary>
    /// Barra de título oscura con el fondo de marca (Windows 11). En Windows
    /// más antiguos estas llamadas no hacen nada y la barra queda la de siempre.
    /// </summary>
    public static void BarraDeTitulo(IntPtr ventana)
    {
        try
        {
            int si = 1;
            DwmSetWindowAttribute(ventana, DWMWA_USE_IMMERSIVE_DARK_MODE, ref si, sizeof(int));
            int fondo = ColorRef(Fondo);
            DwmSetWindowAttribute(ventana, DWMWA_CAPTION_COLOR, ref fondo, sizeof(int));
            int borde = ColorRef(Borde);
            DwmSetWindowAttribute(ventana, DWMWA_BORDER_COLOR, ref borde, sizeof(int));
            int texto = ColorRef(Texto);
            DwmSetWindowAttribute(ventana, DWMWA_TEXT_COLOR, ref texto, sizeof(int));
        }
        catch
        {
            // dwmapi no disponible: barra estándar
        }
    }

    private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_BORDER_COLOR = 34;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_TEXT_COLOR = 36;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int valor, int tamano);

    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, ref uint pcFonts);
}
