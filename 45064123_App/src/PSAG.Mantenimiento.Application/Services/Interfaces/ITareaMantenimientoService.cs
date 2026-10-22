using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.TareasMantenimiento;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Tareas de mantenimiento: carga (operario, supervisión, admin), programación y cierre
/// (mantenimiento, supervisión, admin) e historial. Los permisos se validan acá, no sólo en la UI.
/// </summary>
public interface ITareaMantenimientoService
{
    Task<Result<TareaMantenimientoDto>> CrearAsync(
        CrearTareaMantenimientoRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<TareaMantenimientoDto>>> ObtenerActivasAsync(
        TareasActivasFiltroRequest filtro,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<TareaMantenimientoDto>>> ObtenerHistorialAsync(
        TareasHistorialFiltroRequest filtro,
        TimeZoneInfo zonaPlanta,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Programa todas las tareas indicadas o ninguna: si alguna no existe o no está pendiente, no se guarda nada.
    /// </summary>
    Task<Result<IReadOnlyList<TareaMantenimientoDto>>> ProgramarAsync(
        ProgramarTareasRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<Result<TareaMantenimientoDto>> CerrarAsync(
        long tareaMantenimientoId,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);
}
