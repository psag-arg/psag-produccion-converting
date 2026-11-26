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

    /// <summary>
    /// Código de la cámara (Maestros → Cámaras, T-798 etapa 4). Define la línea. Es la clave con la API: la cámara
    /// corre sólo si está habilitada allá (GET api/camaras/configuracion) y tiene esta calibración local.
    /// </summary>
    public string Camara { get; set; } = "T2-CAM09";

    /// <summary>
    /// CARRILES: brillo de los carriles de rollitos a la salida de la cortadora (cámara 09).
    /// MANGAS: paso de mangas por la mesa y giro del rodillo de la rebobinadora (cámara 10).
    /// </summary>
    public string Metodo { get; set; } = MetodosConteo.Carriles;

    /// <summary>
    /// T-798 etapa 4: el appsettings traía "Activa" (ya no se usa: la habilitación está en Maestros → Cámaras). Si
    /// viene, se ignora y el servicio lo avisa en el log.
    /// </summary>
    public bool ActivaIgnorada { get; set; }

    /// <summary>La entrada de la cámara define su propia ruta o canal del grabador (si no, el canal sale de la API).</summary>
    public bool RutaPropia { get; set; }

    /// <summary>Si la cámara informa paradas, según Maestros → Cámaras (null = sin dato de la API: según el método).</summary>
    public bool? DetectaParadas { get; set; }

    public OpcionesFuente Fuente { get; set; } = new();
    public OpcionesZonas Zonas { get; set; } = new();
    public OpcionesMangas Mangas { get; set; } = new();
    public OpcionesAnalisis Analisis { get; set; } = new();
    public OpcionesVigilancia Vigilancia { get; set; } = new();
    public OpcionesApi Api { get; set; } = new();

    public bool EsMangas => string.Equals(Metodo, MetodosConteo.Mangas, StringComparison.OrdinalIgnoreCase);

    /// <summary>T-785: cámara 8, cuenta bolsones (pasos por una zona, como las mangas) y no detecta paradas.</summary>
    public bool EsBolsones => string.Equals(Metodo, MetodosConteo.Bolsones, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Calibraciones locales de las cámaras (qué sabe analizar el servicio). Sin "ConteoCamara:Camaras" es una sola,
    /// la de la sección (como hasta ahora, cámara 09). Con la lista, cada cámara arranca de la sección común (Fuente,
    /// Api, Analisis, Vigilancia) y pisa lo que defina; si no define su carpeta de pendientes, usa una subcarpeta con su
    /// código. Cuáles corren lo decide la API (Maestros → Cámaras → Habilitada), no este archivo.
    /// </summary>
    public static List<OpcionesConteo> CargarCamaras(IConfiguration configuracion)
    {
        var seccion = configuracion.GetSection(Seccion);
        var camaras = seccion.GetSection("Camaras").GetChildren().ToList();
        if (camaras.Count == 0)
        {
            var unica = new OpcionesConteo();
            seccion.Bind(unica);
            unica.Camara = CodigoDe(seccion) ?? unica.Camara;
            unica.ActivaIgnorada = seccion.GetSection("Activa").Exists();
            unica.RutaPropia = seccion.GetSection("Fuente:RutaRtsp").Exists() || seccion.GetSection("Fuente:Canal").Exists();
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
            o.Camara = CodigoDe(camara) ?? o.Camara;
            o.ActivaIgnorada = camara.GetSection("Activa").Exists();
            o.RutaPropia = camara.GetSection("Fuente:RutaRtsp").Exists() || camara.GetSection("Fuente:Canal").Exists();
            // La cámara de la sección común (la 09 de siempre) conserva su carpeta: no quedan lotes huérfanos al actualizar.
            var esLaDeSiempre = string.Equals(o.Camara, CodigoDe(seccion), StringComparison.OrdinalIgnoreCase);
            if (camara.GetSection("Api:CarpetaPendientes").Value == null && !esLaDeSiempre)
                o.Api.CarpetaPendientes = Path.Combine(o.Api.CarpetaPendientes, o.Camara);
            lista.Add(o);
        }
        if (lista.Select(c => c.Camara.ToUpperInvariant()).Distinct().Count() != lista.Count)
            throw new InvalidOperationException("Hay dos cámaras con el mismo código (Camara) en ConteoCamara:Camaras.");
        return lista;
    }

    /// <summary>
    /// Código de la cámara de una sección: "Camara" o, en un appsettings viejo (el appsettings.local.json del servidor),
    /// "PuntoControl", que era el código del punto de control y la migración conservó. Si vienen los dos, gana "Camara".
    /// </summary>
    private static string? CodigoDe(IConfigurationSection seccion) =>
        !string.IsNullOrWhiteSpace(seccion[nameof(Camara)]) ? seccion[nameof(Camara)]
        : !string.IsNullOrWhiteSpace(seccion["PuntoControl"]) ? seccion["PuntoControl"]
        : null;

    /// <summary>Opciones de la API de la sección común (UrlBase, clave, carpeta de pendientes): para pedir la configuración.</summary>
    public static OpcionesApi CargarApiComun(IConfiguration configuracion)
    {
        var api = new OpcionesApi();
        configuracion.GetSection(Seccion).GetSection(nameof(Api)).Bind(api);
        return api;
    }

    /// <summary>Copia (para aplicarle lo que dice la API sin tocar la calibración cargada del archivo).</summary>
    public OpcionesConteo Copiar()
    {
        var copia = (OpcionesConteo)MemberwiseClone();
        copia.Fuente = (OpcionesFuente)Fuente.Copiar();
        return copia;
    }
}

