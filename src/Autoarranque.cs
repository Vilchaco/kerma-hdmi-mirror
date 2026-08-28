using System.Diagnostics;
using Microsoft.Win32;

namespace HdmiMirror;

/// <summary>
/// SOLO limpieza heredada. Versiones anteriores registraban HdmiMirror en el
/// arranque de Windows (tarea programada o clave Run); ahora el arranque lo
/// gestiona el lanzador escalonado de las mesas, y si quedara el registro
/// antiguo la app se abriria dos veces. Al arrancar se borra lo que esta app
/// hubiera registrado — unicamente lo suyo ("HdmiMirror").
/// </summary>
internal static class Autoarranque
{
    private const string NombreTarea = "HdmiMirror";
    private const string ClaveRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string NombreValor = "HdmiMirror";

    public static void LimpiarRegistroAntiguo()
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(ClaveRun, writable: true);
            if (k?.GetValue(NombreValor) != null)
                k.DeleteValue(NombreValor, throwOnMissingValue: false);
        }
        catch
        {
            // sin acceso al registro: no pasa nada
        }

        if (EjecutarSchtasks($"/Query /TN \"{NombreTarea}\"") == 0)
            EjecutarSchtasks($"/Delete /F /TN \"{NombreTarea}\"");
    }

    private static int EjecutarSchtasks(string argumentos)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = argumentos,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (p == null)
                return -1;

            p.WaitForExit(15000);
            return p.HasExited ? p.ExitCode : -1;
        }
        catch
        {
            return -1;
        }
    }
}
