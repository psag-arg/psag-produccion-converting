using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// T-542: calendario base de horas programadas por línea.
/// </summary>
public class CalendarioLineaTests
{
    private static readonly Turno T1 = new() { TurnoId = 1, Codigo = "T1", Nombre = "Mañana", HoraInicio = new(6, 0), HoraFin = new(14, 0), Orden = 1, Activo = true };
    private static readonly Turno T2 = new() { TurnoId = 2, Codigo = "T2", Nombre = "Tarde", HoraInicio = new(14, 0), HoraFin = new(22, 0), Orden = 2, Activo = true };
    private static readonly Turno T3 = new() { TurnoId = 3, Codigo = "T3", Nombre = "Noche", HoraInicio = new(22, 0), HoraFin = new(6, 0), Orden = 3, Activo = true };
    private static readonly Turno[] Planta = [T1, T2, T3];

    private static readonly DayOfWeek[] LunesAViernes =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    // Octubre 2026: jueves 1 a sábado 31. 22 días hábiles, 5 sábados, 4 domingos.
    private static readonly DateOnly Desde = new(2026, 10, 1);
    private static readonly DateOnly Hasta = new(2026, 10, 31);

    private static CalendarioLinea Calendario(int lineaId, DateOnly vigenteDesde, params (DayOfWeek Dia, int[] Turnos)[] dias) => new()
    {
        LineaId = lineaId,
        VigenteDesde = vigenteDesde,
        Activo = true,
        Turnos = dias.SelectMany(d => d.Turnos.Select(t => new CalendarioLineaTurno { DiaSemana = d.Dia, TurnoId = t })).ToList()
    };

    /// <summary>Línea 1: 3 turnos de lunes a viernes, sábado sólo T1, domingo no trabaja.</summary>
    private static CalendarioLinea Tango2(DateOnly desde) => Calendario(1, desde,
        LunesAViernes.Select(d => (d, new[] { 1, 2, 3 }))
            .Append((DayOfWeek.Saturday, new[] { 1 }))
            .ToArray());

    /// <summary>Línea 2: 2 turnos de lunes a viernes, fin de semana no trabaja.</summary>
    private static CalendarioLinea Futura2(DateOnly desde) => Calendario(2, desde,
        LunesAViernes.Select(d => (d, new[] { 1, 2 })).ToArray());

    private static TiempoProgramadoDia Fila(DateOnly fecha, decimal horas, string origen) => new()
    {
        LineaId = 1, Fecha = fecha, Horas = horas, Origen = origen, Activo = true
    };

