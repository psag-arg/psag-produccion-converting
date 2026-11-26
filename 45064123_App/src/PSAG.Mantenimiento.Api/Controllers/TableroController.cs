using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Api.Autorizacion;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Permisos;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// T-770 · Tablero del operador (tablet). Una sola llamada con todo el turno en proceso. El obligado a turno ve SU turno
/// (el filtro de turno obligatorio fija la línea; sin turno, 409). Los demás (supervisor) eligen la línea y lo ven en
/// sólo lectura: el turno abierto de la línea o, si no hay, el de la hora actual. Modo gerencial (T-770 iteración 2):
/// cualquier turno por <c>turnoProduccionId</c>, con <c>PRODUCCION.TABLERO_GERENCIAL</c>. Los cambios en línea llegan por SignalR
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

    private readonly TimeZoneInfo _zonaPlanta;

    public TableroController(ITableroService tablero, ITurnoProduccionService turnos, IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager,
        TimeZoneInfo zonaPlanta)
    {
        _zonaPlanta = zonaPlanta;
        _tablero = tablero;
        _turnos = turnos;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
    }

    /// <summary>
    /// Tablero del turno (los mismos 4 indicadores en los dos modos).
    /// <list type="bullet">
    /// <item>Obligado a turno (operador): el suyo; el filtro de turno obligatorio fija <paramref name="lineaId"/> y
    /// <paramref name="turnoProduccionId"/> a los de su turno, así que nunca ve otro.</item>
    /// <item>Modo gerencial (<paramref name="turnoProduccionId"/>, VER de <c>PRODUCCION.TABLERO_GERENCIAL</c>): cualquier turno
    /// (en curso o cerrado) de una línea a su alcance, sólo lectura. 404 si no existe.</item>
    /// <item>Por línea (<paramref name="lineaId"/>): el turno abierto de la línea o el de esta hora, sólo lectura; 204 si no
    /// hay. El que es sólo operario (no obligado), únicamente en sus líneas.</item>
    /// </list>
    /// Alcance por línea (T-802, como la corrección de turnos): administrador y mantenimiento todas; el resto sus líneas (sin
    /// líneas asignadas, todas).
    /// </summary>
    [HttpGet("turno")]
    [ProducesResponseType(typeof(TableroTurnoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permiso(AccionPermiso.Ver, Opciones.ProduccionTablero, Opciones.ProduccionTableroGerencial)]
    public async Task<IActionResult> GetTurno([FromQuery] int? lineaId, [FromQuery] long? turnoProduccionId)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        long turnoId;
        var soloLectura = true;
        if (TurnoObligado is { } propio)
        {
            // El filtro de turno obligatorio ya fija turnoProduccionId al suyo en las lecturas; esto es por si se saca.
            if (turnoProduccionId is { } pedido && pedido != propio.TurnoProduccionId)
                return Prohibido("Turno ajeno", "Sólo podés ver el tablero de tu turno.");
            turnoId = propio.TurnoProduccionId;
            soloLectura = false;
        }
        else if (turnoProduccionId is { } elegido)
        {
            if (!await TienePermisoAsync(Opciones.ProduccionTableroGerencial, AccionPermiso.Ver,
                    () => Roles.Any(r => r is RolesSistema.Administrador or RolesSistema.Supervisor or RolesSistema.Gerencia)))
                return Prohibido("Sin permiso", "Para ver otros turnos hace falta el tablero gerencial (Configuración → Roles y permisos).");
            var tp = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(elegido);
            if (tp == null)
                return NotFound(new ProblemDetails { Title = "Turno no encontrado", Detail = "El turno de producción no existe.", Status = StatusCodes.Status404NotFound });
            if (!await AlcanzaAsync(tp.LineaId))
                return Prohibido("Línea no asignada", "Esa línea no está entre las tuyas.");
            turnoId = elegido;
        }
        else
        {
            if (lineaId is not { } linea)
                return BadRequest(new ProblemDetails { Title = "Falta la línea", Detail = "Elegí la línea del tablero.", Status = StatusCodes.Status400BadRequest });
            if (!await AlcanzaAsync(linea))
                return Prohibido("Línea no asignada", "No estás habilitado en esa línea.");
            var actual = await _turnos.ObtenerActualAsync(linea);
            if (actual == null)
                return NoContent();
            turnoId = actual.TurnoProduccionId;
        }

        var resultado = await _tablero.ObtenerAsync(turnoId, soloLectura);
        if (resultado.IsFailure)
            return ProblemaDe(resultado, "No se pudo armar el tablero", "Turno no encontrado");
        var dto = resultado.Value!;
        if (dto.OperadorUsuarioId != null)
            dto.OperadorNombre = (await _userManager.NombresVisiblesAsync([dto.OperadorUsuarioId])).NombreDe(dto.OperadorUsuarioId) ?? dto.OperadorUsuarioId;
        return Ok(dto);
    }

    /// <summary>
    /// Modo gerencial: líneas a tu alcance y sus turnos de producción de la fecha (por defecto hoy, en hora de planta),
    /// para elegir qué turno ver en el tablero.
    /// </summary>
    [HttpGet("turnos")]
    [ProducesResponseType(typeof(TurnosTableroDto), StatusCodes.Status200OK)]
    [Permiso(Opciones.ProduccionTableroGerencial, AccionPermiso.Ver)]
    public async Task<IActionResult> GetTurnos([FromQuery] DateOnly? fecha)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var dia = fecha ?? HoraPlanta.Hoy(_zonaPlanta);
        return Ok(await _tablero.ListarTurnosAsync(dia, Roles, await LineasAsignadasAsync(_unitOfWork.OperadoresLinea)));
    }

    /// <summary>
    /// Alcance por línea del que mira un tablero ajeno: el que es sólo operario, sus líneas (siempre); el resto, la regla de
    /// T-802 (<see cref="TableroIndicadoresReglas.AlcanzaLinea"/>).
    /// </summary>
    private async Task<bool> AlcanzaAsync(int lineaId)
    {
        var lineas = await LineasAsignadasAsync(_unitOfWork.OperadoresLinea);
        if (Roles.Count == 1 && Roles.Contains(RolesSistema.Operario))
            return lineas.Contains(lineaId);
        return TableroIndicadoresReglas.AlcanzaLinea(Roles, lineas, lineaId);
    }

    private ObjectResult Prohibido(string titulo, string detalle) =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails { Title = titulo, Detail = detalle, Status = StatusCodes.Status403Forbidden });
}
