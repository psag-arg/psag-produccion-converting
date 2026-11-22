using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class TurnoProduccionRepository : RepositoryBase<TurnoProduccion>, ITurnoProduccionRepository
{
    public TurnoProduccionRepository(MantenimientoDbContext context) : base(context)
    {
    }

    private IQueryable<TurnoProduccion> ConDetalle() => _dbSet
        .Include(t => t.Linea)
        .Include(t => t.Turno)
        .Include(t => t.Operadores)
        .Include(t => t.Productos).ThenInclude(p => p.ProductoMaestro)
        .Include(t => t.Productos).ThenInclude(p => p.Producciones)
        .AsSplitQuery();

    public Task<TurnoProduccion?> GetConDetalleAsync(long turnoProduccionId, CancellationToken cancellationToken = default) =>
        ConDetalle().FirstOrDefaultAsync(t => t.TurnoProduccionId == turnoProduccionId, cancellationToken);

    public Task<TurnoProduccion?> GetPorClaveAsync(int lineaId, int turnoId, DateOnly fechaProduccion, CancellationToken cancellationToken = default) =>
        ConDetalle().FirstOrDefaultAsync(t => t.LineaId == lineaId && t.TurnoId == turnoId && t.FechaProduccion == fechaProduccion, cancellationToken);

    public Task<TurnoProduccion?> GetAbiertoAsync(int lineaId, CancellationToken cancellationToken = default) =>
        ConDetalle().FirstOrDefaultAsync(t => t.LineaId == lineaId && t.Estado == EstadoTurnoProduccion.ABIERTO, cancellationToken);

    public async Task<IReadOnlyList<TurnoProduccion>> GetAbiertosAsync(CancellationToken cancellationToken = default) =>
        await _dbSet
            .Include(t => t.Operadores)
            .Include(t => t.Productos).ThenInclude(p => p.Producciones)
            .AsSplitQuery()
            .Where(t => t.Estado == EstadoTurnoProduccion.ABIERTO)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TurnoProduccion>> GetPorFechasAsync(
        DateOnly desde, DateOnly hasta, IReadOnlyCollection<int>? lineaIds, CancellationToken cancellationToken = default)
    {
        var query = ConDetalle().Where(t => t.FechaProduccion >= desde && t.FechaProduccion <= hasta);
        if (lineaIds != null)
            query = query.Where(t => lineaIds.Contains(t.LineaId));
        return await query.ToListAsync(cancellationToken);
    }
}
