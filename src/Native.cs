using System.Runtime.InteropServices;
using System.Text;

namespace HdmiMirror;

/// <summary>
/// Todas las llamadas a la API de Windows que necesita la app.
/// Nada aqui tiene logica: son declaraciones y constantes.
/// </summary>
internal static class Native
{
    // ---------- Estilos extendidos de ventana ----------
    public const int GWL_EXSTYLE = -20;

    /// <summary>Necesario para que WS_EX_TRANSPARENT funcione de forma fiable.</summary>
    public const int WS_EX_LAYERED = 0x00080000;

    /// <summary>La ventana deja pasar los clics a la que hay debajo (click-through).</summary>
    public const int WS_EX_TRANSPARENT = 0x00000020;

    /// <summary>La ventana nunca coge el foco, asi que no roba el teclado.</summary>
    public const int WS_EX_NOACTIVATE = 0x08000000;

    /// <summary>Fuera de la barra de tareas y del Alt+Tab.</summary>
    public const int WS_EX_TOOLWINDOW = 0x00000080;

    // ---------- SetWindowPos ----------
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint SWP_NOSIZE     = 0x0001;
    public const uint SWP_NOMOVE     = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    // ---------- Display affinity ----------
    public const uint WDA_NONE = 0x00000000;

    /// <summary>
    /// La ventana se sigue viendo en el monitor fisico pero desaparece de
    /// cualquier captura de pantalla (RustDesk, OBS, Recortes...).
    /// Requiere Windows 10 2004 / build 19041 o superior.
    /// </summary>
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    // ---------- Raster ops y modos de estirado ----------
    public const int SRCCOPY       = 0x00CC0020;
    public const int HALFTONE      = 4;
    public const int COLORONCOLOR  = 3;

    // ---------- PrintWindow ----------
    /// <summary>Captura tambien contenido acelerado por hardware (Chromium, D3D...).</summary>
    public const uint PW_RENDERFULLCONTENT = 0x00000002;

    // ---------- Cursor ----------
    public const int CURSOR_SHOWING = 0x00000001;
    public const int DI_NORMAL      = 0x0003;

    // ---------- Ventanas por capas ----------
    public const int LWA_ALPHA = 0x00000002;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width  => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CURSORINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hCursor;
        public POINT ptScreenPos;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    // ---------- user32 ----------
    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
                                           int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey,
                                                         byte bAlpha, int dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    public static extern bool GetCursorInfo(ref CURSORINFO pci);

