using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>T-798 etapa 2 · Reglas puras del turno de producción (turnos T1 05–13, T2 13–21, T3 21–05).</summary>
public class TurnoProduccionReglasTests
{
    private static readonly TimeZoneInfo Zona = TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");
    private static readonly Turno T1 = new() { TurnoId = 1, Codigo = "T1", HoraInicio = new(5, 0), HoraFin = new(13, 0), Orden = 1 };
    private static readonly Turno T2 = new() { TurnoId = 2, Codigo = "T2", HoraInicio = new(13, 0), HoraFin = new(21, 0), Orden = 2 };
    private static readonly Turno T3 = new() { TurnoId = 3, Codigo = "T3", HoraInicio = new(21, 0), HoraFin = new(5, 0), Orden = 3 };
    private static readonly Turno[] Turnos = [T1, T2, T3];

    private static DateTimeOffset H(int dia, int hora, int minuto = 0) => new(2026, 11, dia, hora, minuto, 0, TimeSpan.FromHours(-3));

    [Theory]
    [InlineData(19, 5, 0, 1, 19)]
    [InlineData(19, 12, 59, 1, 19)]
    [InlineData(19, 13, 0, 2, 19)]
    [InlineData(19, 21, 0, 3, 19)]
    [InlineData(19, 23, 30, 3, 19)]
    [InlineData(20, 2, 0, 3, 19)]    // T3 después de medianoche: del día de las 21:00
    [InlineData(20, 4, 59, 3, 19)]
    [InlineData(20, 5, 0, 1, 20)]
    public void TurnoQueContiene_YFechaProduccion(int dia, int hora, int minuto, int turnoId, int diaProduccion)
    {
        var r = TurnoProduccionReglas.TurnoQueContiene(Turnos, new DateTime(2026, 11, dia, hora, minuto, 0));
        Assert.NotNull(r);
        Assert.Equal(turnoId, r!.Value.Turno.TurnoId);
        Assert.Equal(new DateOnly(2026, 11, diaProduccion), r.Value.FechaProduccion);
    }

    [Fact]
    public void TurnoQueContiene_SinTurnoQueCubraLaHora_DevuelveNull() =>
        Assert.Null(TurnoProduccionReglas.TurnoQueContiene([T1], new DateTime(2026, 11, 19, 14, 0, 0)));

    [Fact]
    public void Ventana_DelT3_CruzaMedianoche()
    {
        var (inicio, fin) = TurnoProduccionReglas.Ventana(new DateOnly(2026, 11, 19), T3, Zona);
        Assert.Equal(H(19, 21), inicio);
        Assert.Equal(H(20, 5), fin);
    }

    [Fact]
    public void MinutosProgramados_SinProgramacionEsElTurnoEntero_ConProgramacionVaciaEs0()
    {
        var fecha = new DateOnly(2026, 11, 19);
        Assert.Equal(480, TurnoProduccionReglas.MinutosProgramados(fecha, T3, Turnos, [], []));
        var vacia = new CalendarioLinea { LineaId = 1, VigenteDesde = new DateOnly(2026, 11, 1) };
        Assert.Equal(0, TurnoProduccionReglas.MinutosProgramados(fecha, T3, Turnos, [vacia], []));
    }

    [Fact]
    public void Vencido_TurnoDeMaquinaCierraALaHoraExacta()
    {
        var t = new TurnoProduccion { Estado = EstadoTurnoProduccion.ABIERTO, FinProgramado = H(19, 13) };
        Assert.False(TurnoProduccionReglas.Vencido(t, H(19, 12, 59)));
        Assert.True(TurnoProduccionReglas.Vencido(t, H(19, 13)));
        t.Estado = EstadoTurnoProduccion.CERRADO;
        Assert.False(TurnoProduccionReglas.Vencido(t, H(19, 20)));
    }

