using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class TareaMantenimientoRepository : ITareaMantenimientoRepository
{
    private readonly MantenimientoDbContext _context;

    public TareaMantenimientoRepository(MantenimientoDbContext context)
    {
        _context = context;
    }

    public async Task<TareaMantenimiento?> GetByIdAsync(long tareaMantenimientoId, CancellationToken cancellationToken = default)
    {
        return await ConRelaciones()
            .FirstOrDefaultAsync(t => t.TareaMantenimientoId == tareaMantenimientoId, cancellationToken);
    }

    public async Task<IReadOnlyList<TareaMantenimiento>> GetByIdsAsync(
        IReadOnlyCollection<long> tareaMantenimientoIds,
        CancellationToken cancellationToken = default)
    {
        return await ConRelaciones()
            .Where(t => tareaMantenimientoIds.Contains(t.TareaMantenimientoId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TareaMantenimiento>> GetActivasAsync(
        int? lineaId = null,
        CriticidadTarea? criticidad = null,
        EstadoTareaMantenimiento? estado = null,
        CancellationToken cancellationToken = default)
    {
        var query = ConRelaciones()
            .Where(t => t.Estado != EstadoTareaMantenimiento.CERRADA);

        if (lineaId.HasValue)
            query = query.Where(t => t.LineaId == lineaId.Value);
        if (criticidad.HasValue)
            query = query.Where(t => t.Criticidad == criticidad.Value);
        if (estado.HasValue)
            query = query.Where(t => t.Estado == estado.Value);

        // La criticidad se guarda como texto: el orden alfabético no sirve, se ordena con un CASE.
        return await query
            .OrderBy(t => t.Criticidad == CriticidadTarea.ALTA ? 0 : t.Criticidad == CriticidadTarea.MEDIA ? 1 : 2)
            .ThenBy(t => t.TareaMantenimientoId)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<TareaMantenimiento> Items, int TotalCount)> GetCerradasPaginadoAsync(
        int page,
        int pageSize,
        int? lineaId = null,
        CriticidadTarea? criticidad = null,
        TipoTareaMantenimiento? tipoTarea = null,
        int? equipoId = null,
        DateTimeOffset? cierreDesde = null,
        DateTimeOffset? cierreHasta = null,
        CancellationToken cancellationToken = default)
    {
        var query = ConRelaciones()
            .Where(t => t.Estado == EstadoTareaMantenimiento.CERRADA);

        if (lineaId.HasValue)
            query = query.Where(t => t.LineaId == lineaId.Value);
        if (criticidad.HasValue)
            query = query.Where(t => t.Criticidad == criticidad.Value);
        if (tipoTarea.HasValue)
            query = query.Where(t => t.TipoTarea == tipoTarea.Value);
        if (equipoId.HasValue)
            query = query.Where(t => t.EquipoId == equipoId.Value);
        if (cierreDesde.HasValue)
            query = query.Where(t => t.FechaHoraCierre >= cierreDesde.Value);
        if (cierreHasta.HasValue)
            query = query.Where(t => t.FechaHoraCierre < cierreHasta.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.FechaHoraCierre)
            .ThenByDescending(t => t.TareaMantenimientoId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task AddAsync(TareaMantenimiento tarea, CancellationToken cancellationToken = default)
    {
        await _context.TareasMantenimiento.AddAsync(tarea, cancellationToken);
    }

    private IQueryable<TareaMantenimiento> ConRelaciones() =>
        _context.TareasMantenimiento
            .Include(t => t.Linea)
            .Include(t => t.Equipo);
}
