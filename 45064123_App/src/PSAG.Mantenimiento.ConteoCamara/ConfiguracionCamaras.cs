using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.ConteoCamara.Salida;

namespace PSAG.Mantenimiento.ConteoCamara;

/// <summary>
/// Una cámara habilitada según la API (GET api/camaras/configuracion, T-798 etapa 4). Espejo de CamaraConfiguracionDto.
/// </summary>
public sealed class CamaraRemota
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? Canal { get; set; }
    public string Funcion { get; set; } = string.Empty;
    public string? Magnitud { get; set; }
    public string? MetodoDeteccion { get; set; }
    public int? CantidadCarriles { get; set; }
    public bool DetectaParadas { get; set; }
    public bool CreaDetenciones { get; set; }
    public int UmbralDetencionSegundos { get; set; }

    public bool EsConteo => string.Equals(Funcion, "CONTEO", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Pide a la API qué cámaras están habilitadas (Maestros → Cámaras: única fuente de verdad). Cada respuesta buena se
/// guarda en <see cref="ArchivoCache"/> (carpeta de pendientes común): si la API no responde, el servicio sigue con la
/// última lista conocida para no dejar de contar.
/// </summary>
public sealed class ClienteConfiguracionCamaras
{
    public const string Ruta = "api/camaras/configuracion";
    public const string ArchivoCache = "camaras-configuracion.json";

    private readonly OpcionesApi _api;
    private readonly HttpClient _http;
    private readonly ILogger _logger;
    private readonly string _rutaCache;

    public ClienteConfiguracionCamaras(OpcionesApi api, HttpClient http, ILogger logger)
    {
        _api = api;
        _http = http;
        _logger = logger;
        var carpeta = Path.IsPathRooted(api.CarpetaPendientes)
            ? api.CarpetaPendientes
            : Path.Combine(AppContext.BaseDirectory, api.CarpetaPendientes);
        Directory.CreateDirectory(carpeta);
        _rutaCache = Path.Combine(carpeta, ArchivoCache);
    }

    /// <summary>
    /// La lista de la API (y la guarda en el cache) o, si la API no responde o rechaza, la del cache
    /// (<c>DeCache</c> = true). Null si no hay ninguna de las dos (primer arranque con la API caída).
    /// </summary>
    public async Task<(List<CamaraRemota>? Lista, bool DeCache)> ObtenerAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_api.ClaveIngesta))
        {
            _logger.LogWarning("Sin ConteoCamara:Api:ClaveIngesta no se puede pedir la configuración de cámaras: se usa la última conocida");
            return (LeerCache(), true);
        }
        try
        {
            using var pedido = new HttpRequestMessage(HttpMethod.Get, Ruta);
            pedido.Headers.Add(PublicadorApi.HeaderClave, _api.ClaveIngesta);
            using var respuesta = await _http.SendAsync(pedido, ct);
            if (respuesta.IsSuccessStatusCode)
            {
                var lista = await respuesta.Content.ReadFromJsonAsync<List<CamaraRemota>>(LoteConteo.Json, ct) ?? [];
                GuardarCache(lista);
                return (lista, false);
            }
            _logger.LogWarning("La API respondió {Status} al pedir la configuración de cámaras: se usa la última conocida", (int)respuesta.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            _logger.LogWarning("No se pudo pedir la configuración de cámaras ({Mensaje}): se usa la última conocida", ex.Message);
        }
        return (LeerCache(), true);
    }

    private void GuardarCache(List<CamaraRemota> lista)
    {
        try
        {
            File.WriteAllText(_rutaCache + ".tmp", JsonSerializer.Serialize(lista, LoteConteo.Json));
            File.Move(_rutaCache + ".tmp", _rutaCache, overwrite: true);
        }
        catch (IOException ex)
        {
            _logger.LogWarning("No se pudo guardar {Archivo}: {Mensaje}", ArchivoCache, ex.Message);
        }
    }

    private List<CamaraRemota>? LeerCache()
    {
        if (!File.Exists(_rutaCache))
            return null;
        try
        {
            return JsonSerializer.Deserialize<List<CamaraRemota>>(File.ReadAllText(_rutaCache), LoteConteo.Json);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("No se pudo leer {Archivo} ({Mensaje})", ArchivoCache, ex.Message);
            return null;
        }
    }
}

/// <summary>Qué cámaras corre el servicio: las de conteo habilitadas en la API para las que hay calibración local.</summary>
public static class PlanCamaras
{
    /// <summary>
    /// Cruza la calibración local (appsettings) con las habilitadas de la API, por código (sin mayúsculas/minúsculas).
    /// A cada una que corre le aplica lo de la API: el canal (si la entrada local no define ruta ni canal) y si detecta
    /// paradas. Las habilitadas de conteo sin calibración local vuelven aparte (se loguean y se alertan).
    /// </summary>
    public static (List<OpcionesConteo> Correr, List<CamaraRemota> SinCalibracion) Elegir(
        IReadOnlyList<OpcionesConteo> locales, IReadOnlyList<CamaraRemota> habilitadas)
    {
        var correr = new List<OpcionesConteo>();
        var sinCalibracion = new List<CamaraRemota>();
        foreach (var remota in habilitadas.Where(r => r.EsConteo))
        {
            var local = locales.FirstOrDefault(l => string.Equals(l.Camara, remota.Codigo, StringComparison.OrdinalIgnoreCase));
            if (local == null)
            {
                sinCalibracion.Add(remota);
                continue;
            }
            var o = local.Copiar();
            if (!o.RutaPropia && remota.Canal is > 0)
                o.Fuente.Canal = remota.Canal;
            o.DetectaParadas = remota.DetectaParadas;
            correr.Add(o);
        }
        return (correr, sinCalibracion);
    }
}
