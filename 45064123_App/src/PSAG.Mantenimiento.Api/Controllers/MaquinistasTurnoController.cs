using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Maquinista de cada turno de cada línea (T-783): el operario lo confirma después del login y el supervisor lo
/// corrige desde "Maquinistas del turno". Las reglas (relación, permisos por línea) se validan acá, no en la Web.
/// </summary>
[Route("api/maquinistas-turno")]
[ApiController]
[Authorize]
public class MaquinistasTurnoController : ApiControllerBase
{
    private readonly IMaquinistaTurnoService _servicio;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeZoneInfo _zonaPlanta;

    public MaquinistasTurnoController(
        IMaquinistaTurnoService servicio, IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager, TimeZoneInfo zonaPlanta)
    {
        _servicio = servicio;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _zonaPlanta = zonaPlanta;
    }

    /// <summary>Qué preguntarle al usuario que acaba de entrar: si es el maquinista del turno en curso (sólo OPERARIO).</summary>
    [HttpGet("propuesta")]
    [ProducesResponseType(typeof(PropuestaMaquinistaTurnoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPropuesta()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var propuesta = await _servicio.ObtenerPropuestaAsync(CurrentUserId, Roles);
        var nombres = await _userManager.NombresVisiblesAsync(propuesta.Opciones.Select(o => o.MaquinistaActualUsuarioId));
        foreach (var o in propuesta.Opciones)
            o.MaquinistaActualNombre = nombres.NombreDe(o.MaquinistaActualUsuarioId);
        return Ok(propuesta);
    }

    /// <summary>El usuario confirma que es el maquinista del turno en curso de la línea.</summary>
    [HttpPost("confirmar")]
    [Authorize(Policy = "RequireOperario")]
    [ProducesResponseType(typeof(ConfirmacionMaquinistaTurnoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirmar([FromBody] ConfirmarMaquinistaTurnoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var nombres = await _userManager.NombresVisiblesAsync([CurrentUserId]);
        var resultado = await _servicio.ConfirmarAsync(CurrentUserId, nombres.NombreDe(CurrentUserId) ?? CurrentUserName ?? CurrentUserId, request.LineaId);
        if (resultado.IsFailure)
            return ProblemaDe(resultado, "No quedaste como maquinista del turno");

        var dto = resultado.Value!;
        if (dto.ReemplazadoUsuarioId != null)
            dto.ReemplazadoNombre = (await _userManager.NombresVisiblesAsync([dto.ReemplazadoUsuarioId])).NombreDe(dto.ReemplazadoUsuarioId);
        return Ok(dto);
    }

    /// <summary>Maquinistas de los turnos que empezaron ese día (por defecto hoy), por línea.</summary>
    [HttpGet]
    [Authorize(Policy = "RequireSupervisor")]
    [ProducesResponseType(typeof(MaquinistasTurnoDiaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDia([FromQuery] DateOnly? fecha)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var dia = await _servicio.ObtenerDiaAsync(
            fecha ?? HoraPlanta.Hoy(_zonaPlanta),
            Roles, await LineasAsignadasAsync(_unitOfWork.UsuariosLineas));

        var nombres = await _userManager.NombresVisiblesAsync(dia.Filas.SelectMany(f =>
            f.Historial.SelectMany(h => new[] { h.UsuarioId, h.RegistradoPorUsuarioId })
                .Concat(f.Asignaciones.Select(a => a.UsuarioId))
                .Append(f.SegunAsignacionUsuarioId)));
        foreach (var f in dia.Filas)
        {
            foreach (var h in f.Historial.Append(f.Vigente).OfType<MaquinistaTurnoRegistroDto>())
            {
                h.Nombre = nombres.NombreDe(h.UsuarioId);
                h.RegistradoPorNombre = nombres.NombreDe(h.RegistradoPorUsuarioId);
            }
            foreach (var a in f.Asignaciones)
                a.Nombre = nombres.NombreDe(a.UsuarioId);
            f.SegunAsignacionNombre = nombres.NombreDe(f.SegunAsignacionUsuarioId);
        }
        return Ok(dia);
    }

    /// <summary>El supervisor carga o corrige el maquinista de un turno (queda el historial).</summary>
    [HttpPost("corregir")]
    [Authorize(Policy = "RequireSupervisor")]
    [ProducesResponseType(typeof(CorreccionMaquinistaTurnoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Corregir([FromBody] CorregirMaquinistaTurnoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var asignacion = await _unitOfWork.UsuariosLineas.GetByIdAsync(request.UsuarioLineaId);
        var nombre = asignacion == null
            ? string.Empty
            : (await _userManager.NombresVisiblesAsync([asignacion.UsuarioId])).NombreDe(asignacion.UsuarioId) ?? asignacion.UsuarioId;

        var resultado = await _servicio.CorregirAsync(
            request, CurrentUserId, Roles, await LineasAsignadasAsync(_unitOfWork.UsuariosLineas), nombre);
        return resultado.IsFailure
            ? ProblemaDe(resultado, "No se pudo corregir el maquinista del turno", "Turno no encontrado", "Sin permiso sobre la línea")
            : Ok(resultado.Value);
    }
}
