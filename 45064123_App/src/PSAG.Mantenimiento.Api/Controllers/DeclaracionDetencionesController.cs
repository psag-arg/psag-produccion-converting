using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Detenciones automáticas de la cámara (T-610): datos para declarar, declarar y aviso de fin de turno.
/// Aparte de DetencionesController para no mezclar el alta manual. La restricción por línea se valida acá.
/// </summary>
[Route("api/detenciones")]
[ApiController]
[Authorize]
public class DeclaracionDetencionesController : ApiControllerBase
{
    private readonly IDeclaracionDetencionService _servicio;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;

    public DeclaracionDetencionesController(IDeclaracionDetencionService servicio, IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
    {
        _servicio = servicio;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    /// <summary>Detención para la pantalla de declaración (hora de parada, arranque sugerido por la cámara, maquinista).</summary>
    [HttpGet("{id:long}/declaracion")]
    [ProducesResponseType(typeof(DeclaracionDetencionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDeclaracion(long id)
    {
        var resultado = await _servicio.ObtenerAsync(id);
        if (resultado.IsFailure)
            return ProblemaDe(resultado, "No se pudo abrir la detención", "Detención no encontrada");
        var dto = resultado.Value!;
        if (dto.MaquinistaUsuarioId != null)
            dto.MaquinistaNombre = (await _userManager.NombresVisiblesAsync([dto.MaquinistaUsuarioId])).NombreDe(dto.MaquinistaUsuarioId);
        return Ok(dto);
    }

    /// <summary>El operador completa la detención automática: pasa a REGISTRADA (flujo normal).</summary>
    [HttpPost("{id:long}/declarar")]
    [Authorize(Policy = "CanCreateDetenciones")]
    [ProducesResponseType(typeof(DeclararDetencionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Declarar(long id, [FromBody] DeclararDetencionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(id);
        if (detencion == null)
            return NotFound(new ProblemDetails { Title = "Detención no encontrada", Status = StatusCodes.Status404NotFound });

        var asignacion = await _unitOfWork.UsuariosLineas.GetByUsuarioYLineaAsync(CurrentUserId, detencion.LineaId);
        if (!DetencionCargaReglas.PuedeCargarEnLinea(Roles, asignacion != null))
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Línea no asignada",
                Detail = DetencionCargaReglas.MensajeLineaNoAsignada,
                Status = StatusCodes.Status403Forbidden
            });

        var resultado = await _servicio.DeclararAsync(id, request, CurrentUserId, asignacion);
        return resultado.IsFailure
            ? ProblemaDe(resultado, "No se pudo declarar la detención", "Detención no encontrada", tituloConcurrencia: "La detención cambió")
            : Ok(resultado.Value);
    }

    /// <summary>
    /// Aviso de fin de turno: pendientes de declaración de las líneas del usuario y minutos para el fin del turno.
    /// Administrador y supervisor sin líneas asignadas ven todas. No bloquea el cierre de turno.
    /// </summary>
    [HttpGet("aviso-fin-turno")]
    [ProducesResponseType(typeof(AvisoFinTurnoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvisoFinTurno([FromQuery] int minutosAviso = DetencionCamaraReglas.MinutosAvisoFinTurnoPorDefecto)
    {
        var lineas = await LineasAsignadasAsync(_unitOfWork.UsuariosLineas);
        var veTodas = lineas.Length == 0 && Roles.Any(r => r is RolesSistema.Administrador or RolesSistema.Supervisor);
        return Ok(await _servicio.ObtenerAvisoFinTurnoAsync(veTodas ? null : lineas, Math.Clamp(minutosAviso, 5, 120)));
    }
}
