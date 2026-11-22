using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Application.DTOs.Camaras;
using PSAG.Mantenimiento.ConteoCamara.Salida;

namespace PSAG.Mantenimiento.ConteoCamara.Tests;

/// <summary>API falsa para GET api/camaras/configuracion: responde lo que se le diga o falla.</summary>
internal sealed class ApiConfiguracionFalsa : HttpMessageHandler
{
    public Func<HttpResponseMessage>? Responder { get; set; }
    public List<string?> ClavesRecibidas { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ClavesRecibidas.Add(request.Headers.TryGetValues(PublicadorApi.HeaderClave, out var v) ? v.Single() : null);
        Assert.Equal("/" + ClienteConfiguracionCamaras.Ruta, request.RequestUri!.AbsolutePath);
        return Responder is { } r ? Task.FromResult(r()) : throw new HttpRequestException("API caída");
    }
}

/// <summary>
/// T-798 etapa 4: el servicio corre sólo las cámaras habilitadas en Maestros → Cámaras que tienen calibración local;
/// si la API no responde, sigue con la última lista conocida.
/// </summary>
public sealed class ConfiguracionCamarasApiTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "psag-config-camaras-" + Guid.NewGuid().ToString("N"));
    private readonly ApiConfiguracionFalsa _api = new();

    public void Dispose()
    {
        if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
    }

    private ClienteConfiguracionCamaras Cliente() =>
        new(new OpcionesApi { CarpetaPendientes = _carpeta, ClaveIngesta = "clave-de-prueba" },
            new HttpClient(_api) { BaseAddress = new Uri("http://api/") }, NullLogger.Instance);

    private static HttpResponseMessage Lista(params CamaraConfiguracionDto[] camaras) =>
        new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(camaras, new JsonSerializerOptions(JsonSerializerDefaults.Web))) };

    private static CamaraConfiguracionDto Remota(string codigo, int? canal = null, string funcion = "CONTEO") => new()
    {
        Codigo = codigo, Nombre = codigo, LineaId = 1, Canal = canal, Funcion = funcion, Magnitud = "MANGAS",
        MetodoDeteccion = "PASOS", DetectaParadas = true, UmbralDetencionSegundos = 300
    };

    private static List<OpcionesConteo> Locales()
    {
        var c09 = new OpcionesConteo { Camara = "T2-CAM09", Metodo = MetodosConteo.Carriles };
        var c10 = new OpcionesConteo { Camara = "T2-CAM10", Metodo = MetodosConteo.Mangas, RutaPropia = true };
        c10.Fuente.RutaRtsp = "/Streaming/Channels/1001";
        var c08 = new OpcionesConteo { Camara = "T2-CAM08", Metodo = MetodosConteo.Bolsones };
        return [c09, c10, c08];
    }

    [Fact]
    public async Task ApiResponde_DevuelveLaListaConLaClaveYLaGuardaEnElCache()
    {
        _api.Responder = () => Lista(Remota("T2-CAM10", 10));

        var (lista, deCache) = await Cliente().ObtenerAsync(CancellationToken.None);

        Assert.False(deCache);
        Assert.Equal("T2-CAM10", Assert.Single(lista!).Codigo);
        Assert.Equal(["clave-de-prueba"], _api.ClavesRecibidas);
        Assert.True(File.Exists(Path.Combine(_carpeta, ClienteConfiguracionCamaras.ArchivoCache)));
    }

    [Fact]
    public async Task SinRespuestaDeLaApi_UsaLaUltimaListaConocida()
    {
        _api.Responder = () => Lista(Remota("T2-CAM10", 10), Remota("T2-CAM09", 9));
        await Cliente().ObtenerAsync(CancellationToken.None);

        // Se reinicia el servicio con la API caída: sale del cache en disco.
        _api.Responder = null;
        var (lista, deCache) = await Cliente().ObtenerAsync(CancellationToken.None);

        Assert.True(deCache);
        Assert.Equal(["T2-CAM10", "T2-CAM09"], lista!.Select(c => c.Codigo));

        // Una respuesta de error (500) tampoco borra la lista.
        _api.Responder = () => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        (lista, deCache) = await Cliente().ObtenerAsync(CancellationToken.None);
        Assert.True(deCache);
        Assert.Equal(2, lista!.Count);
    }

    [Fact]
    public async Task SinApiNiCache_NoHayLista()
    {
        var (lista, _) = await Cliente().ObtenerAsync(CancellationToken.None);
        Assert.Null(lista);
    }

    [Fact]
    public void Plan_CorreSoloLasHabilitadasConCalibracion_YAvisaLasQueNoTienen()
    {
        // Habilitadas en la API: 10 (con calibración), 07 (sin calibración) y una de calidad. La 09 y la 8 no están
        // habilitadas: no corren aunque tengan calibración.
        var remotas = new[] { Remota("t2-cam10", 10), Remota("T2-CAM07", 7), Remota("T2-CAM03", 3, funcion: "CALIDAD") }
            .Select(r => JsonSerializer.Deserialize<CamaraRemota>(JsonSerializer.Serialize(r, LoteConteo.Json), LoteConteo.Json)!)
            .ToList();

        var (correr, sinCalibracion) = PlanCamaras.Elegir(Locales(), remotas);

        Assert.Equal(["T2-CAM10"], correr.Select(c => c.Camara));
        Assert.Equal(["T2-CAM07"], sinCalibracion.Select(r => r.Codigo));   // la de calidad no es del servicio de conteo
    }

    [Fact]
    public void Plan_ElCanalSaleDeLaApiSiElAppsettingsNoLoTrae()
    {
        var remotas = new List<CamaraRemota>
        {
            new() { Codigo = "T2-CAM09", Funcion = "CONTEO", Canal = 9, DetectaParadas = true },
            new() { Codigo = "T2-CAM10", Funcion = "CONTEO", Canal = 12, DetectaParadas = false }
        };
        var locales = Locales();

        var (correr, _) = PlanCamaras.Elegir(locales, remotas);

        var c09 = correr.Single(c => c.Camara == "T2-CAM09");
        var c10 = correr.Single(c => c.Camara == "T2-CAM10");
        Assert.Equal("/Streaming/Channels/901", c09.Fuente.RutaEfectiva);    // de la API
        Assert.Equal("/Streaming/Channels/1001", c10.Fuente.RutaEfectiva);   // la ruta propia del appsettings manda
        Assert.False(c10.DetectaParadas);
        Assert.Null(locales[0].Fuente.Canal);                               // la calibración cargada no se toca
    }
}
