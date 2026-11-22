using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Api.Controllers;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.DTOs.Dashboard;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-798 etapa 3 · Detenciones colgadas del turno de producción, contra SQLite en memoria. Turnos de planta T1 05–13,
/// T2 13–21, T3 21–05; planta UTC-3. Las fechas son de hace unos días (la API no acepta fechas futuras) y el reloj del
/// servicio de turnos se mueve a mano.
/// </summary>
public class DetencionesTurnoProduccionTests : IDisposable
{
    private const string Admin = "admin-t798";
    private const string Oper1 = "oper-1";
    private const string Oper2 = "oper-2";
    private static readonly string[] RolOperario = [RolesSistema.Operario];

    private static readonly TimeZoneInfo ZonaPlanta =
        TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");

    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;
    private readonly UnitOfWork _unitOfWork;
    private readonly TurnoProduccionService _turnos;
    private DateTimeOffset _reloj;

    private readonly Linea _tango2;
    private readonly Turno _t1;
    private readonly Turno _t2;
    private readonly Turno _t3;
    private readonly TipoFalla _mecanica;
    private readonly TipoFalla _programada;
    private readonly Camara _camara;

    /// <summary>Día base: hace 3 días en la planta (00:00).</summary>
    private readonly DateTime _dia;

    public DetencionesTurnoProduccionTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(_conexion).Options);
        _context.Database.EnsureCreated();

        _tango2 = new Linea { Codigo = "T2", Nombre = "Tango 2", Orden = 1, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _t1 = Turno("T1", "Turno 1", 5, 13, 1);
        _t2 = Turno("T2", "Turno 2", 13, 21, 2);
        _t3 = Turno("T3", "Turno 3", 21, 5, 3);
        _mecanica = new TipoFalla { Codigo = "MEC", Nombre = "Mecánica", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _programada = new TipoFalla { Codigo = "PROG", Nombre = "Parada programada", CuentaParaIndicadores = false, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _context.AddRange(_tango2, _t1, _t2, _t3, _mecanica, _programada);
        _context.SaveChanges();

        _camara = new Camara
        {
            LineaId = _tango2.LineaId, Codigo = "TEST-CAM-T798", Nombre = "Cámara de prueba", Funcion = FuncionCamara.Conteo,
            Magnitud = MagnitudProduccion.Mangas, MetodoDeteccion = MetodoDeteccionCamara.Pasos, CantidadCarriles = 1,
            DetectaParadas = true, CreaDetenciones = true, UmbralDetencionSegundos = 300, Habilitada = true,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        var asignada = DateTimeOffset.UtcNow.AddDays(-30);
        _context.AddRange(
            _camara,
            new OperadorLinea { UsuarioId = Oper1, LineaId = _tango2.LineaId, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin },
            new OperadorLinea { UsuarioId = Oper2, LineaId = _tango2.LineaId, FechaAsignacion = asignada, AsignadoPorUsuarioId = Admin });
        _context.SaveChanges();
        _context.ChangeTracker.Clear();

        _dia = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaPlanta).Date.AddDays(-3);
        _reloj = DateTimeOffset.UtcNow;
        _unitOfWork = new UnitOfWork(_context);
        _turnos = new TurnoProduccionService(_unitOfWork, ZonaPlanta, new TurnosProduccionOpciones(),
            NullLogger<TurnoProduccionService>.Instance, () => _reloj);
    }

    public void Dispose()
    {
        _context.Dispose();
        _conexion.Dispose();
    }

    private static Turno Turno(string codigo, string nombre, int desde, int hasta, int orden) => new()
    {
        Codigo = codigo, Nombre = nombre, HoraInicio = new TimeOnly(desde, 0), HoraFin = new TimeOnly(hasta, 0), Orden = orden,
        CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
    };

    /// <summary>Hora de pared de la planta, <paramref name="diasDespues"/> días después del día base.</summary>
    private DateTimeOffset H(int diasDespues, int hora, int minuto = 0) =>
        new(_dia.AddDays(diasDespues).AddHours(hora).AddMinutes(minuto), TimeSpan.FromHours(-3));

    private DateOnly D(int diasDespues) => DateOnly.FromDateTime(_dia.AddDays(diasDespues));

    private DetencionesController Controller(string usuarioId = Admin, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, usuarioId) };
        claims.AddRange((roles.Length == 0 ? [RolesSistema.Administrador] : roles).Select(r => new Claim(ClaimTypes.Role, r)));
        return new DetencionesController(_unitOfWork, new DetencionService(_unitOfWork, NullLogger<DetencionService>.Instance),
            null!, null!, NullLogger<DetencionesController>.Instance, null!, ZonaPlanta, _turnos)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };
    }

    /// <summary>Alta manual con hora desde y hasta (la fecha es la de la hora desde, como manda la Web).</summary>
    private async Task<long> CrearAsync(int dias, int horaDesde, int minutos, TipoFalla? tipo = null, string usuario = Admin, params string[] roles)
    {
        var desde = new TimeOnly(horaDesde, 0);
        var r = await Controller(usuario, roles).CreateDetencion(new CrearDetencionRequest
        {
            LineaId = _tango2.LineaId,
            TipoFallaId = tipo?.TipoFallaId,
            Fecha = D(dias),
            HoraInicio = desde,
            HoraFin = desde.AddMinutes(minutos),
            Descripcion = "Parada de prueba"
        });
        var creada = Assert.IsType<CreatedAtActionResult>(r);
        _context.ChangeTracker.Clear();
        return Assert.IsType<CrearDetencionResponse>(creada.Value).DetencionId;
    }

    private async Task<Detencion> DetencionAsync(long id)
    {
        _context.ChangeTracker.Clear();
        return await _context.Detenciones.Include(d => d.TurnoProduccion).Include(d => d.HistorialEstados)
            .SingleAsync(d => d.DetencionId == id);
    }

    // ===================== Sin turno abierto =====================

    [Fact]
    public async Task DetencionSinTurnoAbierto_CreaElTurnoPorSistemaYLaCuelgaAhi()
    {
        Assert.Empty(await _context.TurnosProduccion.ToListAsync());

        var id = await CrearAsync(0, 10, 15);

        var d = await DetencionAsync(id);
        var tp = Assert.Single(await _context.TurnosProduccion.ToListAsync());
        Assert.Equal(tp.TurnoProduccionId, d.TurnoProduccionId);
        Assert.Equal(OrigenAperturaTurno.SISTEMA, tp.OrigenApertura);
        Assert.Equal(_t1.TurnoId, tp.TurnoId);
        Assert.Equal(D(0), tp.FechaProduccion);
        Assert.Equal(EstadoTurnoProduccion.CERRADO_AUTOMATICO, tp.Estado);   // el turno ya terminó: no se pierde igual
        Assert.Equal(15, d.MinutosEfectivos);
    }

    [Fact]
    public async Task DetencionManualSinOperadorDeTurno_QuedaQuienCargaSiEstaHabilitado()
    {
        var id = await CrearAsync(0, 10, 15, usuario: Oper1, roles: RolOperario);

        Assert.Equal(Oper1, (await DetencionAsync(id)).OperadorUsuarioId);
    }

    // ===================== T3 que cruza medianoche + acumulado =====================

    [Fact]
    public async Task DetencionALas02DelT3_MismoTurnoQueLaDeLas22_YElAcumuladoLasSuma()
    {
        _reloj = H(1, 3);   // 03:00 del día siguiente: el T3 del día base sigue en curso
        var a22 = await CrearAsync(0, 22, 30);
        var a02 = await CrearAsync(1, 2, 20);

        var d22 = await DetencionAsync(a22);
        var d02 = await DetencionAsync(a02);
        Assert.Equal(d22.TurnoProduccionId, d02.TurnoProduccionId);
        Assert.Equal(_t3.TurnoId, d02.TurnoProduccion.TurnoId);
        Assert.Equal(D(0), d02.TurnoProduccion.FechaProduccion);   // del día de las 21:00, no del de las 02:00
        Assert.Equal(EstadoTurnoProduccion.ABIERTO, d02.TurnoProduccion.Estado);

        var resumen = Assert.IsType<ResumenTurnoResponse>(Assert.IsType<OkObjectResult>(
            await Controller().GetResumenTurno(_tango2.LineaId)).Value);
        Assert.Equal(_t3.Nombre, resumen.TurnoNombre);
        Assert.Equal(D(0), resumen.Fecha);
        Assert.Equal(50, resumen.TotalMinutos);
        Assert.Equal([d02.TurnoProduccionId], resumen.TurnoProduccionIds);

        // El listado muestra la fecha de producción del turno.
        var lista = Assert.IsType<PagedResult<DetencionDto>>(Assert.IsType<OkObjectResult>(
            await Controller().GetDetenciones(new DetencionFiltroRequest { LineaId = _tango2.LineaId })).Value);
        Assert.All(lista.Items, i => Assert.Equal(D(0), i.Fecha));
        Assert.All(lista.Items, i => Assert.Equal(_t3.TurnoId, i.TurnoId));
    }

    // ===================== Clasificación (CuentaParaIndicadores) =====================

    [Fact]
    public async Task TipoQueNoCuenta_NoSumaEnIndisponibilidadNiEnElTurno_PeroSeLista()
    {
        _reloj = H(0, 11);   // T1 del día base en curso
        var mecanica = await CrearAsync(0, 6, 20, _mecanica);
        var programada = await CrearAsync(0, 8, 45, _programada);
        var sinTipo = await CrearAsync(0, 9, 5);

        var resumen = Assert.IsType<ResumenTurnoResponse>(Assert.IsType<OkObjectResult>(
            await Controller().GetResumenTurno(_tango2.LineaId)).Value);
        Assert.Equal(25, resumen.TotalMinutos);   // 20 + 5 (sin tipo cuenta); la programada no
        var item = Assert.Single(resumen.Items, i => i.TipoFallaId == _programada.TipoFallaId);
        Assert.Equal(45, item.TotalMinutos);
        Assert.False(item.CuentaParaIndicadores);

        var lista = Assert.IsType<PagedResult<DetencionDto>>(Assert.IsType<OkObjectResult>(
            await Controller().GetDetenciones(new DetencionFiltroRequest { LineaId = _tango2.LineaId })).Value);
        Assert.Equal(3, lista.TotalCount);
        Assert.Contains(lista.Items, i => i.DetencionId == programada);

        var dashboard = new DashboardService(_unitOfWork, ZonaPlanta);
        var indisponibilidad = await dashboard.ObtenerIndisponibilidadAsync(new ReporteFiltroRequest
        {
            LineaId = _tango2.LineaId, Anio = D(0).Year, MesDesde = D(0).Month, MesHasta = D(0).Month
        });
        Assert.True(indisponibilidad.IsSuccess);
        var mes = Assert.Single(indisponibilidad.Value!.Meses, m => m.Mes == D(0).Month);
        Assert.Equal(25, mes.MinutosTotalesParada);
        Assert.DoesNotContain(mes.PorTipoFalla, t => t.TipoFallaId == _programada.TipoFallaId);

        var resumenDashboard = await dashboard.ObtenerResumenAsync(new DashboardFiltroRequest
        {
            LineaId = _tango2.LineaId, FechaDesde = D(0), FechaHasta = D(0)
        }, null);
        Assert.Equal(3, resumenDashboard.Value!.TotalDetenciones);
        Assert.Equal(25, resumenDashboard.Value.MinutosTotalesParada);
        Assert.NotEqual(mecanica, sinTipo);
    }

    // ===================== Edición que mueve la hora a otro turno =====================

    [Fact]
    public async Task EdicionQueMueveLaHoraAOtroTurno_CambiaElTurnoDeProduccionYQuedaEnElHistorial()
    {
        var id = await CrearAsync(0, 10, 15);
        var antes = await DetencionAsync(id);
        Assert.Equal(_t1.TurnoId, antes.TurnoProduccion.TurnoId);
        var turnoAntes = antes.TurnoProduccionId;
        _context.ChangeTracker.Clear();

        var r = await Controller().UpdateDetencion(id, new ActualizarDetencionRequest
        {
            Fecha = D(0), HoraInicio = new TimeOnly(14, 0), HoraFin = new TimeOnly(14, 15),
            Descripcion = "Parada de prueba", RowVersion = antes.RowVersion
        });

        Assert.IsType<OkObjectResult>(r);
        var despues = await DetencionAsync(id);
        Assert.NotEqual(turnoAntes, despues.TurnoProduccionId);
        Assert.Equal(_t2.TurnoId, despues.TurnoProduccion.TurnoId);
        Assert.Equal(D(0), despues.TurnoProduccion.FechaProduccion);
        Assert.Contains(despues.HistorialEstados, h => h.Observacion != null && h.Observacion.StartsWith("Cambio de turno"));
    }

    [Fact]
    public async Task EdicionDentroDelMismoTurno_NoCambiaElTurno()
    {
        var id = await CrearAsync(0, 10, 15);
        var antes = await DetencionAsync(id);
        var turnoAntes = antes.TurnoProduccionId;
        _context.ChangeTracker.Clear();

        await Controller().UpdateDetencion(id, new ActualizarDetencionRequest
        {
            Fecha = D(0), HoraInicio = new TimeOnly(11, 0), HoraFin = new TimeOnly(11, 15),
            Descripcion = "Parada de prueba", RowVersion = antes.RowVersion
        });

        var despues = await DetencionAsync(id);
        Assert.Equal(turnoAntes, despues.TurnoProduccionId);
        Assert.DoesNotContain(despues.HistorialEstados, h => h.Observacion != null && h.Observacion.StartsWith("Cambio de turno"));
    }

    // ===================== Detención automática de cámara: operador del turno =====================

    [Fact]
    public async Task DetencionDeCamara_TomaElOperadorVigenteDelTurnoYElDelRelevoDespuesDelRelevo()
    {
        // Oper1 abre el T1 a las 05:10; Oper2 lo releva a las 09:00.
        _reloj = H(0, 5, 10);
        var apertura = await _turnos.AbrirAsync(_tango2.LineaId, Oper1, OrigenAperturaTurno.LOGIN, null, false, RolOperario, [_tango2.LineaId]);
        Assert.True(apertura.IsSuccess, apertura.ErrorMessage);
        _context.ChangeTracker.Clear();
        _reloj = H(0, 9);
        var relevo = await _turnos.RelevarAsync(apertura.Value!.Turno!.TurnoProduccionId, Oper2, RolOperario, [_tango2.LineaId]);
        Assert.True(relevo.IsSuccess, relevo.ErrorMessage);
        _context.ChangeTracker.Clear();

        _reloj = H(0, 11);
        var conteo = new ConteoCamaraService(_unitOfWork, _turnos, ZonaPlanta, NullLogger<ConteoCamaraService>.Instance);
        ParadaEnCursoIngesta EnCurso(DateTimeOffset inicio) => new()
        {
            IdentificadorExterno = $"{_camara.Codigo}-{inicio.UtcDateTime:yyyyMMddTHHmmss}Z", Inicio = inicio, Hasta = inicio.AddMinutes(6), Tipo = "SIN_MANGAS"
        };
        var r = await conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            Camara = _camara.Codigo,
            ParadasEnCurso = [EnCurso(H(0, 8)), EnCurso(H(0, 10))]
        });
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(2, r.Value!.DetencionesCreadas);

        _context.ChangeTracker.Clear();
        var detenciones = await _context.Detenciones.Include(d => d.TurnoProduccion).ToListAsync();
        var antes = detenciones.Single(d => d.FechaHoraParada == H(0, 8));
        var despues = detenciones.Single(d => d.FechaHoraParada == H(0, 10));
        Assert.Equal(Oper1, antes.OperadorUsuarioId);
        Assert.Equal(Oper2, despues.OperadorUsuarioId);
        Assert.Equal(apertura.Value.Turno.TurnoProduccionId, antes.TurnoProduccionId);
        Assert.Equal(apertura.Value.Turno.TurnoProduccionId, despues.TurnoProduccionId);
        Assert.Single(await _context.TurnosProduccion.ToListAsync());   // usa el turno abierto, no crea otro
    }
}

