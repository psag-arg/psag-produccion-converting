using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Api.Autorizacion;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Permisos;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// T-770 · Tablero del operador (tablet). Una sola llamada con todo el turno en proceso. El obligado a turno ve SU turno
/// (el filtro de turno obligatorio fija la línea; sin turno, 409). Los demás (supervisor) eligen la línea y lo ven en
/// sólo lectura: el turno abierto de la línea o, si no hay, el de la hora actual. Los cambios en línea llegan por SignalR
/// (<c>/hubs/tablero</c>, ver TiempoReal/TableroHub.cs).
/// </summary>
[Route("api/tablero")]
[ApiController]
[Authorize]
public class TableroController : ApiControllerBase
{
    private readonly ITableroService _tablero;
    private readonly ITurnoProduccionService _turnos;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;

    public TableroController(ITableroService tablero, ITurnoProduccionService turnos, IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager)
    {
        _tablero = tablero;
        _turnos = turnos;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    /// <summary>
    /// Tablero del turno. Obligado a turno: el suyo (lineaId se ignora). Resto: <paramref name="lineaId"/> obligatoria,
    /// sólo lectura; 204 si la línea no tiene turno abierto ni turno a esta hora. El que es sólo operario (no obligado)
    /// únicamente en sus líneas.
    /// </summary>
    [HttpGet("turno")]
    [ProducesResponseType(typeof(TableroTurnoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Permiso(Opciones.ProduccionTablero, AccionPermiso.Ver)]
    public async Task<IActionResult> GetTurno([FromQuery] int? lineaId)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        long turnoProduccionId;
        var soloLectura = true;
        if (TurnoObligado is { } propio)
        {
            turnoProduccionId = propio.TurnoProduccionId;
            soloLectura = false;
        }
        else
        {
            if (lineaId is not { } linea)
                return BadRequest(new ProblemDetails { Title = "Falta la línea", Detail = "Elegí la línea del tablero.", Status = StatusCodes.Status400BadRequest });
            if (Roles.Count == 1 && Roles.Contains(RolesSistema.Operario)
                && !(await LineasAsignadasAsync(_unitOfWork.OperadoresLinea)).Contains(linea))
                return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
                {
                    Title = "Línea no asignada",
                    Detail = "No estás habilitado en esa línea.",
                    Status = StatusCodes.Status403Forbidden
                });
            var actual = await _turnos.ObtenerActualAsync(linea);
            if (actual == null)
                return NoContent();
            turnoProduccionId = actual.TurnoProduccionId;
        }

        var resultado = await _tablero.ObtenerAsync(turnoProduccionId, soloLectura);
        if (resultado.IsFailure)
            return ProblemaDe(resultado, "No se pudo armar el tablero", "Turno no encontrado");
        var dto = resultado.Value!;
        if (dto.OperadorUsuarioId != null)
            dto.OperadorNombre = (await _userManager.NombresVisiblesAsync([dto.OperadorUsuarioId])).NombreDe(dto.OperadorUsuarioId) ?? dto.OperadorUsuarioId;
        return Ok(dto);
    }
}
