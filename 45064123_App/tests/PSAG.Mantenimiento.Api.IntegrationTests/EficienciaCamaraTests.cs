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
/// Conteos por turno de la cámara en el Dashboard (ex eficiencia, reunión 03/11), con el turno de noche que cruza
/// medianoche y turno no programado. T-798 etapa 4: sin objetivo ni % (vuelven en la etapa 5 desde el parámetro
/// línea + producto); un turno sin minutos es "sin datos", no 0.
/// </summary>
public class EficienciaCamaraTests : CalidadTestBase
{
    private readonly ConteoCamaraService _conteo;
    private readonly DateOnly _fecha;          // día de planta de hace 2 días
    private readonly DateTimeOffset _inicioDia; // 00:00 de ese día, en hora de planta
    private readonly Turno _noche;

    public EficienciaCamaraTests()
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

    private async Task<EficienciaCamaraPuntoDto> EficienciaAsync()
    {
        var r = await _conteo.ObtenerEficienciaAsync(_fecha, Tango2.LineaId);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        return Assert.Single(r.Value!.Camaras);
    }

    [Fact]
    public async Task ContadasPorTurno_SinObjetivoNiPorcentaje()
    {
        await CargarDatosAsync();

        var camara = await EficienciaAsync();

        Assert.Equal("mangas", camara.Unidad);
        Assert.Equal(MagnitudProduccion.Mangas, camara.Magnitud);
        Assert.Equal(["T1", "T2", "T3"], camara.Turnos.Select(t => t.TurnoCodigo));
        var manana = camara.Turnos[0];
        Assert.Equal(20, manana.Contadas);
        Assert.False(manana.SinDatos);
        Assert.Equal(480, manana.MinutosProgramados);
        Assert.NotNull(manana.TurnoProduccionId);              // los minutos cuelgan del turno de producción
        Assert.Equal("TERMINADO", manana.Momento);
        Assert.Equal(new TimeSpan(6, 0, 0), manana.Inicio.TimeOfDay);
        Assert.Equal(TimeSpan.FromHours(-3), manana.Inicio.Offset);
        // El DTO ya no tiene objetivo ni %: un solo cálculo de eficiencia (etapa 5).
        Assert.Null(typeof(EficienciaTurnoDto).GetProperty("Objetivo"));
        Assert.Null(typeof(EficienciaTurnoDto).GetProperty("Porcentaje"));
    }

    [Fact]
    public async Task TurnoSinMinutos_EsSinDatos_NoCero()
    {
        await CargarDatosAsync();

        var tarde = (await EficienciaAsync()).Turnos.Single(t => t.TurnoCodigo == "T2");

        Assert.True(tarde.SinDatos);
        Assert.Equal(0, tarde.Contadas);
        Assert.Equal(0, tarde.MinutosConDatos);
    }

    [Fact]
    public async Task TurnoDeNoche_SumaAntesYDespuesDeMedianoche_YNoElDelDiaAnterior()
    {
        await CargarDatosAsync();

        var noche = (await EficienciaAsync()).Turnos.Single(t => t.TurnoCodigo == "T3");

        Assert.Equal(15, noche.Contadas);           // 7 + 8; las 100 de las 03:00 son del turno de noche anterior
        Assert.Equal(480, noche.MinutosProgramados);
        Assert.Equal(_fecha.AddDays(1), DateOnly.FromDateTime(noche.Fin.DateTime));
        Assert.Equal(new TimeSpan(6, 0, 0), noche.Fin.TimeOfDay);

        // El día anterior, el turno de noche sí tiene las 100 de las 03:00.
        var anterior = await _conteo.ObtenerEficienciaAsync(_fecha.AddDays(-1), Tango2.LineaId);
        Assert.Equal(100, anterior.Value!.Camaras.Single().Turnos.Single(t => t.TurnoCodigo == "T3").Contadas);
    }

    [Fact]
    public async Task ParadasYMicrocortes_PorTurno()
    {
        await CargarDatosAsync();

        var camara = await EficienciaAsync();
        var manana = camara.Turnos.Single(t => t.TurnoCodigo == "T1");
        var tarde = camara.Turnos.Single(t => t.TurnoCodigo == "T2");

        // Mañana: 1,5 + 6 + 2 = 9,5 min (redondea a 10); las tres paradas empezaron en el turno.
        Assert.Equal(10, manana.MinutosParada);
        Assert.Equal(2, manana.Microcortes);   // 90 s y la de 4 min que empezó 13:58
        Assert.Equal(1, manana.ParadasLargas);
        // Tarde: sólo los 2 minutos de la parada que venía de la mañana; no se cuenta como microcorte de la tarde.
        Assert.Equal(2, tarde.MinutosParada);
        Assert.Equal(0, tarde.Microcortes);
    }

    [Fact]
    public async Task Microcortes_UsanElUmbralEfectivoEnSegundos()
    {
        await PonerUmbralAsync(100);   // 90 s sigue siendo microcorte; 4 y 6 min son largas
        await CargarDatosAsync();

        var camara = await EficienciaAsync();
        var manana = camara.Turnos.Single(t => t.TurnoCodigo == "T1");

        Assert.Equal(1, manana.Microcortes);
        Assert.Equal(2, manana.ParadasLargas);
        Assert.Equal(100, camara.UmbralSegundos);
        Assert.Equal("100 s", camara.UmbralTexto);
    }

