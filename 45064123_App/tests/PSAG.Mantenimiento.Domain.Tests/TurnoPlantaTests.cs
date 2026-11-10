using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// Auditoría M y J: los turnos de la planta (1 mañana 05-13, 2 tarde 13-21, 3 noche 21-05) cubren las 24 h sin
/// huecos (no hay paradas "fuera de todo turno"), y el turno / hoy salen de la hora de la planta, no del servidor.
/// </summary>
public class TurnoPlantaTests
{
    private static readonly Turno Manana = new() { TurnoId = 1, Nombre = "Mañana", HoraInicio = new TimeOnly(5, 0), HoraFin = new TimeOnly(13, 0) };
    private static readonly Turno Tarde = new() { TurnoId = 2, Nombre = "Tarde", HoraInicio = new TimeOnly(13, 0), HoraFin = new TimeOnly(21, 0) };
    private static readonly Turno Noche = new() { TurnoId = 3, Nombre = "Noche", HoraInicio = new TimeOnly(21, 0), HoraFin = new TimeOnly(5, 0) };
    private static readonly Turno[] Turnos = [Manana, Tarde, Noche];

    private static readonly TimeZoneInfo ZonaPlanta =
        TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");

    [Fact]
    public void LosTresTurnos_CubrenCadaMinutoDelDia_ConUnSoloTurno()
    {
        for (var minuto = 0; minuto < 24 * 60; minuto++)
        {
            var hora = new TimeOnly(minuto / 60, minuto % 60);
            Assert.Single(Turnos, t => t.ContieneHora(hora));
        }
    }

    [Theory]
    [InlineData(0, 0, 3)]     // medianoche: noche
    [InlineData(4, 59, 3)]
    [InlineData(5, 0, 1)]     // 05:00 exacto: mañana
    [InlineData(12, 59, 1)]
    [InlineData(13, 0, 2)]
    [InlineData(20, 59, 2)]
    [InlineData(21, 0, 3)]
    [InlineData(23, 59, 3)]
    public void Bordes(int hora, int minuto, int turnoId)
    {
        Assert.Equal(turnoId, Turnos.Single(t => t.ContieneHora(new TimeOnly(hora, minuto))).TurnoId);
    }

    [Fact]
    public void LosSegundosNoDejanHuecos_04_59_59EsNoche()
    {
        // La ingesta y el turno actual buscan por HH:mm (sin segundos).
        var instante = new DateTimeOffset(2026, 11, 10, 4, 59, 59, TimeSpan.FromHours(-3));
        var hora = HoraPlanta.HoraMinuto(ZonaPlanta, instante);
        Assert.Equal(new TimeOnly(4, 59), hora);
        Assert.Same(Noche, Turnos.Single(t => t.ContieneHora(hora)));
    }

    [Fact]
    public void HoraYHoyDeLaPlanta_NoDelServidor()
    {
        // 01:30 UTC del 11/11 = 22:30 del 10/11 en la planta (UTC-3): turno noche del 10.
        var instante = new DateTimeOffset(2026, 11, 11, 1, 30, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 11, 10), HoraPlanta.Hoy(ZonaPlanta, instante));
        Assert.Equal(new TimeOnly(22, 30), HoraPlanta.HoraMinuto(ZonaPlanta, instante));
        Assert.Equal(TimeSpan.FromHours(-3), HoraPlanta.Ahora(ZonaPlanta, instante).Offset);
    }

    [Theory]
    [InlineData(2, 0, 9, 21)]    // noche a las 02:00 del 10: empezó el 9 a las 21
    [InlineData(22, 0, 10, 21)]  // noche a las 22:00 del 10: empezó el 10
    public void InicioTurnoEnCurso_ElNocturnoEsDelDiaEnQueEmpieza(int hora, int minuto, int diaInicio, int horaInicio)
    {
        var inicio = DetencionCamaraReglas.InicioTurnoEnCurso(Noche, new DateTime(2026, 11, 10, hora, minuto, 0));
        Assert.Equal(new DateTime(2026, 11, diaInicio, horaInicio, 0, 0), inicio);
    }

    [Fact]
    public void InicioTurnoEnCurso_TurnoDiurno()
    {
        Assert.Equal(new DateTime(2026, 11, 10, 5, 0, 0), DetencionCamaraReglas.InicioTurnoEnCurso(Manana, new DateTime(2026, 11, 10, 12, 40, 0)));
    }
}
