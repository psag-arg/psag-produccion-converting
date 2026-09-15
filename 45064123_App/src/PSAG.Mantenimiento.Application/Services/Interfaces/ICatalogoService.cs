using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Catalogos;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

public interface ILineaService
{
    Task<Result<IReadOnlyList<LineaDto>>> ObtenerTodasAsync(bool soloActivas = true, CancellationToken cancellationToken = default);
    Task<Result<LineaDto>> ObtenerPorIdAsync(int lineaId, CancellationToken cancellationToken = default);
    Task<Result<LineaDto>> CrearAsync(CrearLineaRequest request, string usuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(int lineaId, ActualizarLineaRequest request, string usuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActivarDesactivarAsync(int lineaId, bool activo, string usuarioId, CancellationToken cancellationToken = default);
}

public interface IEquipoService
{
    Task<Result<IReadOnlyList<EquipoDto>>> ObtenerTodosAsync(bool soloActivos = true, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<EquipoDto>>> ObtenerPorLineaAsync(int lineaId, bool soloActivos = true, CancellationToken cancellationToken = default);
    Task<Result<EquipoDto>> ObtenerPorIdAsync(int equipoId, CancellationToken cancellationToken = default);
    Task<Result<EquipoDto>> CrearAsync(CrearEquipoRequest request, string usuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(int equipoId, ActualizarEquipoRequest request, string usuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActivarDesactivarAsync(int equipoId, bool activo, string usuarioId, CancellationToken cancellationToken = default);
}

public interface ITipoFallaService
{
    Task<Result<IReadOnlyList<TipoFallaDto>>> ObtenerTodosAsync(bool soloActivos = true, CancellationToken cancellationToken = default);
    Task<Result<TipoFallaDto>> ObtenerPorIdAsync(int tipoFallaId, CancellationToken cancellationToken = default);
    Task<Result<TipoFallaDto>> CrearAsync(CrearTipoFallaRequest request, string usuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(int tipoFallaId, ActualizarTipoFallaRequest request, string usuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActivarDesactivarAsync(int tipoFallaId, bool activo, string usuarioId, CancellationToken cancellationToken = default);
}

public interface IDetalleFallaService
{
    Task<Result<IReadOnlyList<DetalleFallaDto>>> ObtenerTodosAsync(bool soloActivos = true, CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<DetalleFallaDto>>> ObtenerPorFiltroAsync(DetalleFallaFiltroRequest filtro, CancellationToken cancellationToken = default);
    Task<Result<DetalleFallaDto>> ObtenerPorIdAsync(int detalleFallaId, CancellationToken cancellationToken = default);
    Task<Result<DetalleFallaDto>> CrearAsync(CrearDetalleFallaRequest request, string usuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(int detalleFallaId, ActualizarDetalleFallaRequest request, string usuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActivarDesactivarAsync(int detalleFallaId, bool activo, string usuarioId, CancellationToken cancellationToken = default);
}

public interface ITurnoService
{
    Task<Result<IReadOnlyList<TurnoDto>>> ObtenerTodosAsync(bool soloActivos = true, CancellationToken cancellationToken = default);
    Task<Result<TurnoDto>> ObtenerPorIdAsync(int turnoId, CancellationToken cancellationToken = default);
    Task<Result<TurnoDto>> ObtenerTurnoActualAsync(CancellationToken cancellationToken = default);
    Task<Result<TurnoDto>> ObtenerTurnoPorHoraAsync(TimeOnly hora, CancellationToken cancellationToken = default);
    Task<Result<TurnoDto>> CrearAsync(CrearTurnoRequest request, string usuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(int turnoId, ActualizarTurnoRequest request, string usuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActivarDesactivarAsync(int turnoId, bool activo, string usuarioId, CancellationToken cancellationToken = default);
}
