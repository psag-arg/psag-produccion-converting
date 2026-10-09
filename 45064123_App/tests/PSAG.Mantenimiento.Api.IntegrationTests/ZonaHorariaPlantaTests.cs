using Microsoft.Extensions.Configuration;
using PSAG.Mantenimiento.Api.Configuration;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

public class ZonaHorariaPlantaTests
{
    private static IConfiguration Configuracion(string? zona) => new ConfigurationBuilder()
        .AddInMemoryCollection(zona == null
            ? new Dictionary<string, string?>()
            : new Dictionary<string, string?> { [ZonaHorariaPlanta.Clave] = zona })
        .Build();

    private static readonly DateTime Mediodia = new(2026, 8, 20, 12, 0, 0);

    [Fact]
    public void SinConfiguracion_UsaArgentina()
    {
        var zona = ZonaHorariaPlanta.Resolver(Configuracion(null));

        Assert.Equal(TimeSpan.FromHours(-3), zona.GetUtcOffset(Mediodia));
    }

    [Fact]
    public void IdIana_SeRespetaAunqueLaZonaDelServidorSeaOtra()
    {
        var zona = ZonaHorariaPlanta.Resolver(Configuracion("Asia/Tokyo"));

        Assert.Equal(TimeSpan.FromHours(9), zona.GetUtcOffset(Mediodia));
    }

    [Fact]
    public void IdInexistente_CaeAlIdDeWindowsDeArgentina()
    {
        var zona = ZonaHorariaPlanta.Resolver(Configuracion("Zona/Inexistente"));

        Assert.Equal(TimeSpan.FromHours(-3), zona.GetUtcOffset(Mediodia));
    }
}
