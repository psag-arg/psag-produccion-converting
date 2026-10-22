using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.TareasMantenimiento;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Tareas de mantenimiento relevadas en la máquina (T-546/T-547).
/// Cargan operario, supervisión y admin; programan y cierran mantenimiento, supervisión y admin.
/// Los permisos se validan en el servicio; los atributos de rol cortan antes. No hay DELETE: las cerradas quedan en el historial.
/// </summary>
[Route("api/tareas-mantenimiento")]
[ApiController]
[Authorize]
public class TareasMantenimientoController : ApiControllerBase
{
    /// <summary>Mismos roles que TareaMantenimientoReglas.RolesCarga.</summary>
    public const string RolesCarga = "OPERARIO,SUPERVISOR,ADMINISTRADOR";

    /// <summary>Mismos roles que TareaMantenimientoReglas.RolesGestion.</summary>
    public const string RolesGestion = "MANTENIMIENTO,SUPERVISOR,ADMINISTRADOR";

    private readonly ITareaMantenimientoService _tareaService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeZoneInfo _zonaPlanta;

    public TareasMantenimientoController(
        ITareaMantenimientoService tareaService,
        UserManager<ApplicationUser> userManager,
        TimeZoneInfo zonaPlanta)
    {
        _tareaService = tareaService;
        _userManager = userManager;
        _zonaPlanta = zonaPlanta;
    }

    /// <summary>
    /// Grilla activa: pendientes y programadas, de más a menos crítica. Todos los usuarios pueden verla.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<TareaMantenimientoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivas([FromQuery] TareasActivasFiltroRequest filtro)
    {
        var resultado = await _tareaService.ObtenerActivasAsync(filtro);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var items = resultado.Value!;
        await CompletarNombresAsync(items);
        return Ok(items);
    }

    /// <summary>
    /// Historial de tareas cerradas, paginado y con filtros.
    /// </summary>
    [HttpGet("historial")]
    [ProducesResponseType(typeof(PagedResult<TareaMantenimientoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistorial([FromQuery] TareasHistorialFiltroRequest filtro)
    {
        var resultado = await _tareaService.ObtenerHistorialAsync(filtro, _zonaPlanta);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var pagina = resultado.Value!;
        await CompletarNombresAsync(pagina.Items);
        return Ok(pagina);
    }

    /// <summary>
    /// Informar una tarea nueva. Queda PENDIENTE.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = RolesCarga)]
    [ProducesResponseType(typeof(TareaMantenimientoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Crear([FromBody] CrearTareaMantenimientoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _tareaService.CrearAsync(request, CurrentUserId, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var dto = resultado.Value!;
        await CompletarNombresAsync([dto]);
        return Ok(dto);
    }

    /// <summary>
    /// Programar una o varias tareas pendientes (todas o ninguna).
    /// </summary>
    [HttpPost("programar")]
    [Authorize(Roles = RolesGestion)]
    [ProducesResponseType(typeof(IEnumerable<TareaMantenimientoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Programar([FromBody] ProgramarTareasRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _tareaService.ProgramarAsync(request, CurrentUserId, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var items = resultado.Value!;
        await CompletarNombresAsync(items);
        return Ok(items);
    }

    /// <summary>
    /// Cerrar una tarea programada. Guarda usuario y fecha de cierre y la pasa al historial.
    /// </summary>
    [HttpPost("{id:long}/cerrar")]
    [Authorize(Roles = RolesGestion)]
    [ProducesResponseType(typeof(TareaMantenimientoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cerrar(long id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _tareaService.CerrarAsync(id, CurrentUserId, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var dto = resultado.Value!;
        await CompletarNombresAsync([dto]);
        return Ok(dto);
    }

    private async Task CompletarNombresAsync(IEnumerable<TareaMantenimientoDto> tareas)
    {
        var lista = tareas.ToList();
        var ids = lista
            .SelectMany(t => new[] { t.CreadoPorUsuarioId, t.ProgramadaPorUsuarioId, t.CerradaPorUsuarioId })
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return;

        var nombres = (await _userManager.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.NombreCompleto, u.UserName })
                .ToListAsync())
            .ToDictionary(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(u.NombreCompleto) ? u.UserName ?? u.Id : u.NombreCompleto);

        string? NombreDe(string? id) => id != null && nombres.TryGetValue(id, out var nombre) ? nombre : null;

        foreach (var tarea in lista)
        {
            tarea.CreadoPorUsuarioNombre = NombreDe(tarea.CreadoPorUsuarioId);
            tarea.ProgramadaPorUsuarioNombre = NombreDe(tarea.ProgramadaPorUsuarioId);
            tarea.CerradaPorUsuarioNombre = NombreDe(tarea.CerradaPorUsuarioId);
        }
    }

    private IActionResult ErrorResult(Result resultado)
    {
        var (status, titulo) = resultado.ErrorCode switch
        {
            "NOT_FOUND" => (StatusCodes.Status404NotFound, "Tarea no encontrada"),
            "FORBIDDEN" => (StatusCodes.Status403Forbidden, "Sin permiso"),
            _ => (StatusCodes.Status400BadRequest, "Error de validación")
        };

        return StatusCode(status, new ProblemDetails
        {
            Title = titulo,
            Detail = resultado.ErrorMessage,
            Status = status,
            Instance = HttpContext.Request.Path
        });
    }
}
