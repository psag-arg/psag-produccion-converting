using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class HoraPlantaTests
{
    private static readonly TimeZoneInfo Argentina = TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires");

    [Fact]
    public void UnaParadaCargadaALas9_SeVeALas9AunqueLaBaseLaDevuelvaEnUtc()
    {
        var cargada = TiempoManualCalculador.Calcular(new DateOnly(2026, 3, 10), new TimeOnly(9, 0), new TimeOnly(9, 25), Argentina);
        var leidaDeLaBase = cargada.Parada.ToUniversalTime();

        var enPlanta = HoraPlanta.En(leidaDeLaBase, Argentina)!.Value;

        Assert.Equal("09:00", enPlanta.ToString("HH:mm"));
        Assert.Equal(TimeSpan.FromHours(-3), enPlanta.Offset);
        Assert.Equal(cargada.Parada, enPlanta);
    }

    [Fact]
    public void ConOtroOffset_ConservaElInstanteYCambiaSoloLaHoraDePared()
    {
        var madrid = new DateTimeOffset(2026, 3, 10, 13, 0, 0, TimeSpan.FromHours(1));

        var enPlanta = HoraPlanta.En(madrid, Argentina)!.Value;

        Assert.Equal(new DateTime(2026, 3, 10, 9, 0, 0), enPlanta.DateTime);
        Assert.Equal(madrid, enPlanta);
    }

    [Fact]
    public void SinHora_DevuelveNull()
    {
        Assert.Null(HoraPlanta.En(null, Argentina));
    }
}
