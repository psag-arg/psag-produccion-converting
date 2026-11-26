using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Produccion;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Maestros de producción (T-798 etapa 6): productos maestro + SKU y parametrización línea + producto (presupuesto).
/// Todos ven; sólo el administrador modifica. Sin borrado: productos y SKU se desactivan; la parametrización se
/// "apaga" con una vigencia nueva deshabilitada.
/// </summary>
public interface IMaestrosProduccionService
{
    Task<IReadOnlyList<ProductoMaestroDto>> ObtenerProductosAsync(bool incluirInactivos, CancellationToken cancellationToken = default);
    Task<ProductoMaestroDto?> ObtenerProductoAsync(int productoMaestroId, CancellationToken cancellationToken = default);
    Task<Result<ProductoMaestroDto>> CrearProductoAsync(GuardarProductoMaestroRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task<Result<ProductoMaestroDto>> ActualizarProductoAsync(int productoMaestroId, GuardarProductoMaestroRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    Task<Result<ProductoSkuDto>> CrearSkuAsync(GuardarProductoSkuRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task<Result<ProductoSkuDto>> ActualizarSkuAsync(int productoSkuId, GuardarProductoSkuRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ParametroLineaProductoDto>> ObtenerParametrosAsync(int? lineaId, CancellationToken cancellationToken = default);
    Task<Result<ParametroLineaProductoDto>> CrearParametroAsync(GuardarParametroLineaProductoRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
    Task<Result<ParametroLineaProductoDto>> ActualizarParametroAsync(int parametroLineaProductoId, GuardarParametroLineaProductoRequest request, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}
