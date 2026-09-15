using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class ProductoRepository : RepositoryBase<Producto>, IProductoRepository
{
    public ProductoRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Producto>> GetByLineaIdAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        return await _context.Productos
            .Where(p => p.Activo && p.LineasProductos.Any(lp => lp.LineaId == lineaId))
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.Nombre)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Producto>> GetActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Productos
            .Where(p => p.Activo)
            .OrderBy(p => p.Orden)
            .ThenBy(p => p.Nombre)
            .ToListAsync(cancellationToken);
    }
}
