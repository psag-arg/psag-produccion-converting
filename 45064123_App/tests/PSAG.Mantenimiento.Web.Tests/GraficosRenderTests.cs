using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>
/// T-770 iteración 2 · Los 4 gráficos SVG se dibujan sin errores con datos y sin datos (HtmlRenderer, sin navegador):
/// la aguja está donde dice el %, "Sin datos" nunca es 0 %, y las coordenadas van con punto decimal.
/// </summary>
public class GraficosRenderTests
{
    private static async Task<string> RenderAsync<T>(Dictionary<string, object?> parametros) where T : IComponent
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<T>(ParameterView.FromDictionary(parametros))).ToHtmlString());
    }

    [Fact]
    public async Task Medidor_ConAgujaYSinDato()
    {
        var html = await RenderAsync<MedidorEficiencia>(new()
        {
            ["Eficiencia"] = new EficienciaTableroDto { Porcentaje = 68m, Estado = "OK", Nivel = "AMARILLO", Real = 7616, ObjetivoALaHora = 11200, ObjetivoTurno = 11200, PorcentajeTurno = 68m }
        });
        Assert.Contains("data-testid=\"aguja\"", html);
        Assert.Contains("68 %", html);
        Assert.Contains("text-warning-emphasis", html);

        var sinDato = await RenderAsync<MedidorEficiencia>(new() { ["Eficiencia"] = new EficienciaTableroDto { Estado = "SIN_DATOS", Nivel = "SIN_DATO" } });
        Assert.DoesNotContain("data-testid=\"aguja\"", sinDato);
        Assert.Contains("Sin datos", sinDato);
    }

    [Fact]
    public async Task Produccion_BolsonesSinDatos_NuncaCeroPorCiento()
    {
        var html = await RenderAsync<BarrasProduccion>(new()
        {
            ["Produccion"] = new ProduccionTableroDto
            {
                ProductoNombre = "HIGIÉNICO SH 30 mts x 4 – PH1 20",
                Productos = ["HIGIÉNICO SH 30 mts x 4 – PH1 20"],
                Mangas = new BarraProduccionDto { Magnitud = "MANGAS", Real = 5000, ObjetivoTurno = 11200, ObjetivoALaHora = 6000, PorcentajeTurno = 44.6m, Estado = "OK", Nivel = "VERDE" },
                Bolsones = new BarraProduccionDto { Magnitud = "BOLSONES", ObjetivoTurno = 6500, Estado = "SIN_DATOS", Nivel = "SIN_DATO" }
            }
        });
        Assert.Contains("Obj: 11.200 mangas", html.Replace(",", "."));
        Assert.Contains("barra-real-MANGAS", html);
        Assert.DoesNotContain("barra-real-BOLSONES", html);
        Assert.Contains("Sin datos", html);
        Assert.Contains("HIGIÉNICO SH 30 mts x 4 – PH1 20", System.Net.WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task Velocidad_YDetenciones_SeDibujan()
    {
        var desde = new DateTimeOffset(2026, 11, 19, 13, 0, 0, TimeSpan.FromHours(-3));
        var v = await RenderAsync<LineaVelocidad>(new()
        {
            ["Velocidad"] = new VelocidadTableroDto
            {
                Desde = desde, EjeDesde = desde, EjeHasta = desde.AddHours(8), MetrosPorMinuto = [600m, 0m, null, 650.5m], Referencia = 670m,
                Actual = 650.5m, Tramos = [new TramoVelocidadDto { Desde = desde, ProductoNombre = "SH 30", MetrosPorManga = 30 }]
            }
        });
        Assert.Contains("<polyline", v);
        Assert.Contains("normal 670 m/min", v);
        Assert.DoesNotMatch("points=\"[^\"]*\\d,\\d+,", v);   // sin comas decimales dentro de las coordenadas

        var d = await RenderAsync<BarrasDetenciones>(new()
        {
            ["Detenciones"] = new DetencionesTipoTableroDto
            {
                Barras = [new BarraTipoFallaDto { Nombre = "FALLA PROCESO", Minutos = 20, Cantidad = 2, Cuenta = true },
                          new BarraTipoFallaDto { Nombre = "Sin declarar", Minutos = 15, Cantidad = 1, Cuenta = true, SinDeclarar = true }],
                MinutosTotal = 35, MinutosPendientes = 15, CantidadPendientes = 1
            }
        });
        Assert.Contains("35 min", d);
        Assert.Contains("15 min", d);
        Assert.Contains("SIN DECLARAR", d);
        Assert.Contains("btn-outline-danger", d);
    }
}
