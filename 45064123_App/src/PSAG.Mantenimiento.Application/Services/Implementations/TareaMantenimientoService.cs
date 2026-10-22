using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.TareasMantenimiento;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

public class TareaMantenimientoService : ITareaMantenimientoService
{
    private const int LongitudMaximaParte = 200;
    private const int LongitudMaximaDetalle = 2000;
    private const int MaximoTareasPorProgramacion = 200;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TareaMantenimientoService> _logger;

    public TareaMantenimientoService(IUnitOfWork unitOfWork, ILogger<TareaMantenimientoService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<TareaMantenimientoDto>> CrearAsync(
        CrearTareaMantenimientoRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (!TareaMantenimientoReglas.PuedeCargar(roles))
            return Result<TareaMantenimientoDto>.Failure("FORBIDDEN", TareaMantenimientoReglas.MensajeSinPermisoCarga);

        var faltan = new List<string>();
        if (request.LineaId <= 0) faltan.Add("línea");
        if (request.EquipoId <= 0) faltan.Add("cuerpo");
        if (string.IsNullOrWhiteSpace(request.Detalle)) faltan.Add("detalle de la tarea");
        if (!Enum.TryParse<TipoTareaMantenimiento>(request.TipoTarea, ignoreCase: true, out var tipo) || !Enum.IsDefined(tipo)) faltan.Add("tipo de tarea");
        if (!Enum.TryParse<CriticidadTarea>(request.Criticidad, ignoreCase: true, out var criticidad) || !Enum.IsDefined(criticidad)) faltan.Add("criticidad");
        if (faltan.Count > 0)
            return Result<TareaMantenimientoDto>.Failure("VALIDATION", "Falta completar: " + string.Join(", ", faltan) + ".");

        var parte = string.IsNullOrWhiteSpace(request.ParteEquipo) ? null : request.ParteEquipo.Trim();
        var detalle = request.Detalle.Trim();
        if (parte?.Length > LongitudMaximaParte)
            return Result<TareaMantenimientoDto>.Failure("VALIDATION", $"La parte del cuerpo admite hasta {LongitudMaximaParte} caracteres.");
        if (detalle.Length > LongitudMaximaDetalle)
            return Result<TareaMantenimientoDto>.Failure("VALIDATION", $"El detalle admite hasta {LongitudMaximaDetalle} caracteres.");

        var linea = await _unitOfWork.Lineas.GetByIdAsync(request.LineaId, cancellationToken);
        if (linea == null || !linea.Activo)
            return Result<TareaMantenimientoDto>.Failure("VALIDATION", "La línea no existe o está inactiva.");

        // La restricción por línea asignada se valida acá, no sólo en la pantalla.
        var asignada = await _unitOfWork.UsuariosLineas.GetByUsuarioYLineaAsync(usuarioId, request.LineaId, cancellationToken) != null;
        if (!TareaMantenimientoReglas.PuedeCargarEnLinea(roles, asignada))
            return Result<TareaMantenimientoDto>.Failure("FORBIDDEN", DetencionCargaReglas.MensajeLineaNoAsignada);

        var equipo = await _unitOfWork.Equipos.GetByIdAsync(request.EquipoId, cancellationToken);
        if (equipo == null || !equipo.Activo || equipo.LineaId != request.LineaId)
            return Result<TareaMantenimientoDto>.Failure("VALIDATION", "El cuerpo elegido no pertenece a la línea o está inactivo.");

        var tarea = new TareaMantenimiento
        {
            LineaId = request.LineaId,
            EquipoId = request.EquipoId,
            ParteEquipo = parte,
            Detalle = detalle,
            TipoTarea = tipo,
            Criticidad = criticidad,
            Estado = EstadoTareaMantenimiento.PENDIENTE,
            CreadoPorUsuarioId = usuarioId,
            ModificadoPorUsuarioId = usuarioId
        };

        await _unitOfWork.TareasMantenimiento.AddAsync(tarea, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tarea de mantenimiento {TareaId} cargada por usuario {UsuarioId} en línea {LineaId}",
            tarea.TareaMantenimientoId, usuarioId, tarea.LineaId);

        tarea.Linea = linea;
        tarea.Equipo = equipo;
        return Result<TareaMantenimientoDto>.Success(ADto(tarea));
    }

    public async Task<Result<IReadOnlyList<TareaMantenimientoDto>>> ObtenerActivasAsync(
        TareasActivasFiltroRequest filtro,
        CancellationToken cancellationToken = default)
    {
        if (!TryParsearOpcional<CriticidadTarea>(filtro.Criticidad, out var criticidad))
            return Result<IReadOnlyList<TareaMantenimientoDto>>.Failure("VALIDATION", "Criticidad no válida.");
        if (!TryParsearOpcional<EstadoTareaMantenimiento>(filtro.Estado, out var estado) || estado == EstadoTareaMantenimiento.CERRADA)
            return Result<IReadOnlyList<TareaMantenimientoDto>>.Failure("VALIDATION", "Estado no válido: la grilla muestra pendientes y programadas.");

        var tareas = await _unitOfWork.TareasMantenimiento.GetActivasAsync(filtro.LineaId, criticidad, estado, cancellationToken);
        return Result<IReadOnlyList<TareaMantenimientoDto>>.Success(tareas.Select(ADto).ToList());
    }

    public async Task<Result<PagedResult<TareaMantenimientoDto>>> ObtenerHistorialAsync(
        TareasHistorialFiltroRequest filtro,
        TimeZoneInfo zonaPlanta,
        CancellationToken cancellationToken = default)
    {
        if (!TryParsearOpcional<CriticidadTarea>(filtro.Criticidad, out var criticidad))
            return Result<PagedResult<TareaMantenimientoDto>>.Failure("VALIDATION", "Criticidad no válida.");
        if (!TryParsearOpcional<TipoTareaMantenimiento>(filtro.TipoTarea, out var tipo))
            return Result<PagedResult<TareaMantenimientoDto>>.Failure("VALIDATION", "Tipo de tarea no válido.");

        var (items, total) = await _unitOfWork.TareasMantenimiento.GetCerradasPaginadoAsync(
            filtro.Page,
            filtro.PageSize,
            filtro.LineaId,
            criticidad,
            tipo,
            filtro.EquipoId,
            InicioDelDia(filtro.FechaDesde, zonaPlanta),
            InicioDelDia(filtro.FechaHasta?.AddDays(1), zonaPlanta),
            cancellationToken);

        return Result<PagedResult<TareaMantenimientoDto>>.Success(new PagedResult<TareaMantenimientoDto>
        {
            Items = items.Select(ADto).ToList(),
            TotalCount = total,
            Page = filtro.Page,
            PageSize = filtro.PageSize
        });
    }

    public async Task<Result<IReadOnlyList<TareaMantenimientoDto>>> ProgramarAsync(
        ProgramarTareasRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (!TareaMantenimientoReglas.PuedeProgramarYCerrar(roles))
            return Result<IReadOnlyList<TareaMantenimientoDto>>.Failure("FORBIDDEN", TareaMantenimientoReglas.MensajeSinPermisoGestion);

        var ids = request.TareaMantenimientoIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result<IReadOnlyList<TareaMantenimientoDto>>.Failure("VALIDATION", "Tildá al menos una tarea para programar.");
        if (ids.Count > MaximoTareasPorProgramacion)
            return Result<IReadOnlyList<TareaMantenimientoDto>>.Failure("VALIDATION", $"Se pueden programar hasta {MaximoTareasPorProgramacion} tareas por vez.");

        var tareas = await _unitOfWork.TareasMantenimiento.GetByIdsAsync(ids, cancellationToken);
        if (tareas.Count != ids.Count)
            return Result<IReadOnlyList<TareaMantenimientoDto>>.Failure("NOT_FOUND", "Alguna de las tareas elegidas no existe. Recargá la grilla.");

        var noPendientes = tareas.Where(t => !t.Estado.PuedeTransicionarA(EstadoTareaMantenimiento.PROGRAMADA)).ToList();
        if (noPendientes.Count > 0)
            return Result<IReadOnlyList<TareaMantenimientoDto>>.Failure("VALIDATION",
                "Sólo se programan tareas pendientes. Ya no están pendientes: "
                + string.Join(", ", noPendientes.Select(t => $"#{t.TareaMantenimientoId}")) + ". Recargá la grilla.");

        var ahora = DateTimeOffset.UtcNow;
        foreach (var tarea in tareas)
            tarea.Programar(usuarioId, ahora);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tareas de mantenimiento {TareaIds} programadas por usuario {UsuarioId}",
            string.Join(",", ids), usuarioId);

        return Result<IReadOnlyList<TareaMantenimientoDto>>.Success(tareas.Select(ADto).ToList());
    }

