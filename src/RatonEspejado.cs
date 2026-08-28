using System.Runtime.InteropServices;

namespace HdmiMirror;

/// <summary>
/// Recoloca los clics cuando el supervisor mira el PANEL FISICO, donde la imagen
/// se ve espejada y por tanto los botones estan en el lado contrario.
///
/// Por que un hook y no interceptar el clic en la ventana:
/// la version anterior volvia la ventana transparente un instante para inyectar
/// el clic y la devolvia a opaca. Pero SendInput es ASINCRONO: deja el evento en
/// la cola del sistema y vuelve enseguida, asi que a menudo restaurabamos la
/// opacidad antes de que Windows procesara el clic y este rebotaba contra
/// nuestra propia ventana. De ahi que "no captara todos los clics".
///
/// Con un hook de bajo nivel el clic se intercepta ANTES de llegar a ninguna
/// ventana, la ventana espejo se queda click-through permanentemente y no hay
/// ninguna carrera posible.
///
/// Ademas se tratan "pulsar" y "soltar" por separado, asi que mantener un boton
/// pulsado funciona.
/// </summary>
internal sealed class RatonEspejado : IDisposable
{
    // El delegate hay que guardarlo en un campo: si solo se pasa a la API, el
    // recolector de basura se lo lleva y el hook empieza a fallar solo.
    private readonly Native.HookProc _callback;
    private IntPtr _hook = IntPtr.Zero;

    private readonly Rectangle _zona;
    private readonly ModoEspejo _modo;

    private bool _manteniendo;
    private Native.POINT _posicionUsuario;
    private bool _botonDerecho;

    public RatonEspejado(Rectangle zonaEspejo, ModoEspejo modo)
    {
        _zona = zonaEspejo;
        _modo = modo;
        _callback = Callback;
    }

    public bool Instalar()
    {
        if (_hook != IntPtr.Zero)
            return true;

        IntPtr modulo = Native.GetModuleHandle(null);
        _hook = Native.SetWindowsHookEx(Native.WH_MOUSE_LL, _callback, modulo, 0);
        return _hook != IntPtr.Zero;
    }

