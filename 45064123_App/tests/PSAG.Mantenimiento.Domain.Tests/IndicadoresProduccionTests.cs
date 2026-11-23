using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// T-798 etapa 5 · Indicadores (funciones puras) con los números del diseño 2.10, turnos de 8 h: objetivo prorrateado,
/// eficiencia sumando (nunca promediando %), "sin datos" y "sin objetivo" nunca 0 %, cobertura, velocidad e
/// indisponibilidad.
/// </summary>
public class IndicadoresProduccionTests
{
    private static DateTimeOffset H(int dia, int hora, int minuto = 0) => new(2026, 11, dia, hora, minuto, 0, TimeSpan.FromHours(-3));

    private static RealProduccion Validada(decimal cantidad) =>
        ProduccionOficialReglas.RealParaIndicadores(Fila(cantidad, EstadoValidacionProduccion.VALIDADA));

    private static ProduccionTurno Fila(decimal? cantidad, EstadoValidacionProduccion estado, string magnitud = MagnitudProduccion.Mangas, long id = 1, long? anuladoPor = null) => new()
    {
        ProduccionTurnoId = id, Magnitud = magnitud, CantidadOficial = cantidad, CantidadCamara = cantidad, EstadoValidacion = estado,
        Origen = OrigenProduccionTurno.CAMARA, FechaHoraRegistro = H(19, 13).AddMinutes(id), AnuladoPorId = anuladoPor
    };

    private static decimal Objetivo(int objetivoTurno, DateTimeOffset desde, DateTimeOffset hasta) =>
        IndicadoresProduccionCalculador.ObjetivoTramo(objetivoTurno, 480, ProduccionOficialReglas.MinutosTramo(desde, hasta))!.Value;

    // ===================== Casos del diseño 2.10 =====================

    [Fact]
    public void T1_SH30_Objetivo11200_Real9300_Es83()
    {
        var objetivo = Objetivo(11200, H(19, 5), H(19, 13));
        var r = IndicadoresProduccionCalculador.Comparar([(objetivo, Validada(9300))]);

        Assert.Equal(11200m, objetivo);
        Assert.Equal(EstadoIndicador.Ok, r.Estado);
        Assert.Equal(83.0m, r.Porcentaje);
        Assert.False(r.Provisorio);
    }

    [Fact]
    public void T2_ConCambioDeProducto_SumaLosObjetivosProrrateados()
    {
        var sh30 = Objetivo(11200, H(19, 13), H(19, 16));   // 3 h
        var sh50 = Objetivo(6720, H(19, 16), H(19, 21));    // 5 h

        var turno = IndicadoresProduccionCalculador.Comparar([(sh30, Validada(3900)), (sh50, Validada(3850))]);

        Assert.Equal(4200m, sh30);
        Assert.Equal(4200m, sh50);
        Assert.Equal(8400m, turno.Objetivo);
        Assert.Equal(7750m, turno.Real);
        Assert.Equal(92.3m, turno.Porcentaje);
        Assert.Equal(92.9m, IndicadoresProduccionCalculador.Comparar([(sh30, Validada(3900))]).Porcentaje);
        Assert.Equal(91.7m, IndicadoresProduccionCalculador.Comparar([(sh50, Validada(3850))]).Porcentaje);
    }

    [Fact]
    public void T3_QueCruzaMedianoche_EsUnSoloTramoDe480Minutos()
    {
        var objetivo = Objetivo(6720, H(19, 21), H(20, 5));
        var r = IndicadoresProduccionCalculador.Comparar([(objetivo, Validada(6200))]);

        Assert.Equal(6720m, objetivo);
        Assert.Equal(92.3m, r.Porcentaje);
    }

    // T-805: el turno cortado a 6 h y el reparto sobre los programados están en ObjetivoTurnoReglasTests.

    // ===================== Sin datos / sin objetivo / cero =====================

    [Fact]
    public void BolsonesSinCamara_EsSinDatos_NoCero()
    {
        var real = ProduccionOficialReglas.RealParaIndicadores(null);
        var r = IndicadoresProduccionCalculador.Comparar([(6500m, real)]);

        Assert.False(real.TieneDato);
        Assert.Equal(EstadoIndicador.SinDatos, r.Estado);
        Assert.Null(r.Porcentaje);
        Assert.Null(r.Real);
        Assert.Equal(6500m, r.Objetivo);
        Assert.Equal(1, r.TramosSinDatos);
    }

    [Fact]
    public void CoberturaMenorAl90_EsSinDatos_ConLaCantidadContadaGuardada()
    {
        var fila = ProduccionOficialReglas.FilaDeCamara(8000m, 420, 480m, 0.90m);   // 87,5 %

        Assert.Equal(EstadoValidacionProduccion.SIN_DATOS, fila.Estado);
        Assert.Null(fila.CantidadOficial);
        Assert.Equal(8000m, fila.CantidadCamara);
        Assert.Equal(420, fila.MinutosConDatos);
        var real = ProduccionOficialReglas.RealParaIndicadores(Fila(null, EstadoValidacionProduccion.SIN_DATOS));
        Assert.Equal(EstadoIndicador.SinDatos, IndicadoresProduccionCalculador.Comparar([(11200m, real)]).Estado);
    }