    [Fact]
    public void CorrespondeAperturaAutomatica_SoloProgramadoSinRegistroYYaEmpezado()
    {
        Assert.True(TurnoProduccionReglas.CorrespondeAperturaAutomatica(480, H(19, 5), H(19, 5), existeRegistro: false));
        Assert.False(TurnoProduccionReglas.CorrespondeAperturaAutomatica(480, H(19, 5), H(19, 4, 59), false));
        Assert.False(TurnoProduccionReglas.CorrespondeAperturaAutomatica(480, H(19, 5), H(19, 6), existeRegistro: true));
        Assert.False(TurnoProduccionReglas.CorrespondeAperturaAutomatica(0, H(19, 5), H(19, 6), false));       // no programado
        Assert.False(TurnoProduccionReglas.CorrespondeAperturaAutomatica(480, H(18, 5), H(19, 6), false));     // hace más de 24 h
    }

    [Fact]
    public void SesionEnTolerancia_HastaElFinMasLaToleranciaDelTurno()
    {
        var t = new TurnoProduccion { Estado = EstadoTurnoProduccion.CERRADO_AUTOMATICO, FinProgramado = H(19, 13), FinReal = H(19, 13) };
        Assert.True(TurnoProduccionReglas.SesionEnTolerancia(t, 30, H(19, 13, 29)));
        Assert.False(TurnoProduccionReglas.SesionEnTolerancia(t, 30, H(19, 13, 30)));
        Assert.False(TurnoProduccionReglas.SesionEnTolerancia(t, 0, H(19, 13)));
        Assert.Equal(H(19, 13, 45), TurnoProduccionReglas.FinTolerancia(t, 45));
        t.Estado = EstadoTurnoProduccion.ABIERTO;   // abierto no es "tolerancia": es su turno
        Assert.False(TurnoProduccionReglas.SesionEnTolerancia(t, 30, H(19, 13, 10)));
    }

    [Fact]
    public void EstadoAlAsegurar_TurnoTerminadoSeCreaCerrado()
    {
        Assert.Equal(EstadoTurnoProduccion.ABIERTO, TurnoProduccionReglas.EstadoAlAsegurar(H(19, 13), H(19, 12, 59)));
        Assert.Equal(EstadoTurnoProduccion.CERRADO_AUTOMATICO, TurnoProduccionReglas.EstadoAlAsegurar(H(19, 13), H(19, 13)));
    }

    [Fact]
    public void SeSuperpone_IgnoraAnuladosYLosIndicados()
    {
        (long, DateTimeOffset, DateTimeOffset?, bool)[] tramos =
        [
            (1, H(19, 5), H(19, 9), false),
            (2, H(19, 9), null, false),
            (3, H(19, 6), H(19, 7), true)   // anulado
        ];
        Assert.True(TurnoProduccionReglas.SeSuperpone(tramos, H(19, 10), null));
        Assert.False(TurnoProduccionReglas.SeSuperpone(tramos, H(19, 10), null, [2]));
        Assert.True(TurnoProduccionReglas.SeSuperpone(tramos, H(19, 8), H(19, 9, 30), [2]));
        Assert.False(TurnoProduccionReglas.SeSuperpone(tramos.Take(1).Append((3, H(19, 6), H(19, 7), true)), H(19, 9), H(19, 10)));   // contiguo no se superpone
        Assert.True(TurnoProduccionReglas.SinSuperposicion(tramos));
        Assert.False(TurnoProduccionReglas.SinSuperposicion([(1, H(19, 5), null, false), (2, H(19, 9), null, false)]));
    }

    [Fact]
    public void Vigentes_SonLosNoAnuladosSinHasta()
    {
        var a = new TurnoProduccionOperador { TurnoProduccionOperadorId = 1, OperadorUsuarioId = "a", Desde = H(19, 5), Hasta = H(19, 9) };
        var b = new TurnoProduccionOperador { TurnoProduccionOperadorId = 2, OperadorUsuarioId = "b", Desde = H(19, 9), AnuladoPorId = 3 };
        var c = new TurnoProduccionOperador { TurnoProduccionOperadorId = 3, OperadorUsuarioId = "c", Desde = H(19, 9) };
        Assert.Equal("c", TurnoProduccionReglas.OperadorVigente([a, b, c])!.OperadorUsuarioId);
        Assert.Null(TurnoProduccionReglas.OperadorVigente([a, b]));
    }

