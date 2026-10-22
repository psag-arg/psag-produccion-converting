using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Analisis;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
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
    /// <summary>
    /// Límite del pedido de subida: una foto de hasta 10 MB más el sobre del multipart.
    /// </summary>
    private const long LimitePedidoAdjunto = AdjuntoImagenReglas.TamanoMaximoBytes + 1024 * 1024;

    private readonly IAnalisisService _analisisService;
    private readonly IAnalisisEdicionService _edicionService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AnalisisController(
        IAnalisisService analisisService,
        IAnalisisEdicionService edicionService,
        UserManager<ApplicationUser> userManager)
    {
        _analisisService = analisisService;
        _edicionService = edicionService;
        _userManager = userManager;
    }

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
    /// Editar solución, causa y plan de un análisis cerrado (desde la Base de soluciones).
    /// Solo administración y supervisión; cada campo cambiado queda en el historial con el valor anterior.
    /// </summary>
    [HttpPut("{id:long}/cerrado")]
    [ProducesResponseType(typeof(AnalisisDetencionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> EditarCerrado(long id, [FromBody] GuardarAnalisisRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _edicionService.EditarCerradoAsync(id, request, CurrentUserId, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var dto = resultado.Value!;
        await CompletarNombresAsync(dto);
        return Ok(dto);
    }

    /// <summary>
    /// Adjuntar una foto (JPG o PNG, hasta 10 MB, hasta 5 por análisis). Campo del formulario: "archivo".
    /// En borrador: el responsable o un administrador. Cerrado: administración y supervisión.
    /// </summary>
    [HttpPost("{id:long}/adjuntos")]
    [RequestSizeLimit(LimitePedidoAdjunto)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimitePedidoAdjunto)]
    [ProducesResponseType(typeof(AdjuntoAnalisisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AgregarAdjunto(long id, IFormFile? archivo)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        if (archivo == null)
        {
            return ErrorResult(Result.Failure("VALIDATION", "Elegí una foto para adjuntar."));
        }

        await using var contenido = archivo.OpenReadStream();
        var resultado = await _edicionService.AgregarAdjuntoAsync(
            id, archivo.FileName, archivo.Length, contenido, CurrentUserId, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var dto = resultado.Value!;
        dto.UsuarioNombre = (await _userManager.NombresVisiblesAsync([dto.UsuarioId])).NombreDe(dto.UsuarioId);
        return Ok(dto);
    }

    /// <summary>
    /// Bajar una foto del análisis. Requiere sesión; los archivos nunca se sirven como estáticos.
    /// </summary>
    [HttpGet("{id:long}/adjuntos/{adjuntoId:long}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAdjunto(long id, long adjuntoId)
    {
        var resultado = await _edicionService.AbrirAdjuntoAsync(id, adjuntoId);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var archivo = resultado.Value!;
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, max-age=3600";
        return File(archivo.Contenido, archivo.ContentType);
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
        var nombres = await _userManager.NombresVisiblesAsync(usuarios);

        foreach (var pendiente in dto.ParaAnalizar.Concat(dto.EnAnalisis).Concat(dto.CerradasPorMi))
        {
            pendiente.ResponsableUsuarioNombre = nombres.NombreDe(pendiente.ResponsableUsuarioId);
            pendiente.AnalisisPorUsuarioNombre = nombres.NombreDe(pendiente.AnalisisPorUsuarioId);
            pendiente.ResponsableNombre = NombreDelResponsable(
                pendiente.ResponsableUsuarioId, pendiente.ResponsableUsuarioNombre, pendiente.ResponsableNombre);
        }

        foreach (var responsable in dto.Responsables.Where(r => r.UsuarioId != null))
        {
            responsable.Nombre = nombres.NombreDe(responsable.UsuarioId) ?? responsable.UsuarioId!;
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
    /// Buscar soluciones en las detenciones cerradas con análisis (máximo 50 fichas; Total cuenta todas).
    /// </summary>
    [HttpGet("soluciones")]
    [ProducesResponseType(typeof(SolucionesResultadoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> BuscarSoluciones([FromQuery] BuscarSolucionesRequest filtro)
    {
        var resultado = await _analisisService.BuscarSolucionesAsync(filtro, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var items = resultado.Value!.Items;
        var nombres = await _userManager.NombresVisiblesAsync(items.Select(s => s.AnalisisPorUsuarioId));
        foreach (var item in items)
        {
            item.AnalisisPorUsuarioNombre = nombres.NombreDe(item.AnalisisPorUsuarioId);
        }

        return Ok(resultado.Value);
    }

    private async Task CompletarNombresAsync(AnalisisDetencionDto dto)
    {
        var ids = new List<string?>
        {
            dto.ResponsableUsuarioId,
            dto.AnalisisPorUsuarioId
        };
        ids.AddRange(dto.FallasParecidas.Select(f => f.AnalisisPorUsuarioId));
        ids.AddRange(dto.Cambios.Select(c => c.UsuarioId));
        ids.AddRange(dto.Adjuntos.Select(a => a.UsuarioId));

        var nombres = await _userManager.NombresVisiblesAsync(ids);
        dto.ResponsableUsuarioNombre = nombres.NombreDe(dto.ResponsableUsuarioId);
        dto.AnalisisPorUsuarioNombre = nombres.NombreDe(dto.AnalisisPorUsuarioId);
        dto.ResponsableNombre = NombreDelResponsable(dto.ResponsableUsuarioId, dto.ResponsableUsuarioNombre, dto.ResponsableNombre);

        foreach (var parecida in dto.FallasParecidas)
        {
            parecida.AnalisisPorUsuarioNombre = nombres.NombreDe(parecida.AnalisisPorUsuarioId);
        }

        foreach (var cambio in dto.Cambios)
        {
            cambio.UsuarioNombre = nombres.NombreDe(cambio.UsuarioId);
        }

        foreach (var adjunto in dto.Adjuntos)
        {
            adjunto.UsuarioNombre = nombres.NombreDe(adjunto.UsuarioId);
        }
    }

    /// <summary>
    /// Responsable por usuario: su nombre. Por rol: el nombre que ya armó el servicio.
    /// </summary>
    private static string? NombreDelResponsable(string? usuarioId, string? usuarioNombre, string? nombreRol) =>
        usuarioId != null ? usuarioNombre ?? "Usuario asignado" : nombreRol;

    private IActionResult ErrorResult(Result resultado) =>
        ProblemaDe(resultado, "Error de validación",
            tituloNoEncontrado: "Detención no encontrada",
            tituloSinPermiso: "Sin permiso para este análisis",
            tituloConcurrencia: "Conflicto de concurrencia");
}