/// <summary>El script 35 de SQL Server deja DET_Detenciones, CPR_Registros y CAL_Registros como el modelo de EF.</summary>
public class DetencionesTurnoScriptTests
{
    [Fact]
    public void Script35_ColumnasFkEIndicesDelModelo_YRollback()
    {
        using var conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        using var context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(conexion).Options);
        var script = ScriptSql.Texto("35_t798_detenciones_turno.sql");
        var rollback = ScriptSql.Texto("35_t798_detenciones_turno_rollback.sql");
        var modelo = context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;

        var detencion = modelo.FindEntityType(typeof(Detencion))!;
        var columnas = detencion.GetProperties().Select(p => p.GetColumnName()).ToList();
        Assert.Contains("TurnoProduccionId", columnas);
        Assert.Contains("OperadorUsuarioId", columnas);
        Assert.DoesNotContain("TurnoId", columnas);
        Assert.DoesNotContain("Fecha", columnas);
        Assert.DoesNotContain("UsuarioLineaId", columnas);

        foreach (var tipo in new[] { typeof(Detencion), typeof(RegistroProceso), typeof(RegistroCalidad) })
        {
            var entidad = modelo.FindEntityType(tipo)!;
            var fk = entidad.GetForeignKeys().Single(f => f.Properties.Any(p => p.Name == "TurnoProduccionId"));
            Assert.Contains(fk.GetConstraintName()!, script);
            Assert.Contains(fk.GetConstraintName()!, rollback);
            foreach (var indice in entidad.GetIndexes().Where(i => i.Properties.Any(p => p.Name is "TurnoProduccionId" or "OperadorUsuarioId")))
            {
                Assert.Contains(indice.GetDatabaseName()!, script);
                Assert.Contains(indice.GetDatabaseName()!, rollback);
            }
        }

        Assert.Contains("ALTER COLUMN [TurnoProduccionId] BIGINT NOT NULL", script);
        Assert.Contains("DROP COLUMN [TurnoId]", script);
        Assert.Contains("DROP COLUMN [Fecha]", script);
        Assert.Contains("DROP COLUMN [UsuarioLineaId]", script);
        Assert.Contains("N''SISTEMA''", script);
        Assert.Contains("DET_DetencionesRespaldoT798", rollback);
        Assert.Contains("ADD [TurnoId] INT NULL", rollback);
    }
}
