using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Calidad;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Control de calidad por cámaras (T-611): registros de defectos con imagen y revisión humana, y sus maestros.
/// Todos los usuarios ven; revisan supervisión, mantenimiento y admin; cargan a mano supervisión y admin;
/// los maestros los administra el admin. Los permisos se validan en el servicio; los atributos de rol cortan antes.
/// No hay DELETE. El detector entra por CalidadIngestaController, con clave de servicio.
/// </summary>
[Route("api/calidad")]
[ApiController]
[Authorize]
public class CalidadController : ApiControllerBase
{
    /// <summary>Mismos roles que CalidadReglas.RolesRevision.</summary>
    public const string RolesRevision = RolesSistema.Supervisor + "," + RolesSistema.Mantenimiento + "," + RolesSistema.Administrador;

    /// <summary>Mismos roles que CalidadReglas.RolesCargaManual.</summary>
    public const string RolesCargaManual = RolesSistema.Supervisor + "," + RolesSistema.Administrador;

    /// <summary>Una imagen de hasta 10 MB más el sobre del multipart.</summary>
    public const long LimitePedidoImagen = AdjuntoImagenReglas.TamanoMaximoBytes + 1024 * 1024;

    private readonly IRegistroCalidadService _registros;
    private readonly ICalidadMaestrosService _maestros;
    private readonly UserManager<ApplicationUser> _userManager;

    public CalidadController(
        IRegistroCalidadService registros,
        ICalidadMaestrosService maestros,
        UserManager<ApplicationUser> userManager)
    {
        _registros = registros;
        _maestros = maestros;
        _userManager = userManager;
    }

    // ===================== Registros =====================

