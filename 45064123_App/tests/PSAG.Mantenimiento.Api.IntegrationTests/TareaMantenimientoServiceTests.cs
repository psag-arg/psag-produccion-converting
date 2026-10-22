using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Api.Controllers;
using PSAG.Mantenimiento.Application.DTOs.TareasMantenimiento;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Tareas de mantenimiento contra SQLite en memoria: permisos por rol, línea asignada y ciclo de estados.
/// </summary>
public class TareaMantenimientoServiceTests : IDisposable
{
    private const string Admin = "admin";
    private const string Operario = "operario";
    private const string Mantenimiento = "mant";
    private const string Supervisor = "sup";

    private static readonly string[] RolOperario = ["OPERARIO"];
    private static readonly string[] RolMantenimiento = ["MANTENIMIENTO"];
    private static readonly string[] RolSupervisor = ["SUPERVISOR"];
    private static readonly string[] RolGerencia = ["GERENCIA"];

    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;
    private readonly TareaMantenimientoService _service;
    private readonly Linea _lineaAsignada;
    private readonly Linea _otraLinea;
    private readonly Equipo _equipo;
    private readonly Equipo _equipoOtraLinea;

    public TareaMantenimientoServiceTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(_conexion).Options);
        _context.Database.EnsureCreated();

        _lineaAsignada = new Linea { Codigo = "T2", Nombre = "Tango 2", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _otraLinea = new Linea { Codigo = "F2", Nombre = "Futura 2", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _context.AddRange(_lineaAsignada, _otraLinea);
        _context.SaveChanges();

        _equipo = new Equipo { LineaId = _lineaAsignada.LineaId, Codigo = "GOF", Nombre = "Gofrador", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _equipoOtraLinea = new Equipo { LineaId = _otraLinea.LineaId, Codigo = "EMP", Nombre = "Empaquetadora", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _context.AddRange(_equipo, _equipoOtraLinea);
        _context.Add(new UsuarioLinea { UsuarioId = Operario, LineaId = _lineaAsignada.LineaId, AsignadoPorUsuarioId = Admin, FechaAsignacion = DateTimeOffset.UtcNow });
        _context.SaveChanges();

        _service = new TareaMantenimientoService(new UnitOfWork(_context), NullLogger<TareaMantenimientoService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _conexion.Dispose();
    }

    private CrearTareaMantenimientoRequest Pedido(string criticidad = "ALTA", int? lineaId = null, int? equipoId = null) => new()
    {
        LineaId = lineaId ?? _lineaAsignada.LineaId,
        EquipoId = equipoId ?? _equipo.EquipoId,
        ParteEquipo = "Rodillo superior",
        Detalle = "Cambiar rodamiento que hace ruido",
        TipoTarea = "MECANICA",
        Criticidad = criticidad
    };

    private async Task<long> CrearComoOperarioAsync(string criticidad = "ALTA")
    {
        var resultado = await _service.CrearAsync(Pedido(criticidad), Operario, RolOperario);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        return resultado.Value!.TareaMantenimientoId;
    }

    [Fact]
    public async Task Operario_CargaEnSuLinea_QuedaPendienteConUsuarioYFecha()
    {
        var resultado = await _service.CrearAsync(Pedido(), Operario, RolOperario);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        var dto = resultado.Value!;
        Assert.Equal("PENDIENTE", dto.Estado);
        Assert.Equal(Operario, dto.CreadoPorUsuarioId);
        Assert.NotEqual(default, dto.FechaHoraCreacion);
        Assert.Equal("Gofrador", dto.EquipoNombre);
        Assert.Equal("Tango 2", dto.LineaNombre);
    }

    [Fact]
    public async Task Operario_EnLineaNoAsignada_Prohibido()
    {
        var resultado = await _service.CrearAsync(
            Pedido(lineaId: _otraLinea.LineaId, equipoId: _equipoOtraLinea.EquipoId), Operario, RolOperario);

        Assert.Equal("FORBIDDEN", resultado.ErrorCode);
        Assert.Empty(_context.TareasMantenimiento);
    }

    [Fact]
    public async Task Supervisor_CargaEnCualquierLinea()
    {
        var resultado = await _service.CrearAsync(
            Pedido(lineaId: _otraLinea.LineaId, equipoId: _equipoOtraLinea.EquipoId), Supervisor, RolSupervisor);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
    }

    [Theory]
    [InlineData("GERENCIA")]
    [InlineData("MANTENIMIENTO")]
    public async Task RolesSinCarga_NoCargan(string rol)
    {
        var resultado = await _service.CrearAsync(Pedido(), "x", [rol]);

        Assert.Equal("FORBIDDEN", resultado.ErrorCode);
    }

    [Fact]
    public async Task Cuerpo_DeOtraLinea_SeRechaza()
    {
        var resultado = await _service.CrearAsync(Pedido(equipoId: _equipoOtraLinea.EquipoId), Supervisor, RolSupervisor);

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    [Theory]
    [InlineData("URGENTE")]
    [InlineData("")]
    public async Task Criticidad_Invalida_SeRechaza(string criticidad)
    {
        var resultado = await _service.CrearAsync(Pedido(criticidad), Operario, RolOperario);

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    [Fact]
    public async Task Operario_NoProgramaNiCierra()
    {
        var id = await CrearComoOperarioAsync();

        var programar = await _service.ProgramarAsync(new ProgramarTareasRequest { TareaMantenimientoIds = [id] }, Operario, RolOperario);
        var cerrar = await _service.CerrarAsync(id, Operario, RolOperario);

        Assert.Equal("FORBIDDEN", programar.ErrorCode);
        Assert.Equal("FORBIDDEN", cerrar.ErrorCode);
        Assert.Equal(EstadoTareaMantenimiento.PENDIENTE, (await _context.TareasMantenimiento.SingleAsync()).Estado);
    }

    [Fact]
    public async Task Gerencia_NoPrograma()
    {
        var id = await CrearComoOperarioAsync();

        var programar = await _service.ProgramarAsync(new ProgramarTareasRequest { TareaMantenimientoIds = [id] }, "ger", RolGerencia);

        Assert.Equal("FORBIDDEN", programar.ErrorCode);
    }

    [Fact]
    public async Task CicloCompleto_PendienteProgramadaCerrada_SaleDeActivasYQuedaEnHistorial()
    {
        var id = await CrearComoOperarioAsync();

        var programar = await _service.ProgramarAsync(new ProgramarTareasRequest { TareaMantenimientoIds = [id] }, Mantenimiento, RolMantenimiento);
        Assert.True(programar.IsSuccess, programar.ErrorMessage);
        Assert.Equal("PROGRAMADA", programar.Value!.Single().Estado);

        var cerrar = await _service.CerrarAsync(id, Supervisor, RolSupervisor);
        Assert.True(cerrar.IsSuccess, cerrar.ErrorMessage);
        Assert.Equal(Supervisor, cerrar.Value!.CerradaPorUsuarioId);
        Assert.NotNull(cerrar.Value.FechaHoraCierre);

        var activas = await _service.ObtenerActivasAsync(new TareasActivasFiltroRequest());
        Assert.Empty(activas.Value!);

        var historial = await _service.ObtenerHistorialAsync(new TareasHistorialFiltroRequest(), TimeZoneInfo.Utc);
        var cerrada = Assert.Single(historial.Value!.Items);
        Assert.Equal(id, cerrada.TareaMantenimientoId);
        Assert.Equal(Mantenimiento, cerrada.ProgramadaPorUsuarioId);
    }

    [Fact]
    public async Task Cerrar_Pendiente_NoSePermite()
    {
        var id = await CrearComoOperarioAsync();

        var cerrar = await _service.CerrarAsync(id, Mantenimiento, RolMantenimiento);

        Assert.Equal("VALIDATION", cerrar.ErrorCode);
    }

    [Fact]
    public async Task Programar_Varias_TodasONinguna()
    {
        var a = await CrearComoOperarioAsync();
        var b = await CrearComoOperarioAsync();
        await _service.ProgramarAsync(new ProgramarTareasRequest { TareaMantenimientoIds = [a] }, Mantenimiento, RolMantenimiento);
        var c = await CrearComoOperarioAsync();

        // a ya está programada: no se programa ninguna.
        var fallida = await _service.ProgramarAsync(new ProgramarTareasRequest { TareaMantenimientoIds = [a, b, c] }, Mantenimiento, RolMantenimiento);
        Assert.Equal("VALIDATION", fallida.ErrorCode);
        _context.ChangeTracker.Clear();
        Assert.Equal(2, await _context.TareasMantenimiento.CountAsync(t => t.Estado == EstadoTareaMantenimiento.PENDIENTE));

        var ok = await _service.ProgramarAsync(new ProgramarTareasRequest { TareaMantenimientoIds = [b, c] }, Supervisor, RolSupervisor);
        Assert.True(ok.IsSuccess, ok.ErrorMessage);
        Assert.Equal(2, ok.Value!.Count);
    }

    [Fact]
    public async Task Programar_IdInexistente_NoEncontrada()
    {
        var resultado = await _service.ProgramarAsync(new ProgramarTareasRequest { TareaMantenimientoIds = [999] }, Mantenimiento, RolMantenimiento);

        Assert.Equal("NOT_FOUND", resultado.ErrorCode);
    }

    [Fact]
    public async Task Activas_OrdenadasPorCriticidadYFiltradas()
    {
        var baja = await CrearComoOperarioAsync("BAJA");
        var alta = await CrearComoOperarioAsync("ALTA");
        var media = await CrearComoOperarioAsync("MEDIA");
        await _service.CrearAsync(Pedido("ALTA", _otraLinea.LineaId, _equipoOtraLinea.EquipoId), Supervisor, RolSupervisor);

        var porLinea = await _service.ObtenerActivasAsync(new TareasActivasFiltroRequest { LineaId = _lineaAsignada.LineaId });
        Assert.Equal(new[] { alta, media, baja }, porLinea.Value!.Select(t => t.TareaMantenimientoId));

        var soloAltas = await _service.ObtenerActivasAsync(new TareasActivasFiltroRequest { Criticidad = "ALTA" });
        Assert.Equal(2, soloAltas.Value!.Count);
        Assert.All(soloAltas.Value!, t => Assert.Equal("ALTA", t.Criticidad));
    }

    [Fact]
    public async Task Historial_FiltraPorCriticidadYFechaDeCierre()
    {
        var alta = await CrearComoOperarioAsync("ALTA");
        var baja = await CrearComoOperarioAsync("BAJA");
        await _service.ProgramarAsync(new ProgramarTareasRequest { TareaMantenimientoIds = [alta, baja] }, Mantenimiento, RolMantenimiento);
        await _service.CerrarAsync(alta, Mantenimiento, RolMantenimiento);
        await _service.CerrarAsync(baja, Mantenimiento, RolMantenimiento);

        var altas = await _service.ObtenerHistorialAsync(new TareasHistorialFiltroRequest { Criticidad = "ALTA" }, TimeZoneInfo.Utc);
        Assert.Equal(alta, Assert.Single(altas.Value!.Items).TareaMantenimientoId);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var deHoy = await _service.ObtenerHistorialAsync(new TareasHistorialFiltroRequest { FechaDesde = hoy, FechaHasta = hoy }, TimeZoneInfo.Utc);
        Assert.Equal(2, deHoy.Value!.TotalCount);

        var deAyer = await _service.ObtenerHistorialAsync(
            new TareasHistorialFiltroRequest { FechaDesde = hoy.AddDays(-1), FechaHasta = hoy.AddDays(-1) }, TimeZoneInfo.Utc);
        Assert.Equal(0, deAyer.Value!.TotalCount);
    }

    // La API también corta por rol antes de llegar al servicio: los atributos tienen que coincidir con las reglas de dominio.
    [Theory]
    [InlineData(nameof(TareasMantenimientoController.Crear), "Carga")]
    [InlineData(nameof(TareasMantenimientoController.Programar), "Gestion")]
    [InlineData(nameof(TareasMantenimientoController.Cerrar), "Gestion")]
    public void Controller_RolesDeCadaAccion_CoincidenConLasReglas(string accion, string tipo)
    {
        var atributo = typeof(TareasMantenimientoController).GetMethod(accion)!
            .GetCustomAttributes<AuthorizeAttribute>()
            .Single(a => a.Roles != null);
        var roles = atributo.Roles!.Split(',').OrderBy(r => r);
        var esperados = (tipo == "Carga" ? TareaMantenimientoReglas.RolesCarga : TareaMantenimientoReglas.RolesGestion).OrderBy(r => r);

        Assert.Equal(esperados, roles);
        if (tipo == "Gestion")
            Assert.DoesNotContain("OPERARIO", roles);
    }

    [Fact]
    public void Controller_NoTieneEndpointsDeBorrado()
    {
        var borrados = typeof(TareasMantenimientoController).GetMethods()
            .Where(m => m.GetCustomAttributes<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>().Any());

        Assert.Empty(borrados);
    }
}