    [Fact]
    public void CoberturaJusta90_EsPendiente()
    {
        var fila = ProduccionOficialReglas.FilaDeCamara(9300.04m, 432, 480m, 0.90m);

        Assert.Equal(EstadoValidacionProduccion.PENDIENTE, fila.Estado);
        Assert.Equal(9300.0m, fila.CantidadOficial);
    }

    [Fact]
    public void ProduccionCeroConCoberturaCompleta_EsCeroPorCiento_Provisorio()
    {
        var fila = ProduccionOficialReglas.FilaDeCamara(0m, 480, 480m, 0.90m);
        var real = ProduccionOficialReglas.RealParaIndicadores(Fila(fila.CantidadOficial, fila.Estado));
        var r = IndicadoresProduccionCalculador.Comparar([(11200m, real)]);

        Assert.Equal(EstadoValidacionProduccion.PENDIENTE, fila.Estado);
        Assert.Equal(EstadoIndicador.Ok, r.Estado);
        Assert.Equal(0m, r.Porcentaje);
        Assert.True(r.Provisorio);
    }

    [Fact]
    public void SinObjetivo_ProductoSinObjetivoOTurnoSinProducto_NuncaCeroPorCiento()
    {
        Assert.Null(IndicadoresProduccionCalculador.ObjetivoTramo(null, 480, 480m));
        var r = IndicadoresProduccionCalculador.Comparar([(null, Validada(9000))]);

        Assert.Equal(EstadoIndicador.SinObjetivo, r.Estado);
        Assert.Null(r.Porcentaje);
        Assert.Equal(9000m, r.Real);
        Assert.Equal(1, r.TramosSinObjetivo);
    }

    [Fact]
    public void TramoSinDatosEnUnGrupo_NoBajaElPorcentaje_PeroSeInforma()
    {
        var r = IndicadoresProduccionCalculador.Comparar([(11200m, Validada(9300)), (11200m, RealProduccion.SinDatos)]);

        Assert.Equal(83.0m, r.Porcentaje);
        Assert.Equal(22400m, r.Objetivo);
        Assert.Equal(1, r.TramosSinDatos);
    }

    // ===================== Agregados (sumas, no promedios) =====================

    [Fact]
    public void PresupuestoVsReal_PorDiaYPorProducto_SumaNoPromedia()
    {
        var t1 = (Objetivo: (decimal?)11200m, Real: Validada(9300));
        var t2sh30 = ((decimal?)4200m, Validada(3900));
        var t2sh50 = ((decimal?)4200m, Validada(3850));
        var t3 = ((decimal?)6720m, Validada(6200));

        var dia = IndicadoresProduccionCalculador.Comparar([t1, t2sh30, t2sh50, t3]);
        var sh30 = IndicadoresProduccionCalculador.Comparar([t1, t2sh30]);
        var sh50 = IndicadoresProduccionCalculador.Comparar([t2sh50, t3]);

        Assert.Equal(26320m, dia.Objetivo);
        Assert.Equal(23250m, dia.Real);
        Assert.Equal(88.3m, dia.Porcentaje);           // el promedio de 83,0 / 92,3 / 92,3 daría 89,2
        Assert.Equal(85.7m, sh30.Porcentaje);          // 13.200 / 15.400
        Assert.Equal(92.0m, sh50.Porcentaje);          // 10.050 / 10.920
    }

    // ===================== Real oficial: una sola regla =====================

    [Fact]
    public void RealParaIndicadores_ValidadaCuenta_PendienteEsProvisorio_LaAnuladaNo()
    {
        var anulada = Fila(9000, EstadoValidacionProduccion.PENDIENTE, id: 1, anuladoPor: 2);
        var validada = Fila(9300, EstadoValidacionProduccion.VALIDADA, id: 2);
        var bolsones = Fila(6000, EstadoValidacionProduccion.PENDIENTE, MagnitudProduccion.Bolsones, id: 3);

        var vigente = ProduccionOficialReglas.Vigente([anulada, validada, bolsones], MagnitudProduccion.Mangas);
        var real = ProduccionOficialReglas.RealParaIndicadores(vigente);
        var pendiente = ProduccionOficialReglas.RealParaIndicadores(bolsones);

        Assert.Same(validada, vigente);
        Assert.Equal((9300m, EstadoRealProduccion.Validada, false), (real.Cantidad!.Value, real.Estado, real.Provisorio));
        Assert.Equal((6000m, EstadoRealProduccion.Pendiente, true), (pendiente.Cantidad!.Value, pendiente.Estado, pendiente.Provisorio));
        Assert.Null(ProduccionOficialReglas.Vigente([anulada], MagnitudProduccion.Mangas));
    }

