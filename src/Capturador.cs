using System.Runtime.InteropServices;

namespace HdmiMirror;

public enum ModoEspejo
{
    Ninguno,
    Horizontal,
    Vertical,
    Rotar180
}

public enum FuenteCaptura
{
    Monitor,
    Ventana
}

/// <summary>
/// Captura una zona de pantalla (o una ventana concreta) a un buffer en memoria
/// y la vuelca espejada sobre un DC de destino.
///
/// El espejo lo hace GDI gratis: StretchBlt con anchura de destino negativa
/// invierte la imagen en horizontal, y con altura negativa en vertical.
/// No hace falta ni shader ni Direct3D.
/// </summary>
internal sealed class Capturador : IDisposable
{
    private IntPtr _memDc = IntPtr.Zero;
    private IntPtr _bitmap = IntPtr.Zero;
    private IntPtr _bitmapPrevio = IntPtr.Zero;
    private int _ancho;
    private int _alto;

    /// <summary>Ultimo error legible, para mostrarlo en el panel de control.</summary>
    public string UltimoError { get; private set; } = "";

    private void AsegurarBuffer(IntPtr dcReferencia, int ancho, int alto)
    {
        if (_memDc != IntPtr.Zero && _ancho == ancho && _alto == alto)
            return;

        LiberarBuffer();

        _memDc = Native.CreateCompatibleDC(dcReferencia);
        _bitmap = Native.CreateCompatibleBitmap(dcReferencia, ancho, alto);
        _bitmapPrevio = Native.SelectObject(_memDc, _bitmap);
        _ancho = ancho;
        _alto = alto;
    }

    private void LiberarBuffer()
    {
        if (_memDc != IntPtr.Zero)
        {
            if (_bitmapPrevio != IntPtr.Zero)
                Native.SelectObject(_memDc, _bitmapPrevio);
            Native.DeleteDC(_memDc);
            _memDc = IntPtr.Zero;
            _bitmapPrevio = IntPtr.Zero;
        }

        if (_bitmap != IntPtr.Zero)
        {
            Native.DeleteObject(_bitmap);
            _bitmap = IntPtr.Zero;
        }

        _ancho = 0;
        _alto = 0;
    }

    /// <summary>
    /// Captura una zona del escritorio virtual.
    /// OJO: se usa SRCCOPY sin CAPTUREBLT a proposito. Sin CAPTUREBLT, GDI
    /// excluye las ventanas por capas (layered), y nuestra propia ventana espejo
    /// es una de ellas. Es la segunda linea de defensa contra el efecto tunel,
    /// ademas de WDA_EXCLUDEFROMCAPTURE.
    /// </summary>
    public bool CapturarMonitor(Rectangle zona, bool incluirCursor)
    {
        IntPtr dcPantalla = Native.GetDC(IntPtr.Zero);
        if (dcPantalla == IntPtr.Zero)
        {
            UltimoError = "No se pudo obtener el DC de la pantalla.";
            return false;
        }

        try
        {
            AsegurarBuffer(dcPantalla, zona.Width, zona.Height);

            bool ok = Native.BitBlt(_memDc, 0, 0, zona.Width, zona.Height,
                                    dcPantalla, zona.X, zona.Y, Native.SRCCOPY);
            if (!ok)
            {
                UltimoError = "BitBlt fallo al capturar el monitor.";
                return false;
            }

            if (incluirCursor)
                DibujarCursor(zona.X, zona.Y);

            return true;
        }
        finally
        {
            Native.ReleaseDC(IntPtr.Zero, dcPantalla);
        }
    }

    // PrintWindow devuelve una imagen en blanco con algunas apps (las que
    // pintan por GPU, o si la ventana objetivo corre como administrador y
    // nosotros no: UIPI bloquea la peticion SIN dar error). Cuando se detecta,
    // se pasa automaticamente a capturar el rectangulo de la ventana en
    // pantalla, que no depende de la colaboracion de la otra app.
    private bool _printWindowRoto;
    private int _framesUniformes;

    /// <summary>Aviso no fatal (p.ej. "cambie a captura por pantalla").</summary>
    public string Nota { get; private set; } = "";