    public void Desinstalar()
    {
        if (_hook == IntPtr.Zero)
            return;

        // Si se desinstala con un boton "mantenido", hay que soltarlo o el
        // sistema se queda con el raton pulsado para siempre.
        if (_manteniendo)
        {
            Enviar(Boton(_botonDerecho ? Native.MOUSEEVENTF_RIGHTUP : Native.MOUSEEVENTF_LEFTUP));
            _manteniendo = false;
        }

        Native.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private Native.POINT Espejar(Native.POINT p)
    {
        var r = p;

        if (_modo == ModoEspejo.Horizontal || _modo == ModoEspejo.Rotar180)
            r.X = _zona.Left + _zona.Right - 1 - p.X;

        if (_modo == ModoEspejo.Vertical || _modo == ModoEspejo.Rotar180)
            r.Y = _zona.Top + _zona.Bottom - 1 - p.Y;

        return r;
    }

    private bool EnZona(Native.POINT p)
    {
        return p.X >= _zona.Left && p.X < _zona.Right
            && p.Y >= _zona.Top && p.Y < _zona.Bottom;
    }

    private static Native.INPUT Boton(uint flags)
    {
        var e = new Native.INPUT();
        e.type = Native.INPUT_MOUSE;
        e.mi.dwFlags = flags;
        e.mi.dwExtraInfo = Native.MARCA_PROPIA;
        return e;
    }

    /// <summary>
    /// Movimiento de cursor inyectado con nuestra marca en dwExtraInfo.
    ///
    /// No se usa SetCursorPos a proposito: su movimiento pasa por este mismo
    /// hook SIN marca y, durante un "mantener pulsado", el propio hook se lo
    /// comeria — el cursor no llegaria a moverse y el clic caeria en la
    /// posicion sin espejar. Con SendInput marcado, el movimiento atraviesa.
    /// </summary>
    private static Native.INPUT Mover(Native.POINT destino)
    {
        int vx = Native.GetSystemMetrics(Native.SM_XVIRTUALSCREEN);
        int vy = Native.GetSystemMetrics(Native.SM_YVIRTUALSCREEN);
        int vw = Math.Max(1, Native.GetSystemMetrics(Native.SM_CXVIRTUALSCREEN));
        int vh = Math.Max(1, Native.GetSystemMetrics(Native.SM_CYVIRTUALSCREEN));

        var e = new Native.INPUT();
        e.type = Native.INPUT_MOUSE;
        e.mi.dwFlags = Native.MOUSEEVENTF_MOVE | Native.MOUSEEVENTF_ABSOLUTE
                     | Native.MOUSEEVENTF_VIRTUALDESK;
        e.mi.dx = (int)Math.Round((destino.X - vx) * 65535.0 / Math.Max(1, vw - 1));
        e.mi.dy = (int)Math.Round((destino.Y - vy) * 65535.0 / Math.Max(1, vh - 1));
        e.mi.dwExtraInfo = Native.MARCA_PROPIA;
        return e;
    }

    /// <summary>
    /// Todos los eventos van en UNA llamada a SendInput: el orden dentro del
    /// flujo de entrada queda garantizado (movimiento antes que pulsacion).
    /// </summary>
    private static void Enviar(params Native.INPUT[] eventos)
    {
        Native.SendInput((uint)eventos.Length, eventos, Marshal.SizeOf<Native.INPUT>());
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

        int mensajeRapido = wParam.ToInt32();

        // Camino caliente: el raton genera cientos de movimientos por segundo
        // y, salvo mientras se mantiene un boton pulsado, no nos interesan.
        // Salir aqui sin leer siquiera la estructura deja el hook casi gratis.
        if (mensajeRapido == Native.WM_MOUSEMOVE && !_manteniendo)
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

        var info = Marshal.PtrToStructure<Native.MSLLHOOKSTRUCT>(lParam);

        // Lo que hemos inyectado nosotros pasa de largo sin tocarlo.
        if (info.dwExtraInfo == Native.MARCA_PROPIA)
            return Native.CallNextHookEx(_hook, nCode, wParam, lParam);

        int mensaje = wParam.ToInt32();

        switch (mensaje)
        {
            case Native.WM_LBUTTONDOWN:
            case Native.WM_RBUTTONDOWN:
            {
                if (_manteniendo || !EnZona(info.pt))
                    break;

                _posicionUsuario = info.pt;
                _botonDerecho = mensaje == Native.WM_RBUTTONDOWN;
                _manteniendo = true;

                // Movimiento y pulsacion juntos, en una sola llamada y ambos
                // con la marca: el cursor llega al boton real y se pulsa alli.
                Native.POINT destino = Espejar(info.pt);
                Enviar(Mover(destino),
                       Boton(_botonDerecho ? Native.MOUSEEVENTF_RIGHTDOWN
                                           : Native.MOUSEEVENTF_LEFTDOWN));

                return (IntPtr)1;   // el clic original no sigue su camino
            }

            case Native.WM_LBUTTONUP:
            case Native.WM_RBUTTONUP:
            {
                if (!_manteniendo)
                    break;

                bool esDerecho = mensaje == Native.WM_RBUTTONUP;
                if (esDerecho != _botonDerecho)
                    break;

                _manteniendo = false;

                // Se suelta el boton (el cursor sigue sobre el control real) y
                // despues el cursor vuelve a donde lo tenia el usuario.
                Enviar(Boton(_botonDerecho ? Native.MOUSEEVENTF_RIGHTUP
                                           : Native.MOUSEEVENTF_LEFTUP),
                       Mover(_posicionUsuario));

                return (IntPtr)1;
            }

            case Native.WM_MOUSEMOVE:
            {
                // Mientras se mantiene un boton pulsado, el cursor tiene que
                // quedarse quieto encima del boton real: si se moviera, la app
                // creeria que el raton ha salido y soltaria el "mantener pulsado".
                if (_manteniendo)
                    return (IntPtr)1;

                break;
            }
        }

        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose() => Desinstalar();
}