    [Fact]
    public void Parcial_DeTramoAbierto_EsProvisorio_YSinMinutosEsSinDatos()
    {
        var parcial = ProduccionOficialReglas.Parcial(4000m, 200);

        Assert.Equal(EstadoRealProduccion.Parcial, parcial.Estado);
        Assert.True(parcial.Provisorio);
        Assert.False(ProduccionOficialReglas.Parcial(0m, 0).TieneDato);
    }

    [Fact]
    public void ConteoDelTramo_SumaSoloLosMinutosDeLaCamaraDentroDelTramo()
    {
        var minutos = new[]
        {
            new MinutoConteo(1, H(19, 12, 59), 50, 60),   // antes
            new MinutoConteo(1, H(19, 13), 20, 60),
            new MinutoConteo(1, H(19, 13), 5, 60),         // otro carril, mismo minuto
            new MinutoConteo(1, H(19, 15, 59), 21, 60),
            new MinutoConteo(1, H(19, 16), 40, 60),        // el Hasta no entra
            new MinutoConteo(2, H(19, 14), 99, 60)         // otra cámara
        };

        var (cantidad, conDatos) = ProduccionOficialReglas.ConteoDelTramo(minutos, 1, H(19, 13), H(19, 16));

        Assert.Equal(46m, cantidad);
        Assert.Equal(2, conDatos);
    }

    // ===================== Velocidad =====================

    [Fact]
    public void Velocidad_22_33MangasPorMinutoPor30Metros_Es670()
    {
        var desde = H(19, 5);
        var minutos = Enumerable.Range(0, 60).Select(i => new MinutoConteo(1, desde.AddMinutes(i), 22.33m, 60))
            .Concat(Enumerable.Range(60, 5).Select(i => new MinutoConteo(1, desde.AddMinutes(i), 0.5m, 0)))   // parada: no entra
            .ToList();

        var v = IndicadoresProduccionCalculador.VelocidadDelTramo(minutos, 1, desde, desde.AddHours(2), 30m);

        Assert.Equal(60, v.MinutosMarcha);
        Assert.Equal(1339.8m, v.Mangas);
        Assert.InRange(v.MetrosPorMinuto!.Value, 669.5m, 670.5m);
        Assert.Null(new VelocidadTramo(100, 10, null).MetrosPorMinuto);   // sin metros por manga
        Assert.Null(IndicadoresProduccionCalculador.Velocidad([new VelocidadTramo(0, 0, 30m)]));
    }

    [Fact]
    public void Velocidad_DeVariosTramos_PonderaPorMinutosEnMarcha()
    {
        // SH 30: 60 min a 22,33 mangas/min (670 m/min); SH 50: 30 min a 13,4 mangas/min (670 m/min).
        var v = IndicadoresProduccionCalculador.Velocidad([new VelocidadTramo(1339.8m, 60, 30m), new VelocidadTramo(402m, 30, 50m)]);

        Assert.Equal(669.9m, v);
    }

    // ===================== Indisponibilidad =====================

    [Fact]
    public void Indisponibilidad_MinutosQueCuentanSobreProgramados_SinProgramadosEsNull()
    {
        Assert.Equal(10.0m, IndicadoresProduccionCalculador.Indisponibilidad(48, 480));
        Assert.Null(IndicadoresProduccionCalculador.Indisponibilidad(48, 0));
    }

    [Fact]
    public void Indisponibilidad_ConUnTipoDeFallaQueNoCuenta()
    {
        var mecanica = new TipoFalla { TipoFallaId = 1, CuentaParaIndicadores = true };
        var programada = new TipoFalla { TipoFallaId = 2, CuentaParaIndicadores = false };
        var detenciones = new[]
        {
            new Detencion { Codigo = "A", TipoFallaId = 1, TipoFalla = mecanica, MinutosEfectivos = 30 },
            new Detencion { Codigo = "B", TipoFallaId = 2, TipoFalla = programada, MinutosEfectivos = 60 },
            new Detencion { Codigo = "C", MinutosEfectivos = 18 }   // sin tipo: cuenta
        };

        Assert.Equal(10.0m, IndicadoresProduccionCalculador.Indisponibilidad(detenciones.MinutosQueCuentan(), 480));
    }

    [Fact]
    public void MinutosProgramados_SinSnapshotSeRecalculan()
    {
        Assert.Equal(480, IndicadoresProduccionCalculador.MinutosProgramados(0, () => 480));
        Assert.Equal(360, IndicadoresProduccionCalculador.MinutosProgramados(360, () => throw new InvalidOperationException("no se recalcula")));
    }
}
