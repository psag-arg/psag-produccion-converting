using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.ConteoCamara.Analisis;
using PSAG.Mantenimiento.ConteoCamara.Salida;

namespace PSAG.Mantenimiento.ConteoCamara.Tests;

/// <summary>API falsa: responde según el cuerpo del lote y anota qué lotes recibió, en orden.</summary>
internal sealed class ApiFalsa : HttpMessageHandler
{
    public Func<string, (HttpStatusCode Status, string Cuerpo)> Responder { get; set; } = _ => (HttpStatusCode.OK, "{}");
    public List<string> Recibidos { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var cuerpo = await request.Content!.ReadAsStringAsync(ct);
        var lote = JsonSerializer.Deserialize<LoteConteo>(cuerpo, LoteConteo.Json)!;
        Recibidos.Add(lote.PuntoControl);
        var (status, respuesta) = Responder(lote.PuntoControl);
        return new HttpResponseMessage(status) { Content = new StringContent(respuesta) };
    }
}

public sealed class PublicadorApiTests : IDisposable
{
    private static readonly TimeSpan Planta = TimeSpan.FromHours(-3);
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), "psag-publicador-" + Guid.NewGuid().ToString("N"));
    private readonly ApiFalsa _api = new();
    private DateTimeOffset _ahora = new(2026, 11, 12, 10, 5, 30, Planta);

    public void Dispose()
    {
        if (Directory.Exists(_carpeta)) Directory.Delete(_carpeta, recursive: true);
    }

    private PublicadorApi Nuevo()
    {
        var o = new OpcionesConteo { PuntoControl = "T2-CAM10" };
        o.Api.CarpetaPendientes = _carpeta;
        o.Api.ClaveIngesta = "clave";
        return new PublicadorApi(o, new HttpClient(_api) { BaseAddress = new Uri("http://api/") }, NullLogger.Instance, () => _ahora);
    }

    /// <summary>Un lote en pendientes; el punto de control hace de nombre para que la API falsa lo reconozca.</summary>
    private void Lote(int orden, string nombre)
    {
        Directory.CreateDirectory(_carpeta);
        File.WriteAllText(Path.Combine(_carpeta, $"lote-{orden:D17}-x.json"),
            JsonSerializer.Serialize(new LoteConteo { PuntoControl = nombre }, LoteConteo.Json));
    }

    private static (HttpStatusCode, string) Problema(HttpStatusCode status, string detalle) =>
        (status, JsonSerializer.Serialize(new { title = "Lote de conteo inválido", status = (int)status, detail = detalle, traceId = Guid.NewGuid() }));

    private string[] Pendientes() => Directory.GetFiles(_carpeta, "lote-*.json").Select(Path.GetFileName).ToArray()!;
    private string[] Rechazados() => Directory.Exists(Path.Combine(_carpeta, PublicadorApi.CarpetaRechazados))
        ? Directory.GetFiles(Path.Combine(_carpeta, PublicadorApi.CarpetaRechazados)).Select(Path.GetFileName).ToArray()!
        : [];

    [Fact]
    public async Task LoteRechazadoPorSuContenido_NoTrabaLaColaYTerminaEnRechazados()
    {
        var p = Nuevo();
        _api.Responder = n => n == "MALO" ? Problema(HttpStatusCode.BadRequest, "Parada X: el fin es anterior al inicio.") : (HttpStatusCode.OK, "{}");
        Lote(1, "MALO");
        Lote(2, "B");
        Lote(3, "C");

        await p.EnviarPendientesAsync(CancellationToken.None);
        Assert.Equal(["MALO", "B", "C"], _api.Recibidos);     // los siguientes se mandaron igual
        Assert.Single(Pendientes());                           // el malo queda para reintentar

        for (var ciclo = 2; ciclo <= PublicadorApi.IntentosAntesDeRechazar; ciclo++)
        {
            Lote(10 + ciclo, "NUEVO");
            await p.EnviarPendientesAsync(CancellationToken.None);
        }

        Assert.Empty(Pendientes());
        Assert.Equal(["lote-00000000000000001-x.json"], Rechazados());
    }

    [Fact]
    public async Task ApiCaida_CortaEnElPrimeroSinDesordenarNiApartar()
    {
        var p = Nuevo();
        _api.Responder = _ => (HttpStatusCode.ServiceUnavailable, "");
        Lote(1, "A");
        Lote(2, "B");

        for (var i = 0; i < 5; i++) await p.EnviarPendientesAsync(CancellationToken.None);

        Assert.All(_api.Recibidos, n => Assert.Equal("A", n));
        Assert.Equal(2, Pendientes().Length);
        Assert.Empty(Rechazados());
    }

    [Fact]
    public async Task MismoRechazoEnTodos_EsDeConfiguracion_CortaYNoApartaNada()
    {
        // Falta el punto de control en Maestros: la API devuelve 400 con el mismo motivo para cualquier lote.
        var p = Nuevo();
        _api.Responder = _ => Problema(HttpStatusCode.BadRequest, "No existe el punto de control T2-CAM10 o está inactivo.");
        for (var i = 1; i <= 5; i++) Lote(i, "L" + i);

        for (var ciclo = 0; ciclo < 5; ciclo++) await p.EnviarPendientesAsync(CancellationToken.None);

        Assert.Equal(10, _api.Recibidos.Count);               // dos por ciclo: no se manda todo el atraso cada vez
        Assert.Equal(5, Pendientes().Length);
        Assert.Empty(Rechazados());
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(413, true)]
    [InlineData(422, true)]
    [InlineData(401, false)]
    [InlineData(404, false)]
    [InlineData(429, false)]
    [InlineData(500, false)]
    public void SoloLos4xxDelContenidoSonDelLote(int status, bool delLote) =>
        Assert.Equal(delLote, PublicadorApi.EsRechazoDelLote(status));

    private static ConteoMinuto Minuto(DateTimeOffset minuto, int marcha, double rollos) =>
        new(minuto, 1, rollos, marcha, 0, 0, 60 - marcha);

    private LoteConteo[] LotesGuardados() => Directory.GetFiles(_carpeta, "lote-*.json").Order(StringComparer.Ordinal)
        .Select(f => JsonSerializer.Deserialize<LoteConteo>(File.ReadAllText(f), LoteConteo.Json)!).ToArray();

    [Fact]
    public void ReinicioDentroDelMismoMinuto_ElMinutoVaUnaSolaVezConLasDosPartes()
    {
        var minuto = new DateTimeOffset(2026, 11, 12, 10, 5, 0, Planta);
        var antes = Nuevo();
        antes.Minutos([Minuto(minuto.AddMinutes(-1), 60, 160)]);
        antes.Minutos([Minuto(minuto, 25, 9)]);                // se detiene a las 10:05:30 con 25 s del minuto
        antes.GuardarPendiente(apartarMinutoEnCurso: true);

        var lote = Assert.Single(LotesGuardados());
        Assert.Equal(minuto.AddMinutes(-1), Assert.Single(lote.Minutos).Minuto);   // el completo sí se manda
        Assert.True(File.Exists(Path.Combine(_carpeta, PublicadorApi.ArchivoMinutoEnCurso)));

        _ahora = minuto.AddSeconds(50);
        var despues = Nuevo();                                 // arranca a los 10:05:4x, el video vuelve en el mismo minuto
        despues.Minutos([Minuto(minuto, 15, 6)]);              // el resto del minuto (la primera parte queda SIN_SENAL)
        _ahora = minuto.AddSeconds(80);
        despues.Minutos([Minuto(minuto.AddMinutes(1), 60, 160)]);
        despues.GuardarPendiente();

        var minutos = LotesGuardados().SelectMany(l => l.Minutos).Where(m => m.Minuto == minuto).ToList();
        var m = Assert.Single(minutos);
        Assert.Equal(40, m.SegundosMarcha);
        Assert.Equal(20, m.SegundosSinSenal);
        Assert.Equal(15m, m.Rollos);
        Assert.False(File.Exists(Path.Combine(_carpeta, PublicadorApi.ArchivoMinutoEnCurso)));
    }

    [Fact]
    public void ReinicioEnOtroMinuto_ElMinutoRetenidoSeMandaComoQuedo()
    {
        var minuto = new DateTimeOffset(2026, 11, 12, 10, 5, 0, Planta);
        var antes = Nuevo();
        antes.Minutos([Minuto(minuto, 25, 9)]);
        antes.GuardarPendiente(apartarMinutoEnCurso: true);
        Assert.Empty(LotesGuardados());

        _ahora = minuto.AddMinutes(10);                        // arranca 10 min después y todavía no hay video
        var despues = Nuevo();
        despues.GuardarPendiente();

        var m = Assert.Single(Assert.Single(LotesGuardados()).Minutos);
        Assert.Equal(minuto, m.Minuto);
        Assert.Equal(25, m.SegundosMarcha);
        Assert.Equal(35, m.SegundosSinSenal);
        Assert.False(File.Exists(Path.Combine(_carpeta, PublicadorApi.ArchivoMinutoEnCurso)));
    }

    [Fact]
    public void AlDetener_UnMinutoViejoQuedoIncompletoPorUnCorte_SeMandaIgual()
    {
        // El video se cortó a las 10:01 y el servicio se detiene a las 10:05:30: ese minuto ya no puede partirse.
        var antes = Nuevo();
        antes.Minutos([Minuto(new DateTimeOffset(2026, 11, 12, 10, 1, 0, Planta), 25, 9)]);
        antes.GuardarPendiente(apartarMinutoEnCurso: true);

        Assert.Single(Assert.Single(LotesGuardados()).Minutos);
        Assert.False(File.Exists(Path.Combine(_carpeta, PublicadorApi.ArchivoMinutoEnCurso)));
    }
}
