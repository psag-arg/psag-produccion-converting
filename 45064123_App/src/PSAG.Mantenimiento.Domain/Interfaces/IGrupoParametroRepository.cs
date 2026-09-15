using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IGrupoParametroRepository : IRepository<GrupoParametro>
{
    /// <summary>
    /// Obtiene los grupos de parámetros aplicables a una línea.
    /// Incluye los globales (LineaId = null) y los específicos de la línea.
    /// </summary>
    Task<IEnumerable<GrupoParametro>> GetByLineaIdAsync(int? lineaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un grupo con todos sus parámetros.
    /// </summary>
    Task<GrupoParametro?> GetByIdWithParametrosAsync(int grupoParametroId, CancellationToken cancellationToken = default);

    Task<IEnumerable<GrupoParametro>> GetActivosAsync(CancellationToken cancellationToken = default);
}
