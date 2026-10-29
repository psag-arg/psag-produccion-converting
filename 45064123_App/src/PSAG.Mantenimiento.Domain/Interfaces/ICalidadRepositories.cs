using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IPuntoControlCalidadRepository : IRepository<PuntoControlCalidad>
{
    /// <summary>
    /// Con la línea, ordenados por línea y nombre. Sólo lectura.
    /// </summary>
    Task<IReadOnlyList<PuntoControlCalidad>> GetListadoAsync(
        int? lineaId = null,
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Por código (ya normalizado en mayúsculas), con la línea.
    /// </summary>
    Task<PuntoControlCalidad?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
}

public interface ITipoDefectoCalidadRepository : IRepository<TipoDefectoCalidad>
{
    Task<IReadOnlyList<TipoDefectoCalidad>> GetListadoAsync(
        bool incluirInactivos = false,
        CancellationToken cancellationToken = default);

    Task<TipoDefectoCalidad?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
}

/// <summary>
/// Filtros de la grilla de calidad. Las fechas son instantes (el servicio convierte los días de planta).
/// </summary>
public sealed record FiltroRegistrosCalidad(
    DateTimeOffset? Desde = null,
    DateTimeOffset? Hasta = null,
    int? TurnoId = null,
    int? LineaId = null,
    int? PuntoControlCalidadId = null,
    int? TipoDefectoCalidadId = null,
    EstadoRevisionCalidad? EstadoRevision = null,
    OrigenRegistroCalidad? Origen = null);

public interface IRegistroCalidadRepository : IRepository<RegistroCalidad>
{
    /// <summary>
    /// Con línea, punto, turno, defecto, producto, detención y revisiones; con seguimiento (para revisar).
    /// </summary>
    Task<RegistroCalidad?> GetByIdConDetalleAsync(long registroCalidadId, CancellationToken cancellationToken = default);

    Task<RegistroCalidad?> GetByIdentificadorExternoAsync(string identificadorExterno, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grilla paginada, de la detección más reciente a la más vieja. Sólo lectura.
    /// </summary>
    Task<(IReadOnlyList<RegistroCalidad> Items, int TotalCount)> GetPaginadoAsync(
        FiltroRegistrosCalidad filtro,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
