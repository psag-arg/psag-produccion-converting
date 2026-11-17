using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>Refresco automático del Dashboard (reunión 17/11/2026): sin solapar llamadas y liberando el timer.</summary>
public class RefrescoPeriodicoTests
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ElTimerLlamaSolo_YAlLiberarloDejaDeLlamar()
    {
        var llamadas = 0;
        var tres = new TaskCompletionSource();
        var refresco = new RefrescoPeriodico(TimeSpan.FromMilliseconds(20), _ =>
        {
            if (Interlocked.Increment(ref llamadas) == 3)
                tres.TrySetResult();
            return Task.CompletedTask;
        });

        refresco.Iniciar();
        await tres.Task.WaitAsync(Espera);
        await refresco.DisposeAsync();
        var alLiberar = Volatile.Read(ref llamadas);
        await Task.Delay(100);

        Assert.Equal(alLiberar, Volatile.Read(ref llamadas));
    }

    [Fact]
    public async Task UnaCargaLenta_NoSeSolapa_LosTicsSeSaltean()
    {
        var enCurso = 0;
        var maximo = 0;
        var llamadas = 0;
        var liberar = new TaskCompletionSource();
        var empezo = new TaskCompletionSource();
        var refresco = new RefrescoPeriodico(TimeSpan.FromMilliseconds(10), async _ =>
        {
            var ahora = Interlocked.Increment(ref enCurso);
            maximo = Math.Max(maximo, ahora);
            Interlocked.Increment(ref llamadas);
            empezo.TrySetResult();
            await liberar.Task;
            Interlocked.Decrement(ref enCurso);
        });

        refresco.Iniciar();
        await empezo.Task.WaitAsync(Espera);
        await Task.Delay(120); // pasan ~12 tics con la carga trabada
        Assert.True(refresco.EnCurso);
        Assert.Equal(1, Volatile.Read(ref llamadas));

        liberar.SetResult();
        await refresco.DisposeAsync();
        Assert.Equal(1, maximo);
    }

    [Fact]
    public async Task Solicitar_ConUnaCargaEnCurso_VuelveACargarUnaSolaVezAlTerminar()
    {
        // Cambio de línea mientras el timer carga: la línea nueva se tiene que cargar igual.
        var llamadas = 0;
        var liberar = new TaskCompletionSource();
        var empezo = new TaskCompletionSource();
        var refresco = new RefrescoPeriodico(TimeSpan.FromHours(1), async _ =>
        {
            if (Interlocked.Increment(ref llamadas) == 1)
            {
                empezo.TrySetResult();
                await liberar.Task;
            }
        });

        var primera = refresco.SolicitarAsync();
        await empezo.Task.WaitAsync(Espera);
        var segunda = refresco.SolicitarAsync();
        var tercera = refresco.SolicitarAsync();
        liberar.SetResult();
        await Task.WhenAll(primera, segunda, tercera).WaitAsync(Espera);

        Assert.Equal(2, llamadas);
        await refresco.DisposeAsync();
    }

    [Fact]
    public async Task UnError_NoCortaElRefresco()
    {
        var llamadas = 0;
        var errores = 0;
        var tres = new TaskCompletionSource();
        var refresco = new RefrescoPeriodico(TimeSpan.FromMilliseconds(15), _ =>
        {
            if (Interlocked.Increment(ref llamadas) >= 3)
                tres.TrySetResult();
            throw new HttpRequestException("API caída");
        }, _ => Interlocked.Increment(ref errores));

        refresco.Iniciar();
        await tres.Task.WaitAsync(Espera);
        await refresco.DisposeAsync();

        Assert.True(Volatile.Read(ref errores) >= 2);
    }

    [Fact]
    public async Task Liberar_CancelaLaCargaEnCurso_YDespuesNoHaceNada()
    {
        var cancelada = new TaskCompletionSource();
        var empezo = new TaskCompletionSource();
        var llamadas = 0;
        var refresco = new RefrescoPeriodico(TimeSpan.FromHours(1), async ct =>
        {
            Interlocked.Increment(ref llamadas);
            empezo.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, ct); }
            catch (OperationCanceledException) { cancelada.TrySetResult(); throw; }
        });

        var carga = refresco.SolicitarAsync();
        await empezo.Task.WaitAsync(Espera);
        await refresco.DisposeAsync();
        await cancelada.Task.WaitAsync(Espera);
        await carga.WaitAsync(Espera);

        await refresco.SolicitarAsync();
        refresco.Iniciar();
        Assert.Equal(1, llamadas);
        await refresco.DisposeAsync(); // dos veces no falla
    }

    [Fact]
    public void IntervaloCeroONegativo_NoSeAcepta()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RefrescoPeriodico(TimeSpan.Zero, _ => Task.CompletedTask));
    }
}

public class OpcionesDashboardTests
{
    private static OpcionesDashboard Leer(string? segundos, string? minutos)
    {
        var valores = new Dictionary<string, string?>();
        if (segundos != null) valores["Dashboard:SegundosRefresco"] = segundos;
        if (minutos != null) valores["Dashboard:MinutosDatosAtrasados"] = minutos;
        return OpcionesDashboard.Leer(new ConfigurationBuilder().AddInMemoryCollection(valores).Build());
    }