    /// <summary>
    /// Captura una ventana concreta. Primero intenta PrintWindow con
    /// PW_RENDERFULLCONTENT (funciona aunque la ventana este tapada); si falla
    /// o devuelve un lienzo vacio, cae a BitBlt del rectangulo en pantalla.
    /// El BitBlt va sin CAPTUREBLT, asi que excluye las ventanas por capas —
    /// nuestra ventana espejo entre ellas — y no produce efecto tunel aunque
    /// el espejo este encima.
    /// </summary>
    public bool CapturarVentana(IntPtr hwnd, bool incluirCursor)
    {
        if (hwnd == IntPtr.Zero || !Native.IsWindowVisible(hwnd))
        {
            UltimoError = "La ventana de origen ya no existe o esta oculta.";
            return false;
        }

        if (!Native.GetWindowRect(hwnd, out Native.RECT r))
        {
            UltimoError = "No se pudo leer el tamano de la ventana de origen.";
            return false;
        }

        int ancho = r.Width;
        int alto = r.Height;
        if (ancho <= 0 || alto <= 0)
        {
            UltimoError = "La ventana de origen tiene tamano cero (minimizada?).";
            return false;
        }

        IntPtr dcPantalla = Native.GetDC(IntPtr.Zero);
        if (dcPantalla == IntPtr.Zero)
        {
            UltimoError = "No se pudo obtener el DC de la pantalla.";
            return false;
        }

        try
        {
            AsegurarBuffer(dcPantalla, ancho, alto);

            bool ok = false;

            if (!_printWindowRoto)
            {
                ok = Native.PrintWindow(hwnd, _memDc, Native.PW_RENDERFULLCONTENT);

                if (ok && EsUniforme())
                {
                    // "Funciona" pero todos los pixeles muestreados son iguales.
                    // Una UI real nunca es un color plano perfecto; tras un
                    // segundo seguido asi damos PrintWindow por roto.
                    if (++_framesUniformes >= 20)
                    {
                        _printWindowRoto = true;
                        Nota = "PrintWindow devolvia una imagen vacia; " +
                               "usando captura de pantalla del rectangulo de la ventana.";
                    }
                }
                else if (ok)
                {
                    _framesUniformes = 0;
                }
            }

            if (_printWindowRoto || !ok)
            {
                ok = Native.BitBlt(_memDc, 0, 0, ancho, alto,
                                   dcPantalla, r.Left, r.Top, Native.SRCCOPY);
                if (!ok)
                {
                    UltimoError = "No se pudo capturar la ventana (PrintWindow y BitBlt fallaron).";
                    return false;
                }
            }

            if (incluirCursor)
                DibujarCursor(r.Left, r.Top);

            return true;
        }
        finally
        {
            Native.ReleaseDC(IntPtr.Zero, dcPantalla);
        }
    }

    /// <summary>
    /// Muestrea una rejilla de 5x5 pixeles del buffer: si todos son identicos,
    /// la captura es (casi seguro) un lienzo vacio. 25 GetPixel por frame es
    /// despreciable.
    /// </summary>
    private bool EsUniforme()
    {
        uint primero = Native.GetPixel(_memDc, _ancho / 10, _alto / 10);

        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                int x = _ancho / 10 + i * (_ancho * 8 / 10) / 4;
                int y = _alto / 10 + j * (_alto * 8 / 10) / 4;
                if (Native.GetPixel(_memDc, x, y) != primero)
                    return false;
            }
        }

        return true;
    }

    // El hotspot de cada cursor se consulta una sola vez y se cachea.
    // GetIconInfo crea DOS bitmaps GDI nuevos en cada llamada (que ademas hay
    // que borrar); hacerlo en cada frame era crear y destruir 60 objetos GDI
    // por segundo para leer un dato que no cambia nunca.
    private readonly Dictionary<IntPtr, Point> _hotspots = new();

    /// <summary>
    /// El cursor no viene en la captura: lo dibuja el sistema por encima de todo.
    /// Como los supervisores trabajan con raton, hay que pintarlo a mano.
    /// </summary>
    private void DibujarCursor(int origenX, int origenY)
    {
        var info = new Native.CURSORINFO();
        info.cbSize = Marshal.SizeOf<Native.CURSORINFO>();

        if (!Native.GetCursorInfo(ref info))
            return;
        if ((info.flags & Native.CURSOR_SHOWING) == 0 || info.hCursor == IntPtr.Zero)
            return;

        if (!_hotspots.TryGetValue(info.hCursor, out Point hotspot))
        {
            if (!Native.GetIconInfo(info.hCursor, out Native.ICONINFO icono))
                return;

            hotspot = new Point(icono.xHotspot, icono.yHotspot);
            if (icono.hbmColor != IntPtr.Zero) Native.DeleteObject(icono.hbmColor);
            if (icono.hbmMask != IntPtr.Zero) Native.DeleteObject(icono.hbmMask);

            // Los handles de cursor los recicla el sistema. Si la cache crece
            // de forma anomala se vacia entera y se rellena con datos frescos.
            if (_hotspots.Count >= 64)
                _hotspots.Clear();
            _hotspots[info.hCursor] = hotspot;
        }

        int x = info.ptScreenPos.X - origenX - hotspot.X;
        int y = info.ptScreenPos.Y - origenY - hotspot.Y;
        Native.DrawIconEx(_memDc, x, y, info.hCursor, 0, 0, 0, IntPtr.Zero, Native.DI_NORMAL);
    }

    /// <summary>
    /// Vuelca el buffer capturado sobre el DC de destino, aplicando el espejo.
    /// </summary>
    public void Presentar(IntPtr dcDestino, int anchoDestino, int altoDestino, ModoEspejo modo)
    {
        if (_memDc == IntPtr.Zero || anchoDestino <= 0 || altoDestino <= 0)
            return;

        int x = 0;
        int y = 0;
        int w = anchoDestino;
        int h = altoDestino;

        // Anchura negativa => la imagen se dibuja de derecha a izquierda.
        if (modo == ModoEspejo.Horizontal || modo == ModoEspejo.Rotar180)
        {
            x = anchoDestino;
            w = -anchoDestino;
        }

        // Altura negativa => de abajo a arriba.
        if (modo == ModoEspejo.Vertical || modo == ModoEspejo.Rotar180)
        {
            y = altoDestino;
            h = -altoDestino;
        }

        bool hayEscalado = _ancho != anchoDestino || _alto != altoDestino;
        Native.SetStretchBltMode(dcDestino, hayEscalado ? Native.HALFTONE : Native.COLORONCOLOR);
        if (hayEscalado)
            Native.SetBrushOrgEx(dcDestino, 0, 0, IntPtr.Zero);

        Native.StretchBlt(dcDestino, x, y, w, h,
                          _memDc, 0, 0, _ancho, _alto, Native.SRCCOPY);
    }

    public void Dispose() => LiberarBuffer();
}
