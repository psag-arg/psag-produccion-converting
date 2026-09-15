using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IEstandarParametroRepository : IRepository<EstandarParametro>
{
    /// <summary>
    /// Obtiene los estándares de parámetros para una línea y producto.
    /// </summary>
    Task<IEnumerable<EstandarParametro>> GetByLineaProductoAsync(
        int lineaId,
        int productoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene el estándar de un parámetro específico para una línea y producto.
    /// </summary>
    Task<EstandarParametro?> GetByLineaProductoParametroAsync(
        int lineaId,
        int productoId,
        int parametroId,
        CancellationToken cancellationToken = default);
}
