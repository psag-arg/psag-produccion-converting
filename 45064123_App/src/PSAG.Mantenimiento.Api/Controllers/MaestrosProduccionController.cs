using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Maestros → Productos (T-798 etapa 6): productos maestro con sus SKU bolsón / pallet. Todos ven; el administrador
/// da de alta y edita. Sin DELETE: se desactivan (Activo = false en el PUT).
/// </summary>
[Route("api/productos-maestro")]
[ApiController]
[Authorize]
public class ProductosMaestroController : ApiControllerBase
{
    private readonly IMaestrosProduccionService _servicio;

    public ProductosMaestroController(IMaestrosProduccionService servicio) => _servicio = servicio;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ProductoMaestroDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] bool incluirInactivos = false) =>
        Ok(await _servicio.ObtenerProductosAsync(incluirInactivos));

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductoMaestroDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPorId(int id) =>
        await _servicio.ObtenerProductoAsync(id) is { } p ? Ok(p) : NotFound();

    [HttpPost]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(ProductoMaestroDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] GuardarProductoMaestroRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var r = await _servicio.CrearProductoAsync(request, CurrentUserId, Roles);
        return r.IsFailure ? Error(r) : Ok(r.Value);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(ProductoMaestroDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] GuardarProductoMaestroRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();
        var r = await _servicio.ActualizarProductoAsync(id, request, CurrentUserId, Roles);
        return r.IsFailure ? Error(r) : Ok(r.Value);
    }

    [HttpPost("skus")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(ProductoSkuDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CrearSku([FromBody] GuardarProductoSkuRequest request)
    {
        var r = await _servicio.CrearSkuAsync(request, Roles);
        return r.IsFailure ? Error(r) : Ok(r.Value);
    }

    [HttpPut("skus/{skuId:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(ProductoSkuDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActualizarSku(int skuId, [FromBody] GuardarProductoSkuRequest request)
    {
        var r = await _servicio.ActualizarSkuAsync(skuId, request, Roles);
        return r.IsFailure ? Error(r) : Ok(r.Value);
    }

    private IActionResult Error(Result r) => ProblemaDe(r, "Error de validación", "No encontrado", "Sin permiso sobre los productos");
}

/// <summary>
/// Maestros → Parámetros línea + producto (T-798 etapa 6) = PRESUPUESTO: habilitado y objetivos por turno, con
/// vigencia. Todos ven; el administrador carga. Sin DELETE: para dejar de producir un producto se carga una vigencia
/// nueva con Habilitado = false. Editar no cambia el pasado: cada tramo de turno guarda su snapshot del objetivo.
/// </summary>
[Route("api/parametros-linea-producto")]
[ApiController]
[Authorize]
public class ParametrosLineaProductoController : ApiControllerBase
{
    private readonly IMaestrosProduccionService _servicio;

    public ParametrosLineaProductoController(IMaestrosProduccionService servicio) => _servicio = servicio;

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ParametroLineaProductoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] int? lineaId = null) => Ok(await _servicio.ObtenerParametrosAsync(lineaId));

    [HttpPost]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(ParametroLineaProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Crear([FromBody] GuardarParametroLineaProductoRequest request)
    {
        var r = await _servicio.CrearParametroAsync(request, Roles);
        return r.IsFailure ? Error(r) : Ok(r.Value);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(ParametroLineaProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Actualizar(int id, [FromBody] GuardarParametroLineaProductoRequest request)
    {
        var r = await _servicio.ActualizarParametroAsync(id, request, Roles);
        return r.IsFailure ? Error(r) : Ok(r.Value);
    }

    private IActionResult Error(Result r) => ProblemaDe(r, "Error de validación", "Parámetro no encontrado", "Sin permiso sobre la parametrización");
}
