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
        Assert.Contains("20 min", d);
        Assert.Contains("15 min", d);
        Assert.Contains("SIN DECLARAR", d);
    }

    // ===================== T-770 ajustes 23/11 =====================

    [Fact]
    public async Task Medidor_EtiquetaCienPorCiento_EntraEnElDibujo_YDiceProvisoria()
    {
        var html = await RenderAsync<MedidorEficiencia>(new()
        {
            ["Eficiencia"] = new EficienciaTableroDto { Porcentaje = 62m, Estado = "OK", Nivel = "ROJO", Provisorio = true, Real = 792, ObjetivoALaHora = 1272, ObjetivoTurno = 11083 }
        });
        var vb = System.Text.RegularExpressions.Regex.Match(html, "viewBox=\"(-?[\\d.]+) (-?[\\d.]+) ([\\d.]+) ([\\d.]+)\"");
        Assert.True(vb.Success, "el medidor no tiene viewBox");
        var x0 = double.Parse(vb.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        var ancho = double.Parse(vb.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
        // Etiquetas: texto centrado en x; "100%" (4 caracteres de 8 px ≈ 20 de ancho) y "0%".
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(html, "<text x=\"([\\d.-]+)\"[^>]*>(\\d+)%</text>"))
        {
            var x = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var medio = m.Groups[2].Value.Length + 1;   // "100%" = 4 caracteres
            Assert.True(x - medio * 2.6 >= x0 && x + medio * 2.6 <= x0 + ancho, $"La etiqueta {m.Groups[2].Value}% se corta (x={x}, viewBox {x0}..{x0 + ancho})");
        }
        Assert.Contains("100%</text>", html);
        Assert.Contains("Provisoria (turno en curso)", html);
        Assert.Contains("text-danger-emphasis", html);     // el semáforo del medidor queda igual (62 % rojo)
    }

    [Fact]
    public async Task Detenciones_BarrasSoloConMinutos_YNombreLargoEnSuRenglon()
    {
        var tipos = new[] { "FALLA MECANICA", "FALLA ELECTRICA", "FALLA DE CONTROL", "CAMBIO DE PRODUCTO", "MANTENIMIENTO PREVENTIVO",
            "FALTA DE MATERIAL", "PROBLEMA DE CALIDAD", "AJUSTE DE MAQUINA", "FALLA PROCESO", "FALLA EQUIPO", "CAMBIO DE FTO.", "SUMINISTRO", "OTRO" };
        var barras = tipos.Select(n => new BarraTipoFallaDto { Nombre = n, Cuenta = true }).ToList();
        barras.Single(b => b.Nombre == "FALLA PROCESO").Minutos = 12;
        barras.Single(b => b.Nombre == "FALLA PROCESO").Cantidad = 1;
        barras.Single(b => b.Nombre == "MANTENIMIENTO PREVENTIVO").Minutos = 7;
        barras.Single(b => b.Nombre == "MANTENIMIENTO PREVENTIVO").Cantidad = 1;
        barras.Add(new BarraTipoFallaDto { Nombre = "PARADA PROGRAMADA", Minutos = 30, Cantidad = 1, Cuenta = false });
        barras.Add(new BarraTipoFallaDto { Nombre = "Sin declarar", Minutos = 0, Cantidad = 0, Cuenta = true, SinDeclarar = true });
        var dto = new DetencionesTipoTableroDto { Barras = barras, MinutosTotal = 19, MinutosNoCuentan = 30 };

        var html = await RenderAsync<BarrasDetenciones>(new() { ["Detenciones"] = dto });
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(html, "data-testid=\"barra-tipo\"").Count);
        Assert.Contains("MANTENIMIENTO PREVENTIVO", html);        // entero, no "MANTENIMIENTO PREVE…"
        Assert.Contains("FALLA PROCESO", html);
        Assert.DoesNotContain("FALLA MECANICA", html);            // tipos en 0: fuera
        Assert.DoesNotContain("SIN DECLARAR", html);              // sin minutos pendientes: fuera
        Assert.DoesNotContain("PARADA PROGRAMADA", html);         // programadas: aparte
        // El nombre va en su renglón y la barra debajo (y distinto): el valor no se pisa con el nombre.
        var nombre = System.Text.RegularExpressions.Regex.Match(html, "<text x=\"0\" y=\"(\\d+)\"[^>]*>MANTENIMIENTO PREVENTIVO");
        Assert.True(nombre.Success);
        var yNombre = int.Parse(nombre.Groups[1].Value);
        var rect = System.Text.RegularExpressions.Regex.Match(html[nombre.Index..], "<rect x=\"0\" y=\"(\\d+)\"");
        Assert.True(int.Parse(rect.Groups[1].Value) > yNombre);

        // Declaradas: sin la barra de "Sin declarar" aunque tenga minutos
        dto.Barras.Single(b => b.SinDeclarar).Minutos = 15;
        Assert.Contains("SIN DECLARAR", await RenderAsync<BarrasDetenciones>(new() { ["Detenciones"] = dto }));
        Assert.DoesNotContain("SIN DECLARAR", await RenderAsync<BarrasDetenciones>(new() { ["Detenciones"] = dto, ["IncluirSinDeclarar"] = false }));

        var vacio = await RenderAsync<BarrasDetenciones>(new() { ["Detenciones"] = new DetencionesTipoTableroDto { Barras = barras.Take(3).ToList() } });
        Assert.Contains("barras-detenciones-vacio", vacio);
    }

    [Fact]
    public async Task Detenciones_CuatroCifras_PendientesDeclara_ElRestoSoloLectura()
    {
        var cifras = new CifrasDetencionesTableroDto
        {
            Pendientes = new() { Cantidad = 2, Minutos = 20 },
            Declaradas = new() { Cantidad = 3, Minutos = 25 },
            Totales = new() { Cantidad = 5, Minutos = 45 },
            Microcortes = new() { Cantidad = 4, Minutos = 3 },
            Programadas = new() { Cantidad = 1, Minutos = 30 }
        };
        var html = await RenderAsync<CifrasDetenciones>(new() { ["Cifras"] = cifras });

        foreach (var (id, modal, cantidad, minutos) in new[]
                 {
                     ("pendientes", "declarar", 2, "20 min"), ("declaradas", "solo-lectura", 3, "25 min"),
                     ("totales", "solo-lectura", 5, "45 min"), ("microcortes", "solo-lectura", 4, "3 min")
                 })
        {
            var tile = System.Text.RegularExpressions.Regex.Match(html, $"<button[^>]*data-testid=\"cifra-{id}\"[^>]*>(.*?)</button>", System.Text.RegularExpressions.RegexOptions.Singleline);
            Assert.True(tile.Success, $"falta la cifra {id}");
            Assert.Contains($"data-modal=\"{modal}\"", tile.Value);
            Assert.Contains($">{cantidad}</span>", tile.Value);
            Assert.Contains(minutos, tile.Value);
        }
        Assert.Contains("Pendientes de declarar", html);
        Assert.Contains("+ 1 programada (30 min, no cuenta)", System.Net.WebUtility.HtmlDecode(html));
        Assert.Contains("btn-outline-danger", html);               // hay pendientes: en rojo

        var sinPendientes = await RenderAsync<CifrasDetenciones>(new() { ["Cifras"] = new CifrasDetencionesTableroDto() });
        Assert.DoesNotContain("btn-outline-danger", sinPendientes);
        Assert.DoesNotContain("cifras-programadas", sinPendientes);
    }

    [Fact]
    public async Task Bloques_ColorDeAcentoPorTema()
    {
        foreach (var tema in Enum.GetValues<TableroUi.Tema>())
        {
            var html = await RenderAsync<TarjetaGrafico>(new() { ["Titulo"] = tema.ToString(), ["Tema"] = tema });
            Assert.Contains($"data-tema=\"{TableroUi.ClaveTema(tema)}\"", html);
            Assert.Contains($"border-top: 6px solid {TableroUi.ColorTema(tema)}", html);
        }
    }
}
