using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

/// <summary>Turnos de producción (PRD_TurnosProduccion) con sus tramos de operador y de producto.</summary>
public interface ITurnoProduccionRepository : IRepository<TurnoProduccion>
{
    /// <summary>El turno con línea, turno de planta, operadores y productos (con su maestro y su producción oficial).</summary>
    Task<TurnoProduccion?> GetConDetalleAsync(long turnoProduccionId, CancellationToken cancellationToken = default);

    /// <summary>El turno de esa línea, turno de planta y fecha de producción (clave única), con detalle.</summary>
    Task<TurnoProduccion?> GetPorClaveAsync(int lineaId, int turnoId, DateOnly fechaProduccion, CancellationToken cancellationToken = default);

    /// <summary>El turno ABIERTO de la línea (hay a lo sumo uno), con detalle.</summary>
    Task<TurnoProduccion?> GetAbiertoAsync(int lineaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// T-802: turnos ABIERTOS en los que el usuario es el operador vigente (tramo no anulado sin Hasta), con línea y turno
    /// de planta, sin tramos; el más reciente primero. Por regla hay a lo sumo uno.
    /// </summary>
    Task<IReadOnlyList<TurnoProduccion>> GetAbiertosDeOperadorAsync(string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Todos los turnos ABIERTOS (de todas las líneas), sin detalle.</summary>
    Task<IReadOnlyList<TurnoProduccion>> GetAbiertosAsync(CancellationToken cancellationToken = default);

    /// <summary>Turnos con fecha de producción en el rango (ambos incluidos), de las líneas indicadas (todas si es null), con detalle.</summary>
    Task<IReadOnlyList<TurnoProduccion>> GetPorFechasAsync(DateOnly desde, DateOnly hasta, IReadOnlyCollection<int>? lineaIds, CancellationToken cancellationToken = default);
}
