using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// Columna Estado del Control de Proceso (reunión 03/11/2026 [76:32]-[77:05]): en la demo un 5 contra
/// "-0,7 (ref.)" y un 45 contra "0" mostraban OK. Sin rango contra el que comparar tiene que avisar
/// "falta estándar", no OK.
/// </summary>
public class EvaluacionPuntoTests
{
    private static readonly RangoControl SinRango = new(null, null);

    private static EvaluacionPunto CargarYEvaluar(EstandarParametro estandar, decimal valor, string? observacion = "obs")
    {
        var rango = RangoControl.DesdeEstandar(estandar);
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, rango, valor, null, observacion);
        Assert.True(r.EsValido, r.Error);
        return PuntoControlValidador.Evaluar(TipoDatoParametro.NUMERICO, rango, r.ValorReal, r.ValorRealTexto, r.FueraDeEstandar);
    }

    [Theory]
    [InlineData("-0,7", 5)]   // caso de la demo: Slide Rodillo 7 · Marcha
    [InlineData("0", 45)]     // caso de la demo: Slide Rodillo 7 · Pasaje Papel
    [InlineData("202", 202)]  // aunque coincida con la referencia, no hay tolerancia: no se puede decir OK
    public void ValorDeReferenciaSinTolerancia_NoEsOk_EsFaltaEstandar(string referencia, decimal valor)
    {
        var estandar = new EstandarParametro { ValorEstandarTexto = referencia };

        Assert.Equal(EvaluacionPunto.SinEstandar, CargarYEvaluar(estandar, valor, observacion: null));
    }

    [Fact]
    public void ReferenciaConTolerancia_SeCompara()
    {
        // Cuando Ferretti pase la tolerancia, "-0,7 ± 0,2" ya compara.
        var estandar = new EstandarParametro { ValorEstandarTexto = "-0,7", ValorMin = -0.7m, Tolerancia = 0.2m };

        Assert.Equal(EvaluacionPunto.Ok, CargarYEvaluar(estandar, -0.6m));
        Assert.Equal(EvaluacionPunto.FueraDeRango, CargarYEvaluar(estandar, 5m));
    }

    [Theory]
    [InlineData(152, EvaluacionPunto.Ok)]
    [InlineData(153, EvaluacionPunto.Ok)]
    [InlineData(151.9, EvaluacionPunto.FueraDeRango)]
    [InlineData(160, EvaluacionPunto.FueraDeRango)]
    public void RangoDeLaPlanilla_DaOkOFuera(double valor, EvaluacionPunto esperado)
    {
        var estandar = new EstandarParametro { ValorEstandarTexto = "152 - 153", ValorMin = 152m, ValorMax = 153m };

        Assert.Equal(esperado, CargarYEvaluar(estandar, (decimal)valor));
    }

    [Fact]
    public void SinEstandarCargado_EsFaltaEstandar()
    {
        Assert.Equal(EvaluacionPunto.SinEstandar,
            PuntoControlValidador.Evaluar(TipoDatoParametro.NUMERICO, SinRango, 1200m, null, fueraDeEstandar: false));
    }

    [Fact]
    public void SinValor_EsSinValor_AunqueFalteElEstandar()
    {
        Assert.Equal(EvaluacionPunto.SinValor,
            PuntoControlValidador.Evaluar(TipoDatoParametro.NUMERICO, SinRango, null, null, false));
        Assert.Equal(EvaluacionPunto.SinValor,
            PuntoControlValidador.Evaluar(TipoDatoParametro.TEXTO, SinRango, null, "  ", false));
    }

    [Fact]
    public void ElDesvioGuardadoManda_NoSeRecomparaContraElRangoDeHoy()
    {
        // Snapshot: si ese día fue desvío, sigue siéndolo aunque hoy el rango lo incluya, y al revés.
        var rangoDeHoy = new RangoControl(0m, 100m);

        Assert.Equal(EvaluacionPunto.FueraDeRango,
            PuntoControlValidador.Evaluar(TipoDatoParametro.NUMERICO, rangoDeHoy, 50m, null, fueraDeEstandar: true));
        Assert.Equal(EvaluacionPunto.Ok,
            PuntoControlValidador.Evaluar(TipoDatoParametro.NUMERICO, rangoDeHoy, 500m, null, fueraDeEstandar: false));
    }

    [Fact]
    public void OkNoOk_YTexto()
    {
        Assert.Equal(EvaluacionPunto.Ok,
            PuntoControlValidador.Evaluar(TipoDatoParametro.OK_NOOK, SinRango, null, "OK", false));
        Assert.Equal(EvaluacionPunto.FueraDeRango,
            PuntoControlValidador.Evaluar(TipoDatoParametro.OK_NOOK, SinRango, null, "NO OK", true));
        Assert.Equal(EvaluacionPunto.Informativo,
            PuntoControlValidador.Evaluar(TipoDatoParametro.TEXTO, SinRango, null, "papel húmedo", false));
    }

    [Theory]
    [InlineData(EvaluacionPunto.Ok, "OK")]
    [InlineData(EvaluacionPunto.FueraDeRango, "FUERA_DE_RANGO")]
    [InlineData(EvaluacionPunto.SinEstandar, "SIN_ESTANDAR")]
    [InlineData(EvaluacionPunto.Informativo, "INFORMATIVO")]
    [InlineData(EvaluacionPunto.SinValor, "SIN_VALOR")]
    public void Codigo_DelDto(EvaluacionPunto evaluacion, string codigo)
    {
        Assert.Equal(codigo, PuntoControlValidador.Codigo(evaluacion));
    }
}