    public async Task<Result<TareaMantenimientoDto>> CerrarAsync(
        long tareaMantenimientoId,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        if (!TareaMantenimientoReglas.PuedeProgramarYCerrar(roles))
            return Result<TareaMantenimientoDto>.Failure("FORBIDDEN", TareaMantenimientoReglas.MensajeSinPermisoGestion);

        var tarea = await _unitOfWork.TareasMantenimiento.GetByIdAsync(tareaMantenimientoId, cancellationToken);
        if (tarea == null)
            return Result<TareaMantenimientoDto>.Failure("NOT_FOUND", "Tarea de mantenimiento no encontrada.");

        if (!tarea.Estado.PuedeTransicionarA(EstadoTareaMantenimiento.CERRADA))
            return Result<TareaMantenimientoDto>.Failure("VALIDATION", tarea.EstaCerrada
                ? "La tarea ya está cerrada."
                : "Sólo se cierran tareas programadas. Programala primero.");

        tarea.Cerrar(usuarioId, DateTimeOffset.UtcNow);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Tarea de mantenimiento {TareaId} cerrada por usuario {UsuarioId}",
            tareaMantenimientoId, usuarioId);

        return Result<TareaMantenimientoDto>.Success(ADto(tarea));
    }

    private static TareaMantenimientoDto ADto(TareaMantenimiento t) => new()
    {
        TareaMantenimientoId = t.TareaMantenimientoId,
        LineaId = t.LineaId,
        LineaNombre = t.Linea?.Nombre ?? string.Empty,
        EquipoId = t.EquipoId,
        EquipoNombre = t.Equipo?.Nombre ?? string.Empty,
        ParteEquipo = t.ParteEquipo,
        Detalle = t.Detalle,
        TipoTarea = t.TipoTarea.ToString(),
        TipoTareaDescripcion = t.TipoTarea.ObtenerDescripcion(),
        Criticidad = t.Criticidad.ToString(),
        CriticidadDescripcion = t.Criticidad.ObtenerDescripcion(),
        Estado = t.Estado.ToString(),
        EstadoDescripcion = t.Estado.ObtenerDescripcion(),
        CreadoPorUsuarioId = t.CreadoPorUsuarioId,
        FechaHoraCreacion = t.FechaHoraCreacion,
        ProgramadaPorUsuarioId = t.ProgramadaPorUsuarioId,
        FechaHoraProgramacion = t.FechaHoraProgramacion,
        CerradaPorUsuarioId = t.CerradaPorUsuarioId,
        FechaHoraCierre = t.FechaHoraCierre
    };

    /// <summary>
    /// Vacío = sin filtro. Texto que no es un valor del enum = inválido.
    /// </summary>
    private static bool TryParsearOpcional<T>(string? texto, out T? valor) where T : struct, Enum
    {
        valor = null;
        if (string.IsNullOrWhiteSpace(texto))
            return true;
        if (!Enum.TryParse<T>(texto, ignoreCase: true, out var parseado) || !Enum.IsDefined(parseado))
            return false;
        valor = parseado;
        return true;
    }

    /// <summary>
    /// Medianoche de la planta de esa fecha, como instante.
    /// </summary>
    private static DateTimeOffset? InicioDelDia(DateOnly? fecha, TimeZoneInfo zonaPlanta) =>
        fecha is { } f ? HoraPlanta.DesdeHoraDePared(f.ToDateTime(TimeOnly.MinValue), zonaPlanta) : null;
}
