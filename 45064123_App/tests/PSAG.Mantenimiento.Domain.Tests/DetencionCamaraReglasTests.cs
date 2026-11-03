using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>T-610: reglas de la detención automática por cámara (umbral, maquinista, declaración, fin de turno).</summary>
public class DetencionCamaraReglasTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 3, 10, 43, 0, TimeSpan.FromHours(-3));

    [Theory]
    [InlineData(299, 5, false)]   // 4:59 es microcorte
    [InlineData(300, 5, true)]    // 5:00 justo crea la detención
    [InlineData(1800, 5, true)]
    [InlineData(150, 2, true)]    // umbral configurado en 2 min
    [InlineData(150, 3, false)]
    public void SuperaUmbral_DesdeElUmbralInclusive(int segundos, int umbral, bool esperado) =>
        Assert.Equal(esperado, DetencionCamaraReglas.SuperaUmbral(T0, T0.AddSeconds(segundos), umbral));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(241)]
    public void UmbralFueraDeRango_SeRechazaYAlEvaluarVuelveA5(int umbral)
    {
        Assert.NotNull(DetencionCamaraReglas.MotivoUmbralInvalido(umbral));
        Assert.Equal(5, DetencionCamaraReglas.UmbralValido(umbral));
        Assert.False(DetencionCamaraReglas.SuperaUmbral(T0, T0.AddMinutes(4), umbral));
        Assert.True(DetencionCamaraReglas.SuperaUmbral(T0, T0.AddMinutes(5), umbral));
    }

    private static UsuarioLinea Asignacion(int id, string usuario, int? turnoId, int diasAtras) => new()
    {
        UsuarioLineaId = id, UsuarioId = usuario, LineaId = 1, TurnoId = turnoId,
        FechaAsignacion = T0.AddDays(-diasAtras), AsignadoPorUsuarioId = "admin"
    };

    [Fact]
    public void Maquinista_ElDelTurno()
    {
        var e = DetencionCamaraReglas.ElegirMaquinista(
            [Asignacion(1, "asevedo", 1, 30), Asignacion(2, "tarde", 2, 30)], turnoId: 1);
        Assert.Equal("asevedo", e.Elegido!.UsuarioId);
    }

    [Fact]
    public void Maquinista_DosEnElTurno_ElAsignadoMasRecienteYLoDejaEscrito()
    {
        var e = DetencionCamaraReglas.ElegirMaquinista(
            [Asignacion(1, "oconnor", 1, 30), Asignacion(2, "asevedo", 1, 2)], turnoId: 1);
        Assert.Equal("asevedo", e.Elegido!.UsuarioId);
        Assert.Contains("2 maquinistas", e.Motivo);
        Assert.Contains("oconnor", e.Motivo);
    }

    [Fact]
    public void Maquinista_NingunoEnElTurno_ElUnicoSinTurnoFijo()
    {
        var e = DetencionCamaraReglas.ElegirMaquinista(
            [Asignacion(1, "tarde", 2, 30), Asignacion(2, "rotativo", null, 30)], turnoId: 1);
        Assert.Equal("rotativo", e.Elegido!.UsuarioId);
    }

    [Fact]
    public void Maquinista_Ninguno_QuedaVacioParaQuienDeclara()
    {
        Assert.Null(DetencionCamaraReglas.ElegirMaquinista([Asignacion(1, "tarde", 2, 30)], turnoId: 1).Elegido);
        Assert.Null(DetencionCamaraReglas.ElegirMaquinista(
            [Asignacion(1, "a", null, 3), Asignacion(2, "b", null, 3)], turnoId: 1).Elegido);
        Assert.Null(DetencionCamaraReglas.ElegirMaquinista([], turnoId: 1).Elegido);
    }

    private static Detencion Pendiente() => new()
    {
        EstadoActual = EstadoDetencion.PENDIENTE_JUSTIFICACION,
        FechaHoraParada = T0,
        OrigenRegistro = OrigenRegistroDetencion.CAMARA
    };

    [Fact]
    public void Declaracion_ExigeDescripcionYHoraOMinutos()
    {
        var ahora = T0.AddHours(1);
        Assert.Equal("La descripción es obligatoria.", DetencionCamaraReglas.MotivoDeclaracionInvalida(Pendiente(), " ", T0.AddMinutes(10), null, ahora));
        Assert.NotNull(DetencionCamaraReglas.MotivoDeclaracionInvalida(Pendiente(), "Se cortó el papel", null, null, ahora));
        Assert.NotNull(DetencionCamaraReglas.MotivoDeclaracionInvalida(Pendiente(), "Se cortó el papel", T0, null, ahora));       // no posterior
        Assert.NotNull(DetencionCamaraReglas.MotivoDeclaracionInvalida(Pendiente(), "Se cortó el papel", ahora.AddHours(1), null, ahora)); // futura
        Assert.Null(DetencionCamaraReglas.MotivoDeclaracionInvalida(Pendiente(), "Se cortó el papel", T0.AddMinutes(12), null, ahora));
        Assert.Null(DetencionCamaraReglas.MotivoDeclaracionInvalida(Pendiente(), "Se cortó el papel", null, 12, ahora));
    }

    [Fact]
    public void Declaracion_SoloSiEstaPendiente()
    {
        var d = Pendiente();
        d.EstadoActual = EstadoDetencion.REGISTRADA;
        Assert.Equal("La detención ya fue declarada.", DetencionCamaraReglas.MotivoDeclaracionInvalida(d, "x", T0.AddMinutes(9), null, T0.AddHours(1)));
    }

    [Fact]
    public void TiempoDeclarado_ConHoraEsCalculado_SinHoraEsDeclarado()
    {
        Assert.Equal((OrigenTiempo.CALCULADO, 12), DetencionCamaraReglas.TiempoDeclarado(T0, T0.AddMinutes(12).AddSeconds(30), null));
        Assert.Equal((OrigenTiempo.DECLARADO, 7), DetencionCamaraReglas.TiempoDeclarado(T0, null, 7));
    }

    private static Turno Turno(int hi, int hf) => new() { HoraInicio = new TimeOnly(hi, 0), HoraFin = new TimeOnly(hf, 0) };

    [Fact]
    public void MinutosParaFinDeTurno_ContemplaLaMedianoche()
    {
        Assert.Equal(30, DetencionCamaraReglas.MinutosParaFinDeTurno(Turno(5, 13), new TimeOnly(12, 30)));
        Assert.Equal(90, DetencionCamaraReglas.MinutosParaFinDeTurno(Turno(21, 5), new TimeOnly(3, 30)));
        Assert.Equal(480, DetencionCamaraReglas.MinutosParaFinDeTurno(Turno(21, 5), new TimeOnly(21, 0)));
        Assert.Null(DetencionCamaraReglas.MinutosParaFinDeTurno(Turno(5, 13), new TimeOnly(14, 0)));
    }

    [Fact]
    public void AvisoFinTurno_SoloConPendientesYCercaDelFin()
    {
        Assert.True(DetencionCamaraReglas.CorrespondeAvisoFinTurno(20, pendientes: 2, minutosAntes: 30));
        Assert.False(DetencionCamaraReglas.CorrespondeAvisoFinTurno(45, pendientes: 2, minutosAntes: 30));
        Assert.False(DetencionCamaraReglas.CorrespondeAvisoFinTurno(20, pendientes: 0, minutosAntes: 30));
        Assert.False(DetencionCamaraReglas.CorrespondeAvisoFinTurno(null, pendientes: 2, minutosAntes: 30));
    }

    [Fact]
    public void VisibilidadDeLaSolapa_PorRolYLinea()
    {
        Assert.True(DetencionCamaraReglas.VeDeclaracion([RolesSistema.Operario]));
        Assert.True(DetencionCamaraReglas.VeDeclaracion([RolesSistema.Supervisor]));
        Assert.False(DetencionCamaraReglas.VeDeclaracion([RolesSistema.Gerencia]));
        Assert.False(DetencionCamaraReglas.VeTodasLasLineasParaDeclarar([RolesSistema.Operario], 0));
        Assert.True(DetencionCamaraReglas.VeTodasLasLineasParaDeclarar([RolesSistema.Administrador], 0));
        Assert.False(DetencionCamaraReglas.VeTodasLasLineasParaDeclarar([RolesSistema.Supervisor], 1));
    }

    [Fact]
    public void EstadoPendiente_SeMuestraComoPendienteDeDeclaracion() =>
        Assert.Equal("Pendiente de declaración", EstadoDetencion.PENDIENTE_JUSTIFICACION.ObtenerDescripcion());
}
