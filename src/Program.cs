namespace HdmiMirror;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // /auto lo pone el registro de autoarranque: la app espera a que
        // aparezca la dealer app e inicia el espejo sin que nadie clique.
        bool automatico = args.Any(a => a.Equals("/auto", StringComparison.OrdinalIgnoreCase));

        // /capturas <carpeta>: solo para la Action "Capturas" de GitHub.
        string carpetaCapturas = null;
        int i = Array.FindIndex(args, a => a.Equals("/capturas", StringComparison.OrdinalIgnoreCase));
        if (i >= 0 && i + 1 < args.Length)
            carpetaCapturas = args[i + 1];

        Application.Run(new PanelControl(automatico, carpetaCapturas));
    }
}
