using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Inspeccion;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Maestro de los planes de inspección por línea (uno por equipo, ej. EMPAQUETADORA TMC) y sus puntos.
/// Igual que el maestro de Control de Proceso: nada se borra, los planes y puntos se desactivan.
/// </summary>
public interface IInspeccionPlantillaService
{
    Task<Result<IReadOnlyList<PlanInspeccionConItemsDto>>> ObtenerPlantillaAsync(int lineaId, CancellationToken cancellationToken = default);

    Task<Result<int>> CrearPlanAsync(GuardarPlanInspeccionRequest request, string usuarioId, CancellationToken cancellationToken = default);

    Task<Result> ActualizarPlanAsync(int planInspeccionId, GuardarPlanInspeccionRequest request, string usuarioId, CancellationToken cancellationToken = default);

    Task<Result<int>> CrearItemAsync(GuardarItemInspeccionRequest request, string usuarioId, CancellationToken cancellationToken = default);

    Task<Result> ActualizarItemAsync(int itemPlanInspeccionId, GuardarItemInspeccionRequest request, string usuarioId, CancellationToken cancellationToken = default);
}
