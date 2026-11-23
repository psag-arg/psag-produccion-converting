using PSAG.Mantenimiento.Api.Autorizacion;
using PSAG.Mantenimiento.Domain.Permisos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Turno de producción (T-798 etapa 2; rehace T-783 sobre el evento turno). El operario toma el turno de sus líneas
/// en "Inicio de turno"; el supervisor (o mantenimiento / administrador) lo corrige desde "Turnos de producción".
/// Los permisos por línea se validan en el servicio; acá se completan los nombres de usuario.
/// </summary>
[Route("api/turnos-produccion")]
[ApiController]
[Authorize]
public class TurnosProduccionController : ApiControllerBase
{
    private const string TituloError = "No se pudo completar la operación del turno";

    private readonly ITurnoProduccionService _servicio;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeZoneInfo _zonaPlanta;

    public TurnosProduccionController(
        ITurnoProduccionService servicio, IUnitOfWork unitOfWork, UserManager<ApplicationUser> userManager, TimeZoneInfo zonaPlanta)
    {
        _servicio = servicio;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _zonaPlanta = zonaPlanta;
    }

    /// <summary>Qué preguntarle al operario que acaba de entrar: sus líneas, el turno en curso, operador y producto actuales.</summary>
    [HttpGet("propuesta")]
    [ProducesResponseType(typeof(PropuestaTurnoProduccionDto), StatusCodes.Status200OK)]
    [Permiso(Opciones.ProduccionOperacionTurno, AccionPermiso.Ver)]
    public async Task<IActionResult> GetPropuesta()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var propuesta = await _servicio.ObtenerPropuestaAsync(CurrentUserId, Roles);
        var nombres = await _userManager.NombresVisiblesAsync(propuesta.Lineas.Select(o => o.OperadorActualUsuarioId));
        foreach (var o in propuesta.Lineas)
            o.OperadorActualNombre = nombres.NombreDe(o.OperadorActualUsuarioId);
        return Ok(propuesta);
    }

    /// <summary>
    /// Abre o toma el turno en curso de la línea. Operario habilitado en la línea: queda como operador (LOGIN).
    /// Supervisor / mantenimiento / administrador sin habilitación en la línea: lo abre como SUPERVISOR.
    /// </summary>
    [HttpPost("abrir")]
    [ProducesResponseType(typeof(AperturaTurnoProduccionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Permiso(Opciones.ProduccionOperacionTurno, AccionPermiso.Alta)]
    public async Task<IActionResult> Abrir([FromBody] AbrirTurnoProduccionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var lineas = await LineasAsignadasAsync(_unitOfWork.OperadoresLinea);
        var origen = Roles.Contains(RolesSistema.Operario) && lineas.Contains(request.LineaId)
            ? OrigenAperturaTurno.LOGIN
            : OrigenAperturaTurno.SUPERVISOR;
        var resultado = await _servicio.AbrirAsync(request.LineaId, CurrentUserId, origen, request.ProductoMaestroId, request.Relevar, Roles, lineas);
        if (resultado.IsFailure)
            return ProblemaDe(resultado, "No se pudo tomar el turno", "Línea no encontrada", "Sin permiso sobre la línea");

        var dto = resultado.Value!;
        var nombres = await _userManager.NombresVisiblesAsync(IdsDe(dto.Turno).Append(dto.ReemplazadoUsuarioId));
        CompletarNombres(dto.Turno, nombres);
        dto.ReemplazadoNombre = nombres.NombreDe(dto.ReemplazadoUsuarioId);
        return Ok(dto);
    }

    /// <summary>Cambio de producto de un turno abierto (cierra el tramo vigente y abre otro con snapshot del objetivo).</summary>
    [HttpPost("{id:long}/cambiar-producto")]
    [ProducesResponseType(typeof(TurnoProduccionDto), StatusCodes.Status200OK)]
    [Permiso(Opciones.ProduccionOperacionTurno, AccionPermiso.Modificar)]
    public async Task<IActionResult> CambiarProducto(long id, [FromBody] CambiarProductoTurnoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var resultado = await _servicio.CambiarProductoAsync(id, request, CurrentUserId, Roles, await LineasAsignadasAsync(_unitOfWork.OperadoresLinea));
        return await RespuestaAsync(resultado);
    }

    /// <summary>El usuario (habilitado en la línea) releva al operador vigente.</summary>
    [HttpPost("{id:long}/relevar")]
    [ProducesResponseType(typeof(TurnoProduccionDto), StatusCodes.Status200OK)]
    [Permiso(Opciones.ProduccionOperacionTurno, AccionPermiso.Modificar)]
    public async Task<IActionResult> Relevar(long id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var resultado = await _servicio.RelevarAsync(id, CurrentUserId, Roles, await LineasAsignadasAsync(_unitOfWork.OperadoresLinea));
        return await RespuestaAsync(resultado);
    }

    /// <summary>Cierre manual del turno (operador de la línea o supervisor).</summary>
    [HttpPost("{id:long}/cerrar")]
    [ProducesResponseType(typeof(TurnoProduccionDto), StatusCodes.Status200OK)]
    [Permiso(Opciones.ProduccionOperacionTurno, AccionPermiso.Modificar)]
    public async Task<IActionResult> Cerrar(long id, [FromBody] CerrarTurnoProduccionRequest? request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var resultado = await _servicio.CerrarAsync(id, request?.Observacion, CurrentUserId, Roles, await LineasAsignadasAsync(_unitOfWork.OperadoresLinea));
        return await RespuestaAsync(resultado);
    }

    /// <summary>Turno abierto de la línea (o el de la hora actual). 204 si no hay.</summary>
    [HttpGet("actual")]
    [ProducesResponseType(typeof(TurnoProduccionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Permiso(Opciones.ProduccionOperacionTurno, AccionPermiso.Ver)]
    public async Task<IActionResult> GetActual([FromQuery] int lineaId)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        // El operario sólo ve sus líneas (como las detenciones); el resto de los roles, todas.
        if (Roles.Count == 1 && Roles.Contains(RolesSistema.Operario)
            && !(await LineasAsignadasAsync(_unitOfWork.OperadoresLinea)).Contains(lineaId))
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Línea no asignada",
                Detail = "No estás habilitado en esa línea.",
                Status = StatusCodes.Status403Forbidden
            });

        var dto = await _servicio.ObtenerActualAsync(lineaId);
        if (dto == null)
            return NoContent();
        CompletarNombres(dto, await _userManager.NombresVisiblesAsync(IdsDe(dto)));
        return Ok(dto);
    }

    /// <summary>Listado del supervisor: cada línea × turno del día (por defecto hoy), con operadores, tramos y estado.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(TurnosProduccionDiaDto), StatusCodes.Status200OK)]
    [Permiso(Opciones.ProduccionTurnos, AccionPermiso.Ver)]
    public async Task<IActionResult> GetDia([FromQuery] DateOnly? fecha, [FromQuery] int? lineaId)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var dia = await _servicio.ObtenerDiaAsync(fecha ?? HoraPlanta.Hoy(_zonaPlanta), lineaId, Roles,
            await LineasAsignadasAsync(_unitOfWork.OperadoresLinea));
        var nombres = await _userManager.NombresVisiblesAsync(dia.Filas.SelectMany(f =>
            IdsDe(f.Turno).Concat(f.OperadoresHabilitados.Select(o => o.UsuarioId))));
        foreach (var f in dia.Filas)
        {
            CompletarNombres(f.Turno, nombres);
            foreach (var o in f.OperadoresHabilitados)
                o.Nombre = nombres.NombreDe(o.UsuarioId) ?? o.UsuarioId;
            f.OperadoresHabilitados = f.OperadoresHabilitados.OrderBy(o => o.Nombre).ToList();
        }
        return Ok(dia);
    }

    /// <summary>Corrección del operador (supervisor, con motivo; el tramo nuevo anula al anterior).</summary>
    [HttpPost("{id:long}/corregir-operador")]
    [ProducesResponseType(typeof(TurnoProduccionDto), StatusCodes.Status200OK)]
    [Permiso(Opciones.ProduccionTurnos, AccionPermiso.Modificar)]
    public async Task<IActionResult> CorregirOperador(long id, [FromBody] CorregirOperadorTurnoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var resultado = await _servicio.CorregirOperadorAsync(id, request, CurrentUserId, Roles, await LineasAsignadasAsync(_unitOfWork.OperadoresLinea));
        return await RespuestaAsync(resultado);
    }

    /// <summary>Corrección del producto (supervisor, con motivo; el tramo nuevo anula al anterior).</summary>
    [HttpPost("{id:long}/corregir-producto")]
    [ProducesResponseType(typeof(TurnoProduccionDto), StatusCodes.Status200OK)]
    [Permiso(Opciones.ProduccionTurnos, AccionPermiso.Modificar)]
    public async Task<IActionResult> CorregirProducto(long id, [FromBody] CorregirProductoTurnoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var resultado = await _servicio.CorregirProductoAsync(id, request, CurrentUserId, Roles, await LineasAsignadasAsync(_unitOfWork.OperadoresLinea));
        return await RespuestaAsync(resultado);
    }

    /// <summary>
    /// Producción oficial de un tramo cerrado (T-798 etapa 5): VALIDAR lo de la cámara, CORREGIRLO (con motivo) o cargar
    /// a MANO (p. ej. bolsones sin cámara). Insert-only: la fila nueva anula a la vigente.
    /// </summary>
    [HttpPost("{id:long}/produccion")]
    [ProducesResponseType(typeof(ProduccionTurnoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [Permiso(Opciones.ProduccionTurnos, AccionPermiso.Modificar)]
    public async Task<IActionResult> RegistrarProduccion(long id, [FromBody] RegistrarProduccionTurnoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var resultado = await _servicio.RegistrarProduccionAsync(id, request, CurrentUserId, Roles, await LineasAsignadasAsync(_unitOfWork.OperadoresLinea));
        return resultado.IsFailure
            ? ProblemaDe(resultado, "No se pudo registrar la producción", "Turno no encontrado", "Sin permiso sobre la línea")
            : Ok(resultado.Value);
    }

    private async Task<IActionResult> RespuestaAsync(Application.Common.Result<TurnoProduccionDto> resultado)
    {
        if (resultado.IsFailure)
            return ProblemaDe(resultado, TituloError, "Turno no encontrado", "Sin permiso sobre la línea");
        var dto = resultado.Value!;
        CompletarNombres(dto, await _userManager.NombresVisiblesAsync(IdsDe(dto)));
        return Ok(dto);
    }

    private static IEnumerable<string?> IdsDe(TurnoProduccionDto? t) =>
        t == null
            ? []
            : t.Operadores.SelectMany(o => new[] { o.OperadorUsuarioId, o.RegistradoPorUsuarioId })
                .Concat(t.Productos.Select(p => p.DeclaradoPorUsuarioId))
                .Append(t.AbiertoPorUsuarioId)
                .Append(t.CerradoPorUsuarioId);

    private static void CompletarNombres(TurnoProduccionDto? t, IReadOnlyDictionary<string, string> nombres)
    {
        if (t == null)
            return;
        t.AbiertoPorNombre = nombres.NombreDe(t.AbiertoPorUsuarioId);
        t.CerradoPorNombre = nombres.NombreDe(t.CerradoPorUsuarioId);
        foreach (var o in t.Operadores)
        {
            o.OperadorNombre = nombres.NombreDe(o.OperadorUsuarioId) ?? o.OperadorUsuarioId;
            o.RegistradoPorNombre = nombres.NombreDe(o.RegistradoPorUsuarioId);
        }
        foreach (var p in t.Productos)
            p.DeclaradoPorNombre = nombres.NombreDe(p.DeclaradoPorUsuarioId);
        // OperadorActual / ProductoActual son las mismas instancias que las de las listas.
    }
}
