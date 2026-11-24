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

    public async Task<IReadOnlyList<TurnoProduccion>> GetAbiertosDeOperadorAsync(string usuarioId, CancellationToken cancellationToken = default) =>
        await _dbSet
            .AsNoTracking()
            .Include(t => t.Linea)
            .Include(t => t.Turno)
            .Where(t => t.Estado == EstadoTurnoProduccion.ABIERTO
                        && t.Operadores.Any(o => o.OperadorUsuarioId == usuarioId && o.Hasta == null && o.AnuladoPorId == null))
            .OrderByDescending(t => t.InicioProgramado)
            .ToListAsync(cancellationToken);

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

    public async Task<IReadOnlyList<TurnoProduccion>> GetCerradosConSesionDeOperadorAsync(string usuarioId, CancellationToken cancellationToken = default) =>
        await _dbSet
            .AsNoTracking()
            .Include(t => t.Linea)
            .Include(t => t.Turno)
            .Where(t => (t.Estado == EstadoTurnoProduccion.CERRADO || t.Estado == EstadoTurnoProduccion.CERRADO_AUTOMATICO)
                        && t.Operadores.Any(o => o.OperadorUsuarioId == usuarioId && o.Hasta == null && o.AnuladoPorId == null))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TurnoProduccion>> GetCerradosConOperadorVigenteAsync(CancellationToken cancellationToken = default) =>
        await _dbSet
            .Include(t => t.Turno)
            .Include(t => t.Operadores)
            .AsSplitQuery()
            .Where(t => (t.Estado == EstadoTurnoProduccion.CERRADO || t.Estado == EstadoTurnoProduccion.CERRADO_AUTOMATICO)
                        && t.Operadores.Any(o => o.Hasta == null && o.AnuladoPorId == null))
            .ToListAsync(cancellationToken);

    public async Task<TurnoProduccion?> GetAnteriorDeLineaAsync(
        int lineaId, DateOnly fechaProduccion, DateTimeOffset antesDe, int diasAtras, CancellationToken cancellationToken = default)
    {
        var desde = fechaProduccion.AddDays(-Math.Max(0, diasAtras));
        var candidatos = await ConDetalle()
            .Where(t => t.LineaId == lineaId && t.FechaProduccion >= desde && t.FechaProduccion <= fechaProduccion)
            .ToListAsync(cancellationToken);
        // En memoria: SQLite (tests) no ordena ni compara DateTimeOffset en la base.
        return candidatos.Where(t => t.InicioProgramado < antesDe).OrderByDescending(t => t.InicioProgramado).FirstOrDefault();
    }
}
