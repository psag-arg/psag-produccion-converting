using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class TipoFallaRepository : RepositoryBase<TipoFalla>, ITipoFallaRepository
{
    public TipoFallaRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<TipoFalla>> GetActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.Activo)
            .OrderBy(t => t.Orden)
            .ThenBy(t => t.Nombre)
            .ToListAsync(cancellationToken);
    }
}
