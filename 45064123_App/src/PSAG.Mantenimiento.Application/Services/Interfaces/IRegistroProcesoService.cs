using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Proceso;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

public interface IRegistroProcesoService
{
    /// <summary>
    /// Inicia un nuevo registro o retoma uno existente para la combinación línea+producto+turno+fecha.
    /// </summary>
    Task<Result<IniciarRegistroProcesoResponse>> IniciarRegistroAsync(
        IniciarRegistroProcesoRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un registro con todos sus detalles agrupados.
    /// </summary>
    Task<Result<RegistroProcesoCompletoDto>> ObtenerCompletoAsync(
        long registroProcesoId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda valores de parámetros.
    /// Calcula si están fuera de estándar y actualiza el estado.
    /// </summary>
    Task<Result> GuardarValoresAsync(
        GuardarValoresRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cierra un registro (no editable después).
    /// </summary>
    Task<Result> CerrarRegistroAsync(
        CerrarRegistroProcesoRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene registros paginados.
    /// </summary>
    Task<Result<PagedResult<RegistroProcesoDto>>> ObtenerPaginadoAsync(
        RegistroProcesoFiltroRequest filtro,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene los grupos de parámetros con sus estándares para una línea+producto.
    /// </summary>
    Task<Result<IReadOnlyList<GrupoParametroDto>>> ObtenerGruposConEstandaresAsync(
        int lineaId,
        int? productoId,
        CancellationToken cancellationToken = default);
}
