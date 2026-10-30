namespace PSAG.Mantenimiento.ConteoCamara;

/// <summary>
/// Configuración del servicio (sección "ConteoCamara" de appsettings.json; las zonas en zonas-camara.json).
/// Las credenciales del NVR NO van acá: se leen del archivo de <see cref="OpcionesFuente.ArchivoCredenciales"/>.
/// La clave de ingesta va en la variable de entorno ConteoCamara__Api__ClaveIngesta (NSSM AppEnvironmentExtra).
/// </summary>
public sealed class OpcionesConteo
{
    public const string Seccion = "ConteoCamara";

    /// <summary>Código del punto de control (Maestros → Calidad) que identifica la cámara. Define la línea.</summary>
    public string PuntoControl { get; set; } = "T2-CAM09";

    public OpcionesFuente Fuente { get; set; } = new();
    public OpcionesZonas Zonas { get; set; } = new();
    public OpcionesAnalisis Analisis { get; set; } = new();
    public OpcionesApi Api { get; set; } = new();
}

public sealed class OpcionesFuente
{
    /// <summary>ffmpeg.exe portable. Relativo a la carpeta del servicio o absoluto.</summary>
    public string RutaFfmpeg { get; set; } = @"ffmpeg\ffmpeg.exe";

    /// <summary>Archivo .env con NVR, NVR_USER y NVR_PASS (fuera del repo, con permisos restringidos).</summary>
    public string ArchivoCredenciales { get; set; } = @"C:\PSAG\secretos\nvr.env";

    public int PuertoRtsp { get; set; } = 554;

    /// <summary>Canal 09 en vivo, stream principal.</summary>
    public string RutaRtsp { get; set; } = "/Streaming/Channels/901";

    /// <summary>Resolución a la que ffmpeg entrega los cuadros (gris, 1 byte por píxel).</summary>
    public int AnchoDecodificado { get; set; } = 640;
    public int AltoDecodificado { get; set; } = 480;

    /// <summary>Cuadros por segundo para el análisis. ~2,8 rollos/s por carril: 10 fps alcanza (Nyquist 5 Hz).</summary>
    public int Fps { get; set; } = 10;

    public int HilosDecodificacion { get; set; } = 2;

    /// <summary>Si no llega un cuadro en este tiempo se reinicia ffmpeg y el hueco queda como SIN_SENAL.</summary>
    public int SegundosSinCuadrosParaReiniciar { get; set; } = 15;

    /// <summary>Espera antes de reconectar (crece hasta 60 s si sigue fallando).</summary>
    public int SegundosEsperaReconexion { get; set; } = 5;
}

public sealed class OpcionesZonas
{
    /// <summary>Resolución de la imagen original de la cámara; las coordenadas de los carriles están en esa escala.</summary>
    public int AnchoImagen { get; set; } = 2560;
    public int AltoImagen { get; set; } = 1920;

    public List<ZonaCarril> Carriles { get; set; } = [];
}

/// <summary>Punto de muestreo de un carril: cuadrado de <see cref="Lado"/> px centrado en (X, Y), en px de la imagen original.</summary>
public sealed class ZonaCarril
{
    public int Carril { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Lado { get; set; } = 32;
}

public sealed class OpcionesAnalisis
{
    public int SegundosVentana { get; set; } = 10;
    public double FrecuenciaMinimaHz { get; set; } = 0.5;
    public double FrecuenciaMaximaHz { get; set; } = 4.9;

    /// <summary>Distancia máxima entre el pico del carril y la frecuencia común para darlo en marcha.</summary>
    public double ToleranciaHz { get; set; } = 0.15;

    /// <summary>Desvío estándar mínimo del brillo (0-255) para considerar que el carril se mueve.</summary>
    public double AmplitudMinima { get; set; } = 8;

    /// <summary>El pico cerca de la frecuencia común tiene que ser al menos esta fracción del pico máximo del carril.</summary>
    public double ProporcionPicoMinima { get; set; } = 0.3;

    /// <summary>Velocidad normal de corte (rollos/s por carril). En las pruebas: 2,78 (167/min) en todos los turnos.</summary>
    public double FrecuenciaNominalHz { get; set; } = 2.78;

    /// <summary>
    /// Debajo de esta fracción de la velocidad normal el carril se informa como ACUMULACION (marcha lenta o a los
    /// tirones, típico con la curva llena), aunque los rollos que pasan se siguen contando.
    /// </summary>
    public double FraccionMinimaNominal { get; set; } = 0.8;

    /// <summary>Ventanas seguidas que hacen falta para cambiar de estado (histéresis). 2 = 20 s.</summary>
    public int VentanasParaConfirmar { get; set; } = 2;

    /// <summary>Paradas más cortas que esto no se informan (quedan igual en los minutos).</summary>
    public int SegundosMinimosParada { get; set; } = 20;
}

public sealed class OpcionesApi
{
    /// <summary>API de Converting en el mismo servidor.</summary>
    public string UrlBase { get; set; } = "http://localhost:5188";

    /// <summary>Misma clave que Calidad:ClaveIngesta de la API. Por variable de entorno, nunca en el archivo.</summary>
    public string? ClaveIngesta { get; set; }

    /// <summary>Lotes que todavía no se pudieron enviar (sobreviven a un reinicio).</summary>
    public string CarpetaPendientes { get; set; } = "pendientes";

    public int SegundosEntreEnvios { get; set; } = 30;
}
