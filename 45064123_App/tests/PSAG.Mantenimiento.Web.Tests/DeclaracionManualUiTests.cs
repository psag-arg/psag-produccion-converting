using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>T-786: vista previa de la declaración manual del supervisor (corte de energía).</summary>
public class DeclaracionManualUiTests
{
    private static readonly List<TurnoDto> Turnos =
    [
        new() { TurnoId = 1, Nombre = "Mañana", HoraInicio = new TimeSpan(6, 0, 0), HoraFin = new TimeSpan(14, 0, 0), Activo = true },
        new() { TurnoId = 2, Nombre = "Tarde", HoraInicio = new TimeSpan(14, 0, 0), HoraFin = new TimeSpan(22, 0, 0), Activo = true },
        new() { TurnoId = 3, Nombre = "Noche", HoraInicio = new TimeSpan(22, 0, 0), HoraFin = new TimeSpan(6, 0, 0), Activo = true }
    ];

    [Theory]
    [InlineData("10:00", "10:40", 40)]
    [InlineData("23:30", "00:15", 45)]    // cruza medianoche: termina al día siguiente
    public void Minutos_ConCruceDeMedianoche(string desde, string hasta, int esperado) =>
        Assert.Equal(esperado, DeclaracionManualUi.Minutos(TimeOnly.Parse(desde), TimeOnly.Parse(hasta)));

    [Theory]
    [InlineData("06:00", "Mañana")]
    [InlineData("13:59", "Mañana")]
    [InlineData("14:00", "Tarde")]
    [InlineData("23:10", "Noche")]
    [InlineData("02:30", "Noche")]
    public void Turno_SaleDeLaHoraDesde(string hora, string turno) =>
        Assert.Equal(turno, DeclaracionManualUi.TurnoDe(Turnos, TimeOnly.Parse(hora))?.Nombre);

    [Fact]
    public void Falta_PideMaquinaHorarioTipoDeFallaYMotivo()
    {
        var d = new TimeOnly(10, 0);
        var h = new TimeOnly(10, 40);
        Assert.Equal("Elegí la máquina.", DeclaracionManualUi.Falta(0, d, h, 1, "Corte"));
        Assert.Equal("Completá la hora desde y hasta.", DeclaracionManualUi.Falta(1, d, null, 1, "Corte"));
        Assert.Contains("distinta", DeclaracionManualUi.Falta(1, d, d, 1, "Corte"));
        Assert.Equal("Elegí el tipo de falla.", DeclaracionManualUi.Falta(1, d, h, null, "Corte"));
        Assert.Equal("Escribí el motivo.", DeclaracionManualUi.Falta(1, d, h, 1, "  "));
        Assert.Null(DeclaracionManualUi.Falta(1, d, h, 1, "Corte de energía"));
    }
}
