using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class ProductoSkuRepository : RepositoryBase<ProductoSku>, IProductoSkuRepository
{
    public ProductoSkuRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ProductoSku>> GetPorMaestrosAsync(IReadOnlyCollection<int>? productoMaestroIds, CancellationToken cancellationToken = default)
    {
        var query = _context.ProductosSku.AsNoTracking().Include(s => s.SkuContenido).AsQueryable();
        if (productoMaestroIds != null)
            query = query.Where(s => productoMaestroIds.Contains(s.ProductoMaestroId));
        return await query.OrderBy(s => s.TipoSku).ThenBy(s => s.CodigoSku).ToListAsync(cancellationToken);
    }
}

public class ParametroLineaProductoRepository : RepositoryBase<ParametroLineaProducto>, IParametroLineaProductoRepository
{
    public ParametroLineaProductoRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<ParametroLineaProducto>> GetListadoAsync(int? lineaId, CancellationToken cancellationToken = default)
    {
        var query = _context.ParametrosLineaProducto.AsNoTracking()
            .Include(p => p.Linea)
            .Include(p => p.ProductoMaestro)
            .AsQueryable();
        if (lineaId is { } id)
            query = query.Where(p => p.LineaId == id);
        return await query.ToListAsync(cancellationToken);
    }
}
