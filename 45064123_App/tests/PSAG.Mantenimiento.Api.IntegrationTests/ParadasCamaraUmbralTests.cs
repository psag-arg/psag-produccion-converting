using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Application.DTOs.Camaras;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Paradas de la cámara y su marca de umbral (T-784) por la ingesta, más las dos cámaras de conteo de la línea (T-785).
/// T-798 etapa 5: la "eficiencia por cámara" (contadas, microcortes por turno) se reemplazó por los indicadores
/// (IndicadoresTests); acá queda lo de las paradas, consultado por api/conteo-camara/paradas.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class ParadasCamaraUmbralTests : CalidadTestBase
{
    private readonly ConteoCamaraService _conteo;
    private readonly DateOnly _fecha;          // día de planta de hace 2 días
    private readonly DateTimeOffset _inicioDia; // 00:00 de ese día, en hora de planta
    private readonly Turno _noche;

    public ParadasCamaraUmbralTests()
    {
        _conteo = new ConteoCamaraService(new UnitOfWork(Context), NuevoTurnosProduccion(new UnitOfWork(Context)), ZonaPlanta, NullLogger<ConteoCamaraService>.Instance);
        _fecha = DiaPlanta(2);
        _inicioDia = new DateTimeOffset(_fecha.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-3));

        // La base trae mañana 06-14 y tarde 14-22; se agrega la noche 22-06 (cruza medianoche).
        _noche = new Turno
        {
            Codigo = "T3", Nombre = "Turno 3 (Noche)", HoraInicio = new TimeOnly(22, 0), HoraFin = new TimeOnly(6, 0), Orden = 3,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        Context.Add(_noche);
        // En estos tests la cámara de la Tango cuenta mangas (como la 10).
        var camara = Context.Camaras.Single(c => c.CamaraId == CamTango.CamaraId);
        camara.Magnitud = MagnitudProduccion.Mangas;
        camara.MetodoDeteccion = MetodoDeteccionCamara.Pasos;
        camara.CantidadCarriles = 1;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    private static MinutoCamaraIngesta Minuto(DateTimeOffset m, decimal mangas) => new()
    {
        Minuto = m, Carril = 1, Rollos = mangas, SegundosMarcha = 60, Estado = "MARCHA"
    };

    private static ParadaCamaraIngesta Parada(DateTimeOffset inicio, int segundos) => new()
    {
        IdentificadorExterno = $"TEST-CAM-T2-{inicio.UtcDateTime:yyyyMMddTHHmmss}Z",
        Inicio = inicio, Fin = inicio.AddSeconds(segundos), Tipo = "PARADA", Carriles = [1]
    };

    private async Task CargarDatosAsync()
    {
        var r = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-T2",
            Minutos =
            [
                Minuto(_inicioDia.AddHours(3), 100),                  // 03:00: turno noche del día ANTERIOR
                Minuto(_inicioDia.AddHours(10), 10),                  // mañana
                Minuto(_inicioDia.AddHours(10).AddMinutes(1), 10),    // mañana
                Minuto(_inicioDia.AddHours(23), 7),                   // noche del día, antes de medianoche
                Minuto(_inicioDia.AddDays(1).AddHours(3), 8)          // noche del día, después de medianoche
            ],
            Paradas =
            [
                Parada(_inicioDia.AddHours(10).AddMinutes(30), 90),   // microcorte (< 5 min)
                Parada(_inicioDia.AddHours(11), 6 * 60),              // larga
                Parada(_inicioDia.AddHours(13).AddMinutes(58), 4 * 60) // cruza a la tarde: 2 min en cada turno
            ]
        });
        Assert.True(r.IsSuccess, r.ErrorMessage);
    }

    private async Task PonerUmbralAsync(int? segundos)
    {
        var camara = await Context.Camaras.SingleAsync(c => c.CamaraId == CamTango.CamaraId);
        camara.UmbralDetencionSegundos = segundos;
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    private async Task<List<ParadaCamaraDto>> ParadasAsync(bool? superaUmbral = null)
    {
        var r = await _conteo.ObtenerParadasAsync(new ParadasCamaraFiltroRequest { FechaDesde = _fecha, FechaHasta = _fecha, SuperaUmbral = superaUmbral });
        Assert.True(r.IsSuccess, r.ErrorMessage);
        return r.Value!.Items.ToList();
    }

    [Fact]
    public async Task Microcortes_UsanElUmbralEfectivoEnSegundos()
    {
        await PonerUmbralAsync(100);   // 90 s sigue siendo microcorte; 4 y 6 min son largas
        await CargarDatosAsync();

        Assert.Equal([90], (await ParadasAsync(superaUmbral: false)).Select(p => p.DuracionSegundos));
        Assert.Equal([240, 360], (await ParadasAsync(superaUmbral: true)).Select(p => p.DuracionSegundos).Order());
        Assert.All(await ParadasAsync(), p => Assert.Equal("100 s", p.UmbralTexto));
    }

    /// <summary>T-784: cada parada queda marcada con el umbral del momento en que pasó (snapshot).</summary>
    [Fact]
    public async Task CambiarElUmbralDespues_NoCambiaLaMarcaDeLasParadasYaRegistradas()
    {
        await CargarDatosAsync();                                    // umbral 5 min
        await PonerUmbralAsync(30);                                  // modo prueba, después

        var guardadas = await Context.ParadasCamara.AsNoTracking().OrderBy(p => p.FechaHoraInicio).ToListAsync();

        Assert.All(guardadas, p => Assert.Equal(300, p.UmbralSegundos));
        Assert.Equal([false, true, false], guardadas.Select(p => p.SuperaUmbral));
        Assert.Equal(2, (await ParadasAsync(superaUmbral: false)).Count);   // igual que con 5 min
    }

    [Fact]
    public async Task ParadasAnterioresAlScript32_SinMarca_UsanElUmbralActual()
    {
        await CargarDatosAsync();
        await Context.ParadasCamara.ExecuteUpdateAsync(u => u
            .SetProperty(p => p.UmbralSegundos, (int?)null)
            .SetProperty(p => p.SuperaUmbral, (bool?)null));
        await PonerUmbralAsync(100);

        Assert.Single(await ParadasAsync(superaUmbral: false));     // como antes de T-784: con el umbral de hoy
        Assert.Equal(2, (await ParadasAsync(superaUmbral: true)).Count);
    }

    [Fact]
    public async Task Paradas_FiltroPorUmbral()
    {
        await CargarDatosAsync();

        var micro = await ParadasAsync(superaUmbral: false);
        var grandes = await ParadasAsync(superaUmbral: true);
        var todas = await ParadasAsync();

        Assert.Equal([90, 240], micro.Select(p => p.DuracionSegundos).Order());
        Assert.All(micro, p => Assert.False(p.SuperaUmbral));
        Assert.Equal(360, Assert.Single(grandes).DuracionSegundos);
        Assert.Equal(3, todas.Count);
        Assert.All(todas, p => Assert.Equal("5 min", p.UmbralTexto));
    }

    [Fact]
    public async Task MicroparadaDeSegundos_SeRegistra()
    {
        var r = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-T2",
            Paradas = [Parada(_inicioDia.AddHours(9), 11)]               // 11 s: antes no llegaba nunca a la API
        });

        Assert.Equal(1, r.Value!.ParadasNuevas);
        var p = await Context.ParadasCamara.AsNoTracking().SingleAsync();
        Assert.Equal(11, p.DuracionSegundos);
        Assert.False(p.SuperaUmbral);
    }

    /// <summary>T-785: la cámara 8 cuenta bolsones; mangas (10) y bolsones (8) van por separado, cada una con su magnitud.</summary>
    [Fact]
    public async Task MangasYBolsones_DosCamarasDeLaLineaCadaUnaConSuMagnitud()
    {
        var alta = await Camaras.CrearAsync(new GuardarCamaraRequest
        {
            LineaId = Tango2.LineaId, Codigo = "test-cam-08", Nombre = "Cámara 8 bolsones", NumeroCanalGrabador = 8,
            Funcion = "conteo", Magnitud = "bolsones", MetodoDeteccion = "pasos", CantidadCarriles = 1, Habilitada = true
        }, Admin, [Domain.Common.RolesSistema.Administrador]);
        Assert.True(alta.IsSuccess, alta.ErrorMessage);
        Assert.Equal("TEST-CAM-08", alta.Value!.Codigo);
        Assert.Equal("BOLSONES", alta.Value.Magnitud);

        var r = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            Camara = "TEST-CAM-08",
            Minutos = [Minuto(_inicioDia.AddHours(10), 1), Minuto(_inicioDia.AddHours(10).AddMinutes(1), 2)]
        });
        Assert.True(r.IsSuccess, r.ErrorMessage);
        await CargarDatosAsync();

        var minutos = await Context.ConteosCamaraMinuto.AsNoTracking().Include(m => m.Camara).ToListAsync();
        Assert.Equal(3, minutos.Where(m => m.Camara.Codigo == "TEST-CAM-08").Sum(m => m.Cantidad));
        Assert.Equal(135, minutos.Where(m => m.Camara.Codigo == "TEST-CAM-T2").Sum(m => m.Cantidad));
    }
}
