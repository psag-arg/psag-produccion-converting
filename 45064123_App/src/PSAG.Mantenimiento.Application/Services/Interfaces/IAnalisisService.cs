using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Analisis;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Identificación de fallas: análisis de una detención, bandeja de pendientes y base de soluciones.
/// Los permisos se validan acá, no solo en la UI.
/// </summary>
public interface IAnalisisService
{
    Task<Result<AnalisisDetencionDto>> ObtenerAsync(
        long detencionId,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<Result<AnalisisDetencionDto>> GuardarBorradorAsync(
        long detencionId,
        GuardarAnalisisRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<Result<AnalisisDetencionDto>> CerrarAsync(
        long detencionId,
        GuardarAnalisisRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<Result<MisPendientesDto>> ObtenerMisPendientesAsync(
        string usuarioId,
        IReadOnlyCollection<string> roles,
        string? responsableRol = null,
        string? responsableUsuarioId = null,
        CancellationToken cancellationToken = default);

    Task<int> ContarPendientesAsync(
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<Result<SolucionesResultadoDto>> BuscarSolucionesAsync(
        BuscarSolucionesRequest filtro,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);
}
