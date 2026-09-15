using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class HallazgoNoOkRepository : IHallazgoNoOkRepository
{
    private readonly MantenimientoDbContext _context;

    public HallazgoNoOkRepository(MantenimientoDbContext context)
    {
        _context = context;
    }

    public async Task<HallazgoNoOk?> GetByIdAsync(long hallazgoNoOkId, CancellationToken cancellationToken = default)
    {
        return await _context.HallazgosNoOk
            .Include(h => h.Linea)
            .Include(h => h.Equipo)
            .FirstOrDefaultAsync(h => h.HallazgoNoOkId == hallazgoNoOkId, cancellationToken);
    }

    public async Task<HallazgoNoOk?> GetByIdWithDetallesAsync(long hallazgoNoOkId, CancellationToken cancellationToken = default)
    {
        return await _context.HallazgosNoOk
            .Include(h => h.Linea)
            .Include(h => h.Equipo)
            .Include(h => h.EjecucionInspeccionDetalle)
                .ThenInclude(d => d.ItemPlanInspeccion)
            .Include(h => h.EjecucionInspeccionDetalle)
                .ThenInclude(d => d.EjecucionInspeccion)
                    .ThenInclude(e => e.PlanInspeccion)
            .FirstOrDefaultAsync(h => h.HallazgoNoOkId == hallazgoNoOkId, cancellationToken);
    }

    public async Task<IReadOnlyList<HallazgoNoOk>> GetPendientesByLineaAsync(
        int lineaId,
        CancellationToken cancellationToken = default)
    {
        return await _context.HallazgosNoOk
            .Include(h => h.Linea)
            .Include(h => h.Equipo)
            .Include(h => h.EjecucionInspeccionDetalle)
                .ThenInclude(d => d.ItemPlanInspeccion)
            .Where(h => h.LineaId == lineaId && !h.Resuelto)
            .OrderByDescending(h => h.FechaHoraHallazgo)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<HallazgoNoOk>> GetAllPendientesAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.HallazgosNoOk
            .Include(h => h.Linea)
            .Include(h => h.Equipo)
            .Include(h => h.EjecucionInspeccionDetalle)
                .ThenInclude(d => d.ItemPlanInspeccion)
            .Where(h => !h.Resuelto)
            .OrderByDescending(h => h.FechaHoraHallazgo)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<HallazgoNoOk> Items, int TotalCount)> GetPaginadoAsync(
        int page,
        int pageSize,
        int? lineaId = null,
        int? equipoId = null,
        bool? soloPendientes = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.HallazgosNoOk
            .Include(h => h.Linea)
            .Include(h => h.Equipo)
            .Include(h => h.EjecucionInspeccionDetalle)
                .ThenInclude(d => d.ItemPlanInspeccion)
            .AsQueryable();

        if (lineaId.HasValue)
            query = query.Where(h => h.LineaId == lineaId.Value);

        if (equipoId.HasValue)
            query = query.Where(h => h.EquipoId == equipoId.Value);

        if (soloPendientes == true)
            query = query.Where(h => !h.Resuelto);
        else if (soloPendientes == false)
            query = query.Where(h => h.Resuelto);

        if (fechaDesde.HasValue)
        {
            var fechaDesdeOffset = new DateTimeOffset(fechaDesde.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(h => h.FechaHoraHallazgo >= fechaDesdeOffset);
        }

        if (fechaHasta.HasValue)
        {
            var fechaHastaOffset = new DateTimeOffset(fechaHasta.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(h => h.FechaHoraHallazgo <= fechaHastaOffset);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(h => h.FechaHoraHallazgo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(HallazgoNoOk hallazgo, CancellationToken cancellationToken = default)
    {
        await _context.HallazgosNoOk.AddAsync(hallazgo, cancellationToken);
    }
}
