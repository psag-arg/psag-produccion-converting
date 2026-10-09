using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Analisis;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Identificación de fallas: análisis de cada detención, pendientes por rol y base de soluciones.
/// La lógica de permisos vive en el servicio; acá solo se completan los nombres de usuario.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AnalisisController : ApiControllerBase
{
    private readonly IAnalisisService _analisisService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AnalisisController(IAnalisisService analisisService, UserManager<ApplicationUser> userManager)
    {
        _analisisService = analisisService;
        _userManager = userManager;
    }

    private IReadOnlyCollection<string> Roles => User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    /// <summary>
    /// Obtener el análisis de una detención. Todos los usuarios pueden leerlo.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(AnalisisDetencionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAnalisis(long id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _analisisService.ObtenerAsync(id, CurrentUserId, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var dto = resultado.Value!;
        await CompletarNombresAsync(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Guardar solución, causa y plan como borrador. Lo puede hacer el responsable o un administrador.
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(AnalisisDetencionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GuardarBorrador(long id, [FromBody] GuardarAnalisisRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _analisisService.GuardarBorradorAsync(id, request, CurrentUserId, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var dto = resultado.Value!;
        await CompletarNombresAsync(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Cerrar el análisis. Exige solución y causa.
    /// </summary>
    [HttpPost("{id:long}/cerrar")]
    [ProducesResponseType(typeof(AnalisisDetencionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CerrarAnalisis(long id, [FromBody] GuardarAnalisisRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _analisisService.CerrarAsync(id, request, CurrentUserId, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var dto = resultado.Value!;
        await CompletarNombresAsync(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Bandeja "Mis pendientes" según los roles del usuario actual.
    /// El administrador puede filtrar por responsable.
    /// </summary>
    [HttpGet("pendientes")]
    [ProducesResponseType(typeof(MisPendientesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMisPendientes(
        [FromQuery] string? responsableRol = null,
        [FromQuery] string? responsableUsuarioId = null)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _analisisService.ObtenerMisPendientesAsync(
            CurrentUserId, Roles, responsableRol, responsableUsuarioId);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var dto = resultado.Value!;
        var usuarios = dto.ParaAnalizar.Concat(dto.EnAnalisis).Concat(dto.CerradasPorMi)
            .SelectMany(p => new[] { p.ResponsableUsuarioId, p.AnalisisPorUsuarioId })
            .Concat(dto.Responsables.Select(r => r.UsuarioId))
            .ToList();
        var nombres = await NombresAsync(usuarios);

        foreach (var pendiente in dto.ParaAnalizar.Concat(dto.EnAnalisis).Concat(dto.CerradasPorMi))
        {
            pendiente.ResponsableUsuarioNombre = NombreDe(nombres, pendiente.ResponsableUsuarioId);
            pendiente.AnalisisPorUsuarioNombre = NombreDe(nombres, pendiente.AnalisisPorUsuarioId);
            pendiente.ResponsableNombre = NombreDelResponsable(
                pendiente.ResponsableUsuarioId, pendiente.ResponsableUsuarioNombre, pendiente.ResponsableNombre);
        }

        foreach (var responsable in dto.Responsables.Where(r => r.UsuarioId != null))
        {
            responsable.Nombre = NombreDe(nombres, responsable.UsuarioId) ?? responsable.UsuarioId!;
        }

        return Ok(dto);
    }

    /// <summary>
    /// Cantidad de pendientes del usuario actual, para el contador del menú.
    /// </summary>
    [HttpGet("pendientes/conteo")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConteoPendientes()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var total = await _analisisService.ContarPendientesAsync(CurrentUserId, Roles);
        return Ok(new { total });
    }

    /// <summary>
    /// Buscar soluciones en las detenciones cerradas con análisis (máximo 50 resultados).
    /// </summary>
    [HttpGet("soluciones")]
    [ProducesResponseType(typeof(IEnumerable<SolucionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> BuscarSoluciones([FromQuery] BuscarSolucionesRequest filtro)
    {
        var resultado = await _analisisService.BuscarSolucionesAsync(filtro);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var items = resultado.Value!;
        var nombres = await NombresAsync(items.Select(s => s.AnalisisPorUsuarioId));
        foreach (var item in items)
        {
            item.AnalisisPorUsuarioNombre = NombreDe(nombres, item.AnalisisPorUsuarioId);
        }

        return Ok(items);
    }

    private async Task CompletarNombresAsync(AnalisisDetencionDto dto)
    {
        var ids = new List<string?>
        {
            dto.ResponsableUsuarioId,
            dto.AnalisisPorUsuarioId
        };
        ids.AddRange(dto.FallasParecidas.Select(f => f.AnalisisPorUsuarioId));

        var nombres = await NombresAsync(ids);
        dto.ResponsableUsuarioNombre = NombreDe(nombres, dto.ResponsableUsuarioId);
        dto.AnalisisPorUsuarioNombre = NombreDe(nombres, dto.AnalisisPorUsuarioId);
        dto.ResponsableNombre = NombreDelResponsable(dto.ResponsableUsuarioId, dto.ResponsableUsuarioNombre, dto.ResponsableNombre);

        foreach (var parecida in dto.FallasParecidas)
        {
            parecida.AnalisisPorUsuarioNombre = NombreDe(nombres, parecida.AnalisisPorUsuarioId);
        }
    }

    private async Task<Dictionary<string, string>> NombresAsync(IEnumerable<string?> usuarioIds)
    {
        var ids = usuarioIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<string, string>();

        var usuarios = await _userManager.Users
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.NombreCompleto, u.UserName })
            .ToListAsync();

        return usuarios.ToDictionary(
            u => u.Id,
            u => string.IsNullOrWhiteSpace(u.NombreCompleto) ? u.UserName ?? u.Id : u.NombreCompleto);
    }

    private static string? NombreDe(Dictionary<string, string> nombres, string? usuarioId) =>
        usuarioId != null && nombres.TryGetValue(usuarioId, out var nombre) ? nombre : null;

    /// <summary>
    /// Responsable por usuario: su nombre. Por rol: el nombre que ya armó el servicio.
    /// </summary>
    private static string? NombreDelResponsable(string? usuarioId, string? usuarioNombre, string? nombreRol) =>
        usuarioId != null ? usuarioNombre ?? "Usuario asignado" : nombreRol;

    private IActionResult ErrorResult(Result resultado)
    {
        var (status, titulo) = resultado.ErrorCode switch
        {
            "NOT_FOUND" => (StatusCodes.Status404NotFound, "Detención no encontrada"),
            "FORBIDDEN" => (StatusCodes.Status403Forbidden, "Sin permiso para este análisis"),
            "CONCURRENCY" => (StatusCodes.Status409Conflict, "Conflicto de concurrencia"),
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
