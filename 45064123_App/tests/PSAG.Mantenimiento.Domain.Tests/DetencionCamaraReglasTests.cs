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
    [InlineData(299, 300, false)]   // 4:59 es microcorte
    [InlineData(300, 300, true)]    // 5:00 justo crea la detención
    [InlineData(1800, 300, true)]
    [InlineData(150, 120, true)]    // umbral configurado en 2 min
    [InlineData(150, 180, false)]
    public void SuperaUmbral_DesdeElUmbralInclusive(int segundos, int umbral, bool esperado) =>
        Assert.Equal(esperado, DetencionCamaraReglas.SuperaUmbral(T0, T0.AddSeconds(segundos), DetencionCamaraReglas.UmbralSegundos(umbral)));

    [Theory]
    [InlineData(40, 30, true)]    // parada de 40 s con umbral de 30 s: crea la detención
    [InlineData(30, 30, true)]    // inclusive
    [InlineData(25, 30, false)]   // 25 s: microcorte
    [InlineData(44, 45, false)]
    [InlineData(45, 45, true)]
    public void SuperaUmbral_EnSegundos(int segundos, int umbralSegundos, bool esperado) =>
        Assert.Equal(esperado, DetencionCamaraReglas.SuperaUmbral(T0, T0.AddSeconds(segundos), umbralSegundos));

    [Fact]
    public void UmbralEfectivo_DeLaCamara_VacioEs5Min()
    {
        // T-798 etapa 4: un solo campo en segundos (CAM_Camaras.UmbralDetencionSegundos).
        Assert.Equal(30, DetencionCamaraReglas.UmbralSegundos(new Camara { UmbralDetencionSegundos = 30 }));
        Assert.Equal(300, DetencionCamaraReglas.UmbralSegundos(new Camara()));
        Assert.Equal(120, DetencionCamaraReglas.UmbralSegundos(new Camara { UmbralDetencionSegundos = 120 }));
        // Valores fuera de rango en la base (no deberían pasar el CHECK): vuelven a 5 min.
        Assert.Equal(300, DetencionCamaraReglas.UmbralSegundos(10));
        Assert.Equal(300, DetencionCamaraReglas.UmbralSegundosValido(20));
        Assert.Equal(300, DetencionCamaraReglas.UmbralSegundos(14401));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(14400, true)]
    [InlineData(14401, false)]
    [InlineData(0, false)]
    public void ValidacionUmbral_De30A14400_VacioValido(int? segundos, bool valido)
    {
        var motivo = DetencionCamaraReglas.MotivoUmbralInvalido(segundos);
        Assert.Equal(valido, motivo == null);
        if (!valido)
            Assert.Contains("entre 30 segundos y 240 minutos", motivo);
    }

    [Theory]
    [InlineData(300, "5 min")]
    [InlineData(60, "1 min")]
    [InlineData(14400, "240 min")]
    [InlineData(45, "45 s")]
    [InlineData(30, "30 s")]
    [InlineData(90, "90 s")]
    public void TextoUmbral_MinutosEnterosOSegundos(int segundos, string esperado) =>
        Assert.Equal(esperado, DetencionCamaraReglas.TextoUmbral(segundos));

    [Fact]
    public void TextoUmbral_DeLaCamara_UsaElUmbralEfectivo()
    {
        Assert.Equal("5 min", DetencionCamaraReglas.TextoUmbral(new Camara()));
        Assert.Equal("45 s", DetencionCamaraReglas.TextoUmbral(new Camara { UmbralDetencionSegundos = 45 }));
    }

    [Fact]
    public void AlertaSinDatos_DespuesDeNMinutosSinMinutos()
    {
        Assert.False(DetencionCamaraReglas.CorrespondeAlertaSinDatos(T0, T0.AddMinutes(5), 5));   // justo 5: todavía no
        Assert.True(DetencionCamaraReglas.CorrespondeAlertaSinDatos(T0, T0.AddMinutes(5).AddSeconds(1), 5));
        Assert.Equal("Cámara sin datos con el turno abierto", DetencionCamaraReglas.DescripcionAlerta(TipoAlertaCamara.SIN_DATOS));
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