public static class MetodosConteo
{
    public const string Carriles = "CARRILES";
    public const string Mangas = "MANGAS";

    /// <summary>Pasos de bolsones por una zona (cámara 8, T-785). Mismo contador que las mangas, sin paradas.</summary>
    public const string Bolsones = "BOLSONES";
}

/// <summary>T-804: detectores de paradas de la cámara de mangas (<see cref="OpcionesMangas.DetectorParadas"/>).</summary>
public static class DetectoresParadas
{
    /// <summary>Ventanas de 10 s con histéresis y reglas A/E/F (default, validado con video del 13 y 14/11).</summary>
    public const string Ventanas = "VENTANAS";

    /// <summary>Cuadro a cuadro por mangas y rodillo (T-784). Activable por parámetro para probar.</summary>
    public const string Mangas = "MANGAS";

    public static bool EsMangas(string? valor) => string.Equals(valor?.Trim(), Mangas, StringComparison.OrdinalIgnoreCase);

    public static bool EsValido(string? valor) =>
        string.Equals(valor?.Trim(), Ventanas, StringComparison.OrdinalIgnoreCase) || EsMangas(valor);
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

    /// <summary>Movimiento medio (0-255) de la mesa que marca que empieza a pasar una manga (pulsos reales: 15-45).</summary>
    public double UmbralPasoAlto { get; set; } = 15;

    /// <summary>Debajo de esto terminó de pasar (histéresis: un pulso cuenta una sola vez).</summary>
    public double UmbralPasoBajo { get; set; } = 4;

    /// <summary>
    /// Dos pasos más juntos que esto son la misma manga (a ritmo normal hay 2,7 s entre mangas). Con 1,2 s una
    /// manga que "rebota" ~1,4 s después de caer se contaba dos veces (14/11: +2,3 %); con 2,0 y PasoAlto 15, 0 %.
    /// </summary>
    public double SegundosMinimosEntrePasos { get; set; } = 2.0;

    /// <summary>Movimiento medio del rodillo a partir del cual se lo da por girando.</summary>
    public double UmbralRodilloGira { get; set; } = 6;

    /// <summary>Fracción de los cuadros de la ventana con el rodillo quieto para dar la ventana como PARADA.</summary>
    public double FraccionRodilloQuieto { get; set; } = 0.5;

    /// <summary>
    /// Regla E: una ventana cuenta como MARCHA sólo si el rodillo giró en al menos esta fracción de sus cuadros.
    /// Entre esto y <see cref="FraccionRodilloQuieto"/> la ventana es PARADA (la máquina paró o arrancó en el medio,
    /// o alguien tapa el rodillo, como al limpiar la mesa con aire). 0 = sin la regla (alcanza con no ser PARADA).
    /// </summary>
    public double FraccionRodilloGiraParaMarcha { get; set; } = 0.9;

    /// <summary>
    /// Regla F: con una parada ya abierta, lo que se mueve en la mesa (operarios, limpieza, una manga que se
    /// acomoda) no es producción. Las mangas de esas ventanas se retienen y se descartan si la parada sigue; se
    /// cuentan sólo si se confirma el arranque (Analisis:VentanasParaConfirmarArranque).
    /// </summary>
    public bool DescontarMangasEnParada { get; set; } = true;

