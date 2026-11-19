using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>Agrupador de microcortes (reunión 17/11/2026): textos que ve el operario.</summary>
public class AgrupacionUiTests
{
    private static readonly TimeSpan Planta = TimeSpan.FromHours(-3);

    private static MicrocorteDto M(int h, int m, int s, int segundos) => new()
    {
        FechaHoraInicio = new DateTimeOffset(2026, 11, 13, h, m, s, Planta),
        FechaHoraFin = new DateTimeOffset(2026, 11, 13, h, m, s, Planta).AddSeconds(segundos),
        DuracionSegundos = segundos
    };

    [Theory]
    [InlineData(45, "45 s")]
    [InlineData(60, "1 min")]
    [InlineData(200, "3 min 20 s")]
    [InlineData(800, "13 min 20 s")]
    public void Duracion(int segundos, string esperado) => Assert.Equal(esperado, AgrupacionUi.Duracion(segundos));

    [Fact]
    public void Lista_OrdenadaPorHora() =>
        Assert.Equal("11:08 (3 min 20 s), 11:12 (2 min)", AgrupacionUi.Lista([M(11, 12, 3, 120), M(11, 8, 23, 200)]));

    [Fact]
    public void Resumen_CasoReal_DetencionDeclarada()
    {
        var a = new AgrupacionDetencionDto
        {
            InicioReal = new DateTimeOffset(2026, 11, 13, 11, 8, 23, Planta),
            CantidadMicrocortes = 2, SegundosMicrocortes = 320, SegundosDetencion = 480, SegundosTotales = 800
        };
        Assert.Equal("Inicio real 11:08 · tiempo total 13 min 20 s (detención 8 min + 2 microcortes 5 min 20 s)", AgrupacionUi.Resumen(a));
    }

    [Fact]
    public void Resumen_PendienteDeDeclarar_AvisaQueFaltaLaDetencion()
    {
        var a = new AgrupacionDetencionDto { CantidadMicrocortes = 1, SegundosMicrocortes = 120, SegundosTotales = 120 };
        Assert.Equal("tiempo total 2 min (1 microcorte 2 min; falta declarar la detención)", AgrupacionUi.Resumen(a));
    }
}
