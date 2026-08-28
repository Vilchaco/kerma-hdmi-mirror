using System.Text;

namespace HdmiMirror;

/// <summary>
/// Recoge el estado real del sistema en vez de suponerlo.
///
/// Las dos preguntas que importan:
///   1. Hay una barrera de privilegios (UIPI) entre HdmiMirror y la dealer app?
///      Si la dealer app corre como administrador y HdmiMirror no, Windows
///      bloquea el foco y los mensajes SIN dar ningun error.
///   2. Esta WS_EX_TRANSPARENT realmente aplicado en la ventana espejo?
/// </summary>
internal static class Diagnostico
{
    public static string Generar(VentanaEspejo espejo, IntPtr ventanaDealer)
    {
        var sb = new StringBuilder();

        sb.AppendLine("=== DIAGNOSTICO HdmiMirror ===");
        sb.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        sb.AppendLine();

        // ---------- 1. Privilegios ----------
        sb.AppendLine("--- PRIVILEGIOS ---");

        bool yoElevado = ProcesoElevado(Environment.ProcessId);
        sb.AppendLine($"HdmiMirror corre como administrador: {SiNo(yoElevado)}");

        if (ventanaDealer != IntPtr.Zero && Native.IsWindow(ventanaDealer))
        {
            Native.GetWindowThreadProcessId(ventanaDealer, out uint pidDealer);
            bool dealerElevada = ProcesoElevado((int)pidDealer);
            sb.AppendLine($"Dealer app corre como administrador: {SiNo(dealerElevada)}");
            sb.AppendLine($"Ejecutable de la dealer app: {RutaProceso((int)pidDealer)}");
            sb.AppendLine();

            if (dealerElevada && !yoElevado)
            {
                sb.AppendLine(">>> PROBLEMA ENCONTRADO <<<");
                sb.AppendLine("La dealer app corre elevada y HdmiMirror no.");
                sb.AppendLine("Windows (UIPI) esta bloqueando el foco y los mensajes.");
                sb.AppendLine("SOLUCION: clic derecho en HdmiMirror.exe -> Ejecutar como administrador.");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("No hay ninguna ventana elegida en 'Mantener foco en'.");
            sb.AppendLine();
        }

        // ---------- 2. Estilos de la ventana espejo ----------
        sb.AppendLine("--- VENTANA ESPEJO ---");

        if (espejo != null && !espejo.IsDisposed && espejo.IsHandleCreated)
        {
            int ex = Native.GetWindowLongCompat(espejo.Handle, Native.GWL_EXSTYLE);
            sb.AppendLine($"Estilos extendidos: 0x{ex:X8}");
            sb.AppendLine($"  WS_EX_LAYERED     (necesario): {SiNo((ex & Native.WS_EX_LAYERED) != 0)}");
            sb.AppendLine($"  WS_EX_TRANSPARENT (click-through): {SiNo((ex & Native.WS_EX_TRANSPARENT) != 0)}");
            sb.AppendLine($"  WS_EX_NOACTIVATE  (no roba foco): {SiNo((ex & Native.WS_EX_NOACTIVATE) != 0)}");

            if ((ex & Native.WS_EX_TRANSPARENT) == 0)
            {
                sb.AppendLine();
                sb.AppendLine(">>> El click-through NO esta aplicado.");
                sb.AppendLine("    Si el modo de clics es 'Pasar directo', esto es un bug.");
            }
        }
        else
        {
            sb.AppendLine("El espejo no esta en marcha (arrancalo antes de diagnosticar).");
        }
        sb.AppendLine();

        // ---------- 3. Prueba de hit-testing ----------
        sb.AppendLine("--- QUE HAY BAJO EL CURSOR ---");
        sb.AppendLine("(WindowFromPoint ignora las ventanas click-through,");
        sb.AppendLine(" asi que si aqui sale la dealer app, el click-through FUNCIONA)");
        sb.AppendLine();

        if (Native.GetCursorPos(out Native.POINT pos))
        {
            sb.AppendLine($"Cursor en: {pos.X}, {pos.Y}");
            IntPtr bajoCursor = Native.WindowFromPoint(pos);
            sb.AppendLine($"Ventana bajo el cursor: {TituloDe(bajoCursor)}");

            Native.GetWindowThreadProcessId(bajoCursor, out uint pidBajo);
            sb.AppendLine($"  proceso: {RutaProceso((int)pidBajo)}");

            if (espejo != null && !espejo.IsDisposed && bajoCursor == espejo.Handle)
            {
                sb.AppendLine();
                sb.AppendLine(">>> El cursor esta topando con la ventana espejo.");
                sb.AppendLine("    El click-through NO esta funcionando.");
            }
        }
        sb.AppendLine();

        // ---------- 4. Foco ----------
        sb.AppendLine("--- FOCO DE TECLADO ---");
        IntPtr enPrimerPlano = Native.GetForegroundWindow();
        sb.AppendLine($"Ventana con el foco ahora: {TituloDe(enPrimerPlano)}");

        if (ventanaDealer != IntPtr.Zero)
        {
            sb.AppendLine($"Deberia tenerlo: {TituloDe(ventanaDealer)}");
            sb.AppendLine($"Coinciden: {SiNo(enPrimerPlano == ventanaDealer)}");
        }

        sb.AppendLine();
        sb.AppendLine("=== FIN ===");

        return sb.ToString();
    }

    private static string SiNo(bool v) => v ? "SI" : "NO";

    private static string TituloDe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !Native.IsWindow(hwnd))
            return "(ninguna)";

        int largo = Native.GetWindowTextLength(hwnd);
        if (largo <= 0)
            return $"(sin titulo) hwnd={hwnd}";

        var sb = new StringBuilder(largo + 1);
        Native.GetWindowText(hwnd, sb, sb.Capacity);
        return $"\"{sb}\"  hwnd={hwnd}";
    }

    private static bool ProcesoElevado(int pid)
    {
        IntPtr proceso = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (proceso == IntPtr.Zero)
            return false;   // no hemos podido ni abrirlo: casi seguro que es elevado

        IntPtr token = IntPtr.Zero;
        try
        {
            if (!Native.OpenProcessToken(proceso, Native.TOKEN_QUERY, out token))
                return false;

            if (!Native.GetTokenInformation(token, Native.TokenElevation,
                                            out uint elevado, sizeof(uint), out _))
                return false;

            return elevado != 0;
        }
        finally
        {
            if (token != IntPtr.Zero) Native.CloseHandle(token);
            Native.CloseHandle(proceso);
        }
    }

    private static string RutaProceso(int pid)
    {
        IntPtr proceso = Native.OpenProcess(Native.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
        if (proceso == IntPtr.Zero)
            return "(no accesible - probablemente corre elevado)";

        try
        {
            uint tam = 1024;
            var sb = new StringBuilder((int)tam);
            if (Native.QueryFullProcessImageName(proceso, 0, sb, ref tam) != 0)
                return sb.ToString();
            return "(desconocido)";
        }
        finally
        {
            Native.CloseHandle(proceso);
        }
    }
}
