using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Services;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>
/// Turno automático en la Web: el obligado sin producto confirmado vuelve a "Inicio de turno"; en la tolerancia de su turno
/// anterior sólo ve sus pendientes y declarar; la barra marca el producto supuesto; y (pedido 24/11) una pendiente muestra la
/// duración que detectó la cámara (nunca "0 min").
/// </summary>
public class TurnoAutomaticoWebTests
{
    private static readonly DateTimeOffset Parada = new(2026, 11, 24, 18, 30, 0, TimeSpan.FromHours(-3));

    private static TurnoProduccionDto Turno(TramoProductoDto? producto, bool sinObjetivos = false) => new()
    {
        TurnoProduccionId = 1, LineaNombre = "TANGO 2", TurnoCodigo = "T1", Estado = "ABIERTO", ProductoActual = producto, SinObjetivos = sinObjetivos,
        InicioProgramado = Parada.AddHours(-13), FinProgramado = Parada.AddHours(-5)
    };

    [Fact]
    public void FaltaProducto_SinProductoOHeredadoSinConfirmar_SalvoLineaSinObjetivos()
    {
        Assert.True(new EstadoTurnoPropio(true, Turno(null)).FaltaProducto);
        Assert.True(new EstadoTurnoPropio(true, Turno(new TramoProductoDto { SinConfirmar = true })).FaltaProducto);
        Assert.False(new EstadoTurnoPropio(true, Turno(new TramoProductoDto())).FaltaProducto);
        Assert.False(new EstadoTurnoPropio(true, Turno(null, sinObjetivos: true)).FaltaProducto);
        Assert.False(new EstadoTurnoPropio(false, Turno(null)).FaltaProducto);   // el no obligado sigue como siempre
    }

    [Theory]
    [InlineData("/detenciones/pendientes", true)]
    [InlineData("/detenciones/9", true)]
    [InlineData("/detenciones/9/declarar?volver=%2Fdetenciones%2Fpendientes", true)]
    [InlineData("/detenciones", false)]
    [InlineData("/detenciones/nueva", false)]
    [InlineData("/produccion/tablero", false)]
    public void EsRutaDeTolerancia(string ruta, bool esperado) => Assert.Equal(esperado, TurnoPropioService.EsRutaDeTolerancia(ruta));

    [Fact]
    public void PuedeEstarEn_EnLaToleranciaSoloSusPendientes_YConTurnoSinProductoSoloInicioDeTurno()
    {
        var tolerancia = new EstadoTurnoPropio(true, null, new SesionToleranciaDto { TurnoProduccionId = 1 });
        Assert.True(tolerancia.PuedeEstarEn("/detenciones/pendientes"));
        Assert.True(tolerancia.PuedeEstarEn("/detenciones/9/declarar"));
        Assert.True(tolerancia.PuedeEstarEn("/produccion/inicio-turno"));
        Assert.False(tolerancia.PuedeEstarEn("/detenciones"));
        Assert.False(tolerancia.PuedeEstarEn("/produccion/tablero"));

        var sinTolerancia = new EstadoTurnoPropio(true, null);
        Assert.False(sinTolerancia.PuedeEstarEn("/detenciones/pendientes"));

        var sinProducto = new EstadoTurnoPropio(true, Turno(null));
        Assert.False(sinProducto.PuedeEstarEn("/produccion/tablero"));
        Assert.True(sinProducto.PuedeEstarEn("/produccion/inicio-turno"));
        Assert.True(new EstadoTurnoPropio(true, Turno(new TramoProductoDto())).PuedeEstarEn("/produccion/tablero"));
        Assert.True(EstadoTurnoPropio.Libre.PuedeEstarEn("/detenciones"));
    }

    [Theory]
    [InlineData(40, "40 s")]
    [InlineData(0, "0 s")]
    [InlineData(59, "59 s")]
    [InlineData(60, "1 min")]
    [InlineData(1800, "30 min")]
    [InlineData(1830, "31 min")]
    public void TextoDuracionDetectada_MenosDeUnMinutoEnSegundos(int segundos, string esperado) =>
        Assert.Equal(esperado, ProduccionUi.TextoDuracionDetectada(segundos));

    [Fact]
    public void BarraYEstados_ProductoSupuesto_YSinAperturaHistorico()
    {
        var t = Turno(new TramoProductoDto { ProductoNombre = "SH 50", Desde = Parada, SinConfirmar = true });
        Assert.Contains(ProduccionUi.PartesBarra(t, null), p => p.Contains("SH 50") && p.Contains("supuesto"));
        Assert.Equal("Sin apertura (histórico)", ProduccionUi.TextoEstado("SIN_APERTURA"));
        t.SinOperador = true;
        Assert.Equal("Abierto sin operador", ProduccionUi.TextoEstadoTurno(t));
    }

    private static async Task<string> RenderAsync(Dictionary<string, object?> parametros)
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<DuracionDetectada>(ParameterView.FromDictionary(parametros))).ToHtmlString());
    }

    [Fact]
    public async Task DuracionDetectada_EnElDetalle_MuestraLaVentanaDeLaParada()
    {
        var html = await RenderAsync(new()
        {
            ["Segundos"] = 1800, ["Detalle"] = true, ["Desde"] = Parada, ["Hasta"] = Parada.AddMinutes(30)
        });
        Assert.Contains("Duración detectada por cámara", html);
        Assert.Contains("30 min", html);
        Assert.Contains("18:30 → 19:00", html);
    }

    [Fact]
    public async Task DuracionDetectada_EnCurso_YEnListados_ConLaMarcaDeSinDeclarar()
    {
        var enCurso = await RenderAsync(new() { ["Segundos"] = 600, ["Detalle"] = true, ["Desde"] = Parada, ["EnCurso"] = true });
        Assert.Contains("18:30 → en curso", enCurso);

        var lista = await RenderAsync(new() { ["Segundos"] = 40 });
        Assert.Contains("40 s", lista);
        Assert.Contains("(detectados, sin declarar)", lista);
        Assert.DoesNotContain("0 min", lista);

        Assert.Equal(string.Empty, (await RenderAsync(new() { ["Segundos"] = null })).Trim());
    }
}
