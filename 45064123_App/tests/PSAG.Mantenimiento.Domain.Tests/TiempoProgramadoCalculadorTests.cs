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
        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes.AddDays(2), DosTurnos, []);

        Assert.Equal(3, dias.Count);
        Assert.All(dias, d => Assert.Equal(16 * 60, d.Minutos));
        Assert.Equal(Lunes, dias[0].Fecha);
        Assert.Equal(Lunes.AddDays(2), dias[2].Fecha);
    }

    [Fact]
    public void ConFilaDeExcepcion_EseDiaUsaSusHoras_LosDemasElEstandar()
    {
        var excepciones = new[] { Excepcion(Lunes.AddDays(1), 7.5m) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes.AddDays(2), DosTurnos, excepciones);

        Assert.Equal(16 * 60, MinutosDe(dias, Lunes));
        Assert.Equal(450, MinutosDe(dias, Lunes.AddDays(1)));
        Assert.Equal(16 * 60, MinutosDe(dias, Lunes.AddDays(2)));
    }

    [Fact]
    public void FilaDeCeroHoras_DiaSinTiempoProgramado()
    {
        var excepciones = new[] { Excepcion(Lunes.AddDays(5), 0m) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(
            Lunes.AddDays(5), Lunes.AddDays(5), DosTurnos, excepciones);

        Assert.Equal(0, MinutosDe(dias, Lunes.AddDays(5)));
    }

    [Fact]
    public void TurnoQueCruzaMedianoche_CuentaOcho_Horas()
    {
        var nocturno = Turno("Noche", 21, 5);

        Assert.Equal(8 * 60, TiempoProgramadoCalculador.DuracionMinutos(nocturno));

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes, [nocturno], []);
        Assert.Equal(8 * 60, MinutosDe(dias, Lunes));
    }

    [Fact]
    public void TurnoDeDiaCompleto_DeMismaHoraInicioYFin_CuentaVeinticuatro_Horas()
    {
        // Mismo criterio que Turno.ContieneHora: si HoraFin no es posterior a HoraInicio, cruza medianoche.
        Assert.Equal(24 * 60, TiempoProgramadoCalculador.DuracionMinutos(Turno("Continuo", 6, 6)));
    }

    [Fact]
    public void TurnoInactivo_NoSuma()
    {
        var turnos = new[] { Turno("Manana", 6, 14), Turno("Baja", 14, 22, activo: false) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes, turnos, []);

        Assert.Equal(8 * 60, MinutosDe(dias, Lunes));
    }

    [Fact]
    public void ExcepcionInactiva_SeIgnora_YRigeElEstandar()
    {
        var excepciones = new[] { Excepcion(Lunes, 0m, activo: false) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes, DosTurnos, excepciones);

        Assert.Equal(16 * 60, MinutosDe(dias, Lunes));
    }

    [Fact]
    public void ExcepcionFueraDelRango_NoAfectaElResultado()
    {
        var excepciones = new[] { Excepcion(Lunes.AddDays(30), 0m) };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes.AddDays(1), DosTurnos, excepciones);

        Assert.All(dias, d => Assert.Equal(16 * 60, d.Minutos));
    }

    [Fact]
    public void RangoInvertido_DevuelveVacio()
    {
        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes.AddDays(1), Lunes, DosTurnos, []);

        Assert.Empty(dias);
    }

    [Fact]
    public void SinTurnosActivos_EstandarEsCero()
    {
        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Lunes, Lunes, [], []);

        Assert.Equal(0, MinutosDe(dias, Lunes));
    }
}
