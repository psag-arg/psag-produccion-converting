using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Api.Controllers;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-610: ingesta del servicio de conteo por cámara (idempotente, todo o nada), cruce de las paradas con
/// detenciones y producción por hora. Reusa la base de calidad: la cámara es un punto de control.
/// </summary>
public class ConteoCamaraServiceTests : CalidadTestBase
{
    private readonly ConteoCamaraService _conteo;
    private readonly DateTimeOffset _dia;     // 10:00 de planta hace 2 días (turno mañana)

    public ConteoCamaraServiceTests()
    {
        _conteo = new ConteoCamaraService(new UnitOfWork(Context), ZonaPlanta, NullLogger<ConteoCamaraService>.Instance);
        var hoy = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaPlanta).Date;
        _dia = new DateTimeOffset(hoy.AddDays(-2).AddHours(10), TimeSpan.FromHours(-3));
    }

    private static MinutoCamaraIngesta Minuto(DateTimeOffset m, int carril, decimal rollos = 166.8m, int marcha = 60, int parada = 0) => new()
    {
        Minuto = m, Carril = carril, Rollos = rollos, SegundosMarcha = marcha, SegundosParada = parada,
        SegundosAcumulacion = 0, SegundosSinSenal = 60 - marcha - parada,
        Estado = marcha >= parada ? "MARCHA" : "PARADA"
    };

    private ParadaCamaraIngesta Parada(DateTimeOffset inicio, int segundos, string tipo = "PARADA") => new()
    {
        IdentificadorExterno = $"TEST-CAM-T2-{inicio.UtcDateTime:yyyyMMddTHHmmss}Z",
        Inicio = inicio,
        Fin = inicio.AddSeconds(segundos),
        Tipo = tipo,
        Carriles = [4, 1, 2, 3, 2]
    };

    private IngestaConteoCamaraRequest Lote(params object[] items) => new()
    {
        PuntoControl = "test-cam-t2",
        Minutos = items.OfType<MinutoCamaraIngesta>().ToList(),
        Paradas = items.OfType<ParadaCamaraIngesta>().ToList()
    };

    [Fact]
    public async Task Ingesta_GuardaConLineaYTurno_YUnReenvioNoDuplica()
    {
        var lote = Lote(Minuto(_dia, 1), Minuto(_dia, 2), Minuto(_dia.AddMinutes(1), 1, 80, marcha: 30, parada: 30), Parada(_dia.AddMinutes(1).AddSeconds(30), 90));

        var primero = await _conteo.IngestarAsync(lote);
        var segundo = await _conteo.IngestarAsync(lote);

        Assert.True(primero.IsSuccess, primero.ErrorMessage);
        Assert.Equal(3, primero.Value!.MinutosNuevos);
        Assert.Equal(1, primero.Value.ParadasNuevas);
        Assert.Equal(0, segundo.Value!.MinutosNuevos);
        Assert.Equal(3, segundo.Value.MinutosDuplicados);
        Assert.Equal(1, segundo.Value.ParadasDuplicadas);

        Context.ChangeTracker.Clear();
        var minutos = await Context.ConteosCamaraMinuto.ToListAsync();
        Assert.Equal(3, minutos.Count);
        Assert.All(minutos, m =>
        {
            Assert.Equal(Tango2.LineaId, m.LineaId);
            Assert.Equal(CamaraTango.PuntoControlCalidadId, m.PuntoControlCalidadId);
            Assert.Equal(TurnoManana.TurnoId, m.TurnoId);
        });
        var parada = await Context.ParadasCamara.SingleAsync();
        Assert.Equal("1,2,3,4", parada.Carriles);
        Assert.Equal(90, parada.DuracionSegundos);
        Assert.Equal(TipoParadaCamara.PARADA, parada.Tipo);
        Assert.Equal(TurnoManana.TurnoId, parada.TurnoId);
    }

    [Fact]
    public async Task Ingesta_PuntoInexistente_Falla()
    {
        var lote = Lote(Minuto(_dia, 1));
        lote.PuntoControl = "NO-EXISTE";

        var r = await _conteo.IngestarAsync(lote);

        Assert.True(r.IsFailure);
        Assert.Contains("Maestros", r.ErrorMessage);
    }

    [Fact]
    public async Task Ingesta_UnMinutoInvalido_NoGuardaNadaDelLote()
    {
        var malo = Minuto(_dia.AddMinutes(1), 2);
        malo.SegundosSinSenal = 5;   // suma 65

        var r = await _conteo.IngestarAsync(Lote(Minuto(_dia, 1), malo, Parada(_dia, 60)));

        Assert.True(r.IsFailure);
        Assert.Contains("sumar 60", r.ErrorMessage);
        Assert.Equal(0, await Context.ConteosCamaraMinuto.CountAsync());
        Assert.Equal(0, await Context.ParadasCamara.CountAsync());
    }

    [Theory]
    [InlineData("OTRO", 60)]
    [InlineData("PARADA", 0)]
    public async Task Ingesta_ParadaInvalida_Falla(string tipo, int segundos)
    {
        var r = await _conteo.IngestarAsync(Lote(Parada(_dia, segundos, tipo)));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task Ingesta_FechaFutura_Falla()
    {
        var futuro = AMinuto(DateTimeOffset.UtcNow.AddHours(2));
        var r = await _conteo.IngestarAsync(Lote(Minuto(futuro, 1)));
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task Paradas_SeCruzanConDetenciones()
    {
        // A: 10:00-10:05 sin nada. B: 11:00-11:10 con detención con horario 11:08-11:20. C: 12:00-12:02 con detención sin horario del mismo día y turno.
        // Otra línea (Futura) con horario a las 10:00: no cuenta para la Tango.
        await _conteo.IngestarAsync(Lote(Parada(_dia, 300), Parada(_dia.AddHours(1), 600), Parada(_dia.AddHours(2), 120)));
        var diaPlanta = DateOnly.FromDateTime(_dia.DateTime);
        Context.AddRange(
            NuevaDetencion("DET-T610-000001", Tango2.LineaId, diaPlanta, _dia.AddHours(1).AddMinutes(8), 12),
            NuevaDetencion("DET-T610-000002", Tango2.LineaId, diaPlanta, null, 5),
            NuevaDetencion("DET-T610-000003", Futura2.LineaId, diaPlanta, _dia, 5));
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var todas = await _conteo.ObtenerParadasAsync(new ParadasCamaraFiltroRequest { FechaDesde = diaPlanta, FechaHasta = diaPlanta });
        var sin = await _conteo.ObtenerParadasAsync(new ParadasCamaraFiltroRequest { FechaDesde = diaPlanta, FechaHasta = diaPlanta, SoloSinDetencion = true });

        Assert.True(todas.IsSuccess, todas.ErrorMessage);
        var items = todas.Value!.Items.OrderBy(i => i.FechaHoraInicio).ToList();
        Assert.Equal(3, items.Count);
        // Con detención con horario gana sobre la posible; la sin horario del día queda como POSIBLE para A y C.
        Assert.Equal("POSIBLE", items[0].Cruce);
        Assert.Equal("CON_DETENCION", items[1].Cruce);
        Assert.Equal(["DET-T610-000001"], items[1].CodigosDetencion);
        Assert.Equal("POSIBLE", items[2].Cruce);
        Assert.Equal(TimeSpan.FromHours(-3), items[0].FechaHoraInicio.Offset);

        // Sin la detención sin horario, A y C quedan sin detención.
        Assert.Equal(0, sin.Value!.Total);
        var detencionSinHora = await Context.Detenciones.SingleAsync(d => d.Codigo == "DET-T610-000002");
        detencionSinHora.Eliminada = true;
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var sin2 = await _conteo.ObtenerParadasAsync(new ParadasCamaraFiltroRequest { FechaDesde = diaPlanta, FechaHasta = diaPlanta, SoloSinDetencion = true });
        Assert.Equal(2, sin2.Value!.Total);
        Assert.Equal(7, sin2.Value.MinutosSinDetencion);
        Assert.All(sin2.Value.Items, i => Assert.Equal("SIN_DETENCION", i.Cruce));
    }

    [Fact]
    public async Task Paradas_RangoInvalido_Falla()
    {
        var r = await _conteo.ObtenerParadasAsync(new ParadasCamaraFiltroRequest { FechaDesde = new DateOnly(2026, 10, 10), FechaHasta = new DateOnly(2026, 10, 1) });
        Assert.True(r.IsFailure);
    }

    [Fact]
    public async Task Produccion_SumaPorHoraYCarril()
    {
        await _conteo.IngestarAsync(Lote(
            Minuto(_dia, 1, 160), Minuto(_dia, 2, 170), Minuto(_dia.AddMinutes(59), 1, 100, marcha: 40, parada: 20),
            Minuto(_dia.AddHours(1), 1, 150)));

        var r = await _conteo.ObtenerProduccionAsync(DateOnly.FromDateTime(_dia.DateTime), Tango2.LineaId);

        Assert.True(r.IsSuccess);
        Assert.Equal(580, r.Value!.TotalRollos);
        Assert.Equal([1, 2], r.Value.Carriles);
        Assert.Equal(2, r.Value.Horas.Count);
        Assert.Equal(260, r.Value.Horas[0].RollosPorCarril[1]);
        Assert.Equal(170, r.Value.Horas[0].RollosPorCarril[2]);
        Assert.Equal(_dia, r.Value.Horas[0].Hora);
        Assert.Equal(3, r.Value.Horas[0].MinutosCarrilMarcha);
    }

    [Fact]
    public void Ingesta_UsaLaClaveDeServicioYNoLaSesion()
    {
        var tipo = typeof(ConteoCamaraIngestaController);
        Assert.NotNull(tipo.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(tipo.GetCustomAttribute<ClaveIngestaCalidadAttribute>());
        Assert.NotNull(typeof(ConteoCamaraController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.DoesNotContain(typeof(ConteoCamaraController).GetMethods(), m => m.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>() != null);
    }

    private static DateTimeOffset AMinuto(DateTimeOffset t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Offset);

    private Detencion NuevaDetencion(string codigo, int lineaId, DateOnly fecha, DateTimeOffset? parada, int minutos) => new()
    {
        Codigo = codigo,
        LineaId = lineaId,
        TurnoId = TurnoManana.TurnoId,
        Fecha = fecha,
        FechaHoraParada = parada,
        OrigenTiempo = parada.HasValue ? OrigenTiempo.CALCULADO : OrigenTiempo.DECLARADO,
        MinutosDeclarados = minutos,
        MinutosEfectivos = minutos,
        Descripcion = "Detención de prueba",
        EstadoActual = EstadoDetencion.REGISTRADA,
        CreadoPorUsuarioId = Admin,
        ModificadoPorUsuarioId = Admin
    };
}
