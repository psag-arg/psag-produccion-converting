using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class MaquinistaTurnoRepository : RepositoryBase<MaquinistaTurno>, IMaquinistaTurnoRepository
{
    public MaquinistaTurnoRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<MaquinistaTurno?> GetVigenteAsync(int lineaId, int turnoId, DateOnly fechaTurno, CancellationToken cancellationToken = default)
    {
        // Pocos registros por turno: se ordena en memoria (en SQLite las fechas con offset no se ordenan en la base).
        var registros = await _dbSet.AsNoTracking()
            .Include(m => m.UsuarioLinea)
            .Where(m => m.LineaId == lineaId && m.TurnoId == turnoId && m.FechaTurno == fechaTurno)
            .ToListAsync(cancellationToken);
        return MaquinistaTurnoReglas.Vigente(registros);
    }

    public async Task<IReadOnlyList<MaquinistaTurno>> GetDelDiaAsync(DateOnly fechaTurno, IReadOnlyCollection<int>? lineasIds = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.AsNoTracking()
            .Include(m => m.UsuarioLinea)
            .Where(m => m.FechaTurno == fechaTurno);
        if (lineasIds != null)
            query = query.Where(m => lineasIds.Contains(m.LineaId));
        var lista = await query.ToListAsync(cancellationToken);
        return lista.OrderBy(m => m.FechaHora).ThenBy(m => m.MaquinistaTurnoId).ToList();
    }
}
