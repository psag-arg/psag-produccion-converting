using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

public class TiempoProgramadoCalculadorTests
{
    private static readonly DateOnly Lunes = new(2026, 8, 3);

    private static Turno Turno(string nombre, int horaInicio, int horaFin, bool activo = true) => new()
    {
        Nombre = nombre,
        HoraInicio = new TimeOnly(horaInicio, 0),
        HoraFin = new TimeOnly(horaFin, 0),
        Activo = activo
    };

    private static TiempoProgramadoDia Excepcion(DateOnly fecha, decimal horas, bool activo = true) => new()
    {
        LineaId = 1,
        Fecha = fecha,
        Horas = horas,
        Origen = "EXCEPCION",
        Activo = activo
    };

    private static int MinutosDe(IReadOnlyList<MinutosProgramadosDia> dias, DateOnly fecha) =>
        dias.Single(d => d.Fecha == fecha).Minutos;

    private static readonly Turno[] DosTurnos =
    [
        Turno("Manana", 6, 14),
        Turno("Tarde", 14, 22),
    ];

    [Fact]
    public void SinExcepciones_CadaDiaSumaLosTurnosActivos()
    {
        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes.AddDays(2), DosTurnos, [], []);

        Assert.Equal(3, dias.Count);
        Assert.All(dias, d => Assert.Equal(16 * 60, d.Minutos));
        Assert.Equal(Lunes, dias[0].Fecha);
        Assert.Equal(Lunes.AddDays(2), dias[2].Fecha);
    }

    [Fact]
    public void ConFilaDeExcepcion_EseDiaUsaSusHoras_LosDemasElEstandar()
    {
        var excepciones = new[] { Excepcion(Lunes.AddDays(1), 7.5m) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes.AddDays(2), DosTurnos, [], excepciones);

        Assert.Equal(16 * 60, MinutosDe(dias, Lunes));
        Assert.Equal(450, MinutosDe(dias, Lunes.AddDays(1)));
        Assert.Equal(16 * 60, MinutosDe(dias, Lunes.AddDays(2)));
    }

    [Fact]
    public void FilaDeCeroHoras_DiaSinTiempoProgramado()
    {
        var excepciones = new[] { Excepcion(Lunes.AddDays(5), 0m) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(
            Lunes.AddDays(5), Lunes.AddDays(5), DosTurnos, [], excepciones);

        Assert.Equal(0, MinutosDe(dias, Lunes.AddDays(5)));
    }

    [Fact]
    public void TurnoQueCruzaMedianoche_CuentaOcho_Horas()
    {
        var nocturno = Turno("Noche", 21, 5);

        Assert.Equal(8 * 60, nocturno.DuracionMinutos());

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes, [nocturno], [], []);
        Assert.Equal(8 * 60, MinutosDe(dias, Lunes));
    }

    [Fact]
    public void TurnoDeDiaCompleto_DeMismaHoraInicioYFin_CuentaVeinticuatro_Horas()
    {
        // Mismo criterio que Turno.ContieneHora: si HoraFin no es posterior a HoraInicio, cruza medianoche.
        Assert.Equal(24 * 60, Turno("Continuo", 6, 6).DuracionMinutos());
    }

    [Fact]
    public void TurnoInactivo_NoSuma()
    {
        var turnos = new[] { Turno("Manana", 6, 14), Turno("Baja", 14, 22, activo: false) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes, turnos, [], []);

        Assert.Equal(8 * 60, MinutosDe(dias, Lunes));
    }

    [Fact]
    public void ExcepcionInactiva_SeIgnora_YRigeElEstandar()
    {
        var excepciones = new[] { Excepcion(Lunes, 0m, activo: false) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes, DosTurnos, [], excepciones);

        Assert.Equal(16 * 60, MinutosDe(dias, Lunes));
    }

    [Fact]
    public void ExcepcionFueraDelRango_NoAfectaElResultado()
    {
        var excepciones = new[] { Excepcion(Lunes.AddDays(30), 0m) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes.AddDays(1), DosTurnos, [], excepciones);

        Assert.All(dias, d => Assert.Equal(16 * 60, d.Minutos));
    }

    [Fact]
    public void RangoInvertido_DevuelveVacio()
    {
        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes.AddDays(1), Lunes, DosTurnos, [], []);

        Assert.Empty(dias);
    }

    [Fact]
    public void SinTurnosActivos_EstandarEsCero()
    {
        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes, [], [], []);

        Assert.Equal(0, MinutosDe(dias, Lunes));
    }

    // T-541: Tango 2 mostraba 25 h por día con origen Estándar. El tiempo programado es la suma de
    // la duración de los turnos; las 25 h salen de un T3 (21-06) que se pisa una hora con T1 (05-13),
    // y eso se corrige en los horarios (script 19), no en el cálculo.

    [Fact]
    public void TresTurnosDeOchoSinSolapamiento_UnDiaEstandarSumaVeinticuatro_Horas()
    {
        var turnos = new[] { Turno("T1", 5, 13), Turno("T2", 13, 21), Turno("T3", 21, 5) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes, turnos, [], []);

        Assert.Equal(24 * 60, MinutosDe(dias, Lunes));
    }

    [Fact]
    public void TurnosQueSeSuperponen_SumanLaDuracionDeCadaUno()
    {
        // Horarios de planta con T3 terminando a las 06: 8 + 8 + 9 = 25 h (se corrige el turno, no la suma).
        var turnos = new[] { Turno("T1", 5, 13), Turno("T2", 13, 21), Turno("T3", 21, 6) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes, turnos, [], []);

        Assert.Equal(25 * 60, MinutosDe(dias, Lunes));
    }

    [Fact]
    public void OctubreHastaEl15_ConTresTurnosDeOcho_DaTrescientasSesenta_Horas()
    {
        var turnos = new[] { Turno("T1", 5, 13), Turno("T2", 13, 21), Turno("T3", 21, 5) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(new(2026, 10, 1), new(2026, 10, 15), turnos, [], []);

        Assert.Equal(15, dias.Count);
        Assert.Equal(15 * 24 * 60, dias.Sum(d => d.Minutos));
    }
}
