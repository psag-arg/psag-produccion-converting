using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class TiempoProgramadoDiaRepository : RepositoryBase<TiempoProgramadoDia>, ITiempoProgramadoDiaRepository
{
    public TiempoProgramadoDiaRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TiempoProgramadoDia>> GetPorLineaYRangoAsync(
        int lineaId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.LineaId == lineaId && t.Fecha >= desde && t.Fecha <= hasta)
            .OrderBy(t => t.Fecha)
            .ToListAsync(cancellationToken);
    }
}