    /// <summary>
    /// T-804: con qué detector salen las paradas de la cámara de mangas (<see cref="DetectoresParadas"/>).
    /// VENTANAS (default, productivo desde el 19/11): histéresis de ventanas de 10 s de <see cref="Analisis.DetectorParadas"/>
    /// con las reglas A, E y F. MANGAS (T-784, para probar): <see cref="Analisis.DetectorParadasMangas"/>, cuadro a cuadro,
    /// con SegundosSinMangasParaParada, MangasParaReanudar y SegundosRodilloQuietoParaParada; ahí la regla F no aplica
    /// (las mangas se cuentan siempre). Sin paradas (cámara 8) da lo mismo: sólo cuenta.
    /// </summary>
    public string DetectorParadas { get; set; } = DetectoresParadas.Ventanas;

    /// <summary>
    /// Sólo con DetectorParadas = MANGAS (T-784): segundos sin mangas, o con el rodillo quieto seguido, para dar la
    /// máquina por parada. Tiene que ser más que la separación del empalme (mangas cada ~7 s con la máquina más lenta).
    /// </summary>
    public double SegundosSinMangasParaParada { get; set; } = 10;

    /// <summary>Sólo con MANGAS: mangas seguidas (a menos de SegundosSinMangasParaParada una de otra) para dar la máquina por reanudada.</summary>
    public int MangasParaReanudar { get; set; } = 2;

    /// <summary>Sólo con MANGAS: segundos seguidos con el rodillo quieto durante la parada para que sea de tipo PARADA (si no, SIN_MANGAS).</summary>
    public double SegundosRodilloQuietoParaParada { get; set; } = 5;
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
    public OpcionesFuente Copiar() => (OpcionesFuente)MemberwiseClone();

    /// <summary>ffmpeg.exe portable. Relativo a la carpeta del servicio o absoluto.</summary>
    public string RutaFfmpeg { get; set; } = @"ffmpeg\ffmpeg.exe";

    /// <summary>Archivo .env con NVR, NVR_USER y NVR_PASS (fuera del repo, con permisos restringidos).</summary>
    public string ArchivoCredenciales { get; set; } = @"C:\PSAG\secretos\nvr.env";

    public int PuertoRtsp { get; set; } = 554;

    /// <summary>Canal 09 en vivo, stream principal. Si hay <see cref="Canal"/>, manda el canal.</summary>
    public string RutaRtsp { get; set; } = "/Streaming/Channels/901";

    /// <summary>
    /// T-785: número de cámara del grabador Hikvision (8, 9, 10...). Si está, la ruta es la del stream principal de
    /// ese canal (/Streaming/Channels/{Canal}01) y no hace falta escribir RutaRtsp. Se configura por cámara.
    /// </summary>
    public int? Canal { get; set; }

    /// <summary>Ruta RTSP que se usa: la del canal si está configurado; si no, RutaRtsp.</summary>
    public string RutaEfectiva => Canal is > 0 ? $"/Streaming/Channels/{Canal}01" : RutaRtsp;

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

    /// <summary>
    /// Regla A: ventanas seguidas en marcha que hacen falta para cerrar una parada abierta (6 = 60 s). Un arranque
    /// más corto (o lo que parece marcha mientras limpian la máquina) queda dentro de la parada. 0 = igual que
    /// <see cref="VentanasParaConfirmar"/>.
    /// </summary>
    public int VentanasParaConfirmarArranque { get; set; }

    /// <summary>Paradas más cortas que esto no se informan (quedan igual en los minutos).</summary>
    public int SegundosMinimosParada { get; set; } = 20;

    /// <summary>
    /// Si el video se corta o se congela con la línea detenida y vuelve antes de esto con la línea todavía
    /// detenida, se retoma la misma parada (mismo identificador). Más largo, o si volvió en marcha, la parada
    /// se cierra donde empezó el corte con FinIncierto (en el medio pudo haber arrancado y vuelto a parar).
    /// </summary>
    public int SegundosMaximosCorteParaRetomar { get; set; } = 120;
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

    /// <summary>Cada cuántos minutos se vuelve a pedir la lista de cámaras habilitadas (GET api/camaras/configuracion).</summary>
    public int MinutosRefrescoConfiguracion { get; set; } = 5;
}