    [Fact]
    public void Permisos_OperarioSoloSusLineas_SupervisorCorrige()
    {
        string[] operario = [RolesSistema.Operario];
        string[] supervisor = [RolesSistema.Supervisor];
        Assert.True(TurnoProduccionReglas.PuedeOperar(operario, [1], 1));
        Assert.False(TurnoProduccionReglas.PuedeOperar(operario, [1], 2));
        Assert.False(TurnoProduccionReglas.PuedeCorregir(operario, [1], 1));
        Assert.True(TurnoProduccionReglas.PuedeCorregir(supervisor, [], 2));
        Assert.False(TurnoProduccionReglas.PuedeCorregir(supervisor, [1], 2));
        Assert.True(TurnoProduccionReglas.PuedeCorregir([RolesSistema.Mantenimiento], [1], 2));
    }

    [Fact]
    public void MotivoTramoInvalido_ValidaMotivoHorasYTurno()
    {
        var abierto = new TurnoProduccion { Estado = EstadoTurnoProduccion.ABIERTO, InicioProgramado = H(19, 5), FinProgramado = H(19, 13) };
        Assert.Null(TurnoProduccionReglas.MotivoTramoInvalido(abierto, H(19, 6), null, H(19, 8), 30, "ok", 500));
        Assert.NotNull(TurnoProduccionReglas.MotivoTramoInvalido(abierto, H(19, 6), null, H(19, 8), 30, " ", 500));
        Assert.NotNull(TurnoProduccionReglas.MotivoTramoInvalido(abierto, H(19, 6), H(19, 6), H(19, 8), 30, "ok", 500));
        Assert.NotNull(TurnoProduccionReglas.MotivoTramoInvalido(abierto, H(19, 3), H(19, 6), H(19, 8), 30, "ok", 500));
        Assert.NotNull(TurnoProduccionReglas.MotivoTramoInvalido(abierto, H(19, 9), null, H(19, 8), 30, "ok", 500));   // futuro
        var cerrado = new TurnoProduccion { Estado = EstadoTurnoProduccion.CERRADO, InicioProgramado = H(19, 5), FinProgramado = H(19, 13), FinReal = H(19, 12) };
        Assert.NotNull(TurnoProduccionReglas.MotivoTramoInvalido(cerrado, H(19, 6), null, H(19, 14), 30, "ok", 500));
        Assert.Null(TurnoProduccionReglas.MotivoTramoInvalido(cerrado, H(19, 6), H(19, 12), H(19, 14), 30, "ok", 500));
    }

    // ===================== T-798 etapa 3: operador a la hora de una detención =====================

    private static TurnoProduccionOperador Tramo(string usuario, DateTimeOffset desde, DateTimeOffset? hasta, long? anuladoPor = null) => new()
    {
        OperadorUsuarioId = usuario, Desde = desde, Hasta = hasta, AnuladoPorId = anuladoPor
    };

    [Fact]
    public void OperadorALaHora_AntesYDespuesDelRelevo()
    {
        var inicio = new DateTimeOffset(2026, 11, 19, 5, 0, 0, TimeSpan.FromHours(-3));
        var relevo = inicio.AddHours(3);
        var tramos = new[] { Tramo("saliente", inicio, relevo), Tramo("entrante", relevo, null), Tramo("anulado", inicio, null, anuladoPor: 9) };

        Assert.Equal("saliente", TurnoProduccionReglas.OperadorALaHora(tramos, relevo.AddMinutes(-1))!.OperadorUsuarioId);
        Assert.Equal("entrante", TurnoProduccionReglas.OperadorALaHora(tramos, relevo)!.OperadorUsuarioId);
        Assert.Equal("entrante", TurnoProduccionReglas.OperadorALaHora(tramos, relevo.AddHours(4))!.OperadorUsuarioId);
        Assert.Null(TurnoProduccionReglas.OperadorALaHora(tramos, inicio.AddMinutes(-1)));
        Assert.Null(TurnoProduccionReglas.OperadorALaHora([], inicio));
    }
}
