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

        Application.Run(new PanelControl(automatico));
    }
}
