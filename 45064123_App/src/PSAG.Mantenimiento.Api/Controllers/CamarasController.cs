using PSAG.Mantenimiento.Api.Autorizacion;
using PSAG.Mantenimiento.Domain.Permisos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Camaras;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Maestro de cámaras (T-798 etapa 4, diseño 2.4): todos ven; el administrador da de alta, edita y desactiva
/// (borrado lógico, sin DELETE). La habilitación de cada cámara es la única fuente de verdad del servicio de conteo,
/// que la lee por <see cref="CamarasConfiguracionController"/>.
/// </summary>
[Route("api/camaras")]
[ApiController]
[Authorize]
public class CamarasController : ApiControllerBase
{
    private readonly ICamaraService _servicio;

    public CamarasController(ICamaraService servicio) => _servicio = servicio;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CamaraDto>), StatusCodes.Status200OK)]
    [Permiso(AccionPermiso.Ver, Opciones.MaestrosCamaras, Opciones.CalidadParadasCamara, Opciones.MaestrosCalidad)]
    public async Task<IActionResult> Get([FromQuery] int? lineaId = null, [FromQuery] bool incluirInactivas = false) =>
        Ok(await _servicio.ObtenerAsync(lineaId, incluirInactivas));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CamaraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permiso(AccionPermiso.Ver, Opciones.MaestrosCamaras, Opciones.CalidadParadasCamara, Opciones.MaestrosCalidad)]
    public async Task<IActionResult> GetPorId(int id) =>
        await _servicio.ObtenerPorIdAsync(id) is { } camara ? Ok(camara) : NotFound();

    [HttpPost]
    [ProducesResponseType(typeof(CamaraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [Permiso(Opciones.MaestrosCamaras, AccionPermiso.Alta)]
    public async Task<IActionResult> Crear([FromBody] GuardarCamaraRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var resultado = await _servicio.CrearAsync(request, CurrentUserId, Roles);
        return resultado.IsFailure ? Error(resultado) : Ok(resultado.Value);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CamaraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permiso(Opciones.MaestrosCamaras, AccionPermiso.Modificar)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] GuardarCamaraRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var resultado = await _servicio.ActualizarAsync(id, request, CurrentUserId, Roles);
        return resultado.IsFailure ? Error(resultado) : Ok(resultado.Value);
    }

    /// <summary>Borrado lógico: la cámara queda inactiva, deshabilitada y sin producción oficial. Sus datos no se tocan.</summary>
    [HttpPost("{id:int}/desactivar")]
    [ProducesResponseType(typeof(CamaraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Permiso(Opciones.MaestrosCamaras, AccionPermiso.Baja)]
    public async Task<IActionResult> Desactivar(int id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var resultado = await _servicio.DesactivarAsync(id, CurrentUserId, Roles);
        return resultado.IsFailure ? Error(resultado) : Ok(resultado.Value);
    }

    private IActionResult Error(Result resultado) =>
        ProblemaDe(resultado, "Error de validación", "Cámara no encontrada", "Sin permiso sobre las cámaras");
}

/// <summary>
/// Configuración para el servicio PSAG_ConteoCamara (T-798 etapa 4): cámaras activas y habilitadas. Sin sesión:
/// misma clave de servicio que la ingesta (header X-Clave-Ingesta contra Calidad:ClaveIngesta; sin clave 503).
/// </summary>
[Route("api/camaras/configuracion")]
[ApiController]
[AllowAnonymous]
[ClaveIngestaCalidad]
public class CamarasConfiguracionController : ApiControllerBase
{
    private readonly ICamaraService _servicio;

    public CamarasConfiguracionController(ICamaraService servicio) => _servicio = servicio;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CamaraConfiguracionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get() => Ok(await _servicio.ObtenerConfiguracionAsync());
}
