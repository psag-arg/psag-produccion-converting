using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Infrastructure.Repositories;

public class DetencionRepository : RepositoryBase<Detencion>, IDetencionRepository
{
    public DetencionRepository(MantenimientoDbContext context) : base(context)
    {
    }

    public async Task<Detencion?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Include(d => d.DetalleFalla)
            .Include(d => d.Turno)
            .FirstOrDefaultAsync(d => d.Codigo == codigo, cancellationToken);
    }

    public async Task<Detencion?> GetByIdIncluyendoEliminadasAsync(long detencionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.DetencionId == detencionId, cancellationToken);
    }

    public async Task<Detencion?> GetByIdWithDetailsAsync(long detencionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Include(d => d.DetalleFalla)
            .Include(d => d.Turno)
            .Include(d => d.HistorialEstados.OrderByDescending(h => h.FechaHora))
            .Include(d => d.Notas.OrderByDescending(n => n.FechaHora))
            .Include(d => d.CambiosAnalisis.OrderByDescending(c => c.FechaHora))
            .Include(d => d.AdjuntosAnalisis.OrderBy(a => a.AnalisisAdjuntoId))
            .AsSplitQuery()
            .FirstOrDefaultAsync(d => d.DetencionId == detencionId, cancellationToken);
    }

    public async Task<IEnumerable<Detencion>> GetByLineaAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Include(d => d.Turno)
            .Where(d => d.LineaId == lineaId)
            .OrderByDescending(d => d.Fecha)
            .ThenByDescending(d => d.FechaHoraCreacion)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Detencion>> GetByEstadoAsync(EstadoDetencion estado, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Include(d => d.Turno)
            .Where(d => d.EstadoActual == estado)
            .OrderByDescending(d => d.Fecha)
            .ThenByDescending(d => d.FechaHoraCreacion)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Detencion>> GetDetencionesAbiertasAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Include(d => d.Turno)
            .Where(d => d.EstadoActual != EstadoDetencion.CERRADA)
            .OrderByDescending(d => d.Fecha)
            .ThenByDescending(d => d.FechaHoraCreacion)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Detencion>> GetDetencionesPorFechaAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Include(d => d.DetalleFalla)
            .Include(d => d.Turno)
            .Where(d => d.Fecha >= desde && d.Fecha <= hasta)
            .OrderByDescending(d => d.Fecha)
            .ThenByDescending(d => d.FechaHoraCreacion)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Detencion>> GetDetencionesTurnoActualAsync(
        int lineaId,
        int turnoId,
        DateOnly fecha,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Include(d => d.DetalleFalla)
            .Include(d => d.Turno)
            .Where(d => d.LineaId == lineaId && d.TurnoId == turnoId && d.Fecha == fecha)
            .OrderByDescending(d => d.FechaHoraCreacion)
            .ToListAsync(cancellationToken);
    }

    public async Task<string> GenerarCodigoDetencionAsync(CancellationToken cancellationToken = default)
    {
        var fecha = DateTimeOffset.UtcNow.ToString("yyyyMMdd");

        // IgnoreQueryFilters: el código es único en la tabla, también entre eliminadas.
        var ultimoCodigo = await _dbSet
            .IgnoreQueryFilters()
            .Where(d => d.Codigo.StartsWith($"DET-{fecha}"))
            .OrderByDescending(d => d.Codigo)
            .Select(d => d.Codigo)
            .FirstOrDefaultAsync(cancellationToken);

        int siguienteNumero = 1;
        if (!string.IsNullOrEmpty(ultimoCodigo))
        {
            var partes = ultimoCodigo.Split('-');
            if (partes.Length == 3 && int.TryParse(partes[2], out int numero))
            {
                siguienteNumero = numero + 1;
            }
        }

        return $"DET-{fecha}-{siguienteNumero:D6}";
    }

    public async Task<IReadOnlyList<int>> GetLineasConDetencionesAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(d => d.Fecha >= desde && d.Fecha <= hasta)
            .Select(d => d.LineaId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Detencion>> GetAnalisisAbiertosAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Where(AnalisisFallaReglas.EstaAbiertoExpr)
            .OrderBy(d => d.Fecha)
            .ThenBy(d => d.DetencionId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Detencion>> GetAnalisisAbiertosAsignadosAAsync(
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        return await AnalisisAbiertosAsignadosA(usuarioId, roles)
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .OrderBy(d => d.Fecha)
            .ThenBy(d => d.DetencionId)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAnalisisAbiertosParaAsync(
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        return AnalisisAbiertosAsignadosA(usuarioId, roles).CountAsync(cancellationToken);
    }

    /// <summary>
    /// Lista y conteo salen de acá: la misma consulta no puede divergir.
    /// </summary>
    private IQueryable<Detencion> AnalisisAbiertosAsignadosA(string usuarioId, IReadOnlyCollection<string> roles) =>
        _dbSet
            .Where(AnalisisFallaReglas.EstaAbiertoExpr)
            .Where(AnalisisFallaReglas.AsignadaAExpr(usuarioId, roles));

    public async Task<IEnumerable<Detencion>> GetAnalisisCerradosPorUsuarioAsync(
        string usuarioId,
        int limite,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Where(d => d.RequiereAnalisis
                && d.EstadoActual == EstadoDetencion.CERRADA
                && d.AnalisisPorUsuarioId == usuarioId)
            .OrderByDescending(d => d.FechaHoraAnalisis)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Detencion>> GetSolucionesAsync(
        string? busqueda = null,
        int? lineaId = null,
        int? equipoId = null,
        int? tipoFallaId = null,
        long? excluirDetencionId = null,
        int limite = 50,
        CancellationToken cancellationToken = default)
    {
        return await QuerySoluciones(busqueda, lineaId, equipoId, tipoFallaId, excluirDetencionId)
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Include(d => d.AdjuntosAnalisis.OrderBy(a => a.AnalisisAdjuntoId))
            .AsSplitQuery()
            .OrderByDescending(d => d.Fecha)
            .ThenByDescending(d => d.DetencionId)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ContarSolucionesAsync(
        string? busqueda = null,
        int? lineaId = null,
        int? equipoId = null,
        int? tipoFallaId = null,
        CancellationToken cancellationToken = default)
    {
        return await QuerySoluciones(busqueda, lineaId, equipoId, tipoFallaId, null).CountAsync(cancellationToken);
    }

    /// <summary>
    /// Fallas cerradas con solución o causa que coinciden con los filtros. Lista y conteo de la
    /// base de soluciones la usan juntos, así el contador no puede diferir de las fichas.
    /// </summary>
    private IQueryable<Detencion> QuerySoluciones(
        string? busqueda,
        int? lineaId,
        int? equipoId,
        int? tipoFallaId,
        long? excluirDetencionId)
    {
        var query = _dbSet
            .Where(d => d.EstadoActual == EstadoDetencion.CERRADA
                && ((d.AccionesRealizadas != null && d.AccionesRealizadas != "")
                    || (d.CausaFalla != null && d.CausaFalla != "")));

        if (lineaId.HasValue)
            query = query.Where(d => d.LineaId == lineaId.Value);

        if (equipoId.HasValue)
            query = query.Where(d => d.EquipoId == equipoId.Value);

        if (tipoFallaId.HasValue)
            query = query.Where(d => d.TipoFallaId == tipoFallaId.Value);

        if (excluirDetencionId.HasValue)
            query = query.Where(d => d.DetencionId != excluirDetencionId.Value);

        foreach (var palabra in (busqueda ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var buscada = palabra.ToLower();
            query = query.Where(d => d.Codigo.ToLower().Contains(buscada)
                || d.Descripcion.ToLower().Contains(buscada)
                || (d.AccionesRealizadas != null && d.AccionesRealizadas.ToLower().Contains(buscada))
                || (d.CausaFalla != null && d.CausaFalla.ToLower().Contains(buscada))
                || (d.PlanAccion != null && d.PlanAccion.ToLower().Contains(buscada)));
        }

        return query;
    }

    public async Task<(IEnumerable<Detencion> Items, int TotalCount)> GetPaginadoAsync(
        int page,
        int pageSize,
        int? lineaId = null,
        int? equipoId = null,
        EstadoDetencion? estado = null,
        int? tipoFallaId = null,
        int? detalleFallaId = null,
        int? turnoId = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        string? busqueda = null,
        bool? analisisPendiente = null,
        bool incluirEliminadas = false,
        string? ordenarPor = null,
        bool ordenDescendente = true,
        int? minutosMinimos = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Detencion> query = incluirEliminadas ? _dbSet.IgnoreQueryFilters() : _dbSet;
        query = query
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Include(d => d.DetalleFalla)
            .Include(d => d.Turno);

        // Aplicar filtros
        if (lineaId.HasValue)
            query = query.Where(d => d.LineaId == lineaId.Value);

        if (equipoId.HasValue)
            query = query.Where(d => d.EquipoId == equipoId.Value);

        if (estado.HasValue)
            query = query.Where(d => d.EstadoActual == estado.Value);

        if (tipoFallaId.HasValue)
            query = query.Where(d => d.TipoFallaId == tipoFallaId.Value);

        if (detalleFallaId.HasValue)
            query = query.Where(d => d.DetalleFallaId == detalleFallaId.Value);

        if (turnoId.HasValue)
            query = query.Where(d => d.TurnoId == turnoId.Value);

        if (fechaDesde.HasValue)
            query = query.Where(d => d.Fecha >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(d => d.Fecha <= fechaHasta.Value);

        if (minutosMinimos.HasValue)
            query = query.Where(d => d.MinutosEfectivos >= minutosMinimos.Value);

        if (analisisPendiente.HasValue)
        {
            var abierto = AnalisisFallaReglas.EstaAbiertoExpr;
            query = analisisPendiente.Value
                ? query.Where(abierto)
                : query.Where(Expression.Lambda<Func<Detencion, bool>>(Expression.Not(abierto.Body), abierto.Parameters));
        }

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var busquedaLower = busqueda.ToLower();
            query = query.Where(d =>
                d.Codigo.ToLower().Contains(busquedaLower) ||
                d.Descripcion.ToLower().Contains(busquedaLower) ||
                (d.ResolucionDescripcion != null && d.ResolucionDescripcion.ToLower().Contains(busquedaLower)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Aplicar ordenamiento
        query = ordenarPor?.ToLower() switch
        {
            "codigo" => ordenDescendente ? query.OrderByDescending(d => d.Codigo) : query.OrderBy(d => d.Codigo),
            "linea" => ordenDescendente ? query.OrderByDescending(d => d.Linea.Nombre) : query.OrderBy(d => d.Linea.Nombre),
            "estado" => ordenDescendente ? query.OrderByDescending(d => d.EstadoActual) : query.OrderBy(d => d.EstadoActual),
            "tipofalla" => ordenDescendente ? query.OrderByDescending(d => d.TipoFalla!.Nombre) : query.OrderBy(d => d.TipoFalla!.Nombre),
            "minutos" => ordenDescendente ? query.OrderByDescending(d => d.MinutosEfectivos) : query.OrderBy(d => d.MinutosEfectivos),
            _ => ordenDescendente
                ? query.OrderByDescending(d => d.Fecha).ThenByDescending(d => d.FechaHoraCreacion)
                : query.OrderBy(d => d.Fecha).ThenBy(d => d.FechaHoraCreacion)
        };

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
