using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Analisis;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Corrección de análisis cerrados (con historial) y fotos del análisis. Los permisos se validan acá.
/// </summary>
public interface IAnalisisEdicionService
{
    /// <summary>
    /// Guarda solución, causa y plan sobre el análisis cerrado original y deja un registro por campo cambiado.
    /// Solo administración y supervisión.
    /// </summary>
    Task<Result<AnalisisDetencionDto>> EditarCerradoAsync(
        long detencionId,
        GuardarAnalisisRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Agrega una foto JPG o PNG al análisis (en borrador o, con permiso, cerrado).
    /// </summary>
    Task<Result<AdjuntoAnalisisDto>> AgregarAdjuntoAsync(
        long detencionId,
        string? nombreOriginal,
        long tamanoBytes,
        Stream contenido,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre una foto para servirla. Cualquier usuario autenticado puede verla (igual que el análisis).
    /// </summary>
    Task<Result<AdjuntoArchivo>> AbrirAdjuntoAsync(
        long detencionId,
        long adjuntoId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Archivo abierto para enviar al navegador. Quien lo recibe cierra el stream.
/// </summary>
public sealed record AdjuntoArchivo(Stream Contenido, string ContentType, string NombreOriginal);