    /// <summary>
    /// Grilla paginada con filtros por fecha (días de planta), turno, línea, punto de control, defecto, estado y origen.
    /// </summary>
    [HttpGet("registros")]
    [ProducesResponseType(typeof(PagedResult<RegistroCalidadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRegistros([FromQuery] RegistrosCalidadFiltroRequest filtro)
    {
        var resultado = await _registros.ObtenerPaginadoAsync(filtro);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        await CompletarNombresAsync(resultado.Value!.Items);
        return Ok(resultado.Value);
    }

    /// <summary>
    /// Detalle con el historial de revisión.
    /// </summary>
    [HttpGet("registros/{id:long}")]
    [ProducesResponseType(typeof(RegistroCalidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRegistro(long id)
    {
        var resultado = await _registros.ObtenerAsync(id);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        await CompletarNombresAsync([resultado.Value!]);
        return Ok(resultado.Value);
    }

    /// <summary>
    /// Imagen del registro. Requiere sesión; los archivos nunca se sirven como estáticos.
    /// </summary>
    [HttpGet("registros/{id:long}/imagen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImagen(long id)
    {
        var resultado = await _registros.AbrirImagenAsync(id);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        var archivo = resultado.Value!;
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Cache-Control"] = "private, max-age=3600";
        return File(archivo.Contenido, archivo.ContentType);
    }

    /// <summary>
    /// Carga manual de un registro (multipart; imagen opcional en el campo "archivo"). Nace confirmado.
    /// </summary>
    [HttpPost("registros")]
    [Authorize(Roles = RolesCargaManual)]
    [RequestSizeLimit(LimitePedidoImagen)]
    [RequestFormLimits(MultipartBodyLengthLimit = LimitePedidoImagen)]
    [ProducesResponseType(typeof(RegistroCalidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CrearManual([FromForm] CrearRegistroCalidadManualRequest request, IFormFile? archivo)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        await using var contenido = archivo?.OpenReadStream();
        var imagen = archivo != null ? new ImagenEntrante(archivo.FileName, archivo.Length, contenido!) : null;

        var resultado = await _registros.CrearManualAsync(request, imagen, CurrentUserId, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        await CompletarNombresAsync([resultado.Value!]);
        return Ok(resultado.Value);
    }

    /// <summary>
    /// Marcar como CONFIRMADO o FALSO_POSITIVO. Guarda quién y cuándo y deja el cambio en el historial.
    /// </summary>
    [HttpPost("registros/{id:long}/revision")]
    [Authorize(Roles = RolesRevision)]
    [ProducesResponseType(typeof(RegistroCalidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revisar(long id, [FromBody] RevisarRegistroCalidadRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _registros.RevisarAsync(id, request, CurrentUserId, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        await CompletarNombresAsync([resultado.Value!]);
        return Ok(resultado.Value);
    }

    /// <summary>
    /// Relacionar el registro con una detención de la misma línea (por código DET-...). Código vacío = quitar la relación.
    /// </summary>
    [HttpPost("registros/{id:long}/detencion")]
    [Authorize(Roles = RolesRevision)]
    [ProducesResponseType(typeof(RegistroCalidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VincularDetencion(long id, [FromBody] VincularDetencionCalidadRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _registros.VincularDetencionAsync(id, request, CurrentUserId, Roles);
        if (resultado.IsFailure)
            return ErrorResult(resultado);

        await CompletarNombresAsync([resultado.Value!]);
        return Ok(resultado.Value);
    }

    // ===================== Maestros =====================

    [HttpGet("puntos-control")]
    [ProducesResponseType(typeof(IEnumerable<PuntoControlCalidadDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPuntosControl([FromQuery] int? lineaId = null, [FromQuery] bool incluirInactivos = false) =>
        Ok(await _maestros.ObtenerPuntosControlAsync(lineaId, incluirInactivos));

    [HttpPost("puntos-control")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(PuntoControlCalidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearPuntoControl([FromBody] GuardarPuntoControlCalidadRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _maestros.CrearPuntoControlAsync(request, CurrentUserId, Roles);
        return resultado.IsFailure ? ErrorResult(resultado) : Ok(resultado.Value);
    }

    [HttpPut("puntos-control/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(PuntoControlCalidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActualizarPuntoControl(int id, [FromBody] GuardarPuntoControlCalidadRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _maestros.ActualizarPuntoControlAsync(id, request, CurrentUserId, Roles);
        return resultado.IsFailure ? ErrorResult(resultado) : Ok(resultado.Value);
    }

    [HttpGet("tipos-defecto")]
    [ProducesResponseType(typeof(IEnumerable<TipoDefectoCalidadDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTiposDefecto([FromQuery] bool incluirInactivos = false) =>
        Ok(await _maestros.ObtenerTiposDefectoAsync(incluirInactivos));

    [HttpPost("tipos-defecto")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(TipoDefectoCalidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearTipoDefecto([FromBody] GuardarTipoDefectoCalidadRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _maestros.CrearTipoDefectoAsync(request, CurrentUserId, Roles);
        return resultado.IsFailure ? ErrorResult(resultado) : Ok(resultado.Value);
    }

    [HttpPut("tipos-defecto/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(TipoDefectoCalidadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActualizarTipoDefecto(int id, [FromBody] GuardarTipoDefectoCalidadRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var resultado = await _maestros.ActualizarTipoDefectoAsync(id, request, CurrentUserId, Roles);
        return resultado.IsFailure ? ErrorResult(resultado) : Ok(resultado.Value);
    }

    // ===================== Ayudas =====================

    private async Task CompletarNombresAsync(IEnumerable<RegistroCalidadDto> registros)
    {
        var lista = registros.ToList();
        var nombres = await _userManager.NombresVisiblesAsync(lista.SelectMany(r =>
            new[] { r.CreadoPorUsuarioId, r.RevisadoPorUsuarioId }.Concat(r.Revisiones.Select(v => v.UsuarioId))));

        foreach (var registro in lista)
        {
            registro.CreadoPorUsuarioNombre = NombreODetector(nombres, registro.CreadoPorUsuarioId);
            registro.RevisadoPorUsuarioNombre = nombres.NombreDe(registro.RevisadoPorUsuarioId);
            foreach (var revision in registro.Revisiones)
                revision.UsuarioNombre = nombres.NombreDe(revision.UsuarioId);
        }
    }

    private static string? NombreODetector(IReadOnlyDictionary<string, string> nombres, string usuarioId) =>
        usuarioId == Domain.Entities.RegistroCalidad.UsuarioDetector ? "Detector (cámara)" : nombres.NombreDe(usuarioId);

    private IActionResult ErrorResult(Result resultado) =>
        ProblemaDe(resultado, "Error de validación",
            tituloNoEncontrado: "No encontrado",
            tituloSinPermiso: "Sin permiso",
            tituloConcurrencia: "Conflicto de concurrencia");
}