    [Fact]
    public void SinConfiguracion_CadaMinuto_YCincoMinutosParaDatosViejos()
    {
        var o = Leer(null, null);
        Assert.Equal(60, o.SegundosRefresco);
        Assert.Equal(5, o.MinutosDatosAtrasados);
        Assert.True(o.RefrescoActivo);
    }

    [Theory]
    [InlineData("120", 120, true)]
    [InlineData("5", 15, true)]      // muy seguido: mínimo 15 s
    [InlineData("99999", 3600, true)]
    [InlineData("0", 0, false)]      // 0 apaga el refresco automático
    [InlineData("-10", 15, true)]
    [InlineData("abc", 60, true)]
    public void SegundosRefresco_Acotado(string valor, int esperado, bool activo)
    {
        var o = Leer(valor, null);
        Assert.Equal(esperado, o.SegundosRefresco);
        Assert.Equal(activo, o.RefrescoActivo);
    }

    [Theory]
    [InlineData("10", 10)]
    [InlineData("0", 5)]
    [InlineData("x", 5)]
    public void MinutosDatosAtrasados(string valor, int esperado)
    {
        Assert.Equal(esperado, Leer(null, valor).MinutosDatosAtrasados);
    }
}

/// <summary>"Datos hasta las HH:mm" en la tarjeta de cada cámara.</summary>
public class DatosHastaUiTests
{
    private static readonly TimeSpan Planta = TimeSpan.FromHours(-3);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 17, 10, 18, 0, Planta);

    private static EficienciaCamaraPuntoDto Camara(DateTimeOffset? hasta, string estado, int? atraso = null) => new()
    {
        PuntoControlCalidadId = 1, LineaNombre = "Tango 2", NumeroCamara = 9, Unidad = "rollos",
        DatosHasta = hasta, EstadoDatos = estado, MinutosSinDatosNuevos = atraso
    };

    [Fact]
    public void DeHoy_SoloLaHora()
    {
        var c = Camara(new DateTimeOffset(2026, 11, 17, 10, 16, 0, Planta), "AL_DIA", 2);
        Assert.Equal("Datos hasta las 10:16", EficienciaUi.TextoDatosHasta(c, Ahora));
        Assert.Contains("10:15 a 10:16", EficienciaUi.TituloDatosHasta(c));
        Assert.Contains("bg-success-subtle", EficienciaUi.ClaseDatosHasta(c.EstadoDatos));
        Assert.False(EficienciaUi.Atrasada(c));
    }

    [Fact]
    public void DeOtroDia_ConFecha()
    {
        var c = Camara(new DateTimeOffset(2026, 11, 15, 22, 40, 0, Planta), "ATRASADO", 2138);
        Assert.Equal("Datos hasta el 15/11 22:40", EficienciaUi.TextoDatosHasta(c, Ahora));
        Assert.True(EficienciaUi.Atrasada(c));
        Assert.Equal("text-bg-danger", EficienciaUi.ClaseDatosHasta(c.EstadoDatos));
        Assert.StartsWith("Sin datos nuevos hace 35 h 38 min", EficienciaUi.AvisoAtraso(c));
    }

    [Fact]
    public void SinDatos()
    {
        var c = Camara(null, "SIN_DATOS");
        Assert.Equal("Sin datos todavía", EficienciaUi.TextoDatosHasta(c, Ahora));
        Assert.Equal("text-bg-secondary", EficienciaUi.ClaseDatosHasta(c.EstadoDatos));
    }

    [Fact]
    public void HoraGeneral_LaMasViejaDeLasAlDia_SinContarLasAtrasadasNiLasSinDatos()
    {
        var camaras = new[]
        {
            Camara(new DateTimeOffset(2026, 11, 17, 10, 17, 0, Planta), "AL_DIA"),
            Camara(new DateTimeOffset(2026, 11, 17, 10, 16, 0, Planta), "AL_DIA"),
            Camara(new DateTimeOffset(2026, 11, 17, 8, 0, 0, Planta), "ATRASADO"),
            Camara(null, "SIN_DATOS")
        };
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 10, 16, 0, Planta), EficienciaUi.DatosHastaGeneral(camaras));
        Assert.Null(EficienciaUi.DatosHastaGeneral([Camara(null, "SIN_DATOS")]));
    }

    [Fact]
    public void Camaras_UnaPorPunto_HoyPrimero()
    {
        var hoy = new EficienciaCamaraDiaDto { Camaras = [new() { PuntoControlCalidadId = 1, EstadoDatos = "AL_DIA" }] };
        var ayer = new EficienciaCamaraDiaDto
        {
            Camaras = [new() { PuntoControlCalidadId = 1, EstadoDatos = "viejo" }, new() { PuntoControlCalidadId = 2 }]
        };
        var r = EficienciaUi.Camaras(hoy, ayer);
        Assert.Equal([1, 2], r.Select(c => c.PuntoControlCalidadId));
        Assert.Equal("AL_DIA", r[0].EstadoDatos);
        Assert.Empty(EficienciaUi.Camaras(null, null));
    }
}

