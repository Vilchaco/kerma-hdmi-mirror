using System.Text.Json;

namespace HdmiMirror;

/// <summary>
/// Guarda la configuracion junto al ejecutable para no tener que reconfigurar
/// la app cada vez que se abre en una mesa.
///
/// Las ventanas se recuerdan por TITULO y no por handle: el handle cambia cada
/// vez que la dealer app se reinicia, el titulo no.
/// </summary>
internal sealed class Ajustes
{
    public string TituloOrigen { get; set; } = "";
    public int MonitorOrigen { get; set; } = -1;
    public int MonitorSalida { get; set; }
    public int Espejo { get; set; }
    public int Fps { get; set; } = 20;
    public bool OcultarDeCapturas { get; set; } = true;
    public bool DibujarCursor { get; set; } = true;
    public bool ClicsEspejados { get; set; }
    public string TituloFoco { get; set; } = "";
    public bool VigilanteActivo { get; set; }

    /// <summary>Al abrir la app, esperar a la dealer app e iniciar el espejo solo.</summary>
    public bool IniciarAlAbrir { get; set; }

    /// <summary>
    /// Posicion del panel en pantalla. null = nunca se ha movido: centrado.
    /// El tamaño no se guarda porque no es libre: lo decide el contenido, y lo
    /// unico que lo cambia es si "Ajustes avanzados" esta desplegado.
    /// </summary>
    public int? PanelX { get; set; }
    public int? PanelY { get; set; }
    public bool AvanzadoAbierto { get; set; }

    private static string Ruta =>
        Path.Combine(AppContext.BaseDirectory, "hdmimirror.config.json");

    public static Ajustes Cargar()
    {
        try
        {
            if (File.Exists(Ruta))
                return JsonSerializer.Deserialize<Ajustes>(File.ReadAllText(Ruta)) ?? new Ajustes();
        }
        catch
        {
            // Config corrupta o sin permisos: se arranca con los valores por defecto.
        }

        return new Ajustes();
    }

    public void Guardar()
    {
        try
        {
            var opciones = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(Ruta, JsonSerializer.Serialize(this, opciones));
        }
        catch
        {
            // Si la carpeta es de solo lectura no pasa nada: solo se pierde el recuerdo.
        }
    }
}
