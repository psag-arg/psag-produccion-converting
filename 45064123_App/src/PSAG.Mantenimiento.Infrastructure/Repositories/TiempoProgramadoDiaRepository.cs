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

    public async Task<IEnumerable<TiempoProgramadoDia>> GetPorRangoAsync(
        DateOnly desde,
        DateOnly hasta,
        int? lineaId = null,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => (!lineaId.HasValue || t.LineaId == lineaId.Value) && t.Fecha >= desde && t.Fecha <= hasta)
            .OrderBy(t => t.Fecha)
            .ToListAsync(cancellationToken);
    }
}