    [DllImport("user32.dll")]
    public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [DllImport("user32.dll")]
    public static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon,
                                         int cxWidth, int cyWidth, uint istepIfAniCur,
                                         IntPtr hbrFlickerFreeDraw, int diFlags);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    // GetWindowLong / SetWindowLong tienen dos variantes segun la arquitectura.
    // El proyecto se compila como x64, pero envolvemos ambas por seguridad.
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    public static int GetWindowLongCompat(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8
            ? (int)GetWindowLongPtr64(hWnd, nIndex).ToInt64()
            : GetWindowLong32(hWnd, nIndex);
    }

    public static void SetWindowLongCompat(IntPtr hWnd, int nIndex, int dwNewLong)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hWnd, nIndex, new IntPtr(dwNewLong));
        else
            SetWindowLong32(hWnd, nIndex, dwNewLong);
    }

    // ---------- Atajos globales ----------
    // Imprescindibles: con un solo monitor, la ventana espejo tapa el panel de
    // control y sin atajo te quedas atrapado sin poder pararlo.
    public const int WM_HOTKEY   = 0x0312;
    public const uint MOD_ALT     = 0x0001;
    public const uint MOD_CONTROL = 0x0002;

    public const int HOTKEY_PARAR = 1;
    public const int HOTKEY_FOCO  = 2;
    public const int HOTKEY_DIAG  = 3;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // ---------- Inyeccion de clics ----------
    // Para el modo "espejado": recibimos el clic, calculamos donde esta de verdad
    // el boton (al otro lado de la pantalla) e inyectamos alli un clic real.
    public const uint INPUT_MOUSE = 0;
    public const uint MOUSEEVENTF_LEFTDOWN  = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP    = 0x0004;
    public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    public const uint MOUSEEVENTF_RIGHTUP   = 0x0010;

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    /// <summary>
    /// INPUT es una union en C. Como aqui solo inyectamos raton y MOUSEINPUT es
    /// el miembro mas grande, declararlo asi da el tamano correcto (40 bytes en x64).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern bool SetCursorPos(int X, int Y);

    // ---------- Hook de raton de bajo nivel ----------
    // Intercepta los clics ANTES de que lleguen a ninguna ventana, para poder
    // recolocarlos. Asi la ventana espejo puede quedarse click-through siempre
    // y no hay ninguna carrera entre cambiar estilos e inyectar el clic.
    public const int WH_MOUSE_LL = 14;

    public const int WM_MOUSEMOVE   = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP   = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP   = 0x0205;

    /// <summary>
    /// Marca que ponemos en dwExtraInfo a los eventos que inyectamos nosotros,
    /// para reconocerlos en el hook y no volver a procesarlos (bucle infinito).
    /// </summary>
    public static readonly IntPtr MARCA_PROPIA = new IntPtr(0x48444D52);

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    // ---------- Foco de teclado ----------
    // El escaner Datalogic funciona como un teclado: lo que escanea va a la
    // ventana que tenga el FOCO, que no es lo mismo que estar always-on-top.
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    // ---------- Diagnostico ----------
    // WindowFromPoint IGNORA las ventanas con WS_EX_TRANSPARENT. Por eso sirve
    // como prueba definitiva: si apuntando al espejo devuelve la dealer app,
    // el click-through esta funcionando de verdad.
    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint TOKEN_QUERY = 0x0008;

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool GetTokenInformation(IntPtr TokenHandle, int TokenInformationClass,
                                                  out uint TokenInformation, uint TokenInformationLength,
                                                  out uint ReturnLength);

    /// <summary>TokenElevation = 20 en la enumeracion TOKEN_INFORMATION_CLASS.</summary>
    public const int TokenElevation = 20;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern uint QueryFullProcessImageName(IntPtr hProcess, uint dwFlags,
                                                        StringBuilder lpExeName, ref uint lpdwSize);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // ---------- WinEvent (vigilante de foco por eventos) ----------
    // En vez de sondear GetForegroundWindow en un timer, Windows nos llama
    // solo cuando la ventana en primer plano cambia. Coste cero en reposo.
    public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    public const uint WINEVENT_OUTOFCONTEXT   = 0x0000;

    public delegate void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
                                      int idObject, int idChild, uint dwEventThread,
                                      uint dwmsEventTime);

    [DllImport("user32.dll")]
    public static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax,
                                                IntPtr hmodWinEventProc, WinEventProc pfnWinEventProc,
                                                uint idProcess, uint idThread, uint dwFlags);

    [DllImport("user32.dll")]
    public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

    // ---------- Movimiento absoluto inyectado ----------
    // Para mover el cursor con SendInput (y no con SetCursorPos): asi el
    // movimiento lleva nuestra marca en dwExtraInfo y atraviesa el hook de
    // raton en vez de ser bloqueado por el.
    public const uint MOUSEEVENTF_MOVE        = 0x0001;
    public const uint MOUSEEVENTF_ABSOLUTE    = 0x8000;
    public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;

    public const int SM_XVIRTUALSCREEN  = 76;
    public const int SM_YVIRTUALSCREEN  = 77;
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    // ---------- gdi32 ----------
    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int cx, int cy);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr ho);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    public static extern bool BitBlt(IntPtr hdcDest, int x, int y, int cx, int cy,
                                     IntPtr hdcSrc, int x1, int y1, int rop);

    [DllImport("gdi32.dll")]
    public static extern bool StretchBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
                                         IntPtr hdcSrc, int xSrc, int ySrc, int wSrc, int hSrc, int rop);

    [DllImport("gdi32.dll")]
    public static extern int SetStretchBltMode(IntPtr hdc, int mode);

    [DllImport("gdi32.dll")]
    public static extern bool SetBrushOrgEx(IntPtr hdc, int x, int y, IntPtr lppt);

    /// <summary>Lee un pixel de un DC. Se usa para detectar capturas vacias.</summary>
    [DllImport("gdi32.dll")]
    public static extern uint GetPixel(IntPtr hdc, int x, int y);
}
