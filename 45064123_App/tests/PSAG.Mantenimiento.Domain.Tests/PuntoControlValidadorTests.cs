using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Domain.Tests;

/// <summary>
/// T-545 · Validación de la carga de Control de Proceso según el tipo de dato del punto.
/// </summary>
public class PuntoControlValidadorTests
{
    private static readonly RangoControl Rango3a4 = new(3m, 4m);
    private static readonly RangoControl SinRango = new(null, null);

    // ---------- Numérico ----------

    [Theory]
    [InlineData(3.0)]
    [InlineData(3.5)]
    [InlineData(4.0)]
    public void Numerico_DentroDelRango_EsValidoYNoEsDesvio(double valor)
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, Rango3a4, (decimal)valor, null, null);

        Assert.True(r.EsValido);
        Assert.False(r.FueraDeEstandar);
        Assert.Equal((decimal)valor, r.ValorReal);
    }

    [Theory]
    [InlineData(2.99)]
    [InlineData(6)]
    public void Numerico_FueraDeRango_SinObservacion_EsError(double valor)
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, Rango3a4, (decimal)valor, null, "  ");

        Assert.False(r.EsValido);
        Assert.Contains("observación es obligatoria", r.Error);
    }

    [Fact]
    public void Numerico_FueraDeRango_ConObservacion_EsValidoYMarcaDesvio()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, Rango3a4, 6m, null, "Se cambió el rollo");

        Assert.True(r.EsValido);
        Assert.True(r.FueraDeEstandar);
    }

    [Fact]
    public void Numerico_SoloMinimo_ControlaSoloElPiso()
    {
        var piso = new RangoControl(10m, null);

        Assert.False(PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, piso, 1000m, null, null).FueraDeEstandar);
        Assert.True(PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, piso, 9m, null, "x").FueraDeEstandar);
    }

    [Fact]
    public void Numerico_SinRango_NuncaEsDesvio()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, SinRango, -5m, null, null);

        Assert.True(r.EsValido);
        Assert.False(r.FueraDeEstandar);
    }

    [Fact]
    public void Numerico_ConTexto_EsError()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, Rango3a4, null, "tres", null);

        Assert.False(r.EsValido);
    }

    [Fact]
    public void Rango_SeValidaIgualQueNumerico()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.RANGO, Rango3a4, 5m, null, null);

        Assert.False(r.EsValido);
    }

    [Theory]
    [InlineData(TipoDatoParametro.NUMERICO)]
    [InlineData(TipoDatoParametro.OK_NOOK)]
    [InlineData(TipoDatoParametro.TEXTO)]
    public void SinValor_NoEsErrorPeroNoSeGuarda(TipoDatoParametro tipo)
    {
        var r = PuntoControlValidador.ValidarCarga(tipo, Rango3a4, null, "   ", null);

        Assert.True(r.EsValido);
        Assert.True(r.SinValor);
    }

    // ---------- OK / NO OK ----------

    [Theory]
    [InlineData("OK", "OK")]
    [InlineData(" ok ", "OK")]
    [InlineData("NO OK", "NO OK")]
    [InlineData("no_ok", "NO OK")]
    [InlineData("NoOk", "NO OK")]
    public void OkNoOk_NormalizaLaRespuesta(string cargado, string esperado)
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.OK_NOOK, SinRango, null, cargado, "obs");

        Assert.True(r.EsValido);
        Assert.Equal(esperado, r.ValorRealTexto);
    }

    [Fact]
    public void OkNoOk_Ok_NoEsDesvio()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.OK_NOOK, SinRango, null, "OK", null);

        Assert.True(r.EsValido);
        Assert.False(r.FueraDeEstandar);
    }

    [Fact]
    public void OkNoOk_NoOk_SinObservacion_EsError()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.OK_NOOK, SinRango, null, "NO OK", null);

        Assert.False(r.EsValido);
        Assert.Contains("NO OK", r.Error);
    }

    [Fact]
    public void OkNoOk_NoOk_ConObservacion_EsDesvio()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.OK_NOOK, SinRango, null, "NO OK", "Pérdida de aire");

        Assert.True(r.EsValido);
        Assert.True(r.FueraDeEstandar);
    }

    [Theory]
    [InlineData("SI")]
    [InlineData("bien")]
    public void OkNoOk_OtraRespuesta_EsError(string cargado)
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.OK_NOOK, SinRango, null, cargado, null);

        Assert.False(r.EsValido);
    }

    [Fact]
    public void OkNoOk_ConNumero_EsError()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.OK_NOOK, SinRango, 1m, null, null);

        Assert.False(r.EsValido);
    }

    // ---------- Texto ----------

    [Fact]
    public void Texto_SeGuardaRecortadoYNuncaEsDesvio()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.TEXTO, SinRango, null, "  Lote 1234 ", null);

        Assert.True(r.EsValido);
        Assert.False(r.FueraDeEstandar);
        Assert.Equal("Lote 1234", r.ValorRealTexto);
    }

    [Fact]
    public void Texto_MasLargoQueLaColumna_EsError()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.TEXTO, SinRango, null, new string('x', 201), null);

        Assert.False(r.EsValido);
    }

    [Fact]
    public void Texto_ConNumero_EsError()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.TEXTO, SinRango, 3m, null, null);

        Assert.False(r.EsValido);
    }

    [Fact]
    public void Observacion_MasLargaQueLaColumna_EsError()
    {
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, Rango3a4, 3.5m, null, new string('x', 1001));

        Assert.False(r.EsValido);
    }

    // ---------- Definición (maestro) ----------

    [Fact]
    public void Definicion_SinNombre_EsInvalida()
    {
        Assert.NotNull(PuntoControlValidador.MotivoDefinicionInvalida(" ", TipoDatoParametro.NUMERICO, null, null));
    }

    [Fact]
    public void Definicion_MinimoMayorQueMaximo_EsInvalida()
    {
        Assert.NotNull(PuntoControlValidador.MotivoDefinicionInvalida("Presión", TipoDatoParametro.NUMERICO, 5m, 4m));
    }

    [Theory]
    [InlineData(TipoDatoParametro.OK_NOOK)]
    [InlineData(TipoDatoParametro.TEXTO)]
    public void Definicion_RangoEnPuntoNoNumerico_EsInvalida(TipoDatoParametro tipo)
    {
        Assert.NotNull(PuntoControlValidador.MotivoDefinicionInvalida("Punto", tipo, 1m, null));
    }

    [Fact]
    public void Definicion_NumericaConRango_EsValida()
    {
        Assert.Null(PuntoControlValidador.MotivoDefinicionInvalida("Presión de soplo", TipoDatoParametro.NUMERICO, 3.8m, 4.1m));
    }

    // ---------- Snapshot del estándar ----------

    [Fact]
    public void DescribirEstandar_Numerico_ArmaRangoConUnidad()
    {
        Assert.Equal("3,8 - 4,1 bar", PuntoControlValidador.DescribirEstandar(TipoDatoParametro.NUMERICO, new RangoControl(3.8m, 4.1m), "bar"));
        Assert.Equal("≥ 10", PuntoControlValidador.DescribirEstandar(TipoDatoParametro.NUMERICO, new RangoControl(10m, null), null));
        Assert.Null(PuntoControlValidador.DescribirEstandar(TipoDatoParametro.NUMERICO, SinRango, "bar"));
        Assert.Equal("OK", PuntoControlValidador.DescribirEstandar(TipoDatoParametro.OK_NOOK, SinRango, null));
        Assert.Null(PuntoControlValidador.DescribirEstandar(TipoDatoParametro.TEXTO, SinRango, null));
    }

    [Fact]
    public void RangoDesdeEstandar_RespetaToleranciaYValorExacto()
    {
        var conTolerancia = RangoControl.DesdeEstandar(new EstandarParametro { ValorMin = 152m, Tolerancia = 2m });
        var exacto = RangoControl.DesdeEstandar(new EstandarParametro { ValorMin = 98.5m, Tolerancia = 0m });
        var rango = RangoControl.DesdeEstandar(new EstandarParametro { ValorMin = 152m, ValorMax = 153m });

        Assert.Equal(new RangoControl(150m, 154m), conTolerancia);
        Assert.Equal(new RangoControl(98.5m, 98.5m), exacto);
        Assert.Equal(new RangoControl(152m, 153m), rango);
    }

    [Fact]
    public void RangoDesdeEstandar_SoloMinimo_EsPisoInclusivo()
    {
        // "> 10mm" de la planilla de Tango 2: ValorMin = 10, sin máximo ni tolerancia.
        var piso = RangoControl.DesdeEstandar(new EstandarParametro { ValorEstandarTexto = "> 10mm", ValorMin = 10m });

        Assert.Equal(new RangoControl(10m, null), piso);
        Assert.False(PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, piso, 25m, null, null).FueraDeEstandar);
        Assert.False(PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, piso, 10m, null, null).FueraDeEstandar);
        Assert.NotNull(PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, piso, 8m, null, null).Error);
    }

    [Fact]
    public void RangoDesdeEstandar_SoloTexto_EsReferenciaSinDesvio()
    {
        // Valor único de la planilla ("202"): se muestra como referencia, no se valida.
        var referencia = RangoControl.DesdeEstandar(new EstandarParametro { ValorEstandarTexto = "202" });

        Assert.False(referencia.TieneLimites);
        var r = PuntoControlValidador.ValidarCarga(TipoDatoParametro.NUMERICO, referencia, 250m, null, null);
        Assert.True(r.EsValido);
        Assert.False(r.FueraDeEstandar);
    }
}
