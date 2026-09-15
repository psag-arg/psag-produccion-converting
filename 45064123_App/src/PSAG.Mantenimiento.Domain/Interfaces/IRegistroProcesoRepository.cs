using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IRegistroProcesoRepository : IRepository<RegistroProceso>
{
    /// <summary>
    /// Obtiene un registro de proceso con todos sus detalles.
    /// </summary>
    Task<RegistroProceso?> GetByIdWithDetailsAsync(long registroProcesoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca un registro existente para la combinación línea + producto + turno + fecha.
    /// Un registro por combinación; si ya existe, se retoma.
    /// </summary>
    Task<RegistroProceso?> GetByLineaProductoTurnoFechaAsync(
        int lineaId,
        int productoId,
        int turnoId,
        DateOnly fecha,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene registros paginados con filtros.
    /// </summary>
    Task<(IEnumerable<RegistroProceso> Items, int TotalCount)> GetPaginadoAsync(
        int page,
        int pageSize,
        int? lineaId = null,
        int? productoId = null,
        int? turnoId = null,
        EstadoRegistroProceso? estado = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        string? ordenarPor = null,
        bool ordenDescendente = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los registros de una línea y producto en un rango de fechas.
    /// Para la consulta histórica.
    /// </summary>
    Task<IEnumerable<RegistroProceso>> GetHistoricoAsync(
        int lineaId,
        int productoId,
        DateOnly fechaDesde,
        DateOnly fechaHasta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta los desvíos por línea, producto y parámetro.
    /// Para el dashboard.
    /// </summary>
    Task<IEnumerable<(int LineaId, int ProductoId, int ParametroId, int CantidadDesvios)>> GetConteoDesviosAsync(
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        CancellationToken cancellationToken = default);
}
