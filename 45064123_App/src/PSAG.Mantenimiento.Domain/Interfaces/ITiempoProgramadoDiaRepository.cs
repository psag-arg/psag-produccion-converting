using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface ITiempoProgramadoDiaRepository : IRepository<TiempoProgramadoDia>
{
    /// <summary>
    /// Excepciones de una línea dentro del rango (ambos extremos incluidos).
    /// </summary>
    Task<IEnumerable<TiempoProgramadoDia>> GetPorLineaYRangoAsync(
        int lineaId,
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancellationToken = default);
}
