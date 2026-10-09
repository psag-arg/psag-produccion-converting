using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

public interface IDetencionService
{
    Task<Result<CrearDetencionResponse>> CrearDetencionAsync(
        CrearDetencionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);

    Task<Result<DetencionDto>> ObtenerPorIdAsync(
        long detencionId,
        CancellationToken cancellationToken = default);

    Task<Result<DetencionDto>> ObtenerPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default);

    Task<Result<DetencionDetalleDto>> ObtenerDetalleAsync(
        long detencionId,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<DetencionListaDto>>> ObtenerPaginadoAsync(
        DetencionFiltroRequest filtro,
        string usuarioId,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default);

    Task<Result> ActualizarDetencionAsync(
        long detencionId,
        ActualizarDetencionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);

    Task<Result> CambiarEstadoAsync(
        long detencionId,
        CambiarEstadoDetencionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);

    Task<Result> IniciarSeguimientoAsync(
        long detencionId,
        IniciarSeguimientoRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);

    Task<Result> CerrarDetencionAsync(
        long detencionId,
        CerrarDetencionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);

    Task<Result> AgregarNotaAsync(
        long detencionId,
        AgregarNotaDetencionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DetencionHistorialDto>>> ObtenerHistorialAsync(
        long detencionId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DetencionNotaDto>>> ObtenerNotasAsync(
        long detencionId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DetencionListaDto>>> ObtenerDetencionesAbiertasAsync(
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica las reglas de análisis a una detención recién creada o editada.
    /// No guarda cambios: lo hace quien llama, después de este paso.
    /// </summary>
    Task AplicarReglaAnalisisAsync(Detencion detencion, CancellationToken cancellationToken = default);
}
