using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class CalendarioLineaRepository : RepositoryBase<CalendarioLinea>, ICalendarioLineaRepository
{
    public CalendarioLineaRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<CalendarioLinea>> GetActivosAsync(int? lineaId = null, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(c => c.Turnos)
            .Where(c => c.Activo && (!lineaId.HasValue || c.LineaId == lineaId.Value))
            .OrderBy(c => c.LineaId)
            .ThenBy(c => c.VigenteDesde)
            .ToListAsync(cancellationToken);
    }
}
