using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Proceso;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Maestro de la plantilla de Control de Proceso por línea (T-545): grupos y puntos de control.
/// Nada se borra: los puntos y grupos se desactivan.
/// </summary>
public interface IControlProcesoPlantillaService
{
    Task<Result<IReadOnlyList<GrupoParametroDto>>> ObtenerPlantillaAsync(int lineaId, CancellationToken cancellationToken = default);

    Task<Result<int>> CrearGrupoAsync(GuardarGrupoControlRequest request, string usuarioId, CancellationToken cancellationToken = default);

    Task<Result> ActualizarGrupoAsync(int grupoParametroId, GuardarGrupoControlRequest request, string usuarioId, CancellationToken cancellationToken = default);

    Task<Result<int>> CrearPuntoAsync(GuardarPuntoControlRequest request, string usuarioId, CancellationToken cancellationToken = default);

    Task<Result> ActualizarPuntoAsync(int parametroId, GuardarPuntoControlRequest request, string usuarioId, CancellationToken cancellationToken = default);
}
