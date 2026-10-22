using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Interfaces;

public interface IParametroRepository : IRepository<Parametro>
{
    Task<IEnumerable<Parametro>> GetByGrupoIdAsync(int grupoParametroId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Parametro>> GetActivosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Plantilla de Control de Proceso de una línea (T-545): puntos activos de los grupos activos
    /// asignados a esa línea (LineaId = lineaId; los grupos globales no entran), con su grupo.
    /// </summary>
    Task<IEnumerable<Parametro>> GetPlantillaLineaAsync(int lineaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cantidad de puntos (activos o no) de los grupos de una línea. Sirve para numerar códigos.
    /// </summary>
    Task<int> CountByLineaAsync(int lineaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indica si el punto ya tiene valores cargados en algún control de proceso.
    /// </summary>
    Task<bool> TieneCargasAsync(int parametroId, CancellationToken cancellationToken = default);
}
