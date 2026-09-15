using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IDetalleFallaRepository : IRepository<DetalleFalla>
{
    Task<IEnumerable<DetalleFalla>> GetActivosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene detalles de falla filtrados por equipo y/o tipo de falla.
    /// Incluye genéricos (EquipoId = NULL y/o TipoFallaId = NULL).
    /// </summary>
    Task<IEnumerable<DetalleFalla>> GetByFiltroAsync(
        int? equipoId,
        int? tipoFallaId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Valida que el detalle de falla sea coherente con el equipo y tipo de falla.
    /// </summary>
    Task<bool> EsCoherenteAsync(
        int detalleFallaId,
        int? equipoId,
        int? tipoFallaId,
        CancellationToken cancellationToken = default);
}
