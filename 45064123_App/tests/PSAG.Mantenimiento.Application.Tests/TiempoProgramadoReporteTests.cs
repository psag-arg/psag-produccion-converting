using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Application.Tests;

public class TiempoProgramadoReporteTests
{
    private static readonly DateOnly Desde = new(2026, 3, 1);
    private static readonly DateOnly Hasta = new(2026, 4, 30);

    private static readonly Turno[] TresTurnos =
    [
        new() { Nombre = "Manana", HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0), Activo = true },
        new() { Nombre = "Tarde", HoraInicio = new TimeOnly(14, 0), HoraFin = new TimeOnly(22, 0), Activo = true },
        new() { Nombre = "Noche", HoraInicio = new TimeOnly(22, 0), HoraFin = new TimeOnly(6, 0), Activo = true },
    ];

    private static TiempoProgramadoDia Excepcion(DateOnly fecha, decimal horas, string origen = "EXCEPCION", string? motivo = null, bool activo = true) => new()
    {
        LineaId = 1,
        Fecha = fecha,
        Horas = horas,
        Origen = origen,
        Motivo = motivo,
        Activo = activo
    };

    [Fact]
    public void DiaSinExcepcion_TomaElEstandarDeLosTurnosYOrigenEstandar()
    {
        var dias = TiempoProgramadoReporte.DiasDeLinea(1, "Tango 2", new(2026, 3, 2), new(2026, 3, 2), TresTurnos, []);

        var dia = Assert.Single(dias);
        Assert.Equal(24 * 60, dia.Minutos);
        Assert.Equal(24, dia.Horas);
        Assert.Equal("ESTANDAR", dia.Origen);
        Assert.Null(dia.Motivo);
        Assert.Equal("Tango 2", dia.LineaNombre);
    }

    [Fact]
    public void ExcepcionActiva_GanaSobreElEstandarYLlevaMotivo()
    {
        var excepciones = new[] { Excepcion(new(2026, 3, 9), 6, motivo: "Paro parcial") };

        var dia = TiempoProgramadoReporte.DiasDeLinea(1, "Tango 2", new(2026, 3, 9), new(2026, 3, 9), TresTurnos, excepciones).Single();

        Assert.Equal(360, dia.Minutos);
        Assert.Equal(6, dia.Horas);
        Assert.Equal("EXCEPCION", dia.Origen);
        Assert.Equal("Paro parcial", dia.Motivo);
    }

    [Fact]
    public void HistoricoConCeroHoras_DiaSinProduccionCuentaCero()
    {
        var excepciones = new[] { Excepcion(new(2026, 3, 1), 0, origen: "HISTORICO") };

        var dia = TiempoProgramadoReporte.DiasDeLinea(1, "Tango 2", new(2026, 3, 1), new(2026, 3, 1), TresTurnos, excepciones).Single();

        Assert.Equal(0, dia.Minutos);
        Assert.Equal("HISTORICO", dia.Origen);
    }

    [Fact]
    public void ExcepcionInactiva_SeIgnoraYRigeElEstandar()
    {
        var excepciones = new[] { Excepcion(new(2026, 3, 9), 0, activo: false) };

        var dia = TiempoProgramadoReporte.DiasDeLinea(1, "Tango 2", new(2026, 3, 9), new(2026, 3, 9), TresTurnos, excepciones).Single();

        Assert.Equal(24 * 60, dia.Minutos);
        Assert.Equal("ESTANDAR", dia.Origen);
    }

    [Fact]
    public void MinutosPorMes_SumaLosDiasDeCadaMes()
    {
        var excepciones = new[] { Excepcion(new(2026, 3, 9), 6), Excepcion(new(2026, 4, 2), 0, origen: "HISTORICO") };
        var dias = TiempoProgramadoReporte.DiasDeLinea(1, "Tango 2", Desde, Hasta, TresTurnos, excepciones);

        var porMes = TiempoProgramadoReporte.MinutosPorMes(dias);

        // Marzo: 31 días de 24 h, menos 18 h de la excepción del 9/3 (6 h en lugar de 24 h).
        Assert.Equal(31 * 24 * 60 - 18 * 60, porMes[3]);
        // Abril: 30 días de 24 h, menos el día 2/4 que queda en 0 h.
        Assert.Equal(29 * 24 * 60, porMes[4]);
        Assert.Equal(2, porMes.Count);
    }

    [Fact]
    public void Horas_RedondeaADosDecimales()
    {
        Assert.Equal(647.5, TiempoProgramadoReporte.Horas(38850));
        Assert.Equal(6.17, TiempoProgramadoReporte.Horas(370));
        Assert.Equal(0, TiempoProgramadoReporte.Horas(0));
    }

    private static readonly Dictionary<int, int> ProgramadoTodoElAnio =
        Enumerable.Range(1, 12).ToDictionary(m => m, _ => 1000);

    [Fact]
    public void MesSinDetencionesEnElMedio_AparecePara_NoSubirElPromedio()
    {
        var meses = TiempoProgramadoReporte.MesesDelReporte(
            2026, 1, 12, [3, 4, 6, 7, 8], ProgramadoTodoElAnio, new DateOnly(2026, 10, 11));

        Assert.Equal([3, 4, 5, 6, 7, 8, 9, 10], meses);
    }

    [Fact]
    public void MesesFuturos_NoAparecen_SalvoQueTenganDatos()
    {
        var hoy = new DateOnly(2026, 10, 11);

        Assert.Equal([3, 4, 5, 6, 7, 8, 9, 10],
            TiempoProgramadoReporte.MesesDelReporte(2026, 1, 12, [3], ProgramadoTodoElAnio, hoy));
        Assert.Equal([3, 4, 5, 6, 7, 8, 9, 10, 11],
            TiempoProgramadoReporte.MesesDelReporte(2026, 1, 12, [3, 11], ProgramadoTodoElAnio, hoy));
    }

    [Fact]
    public void SinDatos_NoHayMeses()
    {
        Assert.Empty(TiempoProgramadoReporte.MesesDelReporte(2025, 1, 12, [], ProgramadoTodoElAnio, new DateOnly(2026, 10, 11)));
    }

    [Fact]
    public void AnioPasado_LlegaHastaDiciembre()
    {
        var meses = TiempoProgramadoReporte.MesesDelReporte(2025, 1, 12, [10], ProgramadoTodoElAnio, new DateOnly(2026, 10, 11));

        Assert.Equal([10, 11, 12], meses);
    }

    [Fact]
    public void HastaHoy_CortaElAnioEnCurso()
    {
        var hoy = new DateOnly(2026, 10, 11);

        Assert.Equal(hoy, TiempoProgramadoReporte.HastaHoy(2026, hoy));
        Assert.Equal(new DateOnly(2025, 12, 31), TiempoProgramadoReporte.HastaHoy(2025, hoy));
    }
}
