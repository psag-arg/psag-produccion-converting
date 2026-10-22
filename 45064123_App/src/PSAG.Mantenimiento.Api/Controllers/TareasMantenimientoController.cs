using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.TareasMantenimiento;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Common;
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
    public const string RolesCarga = RolesSistema.Operario + "," + RolesSistema.Supervisor + "," + RolesSistema.Administrador;

    /// <summary>Mismos roles que TareaMantenimientoReglas.RolesGestion.</summary>
    public const string RolesGestion = RolesSistema.Mantenimiento + "," + RolesSistema.Supervisor + "," + RolesSistema.Administrador;

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
        var nombres = await _userManager.NombresVisiblesAsync(
            lista.SelectMany(t => new[] { t.CreadoPorUsuarioId, t.ProgramadaPorUsuarioId, t.CerradaPorUsuarioId }));

        foreach (var tarea in lista)
        {
            tarea.CreadoPorUsuarioNombre = nombres.NombreDe(tarea.CreadoPorUsuarioId);
            tarea.ProgramadaPorUsuarioNombre = nombres.NombreDe(tarea.ProgramadaPorUsuarioId);
            tarea.CerradaPorUsuarioNombre = nombres.NombreDe(tarea.CerradaPorUsuarioId);
        }
    }

    private IActionResult ErrorResult(Result resultado) =>
        ProblemaDe(resultado, "Error de validación",
            tituloNoEncontrado: "Tarea no encontrada",
            tituloSinPermiso: "Sin permiso");
}