    [Fact]
    public void DosLineasConCalendariosDistintos_DanHorasDistintasEnElMismoMes()
    {
        var tango = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Desde, Hasta, Planta, [Tango2(Desde)], []);
        var futura = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Desde, Hasta, Planta, [Futura2(Desde)], []);

        Assert.Equal((22 * 24 + 5 * 8) * 60, tango.Sum(d => d.Minutos));  // 568 h
        Assert.Equal(22 * 16 * 60, futura.Sum(d => d.Minutos));          // 352 h
    }

    [Fact]
    public void SabadoNoTrabajado_SumaCero_YDiceBaseDeLinea()
    {
        var sabado = new DateOnly(2026, 10, 3);

        var dia = Assert.Single(TiempoProgramadoCalculador.MinutosProgramadosPorDia(sabado, sabado, Planta, [Futura2(Desde)], []));

        Assert.Equal(0, dia.Minutos);
        Assert.Equal(OrigenTiempoProgramado.BaseLinea, dia.Origen);
        Assert.Equal(string.Empty, dia.Turnos);
    }

    [Fact]
    public void CadaDiaDiceQueTurnosSuman()
    {
        var viernes = new DateOnly(2026, 10, 2);
        var sabado = viernes.AddDays(1);

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(viernes, sabado, Planta, [Tango2(Desde)], []);

        Assert.Equal("T1 + T2 + T3", dias[0].Turnos);
        Assert.Equal(24 * 60, dias[0].Minutos);
        Assert.Equal("T1", dias[1].Turnos);
        Assert.Equal(8 * 60, dias[1].Minutos);
    }

    [Fact]
    public void SinCalendario_RigeElEstandarTodosLosDias()
    {
        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Desde, Hasta, Planta, [], []);

        Assert.All(dias, d =>
        {
            Assert.Equal(24 * 60, d.Minutos);
            Assert.Equal(OrigenTiempoProgramado.Estandar, d.Origen);
        });
    }

    [Fact]
    public void AntesDeVigenteDesde_RigeElEstandar_DesdeEseDiaElCalendario()
    {
        var domingo11 = new DateOnly(2026, 10, 11);
        var domingo18 = new DateOnly(2026, 10, 18);

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Desde, Hasta, Planta, [Futura2(new DateOnly(2026, 10, 15))], []);

        Assert.Equal(24 * 60, dias.Single(d => d.Fecha == domingo11).Minutos);
        Assert.Equal(OrigenTiempoProgramado.Estandar, dias.Single(d => d.Fecha == domingo11).Origen);
        Assert.Equal(0, dias.Single(d => d.Fecha == domingo18).Minutos);
    }

    [Fact]
    public void VersionMasNueva_ReemplazaALaAnteriorDesdeSuFecha()
    {
        var anterior = Futura2(Desde);
        var nueva = Calendario(2, new DateOnly(2026, 10, 15), (DayOfWeek.Saturday, [1]));
        var sabado10 = new DateOnly(2026, 10, 10);
        var sabado17 = new DateOnly(2026, 10, 17);
        var lunes19 = new DateOnly(2026, 10, 19);

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Desde, Hasta, Planta, [nueva, anterior], []);

        Assert.Equal(0, dias.Single(d => d.Fecha == sabado10).Minutos);
        Assert.Equal(8 * 60, dias.Single(d => d.Fecha == sabado17).Minutos);
        Assert.Equal(0, dias.Single(d => d.Fecha == lunes19).Minutos);
    }

    [Fact]
    public void VersionInactiva_SeIgnora()
    {
        var quitada = Futura2(Desde);
        quitada.Activo = false;

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(Desde, Hasta, Planta, [quitada], []);

        Assert.All(dias, d => Assert.Equal(OrigenTiempoProgramado.Estandar, d.Origen));
    }

    [Fact]
    public void ExcepcionEHistorico_GananSobreElCalendario()
    {
        var sabado = new DateOnly(2026, 10, 3);
        var lunes = new DateOnly(2026, 10, 5);
        var excepciones = new[] { Fila(sabado, 12m, "EXCEPCION"), Fila(lunes, 23.5m, "HISTORICO") };

        var dias = TiempoProgramadoCalculador.MinutosProgramadosPorDia(sabado, lunes, Planta, [Futura2(Desde)], excepciones);

        Assert.Equal((12 * 60, "EXCEPCION"), (dias[0].Minutos, dias[0].Origen));
        Assert.Equal((0, OrigenTiempoProgramado.BaseLinea), (dias[1].Minutos, dias[1].Origen));
        Assert.Equal((1410, "HISTORICO"), (dias[2].Minutos, dias[2].Origen));
    }

    [Fact]
    public void TurnoInactivoEnElCalendario_NoSuma()
    {
        var t2Inactivo = new Turno { TurnoId = 2, Codigo = "T2", HoraInicio = new(14, 0), HoraFin = new(22, 0), Activo = false };
        var lunes = new DateOnly(2026, 10, 5);

        var dia = Assert.Single(TiempoProgramadoCalculador.MinutosProgramadosPorDia(lunes, lunes, [T1, t2Inactivo, T3], [Futura2(Desde)], []));

        Assert.Equal(8 * 60, dia.Minutos);
        Assert.Equal("T1", dia.Turnos);
    }

    [Fact]
    public void LunesConT1YT3_SumaLaDuracionDeAmbosTurnos()
    {
        // Horarios de planta: T1 05-13 y T3 21-06. Superponerlos en el reloj de un solo día daba 16 h;
        // la línea trabaja 8 h de T1 más 9 h de T3.
        var t1 = new Turno { TurnoId = 1, Codigo = "T1", HoraInicio = new(5, 0), HoraFin = new(13, 0), Activo = true };
        var t3 = new Turno { TurnoId = 3, Codigo = "T3", HoraInicio = new(21, 0), HoraFin = new(6, 0), Activo = true };
        var lunes = new DateOnly(2026, 10, 5);
        var calendario = Calendario(1, Desde, (DayOfWeek.Monday, [1, 3]));

        var dia = Assert.Single(TiempoProgramadoCalculador.MinutosProgramadosPorDia(lunes, lunes, [t1, t3], [calendario], []));

        Assert.Equal(17 * 60, dia.Minutos);
        Assert.Equal("T1 + T3", dia.Turnos);
    }

    [Fact]
    public void HistoricoMarzoAAgosto_NoCambiaConUnCalendarioCargado()
    {
        // Las filas HISTORICO (script 18) ganan sobre cualquier calendario.
        var historico = new[]
        {
            Fila(new DateOnly(2026, 3, 1), 16m, "HISTORICO"),
            Fila(new DateOnly(2026, 3, 7), 24m, "HISTORICO"),
            Fila(new DateOnly(2026, 3, 8), 8m, "HISTORICO")
        };
        var desde = new DateOnly(2026, 3, 1);
        var hasta = new DateOnly(2026, 3, 8);

        var sinCalendario = TiempoProgramadoCalculador.MinutosProgramadosPorDia(desde, hasta, Planta, [], historico);
        var conCalendario = TiempoProgramadoCalculador.MinutosProgramadosPorDia(desde, hasta, Planta, [Futura2(new DateOnly(2026, 1, 1))], historico);

        foreach (var fila in historico)
        {
            var antes = sinCalendario.Single(d => d.Fecha == fila.Fecha);
            var despues = conCalendario.Single(d => d.Fecha == fila.Fecha);
            Assert.Equal(antes.Minutos, despues.Minutos);
            Assert.Equal((int)(fila.Horas * 60), despues.Minutos);
            Assert.Equal(OrigenTiempoProgramado.Historico, despues.Origen);
        }
    }

    [Fact]
    public void PuedeEditar_AdministradorTodas_SupervisorSusLineas_OtrosNinguna()
    {
        Assert.True(CalendarioLineaReglas.PuedeEditar([RolesSistema.Administrador], [], 5));
        Assert.True(CalendarioLineaReglas.PuedeEditar([RolesSistema.Supervisor], [1, 2], 2));
        Assert.False(CalendarioLineaReglas.PuedeEditar([RolesSistema.Supervisor], [1, 2], 3));
        Assert.False(CalendarioLineaReglas.PuedeEditar([RolesSistema.Operario], [3], 3));
        Assert.False(CalendarioLineaReglas.PuedeEditar([RolesSistema.Mantenimiento], [3], 3));
    }

    [Fact]
    public void MotivoInvalido_TurnoInexistenteOInactivo_DiaFueraDeRango()
    {
        Assert.Null(CalendarioLineaReglas.MotivoInvalido([(DayOfWeek.Monday, 1), (DayOfWeek.Sunday, 3)], Planta));
        Assert.Null(CalendarioLineaReglas.MotivoInvalido([], Planta));
        Assert.NotNull(CalendarioLineaReglas.MotivoInvalido([(DayOfWeek.Monday, 99)], Planta));
        Assert.NotNull(CalendarioLineaReglas.MotivoInvalido([((DayOfWeek)7, 1)], Planta));
    }

    [Fact]
    public void Filas_SinRepetidos()
    {
        var filas = CalendarioLineaReglas.Filas([(DayOfWeek.Monday, 1), (DayOfWeek.Monday, 1), (DayOfWeek.Saturday, 2)]);

        Assert.Equal(2, filas.Count);
    }
}
