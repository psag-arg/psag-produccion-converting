using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IProductoMaestroRepository : IRepository<ProductoMaestro>
{
    /// <summary>
    /// Productos activos que se pueden producir en la línea en esa fecha: los que tienen el parámetro
    /// línea-producto vigente (mayor VigenteDesde &lt;= fecha) y habilitado.
    /// </summary>
    Task<IEnumerable<ProductoMaestro>> GetByLineaIdAsync(int lineaId, DateOnly fecha, CancellationToken cancellationToken = default);
    /// <summary>
    /// Todas las vigencias de la parametrización línea-producto de la línea (con el maestro), para elegir la
    /// vigente con <see cref="Services.ParametroLineaProductoVigencia"/>.
    /// </summary>
    Task<IReadOnlyList<ParametroLineaProducto>> GetParametrosLineaAsync(int lineaId, CancellationToken cancellationToken = default);

    Task<IEnumerable<ProductoMaestro>> GetActivosAsync(CancellationToken cancellationToken = default);
}
