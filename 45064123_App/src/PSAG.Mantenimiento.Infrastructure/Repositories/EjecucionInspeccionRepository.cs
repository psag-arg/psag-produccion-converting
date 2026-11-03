using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class EjecucionInspeccionRepository : IEjecucionInspeccionRepository
{
    private readonly MantenimientoDbContext _context;

    public EjecucionInspeccionRepository(MantenimientoDbContext context)
    {
        _context = context;
    }

    public async Task<EjecucionInspeccion?> GetByIdAsync(long ejecucionInspeccionId, CancellationToken cancellationToken = default)
    {
        return await _context.EjecucionesInspeccion
            .Include(e => e.PlanInspeccion)
                .ThenInclude(p => p.Linea)
            .Include(e => e.PlanInspeccion)
                .ThenInclude(p => p.Equipo)
            .Include(e => e.Turno)
            .FirstOrDefaultAsync(e => e.EjecucionInspeccionId == ejecucionInspeccionId, cancellationToken);
    }

    public async Task<EjecucionInspeccion?> GetByIdWithDetallesAsync(long ejecucionInspeccionId, CancellationToken cancellationToken = default)
    {
        return await _context.EjecucionesInspeccion
            .Include(e => e.PlanInspeccion)
                .ThenInclude(p => p.Linea)
            .Include(e => e.PlanInspeccion)
                .ThenInclude(p => p.Equipo)
            .Include(e => e.PlanInspeccion)
                .ThenInclude(p => p.Items.Where(i => i.Activo).OrderBy(i => i.Orden))
            .Include(e => e.Turno)
            .Include(e => e.Detalles)
                .ThenInclude(d => d.ItemPlanInspeccion)
            .FirstOrDefaultAsync(e => e.EjecucionInspeccionId == ejecucionInspeccionId, cancellationToken);
    }

    public async Task<EjecucionInspeccion?> GetUltimaEjecucionAsync(
        int planInspeccionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EjecucionesInspeccion
            .Where(e => e.PlanInspeccionId == planInspeccionId && e.Completada)
            .OrderByDescending(e => e.Fecha)
            .ThenByDescending(e => e.FechaHoraFin)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EjecucionInspeccion>> GetByPlanAsync(
        int planInspeccionId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EjecucionesInspeccion
            .Include(e => e.Turno)
            .Where(e => e.PlanInspeccionId == planInspeccionId)
            .OrderByDescending(e => e.Fecha)
            .ThenByDescending(e => e.FechaHoraInicio)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EjecucionInspeccion>> GetByLineaYFechasAsync(
        int lineaId,
        DateOnly fechaDesde,
        DateOnly fechaHasta,
        CancellationToken cancellationToken = default)
    {
        return await _context.EjecucionesInspeccion
            .Include(e => e.PlanInspeccion)
                .ThenInclude(p => p.Equipo)
            .Include(e => e.Turno)
            .Where(e => e.PlanInspeccion.LineaId == lineaId &&
                        e.Fecha >= fechaDesde &&
                        e.Fecha <= fechaHasta)
            .OrderByDescending(e => e.Fecha)
            .ThenByDescending(e => e.FechaHoraInicio)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<EjecucionInspeccion> Items, int TotalCount)> GetPaginadoAsync(
        int page,
        int pageSize,
        int? lineaId = null,
        int? planInspeccionId = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        bool? soloConNoOk = null,
        CancellationToken cancellationToken = default,
        int? equipoId = null,
        TipoPuesto? puesto = null,
        int? frecuenciaDias = null,
        bool? completada = null)
    {
        var query = _context.EjecucionesInspeccion
            .Include(e => e.PlanInspeccion)
                .ThenInclude(p => p.Linea)
            .Include(e => e.PlanInspeccion)
                .ThenInclude(p => p.Equipo)
            .Include(e => e.Turno)
            .AsQueryable();

        if (lineaId.HasValue)
            query = query.Where(e => e.PlanInspeccion.LineaId == lineaId.Value);

        if (planInspeccionId.HasValue)
            query = query.Where(e => e.PlanInspeccionId == planInspeccionId.Value);

        if (fechaDesde.HasValue)
            query = query.Where(e => e.Fecha >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(e => e.Fecha <= fechaHasta.Value);

        if (soloConNoOk == true)
            query = query.Where(e => e.CantidadNoOk > 0);

        if (equipoId.HasValue)
            query = query.Where(e => e.PlanInspeccion.EquipoId == equipoId.Value);

        // "Quién realiza": la inspección de ese puesto, o una de todos los puntos que tenga puntos de ese puesto.
        if (puesto.HasValue)
            query = query.Where(e => e.Puesto == puesto.Value
                || (e.Puesto == null && e.PlanInspeccion.Items.Any(i => i.Activo && i.QuienRealiza == puesto.Value)));

        // Frecuencia: la inspección tiene puntos con esa frecuencia (del puesto con que se hizo).
        if (frecuenciaDias.HasValue)
            query = query.Where(e => e.PlanInspeccion.Items.Any(i => i.Activo && i.FrecuenciaDias == frecuenciaDias.Value
                && (e.Puesto == null || i.QuienRealiza == e.Puesto)));

        if (completada.HasValue)
            query = query.Where(e => e.Completada == completada.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.Fecha)
            .ThenByDescending(e => e.FechaHoraInicio)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<EjecucionInspeccion?> GetByPlanFechaPuestoAsync(int planInspeccionId, DateOnly fecha, TipoPuesto? puesto, CancellationToken cancellationToken = default)
    {
        return await _context.EjecucionesInspeccion
            .Include(e => e.Turno)
            .Where(e => e.PlanInspeccionId == planInspeccionId && e.Fecha == fecha && e.Puesto == puesto)
            .OrderByDescending(e => e.EjecucionInspeccionId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Dictionary<int, DateOnly>> GetUltimasRespuestasPorItemAsync(int planInspeccionId, DateOnly antesDe, CancellationToken cancellationToken = default)
    {
        var filas = await _context.EjecucionesInspeccionDetalle
            .Where(d => d.EjecucionInspeccion.PlanInspeccionId == planInspeccionId && d.EjecucionInspeccion.Fecha < antesDe)
            .Select(d => new { d.ItemPlanInspeccionId, d.EjecucionInspeccion.Fecha })
            .ToListAsync(cancellationToken);

        return filas
            .GroupBy(f => f.ItemPlanInspeccionId)
            .ToDictionary(g => g.Key, g => g.Max(f => f.Fecha));
    }

    public async Task AddAsync(EjecucionInspeccion ejecucion, CancellationToken cancellationToken = default)
    {
        await _context.EjecucionesInspeccion.AddAsync(ejecucion, cancellationToken);
    }
}
