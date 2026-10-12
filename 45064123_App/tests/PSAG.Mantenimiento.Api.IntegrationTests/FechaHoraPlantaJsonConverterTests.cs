using System.Text.Json;
using PSAG.Mantenimiento.Api.Configuration;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

public class FechaHoraPlantaJsonConverterTests
{
    private sealed record Fechas(DateTimeOffset Registrado, DateTimeOffset? Cierre, DateTimeOffset? SinCierre);

    private static readonly JsonSerializerOptions Opciones = new()
    {
        Converters = { new FechaHoraPlantaJsonConverter(TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires")) }
    };

    [Fact]
    public void LasFechasSalenConElOffsetDeLaPlanta_TambienLasNullables()
    {
        // Así las devuelve SQLite: en UTC.
        var utc = new DateTimeOffset(2026, 10, 12, 15, 54, 0, TimeSpan.Zero);

        var json = JsonSerializer.Serialize(new Fechas(utc, utc, null), Opciones);

        Assert.Equal("""{"Registrado":"2026-10-12T12:54:00-03:00","Cierre":"2026-10-12T12:54:00-03:00","SinCierre":null}""", json);
    }

    [Fact]
    public void AlLeer_ConservaElInstante()
    {
        var leidas = JsonSerializer.Deserialize<Fechas>("""{"Registrado":"2026-10-12T15:54:00Z","Cierre":"2026-10-12T16:54:00+01:00"}""", Opciones)!;

        Assert.Equal(new DateTimeOffset(2026, 10, 12, 15, 54, 0, TimeSpan.Zero), leidas.Registrado);
        Assert.Equal(leidas.Registrado, leidas.Cierre);
    }
}
