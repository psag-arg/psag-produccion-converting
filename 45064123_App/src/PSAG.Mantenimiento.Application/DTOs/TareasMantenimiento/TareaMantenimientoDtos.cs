using PSAG.Mantenimiento.Application.Common;

namespace PSAG.Mantenimiento.Application.DTOs.TareasMantenimiento;

/// <summary>
/// Tarea de mantenimiento para la grilla activa, la hoja de impresión y el historial.
/// Los nombres de usuario los completa el controller.
/// </summary>
public class TareaMantenimientoDto
{
    public long TareaMantenimientoId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int EquipoId { get; set; }
    public string EquipoNombre { get; set; } = string.Empty;
    public string? ParteEquipo { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public string TipoTarea { get; set; } = string.Empty;
    public string TipoTareaDescripcion { get; set; } = string.Empty;
    public string Criticidad { get; set; } = string.Empty;
    public string CriticidadDescripcion { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;

    public string CreadoPorUsuarioId { get; set; } = string.Empty;
    public string? CreadoPorUsuarioNombre { get; set; }
    public DateTimeOffset FechaHoraCreacion { get; set; }

    public string? ProgramadaPorUsuarioId { get; set; }
    public string? ProgramadaPorUsuarioNombre { get; set; }
    public DateTimeOffset? FechaHoraProgramacion { get; set; }

    public string? CerradaPorUsuarioId { get; set; }
    public string? CerradaPorUsuarioNombre { get; set; }
    public DateTimeOffset? FechaHoraCierre { get; set; }
}

/// <summary>
/// Alta de una tarea. Tipo y criticidad llegan como texto del enum (MECANICA, ALTA, ...).
/// </summary>
public class CrearTareaMantenimientoRequest
{
    public int LineaId { get; set; }
    public int EquipoId { get; set; }
    public string? ParteEquipo { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public string TipoTarea { get; set; } = string.Empty;
    public string Criticidad { get; set; } = string.Empty;
}

/// <summary>
/// Programar una o varias tareas pendientes de una vez (los tildes de la grilla).
/// </summary>
public class ProgramarTareasRequest
{
    public List<long> TareaMantenimientoIds { get; set; } = new();
}

/// <summary>
/// Filtros de la grilla activa (pendientes y programadas).
/// </summary>
public class TareasActivasFiltroRequest
{
    public int? LineaId { get; set; }
    public string? Criticidad { get; set; }
    public string? Estado { get; set; }
}

/// <summary>
/// Filtros del historial de cerradas. Las fechas son de cierre, en hora de planta.
/// </summary>
public class TareasHistorialFiltroRequest : PaginationParams
{
    public int? LineaId { get; set; }
    public int? EquipoId { get; set; }
    public string? Criticidad { get; set; }
    public string? TipoTarea { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
}
