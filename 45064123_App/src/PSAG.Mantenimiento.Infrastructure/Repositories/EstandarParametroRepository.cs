using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class EstandarParametroRepository : RepositoryBase<EstandarParametro>, IEstandarParametroRepository
{
    public EstandarParametroRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<EstandarParametro>> GetByLineaProductoAsync(
        int lineaId,
        int productoId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EstandaresParametro
            .Where(e => e.Activo && e.LineaId == lineaId && e.ProductoId == productoId)
            .Include(e => e.Parametro)
                .ThenInclude(p => p.GrupoParametro)
            .OrderBy(e => e.Parametro.GrupoParametro.Orden)
            .ThenBy(e => e.Parametro.Seccion)
            .ThenBy(e => e.Parametro.Orden)
            .ToListAsync(cancellationToken);
    }

    public async Task<EstandarParametro?> GetByLineaProductoParametroAsync(
        int lineaId,
        int productoId,
        int parametroId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EstandaresParametro
            .FirstOrDefaultAsync(e =>
                e.Activo &&
                e.LineaId == lineaId &&
                e.ProductoId == productoId &&
                e.ParametroId == parametroId,
                cancellationToken);
    }
}
