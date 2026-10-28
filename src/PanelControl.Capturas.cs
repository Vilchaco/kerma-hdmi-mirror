using System.Drawing.Imaging;

namespace HdmiMirror;

/// <summary>
/// Modo /capturas: abre el panel, lo pone en varios estados, guarda una imagen
/// de cada uno y se cierra. Lo usa la Action "Capturas" de GitHub para ver el
/// panel en Windows real sin tener una mesa delante. No toca la configuracion
/// de la mesa, ni el registro, ni arranca el espejo.
/// </summary>
internal sealed partial class PanelControl
{
    private readonly string _carpetaCapturas;

    private bool ModoCaptura => !string.IsNullOrEmpty(_carpetaCapturas);

    private async Task HacerCapturas()
    {
        try
        {
            Directory.CreateDirectory(_carpetaCapturas);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(40, 40);
            TopMost = true;
            Activate();

            // 1. Estado inicial, tal cual arranca en una mesa sin configurar
            await Capturar("1-parado");

            // 2. Con los ajustes avanzados desplegados
            MostrarAvanzado(true);
            await Capturar("2-avanzado");
            MostrarAvanzado(false);

            // 3. Un aviso (lo que sale si se pulsa Iniciar sin dealer app)
            MostrarAviso("Elige la dealer app (o un monitor en Ajustes avanzados).");
            await Capturar("3-aviso");
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(_carpetaCapturas, "error.txt"), ex.ToString());
        }
        finally
        {
            Close();
        }
    }

    /// <summary>
    /// Guarda dos imagenes: lo que se ve en pantalla (incluye la barra de
    /// titulo real de Windows) y lo que pinta el propio formulario, por si el
    /// escritorio del servidor de GitHub no deja copiar la pantalla.
    /// </summary>
    private async Task Capturar(string nombre)
    {
        PerformLayout();
        Refresh();
        await Task.Delay(700);

        using (var pantalla = new Bitmap(Width, Height))
        {
            using (var g = Graphics.FromImage(pantalla))
                g.CopyFromScreen(Location, Point.Empty, Size);
            pantalla.Save(Path.Combine(_carpetaCapturas, nombre + "-pantalla.png"), ImageFormat.Png);
        }

        using var dibujo = new Bitmap(Width, Height);
        DrawToBitmap(dibujo, new Rectangle(Point.Empty, Size));
        dibujo.Save(Path.Combine(_carpetaCapturas, nombre + "-dibujo.png"), ImageFormat.Png);
    }
}
