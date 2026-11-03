using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Detenciones automáticas (cámara, T-610) pendientes de declaración y el aviso de fin de turno.
/// </summary>
public interface IDeclaracionDetencionService
{
    Task<Result<DeclaracionDetencionDto>> ObtenerAsync(long detencionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completa la detención y la pasa a REGISTRADA. <paramref name="asignacionPropia"/>: la asignación de quien
    /// declara en la línea (para completar el maquinista si faltaba). La restricción por línea la valida la API.
    /// </summary>
    Task<Result<DeclararDetencionResponse>> DeclararAsync(
        long detencionId, DeclararDetencionRequest request, string usuarioId, UsuarioLinea? asignacionPropia,
        CancellationToken cancellationToken = default);

    /// <summary>Pendientes de declaración de esas líneas (null = todas) y minutos para el fin del turno en curso.</summary>
    Task<AvisoFinTurnoDto> ObtenerAvisoFinTurnoAsync(
        IReadOnlyCollection<int>? lineasIds, int minutosAviso = Domain.Services.DetencionCamaraReglas.MinutosAvisoFinTurnoPorDefecto,
        CancellationToken cancellationToken = default);
}
