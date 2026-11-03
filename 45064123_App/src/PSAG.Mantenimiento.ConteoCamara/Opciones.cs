using Microsoft.Extensions.Configuration;

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

    /// <summary>
    /// CARRILES: brillo de los carriles de rollitos a la salida de la cortadora (cámara 09).
    /// MANGAS: paso de mangas por la mesa y giro del rodillo de la rebobinadora (cámara 10).
    /// </summary>
    public string Metodo { get; set; } = MetodosConteo.Carriles;

    /// <summary>Apagada, el servicio no la lee (las herramientas sí, con --camara). Para dejarla lista sin permiso en el NVR.</summary>
    public bool Activa { get; set; } = true;

    public OpcionesFuente Fuente { get; set; } = new();
    public OpcionesZonas Zonas { get; set; } = new();
    public OpcionesMangas Mangas { get; set; } = new();
    public OpcionesAnalisis Analisis { get; set; } = new();
    public OpcionesVigilancia Vigilancia { get; set; } = new();
    public OpcionesApi Api { get; set; } = new();

    public bool EsMangas => string.Equals(Metodo, MetodosConteo.Mangas, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Cámaras que corre el servicio. Sin "ConteoCamara:Camaras" es una sola, la de la sección (como hasta ahora,
    /// cámara 09). Con la lista, cada cámara arranca de la sección común (Fuente, Api, Analisis, Vigilancia) y
    /// pisa lo que defina; si no define su carpeta de pendientes, usa una subcarpeta con su código.
    /// </summary>
    public static List<OpcionesConteo> CargarCamaras(IConfiguration configuracion)
    {
        var seccion = configuracion.GetSection(Seccion);
        var camaras = seccion.GetSection("Camaras").GetChildren().ToList();
        if (camaras.Count == 0)
        {
            var unica = new OpcionesConteo();
            seccion.Bind(unica);
            return [unica];
        }

        var lista = new List<OpcionesConteo>();
        foreach (var camara in camaras)
        {
            var o = new OpcionesConteo();
            seccion.GetSection(nameof(Fuente)).Bind(o.Fuente);
            seccion.GetSection(nameof(Analisis)).Bind(o.Analisis);
            seccion.GetSection(nameof(Vigilancia)).Bind(o.Vigilancia);
            seccion.GetSection(nameof(Api)).Bind(o.Api);
            // Los carriles de la 09 siguen en zonas-camara.json (ConteoCamara:Zonas), salvo que la cámara traiga los suyos.
            if (!camara.GetSection(nameof(Zonas)).Exists())
                seccion.GetSection(nameof(Zonas)).Bind(o.Zonas);
            camara.Bind(o);
            // La cámara de la sección común (la 09 de siempre) conserva su carpeta: no quedan lotes huérfanos al actualizar.
            var esLaDeSiempre = string.Equals(o.PuntoControl, seccion[nameof(PuntoControl)], StringComparison.OrdinalIgnoreCase);
            if (camara.GetSection("Api:CarpetaPendientes").Value == null && !esLaDeSiempre)
                o.Api.CarpetaPendientes = Path.Combine(o.Api.CarpetaPendientes, o.PuntoControl);
            lista.Add(o);
        }
        if (lista.Select(c => c.PuntoControl.ToUpperInvariant()).Distinct().Count() != lista.Count)
            throw new InvalidOperationException("Hay dos cámaras con el mismo PuntoControl en ConteoCamara:Camaras.");
        return lista;
    }
}

public static class MetodosConteo
{
    public const string Carriles = "CARRILES";
    public const string Mangas = "MANGAS";
}

/// <summary>Rectángulo en px de la imagen original de la cámara (se escala a la resolución decodificada).</summary>
public sealed class Rectangulo
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Ancho { get; set; }
    public int Alto { get; set; }
}

/// <summary>
/// Cámara 10 (rebobinadora). Medido en grabaciones del 03/11 (día y noche), a 320x180 y 5 fps: con la máquina
/// en marcha cada manga que cruza la mesa da un pulso de movimiento de 15-45 (fondo 0-2) cada ~2,7 s; el rodillo
/// de arriba a la izquierda tiene ~20 girando y 0-1 quieto.
/// </summary>
public sealed class OpcionesMangas
{
    /// <summary>Resolución de la imagen original; las zonas están en esa escala.</summary>
    public int AnchoImagen { get; set; } = 2560;
    public int AltoImagen { get; set; } = 1440;

    /// <summary>Franja de la mesa por donde cruzan las mangas.</summary>
    public Rectangulo Mesa { get; set; } = new() { X = 1120, Y = 160, Ancho = 960, Alto = 560 };

    /// <summary>Rodillo verde (de acompañamiento). Null o UsarRodillo=false: sólo mangas.</summary>
    public Rectangulo? Rodillo { get; set; } = new() { X = 240, Y = 80, Ancho = 320, Alto = 480 };

    public bool UsarRodillo { get; set; } = true;

    /// <summary>Movimiento medio (0-255) de la mesa que marca que empieza a pasar una manga.</summary>
    public double UmbralPasoAlto { get; set; } = 12;

    /// <summary>Debajo de esto terminó de pasar (histéresis: un pulso cuenta una sola vez).</summary>
    public double UmbralPasoBajo { get; set; } = 4;

    /// <summary>Dos pasos más juntos que esto son la misma manga (a ritmo normal hay 2,7 s entre mangas).</summary>
    public double SegundosMinimosEntrePasos { get; set; } = 1.2;

    /// <summary>Movimiento medio del rodillo a partir del cual se lo da por girando.</summary>
    public double UmbralRodilloGira { get; set; } = 6;

    /// <summary>Fracción de los cuadros de la ventana con el rodillo quieto para dar la ventana como PARADA.</summary>
    public double FraccionRodilloQuieto { get; set; } = 0.5;
}

/// <summary>
/// Vigilancia del video: imagen congelada (cuadros idénticos: ni el reloj de la cámara ni el ruido cambian) y
/// pérdida de conexión. Mientras dura no se analiza nada (no hay paradas falsas) y se manda una alerta.
/// </summary>
public sealed class OpcionesVigilancia
{
    /// <summary>Segundos seguidos de cuadros idénticos para dar la imagen por congelada.</summary>
    public double SegundosCongelado { get; set; } = 6;

    /// <summary>
    /// Diferencia máxima de un píxel entre dos cuadros seguidos que todavía cuenta como "idéntico". Con la
    /// máquina y el rodillo quietos el ruido da 15 o más en algún píxel, y el reloj cambia cada segundo.
    /// </summary>
    public int DiferenciaMaximaIdentico { get; set; } = 4;

    /// <summary>Sin cuadros del grabador durante este tiempo se abre la alerta de pérdida de conexión.</summary>
    public int SegundosSinVideoParaAlerta { get; set; } = 60;
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
