using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class PlanInspeccionRepository : IPlanInspeccionRepository
{
    private readonly MantenimientoDbContext _context;

    public PlanInspeccionRepository(MantenimientoDbContext context)
    {
        _context = context;
    }

    public async Task<PlanInspeccion?> GetByIdAsync(int planInspeccionId, CancellationToken cancellationToken = default)
    {
        return await _context.PlanesInspeccion
            .Include(p => p.Linea)
            .Include(p => p.Equipo)
            .FirstOrDefaultAsync(p => p.PlanInspeccionId == planInspeccionId, cancellationToken);
    }

    public async Task<PlanInspeccion?> GetByIdWithItemsAsync(int planInspeccionId, CancellationToken cancellationToken = default)
    {
        return await _context.PlanesInspeccion
            .Include(p => p.Linea)
            .Include(p => p.Equipo)
            .Include(p => p.Items.Where(i => i.Activo).OrderBy(i => i.Orden))
            .FirstOrDefaultAsync(p => p.PlanInspeccionId == planInspeccionId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlanInspeccion>> GetByLineaYPuestoAsync(
        int lineaId,
        TipoPuesto puesto,
        CancellationToken cancellationToken = default)
    {
        return await _context.PlanesInspeccion
            .Include(p => p.Linea)
            .Include(p => p.Equipo)
            .Include(p => p.Items.Where(i => i.Activo).OrderBy(i => i.Orden))
            .Where(p => p.LineaId == lineaId && p.PuestoResponsable == puesto && p.Activo)
            .OrderBy(p => p.Orden)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlanInspeccion>> GetByLineaAsync(
        int lineaId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PlanesInspeccion
            .Include(p => p.Linea)
            .Include(p => p.Equipo)
            .Where(p => p.LineaId == lineaId && p.Activo)
            .OrderBy(p => p.Orden)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlanInspeccion>> GetPendientesHoyAsync(
        int lineaId,
        TipoPuesto puesto,
        CancellationToken cancellationToken = default)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // Obtener planes activos para línea y puesto
        var planes = await _context.PlanesInspeccion
            .Include(p => p.Linea)
            .Include(p => p.Equipo)
            .Include(p => p.Items.Where(i => i.Activo).OrderBy(i => i.Orden))
            .Where(p => p.LineaId == lineaId && p.PuestoResponsable == puesto && p.Activo)
            .ToListAsync(cancellationToken);

        // Filtrar los que están pendientes según frecuencia
        var planesPendientes = new List<PlanInspeccion>();

        foreach (var plan in planes)
        {
            // Obtener última ejecución completa
            var ultimaEjecucion = await _context.EjecucionesInspeccion
                .Where(e => e.PlanInspeccionId == plan.PlanInspeccionId && e.Completada)
                .OrderByDescending(e => e.Fecha)
                .FirstOrDefaultAsync(cancellationToken);

            bool pendiente = false;

            if (ultimaEjecucion == null)
            {
                // Nunca ejecutado, está pendiente
                pendiente = true;
            }
            else
            {
                // Calcular si venció según frecuencia
                var diasDesdeUltima = hoy.DayNumber - ultimaEjecucion.Fecha.DayNumber;
                pendiente = diasDesdeUltima >= plan.FrecuenciaDias;
            }

            if (pendiente)
            {
                planesPendientes.Add(plan);
            }
        }

        return planesPendientes.OrderBy(p => p.Orden).ToList();
    }

    public async Task AddAsync(PlanInspeccion planInspeccion, CancellationToken cancellationToken = default)
    {
        await _context.PlanesInspeccion.AddAsync(planInspeccion, cancellationToken);
    }

    public async Task<IReadOnlyList<PlanInspeccion>> GetAllActivosAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PlanesInspeccion
            .Include(p => p.Linea)
            .Include(p => p.Equipo)
            .Where(p => p.Activo)
            .OrderBy(p => p.LineaId)
            .ThenBy(p => p.Orden)
            .ToListAsync(cancellationToken);
    }
}
