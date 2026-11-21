using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IProductoMaestroRepository : IRepository<ProductoMaestro>
{
    /// <summary>
    /// Productos activos que se pueden producir en la línea en esa fecha: los que tienen el parámetro
    /// línea-producto vigente (mayor VigenteDesde &lt;= fecha) y habilitado.
    /// </summary>
    Task<IEnumerable<ProductoMaestro>> GetByLineaIdAsync(int lineaId, DateOnly fecha, CancellationToken cancellationToken = default);
    Task<IEnumerable<ProductoMaestro>> GetActivosAsync(CancellationToken cancellationToken = default);
}
