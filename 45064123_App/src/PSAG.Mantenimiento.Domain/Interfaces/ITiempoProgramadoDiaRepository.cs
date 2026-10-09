using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface ITiempoProgramadoDiaRepository : IRepository<TiempoProgramadoDia>
{
    /// <summary>
    /// Excepciones dentro del rango (ambos extremos incluidos), de una línea o de todas si lineaId es null.
    /// </summary>
    Task<IEnumerable<TiempoProgramadoDia>> GetPorRangoAsync(
        DateOnly desde,
        DateOnly hasta,
        int? lineaId = null,
        CancellationToken cancellationToken = default);
}