    /// <summary>T-784: cada parada queda marcada con el umbral del momento en que pasó (snapshot).</summary>
    [Fact]
    public async Task CambiarElUmbralDespues_NoCambiaLaMarcaDeLasParadasYaRegistradas()
    {
        await CargarDatosAsync();                                    // umbral 5 min
        await PonerUmbralAsync(30);                                  // modo prueba, después

        var manana = (await EficienciaAsync()).Turnos.Single(t => t.TurnoCodigo == "T1");
        var guardadas = await Context.ParadasCamara.AsNoTracking().OrderBy(p => p.FechaHoraInicio).ToListAsync();

        Assert.Equal(2, manana.Microcortes);                         // igual que con 5 min
        Assert.Equal(1, manana.ParadasLargas);
        Assert.All(guardadas, p => Assert.Equal(300, p.UmbralSegundos));
        Assert.Equal([false, true, false], guardadas.Select(p => p.SuperaUmbral));
    }

    [Fact]
    public async Task ParadasAnterioresAlScript32_SinMarca_UsanElUmbralActual()
    {
        await CargarDatosAsync();
        await Context.ParadasCamara.ExecuteUpdateAsync(u => u
            .SetProperty(p => p.UmbralSegundos, (int?)null)
            .SetProperty(p => p.SuperaUmbral, (bool?)null));
        await PonerUmbralAsync(100);

        var manana = (await EficienciaAsync()).Turnos.Single(t => t.TurnoCodigo == "T1");

        Assert.Equal(1, manana.Microcortes);                         // como antes de T-784: con el umbral de hoy
        Assert.Equal(2, manana.ParadasLargas);
    }

    [Fact]
    public async Task MinutosMicroparadas_SeparanLoQueNoLlegoAlUmbral()
    {
        await CargarDatosAsync();

        var camara = await EficienciaAsync();
        var manana = camara.Turnos.Single(t => t.TurnoCodigo == "T1");
        var tarde = camara.Turnos.Single(t => t.TurnoCodigo == "T2");

        Assert.Equal(10, manana.MinutosParada);
        Assert.Equal(4, manana.MinutosMicroparadas);                 // 1,5 + 2 (la de 4 min recortada al turno)
        Assert.Equal(2, tarde.MinutosMicroparadas);                  // los otros 2 min de la de 4 min
    }

    [Fact]
    public async Task Paradas_FiltroPorUmbral()
    {
        await CargarDatosAsync();
        var filtro = new ParadasCamaraFiltroRequest { FechaDesde = _fecha, FechaHasta = _fecha };

        filtro.SuperaUmbral = false;
        var micro = (await _conteo.ObtenerParadasAsync(filtro)).Value!.Items;
        filtro.SuperaUmbral = true;
        var grandes = (await _conteo.ObtenerParadasAsync(filtro)).Value!.Items;
        filtro.SuperaUmbral = null;
        var todas = (await _conteo.ObtenerParadasAsync(filtro)).Value!.Items;

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

    /// <summary>T-785: la cámara 8 cuenta bolsones; mangas (10) y bolsones (8) van por separado, cada una con su unidad.</summary>
    [Fact]
    public async Task MangasYBolsones_DosCamarasDeLaLineaCadaUnaConSuUnidad()
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

        var camaras = (await _conteo.ObtenerEficienciaAsync(_fecha, Tango2.LineaId)).Value!.Camaras;

        Assert.Equal(2, camaras.Count);
        var bolsones = camaras.Single(c => c.Codigo == "TEST-CAM-08");
        Assert.Equal("bolsones", bolsones.Unidad);
        Assert.Equal(3, bolsones.Turnos.Single(t => t.TurnoCodigo == "T1").Contadas);
        Assert.Equal("mangas", camaras.Single(c => c.Codigo == "TEST-CAM-T2").Unidad);
    }

    [Fact]
    public async Task SinUmbralCargado_Es5Minutos()
    {
        await CargarDatosAsync();

        var camara = await EficienciaAsync();

        Assert.Equal(300, camara.UmbralSegundos);
        Assert.Equal("5 min", camara.UmbralTexto);
    }

    [Fact]
    public async Task DiaConExcepcionDeCeroHoras_TurnoNoProgramado()
    {
        // La excepción existe antes del primer dato: el turno de producción se crea con 0 minutos programados (snapshot).
        Context.Add(new TiempoProgramadoDia
        {
            LineaId = Tango2.LineaId, Fecha = _fecha, Horas = 0, Origen = "EXCEPCION", Motivo = "Parada de planta",
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        });
        await Context.SaveChangesAsync();
        await CargarDatosAsync();

        var manana = (await EficienciaAsync()).Turnos[0];

        Assert.Equal(0, manana.MinutosProgramados);
        Assert.Equal(20, manana.Contadas);
    }

    [Fact]
    public async Task SoloCamarasDeConteoConDatosOHabilitadas()
    {
        await CargarDatosAsync();
        var futura = await Context.Camaras.SingleAsync(c => c.CamaraId == CamFutura.CamaraId);
        futura.Habilitada = false;   // sin datos y deshabilitada: no aparece
        Context.Add(new Camara
        {
            LineaId = Tango2.LineaId, Codigo = "TEST-CAM-CAL", Nombre = "Cámara de calidad", Funcion = FuncionCamara.Calidad,
            Habilitada = true, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        });
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var r = await _conteo.ObtenerEficienciaAsync(_fecha, null);

        Assert.Equal([CamTango.CamaraId], r.Value!.Camaras.Select(c => c.CamaraId));
    }

    [Fact]
    public async Task TurnoEnCurso_Momento()
    {
        var ahora = _inicioDia.AddHours(10); // 10:00: van 4 de las 8 h de la mañana

        var r = await _conteo.ObtenerEficienciaAsync(_fecha, Tango2.LineaId, ahora);
        var turnos = r.Value!.Camaras.Single().Turnos;

        Assert.Equal("EN_CURSO", turnos[0].Momento);
        Assert.Equal("SIN_EMPEZAR", turnos[1].Momento);
        Assert.All(turnos, t => Assert.True(t.SinDatos));
    }
}
