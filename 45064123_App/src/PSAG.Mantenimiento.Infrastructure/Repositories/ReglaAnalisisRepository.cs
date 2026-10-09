using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class ReglaAnalisisRepository : RepositoryBase<ReglaAnalisis>, IReglaAnalisisRepository
{
    public ReglaAnalisisRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<ReglaAnalisis>> GetActivasAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(r => r.Activo)
            .OrderBy(r => r.ReglaAnalisisId)
            .ToListAsync(cancellationToken);
    }
}