/// <summary>
/// La tarjeta "Eficiencia (cámara)" renderizada de verdad (HtmlRenderer de ASP.NET Core, sin navegador) con 1, 2 y 3
/// cámaras: cada una con su unidad y su "datos hasta", sin nada propio de un número de cámara.
/// </summary>
public class EficienciaCamaraRenderTests
{
    private static readonly TimeSpan Planta = TimeSpan.FromHours(-3);
    private static readonly DateTimeOffset Ahora = new(2026, 11, 17, 10, 18, 0, Planta);

    private static EficienciaCamaraPuntoDto Camara(int id, int numero, string metodo, string unidad, DateTimeOffset? hasta, string estado, int? atraso = null) => new()
    {
        PuntoControlCalidadId = id, Codigo = $"T2-CAM{numero:00}", Nombre = $"Cámara {numero:00}", LineaId = 1,
        LineaNombre = "Tango 2", NumeroCamara = numero, MetodoConteo = metodo, Unidad = unidad, UmbralTexto = "5 min",
        DatosHasta = hasta, EstadoDatos = estado, MinutosSinDatosNuevos = atraso,
        Turnos =
        [
            new()
            {
                TurnoId = 1, TurnoCodigo = "T1", TurnoNombre = "Turno 1 (Mañana)", Estado = "SIN_OBJETIVO", Momento = "EN_CURSO",
                Inicio = new DateTimeOffset(2026, 11, 17, 6, 0, 0, Planta), Fin = new DateTimeOffset(2026, 11, 17, 14, 0, 0, Planta),
                Contadas = hasta == null ? 0 : 1234
            }
        ]
    };

    private static EficienciaCamaraPuntoDto Cam09 => Camara(9, 9, "CARRILES", "rollos", Ahora.AddMinutes(-2), "AL_DIA", 2);
    private static EficienciaCamaraPuntoDto Cam10 => Camara(10, 10, "MANGAS", "mangas", Ahora.AddMinutes(-40), "ATRASADO", 40);
    private static EficienciaCamaraPuntoDto Cam08 => Camara(8, 8, "BOLSONES", "bolsones", null, "SIN_DATOS");

    private static async Task<string> RenderAsync(params EficienciaCamaraPuntoDto[] camaras)
    {
        var servicios = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(servicios, NullLoggerFactory.Instance);
        var hoy = new EficienciaCamaraDiaDto { Fecha = new DateOnly(2026, 11, 17), GeneradoEn = Ahora, MinutosAtraso = 5, Camaras = camaras.ToList() };
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var salida = await renderer.RenderComponentAsync<EficienciaCamara>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(EficienciaCamara.Hoy)] = hoy,
                [nameof(EficienciaCamara.Ayer)] = new EficienciaCamaraDiaDto { Fecha = new DateOnly(2026, 11, 16) },
                [nameof(EficienciaCamara.Cargando)] = false
            }));
            return System.Net.WebUtility.HtmlDecode(salida.ToHtmlString());
        });
    }

    private static int Tarjetas(string html) => html.Split("eficiencia-camara").Length - 1;

    [Fact]
    public async Task UnaCamara()
    {
        var html = await RenderAsync(Cam09);

        Assert.Equal(1, Tarjetas(html));
        Assert.Contains("Tango 2 · Cámara 09 (rollos)", html);
        Assert.Contains("Datos hasta las 10:16", html);
        Assert.DoesNotContain("Sin datos nuevos", html);
    }

    [Fact]
    public async Task DosCamaras_LaAtrasadaSeMarca()
    {
        var html = await RenderAsync(Cam09, Cam10);

        Assert.Equal(2, Tarjetas(html));
        Assert.Contains("Cámara 09 (rollos)", html);
        Assert.Contains("Cámara 10 (mangas)", html);
        Assert.Contains("Datos hasta las 09:38", html);
        Assert.Contains("Sin datos nuevos hace 40 min", html);
        Assert.Contains("border-danger", html);
    }

    [Fact]
    public async Task TresCamaras_ConLaDeBolsonesSinDatosTodavia()
    {
        var html = await RenderAsync(Cam08, Cam09, Cam10);

        Assert.Equal(3, Tarjetas(html));
        Assert.Contains("Tango 2 · Cámara 08 (bolsones)", html);
        Assert.Contains("Sin datos todavía", html);
        Assert.Contains("data-punto=\"T2-CAM08\"", html);
        // Orden de la API (no se reordena en la Web).
        Assert.True(html.IndexOf("T2-CAM08", StringComparison.Ordinal) < html.IndexOf("T2-CAM09", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SinCamaras_YSinDatos()
    {
        Assert.Contains("No hay cámaras que cuenten producción", await RenderAsync());

        var servicios = new ServiceCollection().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(servicios, NullLoggerFactory.Instance);
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
            System.Net.WebUtility.HtmlDecode((await renderer.RenderComponentAsync<EficienciaCamara>(ParameterView.Empty)).ToHtmlString()));
        Assert.Contains("No se pudo cargar la eficiencia", html);
    }
}
