using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

/// <summary>SKU de los productos maestro (ABM_ProductosSku, T-798 etapa 6: ABM desde Maestros → Productos).</summary>
public interface IProductoSkuRepository : IRepository<ProductoSku>
{
    /// <summary>SKU de los maestros indicados (todos si es null), con el SKU contenido cargado.</summary>
    Task<IReadOnlyList<ProductoSku>> GetPorMaestrosAsync(IReadOnlyCollection<int>? productoMaestroIds, CancellationToken cancellationToken = default);
}

/// <summary>Parametrización línea-producto (PRD_ParametrosLineaProducto, T-798 etapa 6: ABM del presupuesto).</summary>
public interface IParametroLineaProductoRepository : IRepository<ParametroLineaProducto>
{
    /// <summary>Todas las vigencias (de una línea o de todas), con la línea y el maestro cargados.</summary>
    Task<IReadOnlyList<ParametroLineaProducto>> GetListadoAsync(int? lineaId, CancellationToken cancellationToken = default);
}
