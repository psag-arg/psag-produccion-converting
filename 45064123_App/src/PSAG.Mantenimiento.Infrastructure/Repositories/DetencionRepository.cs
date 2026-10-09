using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
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

    public async Task<IEnumerable<Detencion>> GetAnalisisAbiertosAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Where(d => d.RequiereAnalisis && d.EstadoActual != EstadoDetencion.CERRADA)
            .OrderBy(d => d.Fecha)
            .ThenBy(d => d.DetencionId)
            .ToListAsync(cancellationToken);
    }

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
        var query = _dbSet
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
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
            query = query.Where(d => d.Descripcion.ToLower().Contains(buscada)
                || (d.AccionesRealizadas != null && d.AccionesRealizadas.ToLower().Contains(buscada))
                || (d.CausaFalla != null && d.CausaFalla.ToLower().Contains(buscada)));
        }

        return await query
            .OrderByDescending(d => d.Fecha)
            .ThenByDescending(d => d.DetencionId)
            .Take(limite)
            .ToListAsync(cancellationToken);
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
        string? ordenarPor = null,
        bool ordenDescendente = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .Include(d => d.Linea)
            .Include(d => d.Equipo)
            .Include(d => d.TipoFalla)
            .Include(d => d.DetalleFalla)
            .Include(d => d.Turno)
            .AsQueryable();

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
