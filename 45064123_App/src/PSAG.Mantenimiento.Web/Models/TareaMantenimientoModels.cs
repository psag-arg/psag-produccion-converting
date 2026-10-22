namespace PSAG.Mantenimiento.Web.Models;

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

public class CrearTareaMantenimientoRequest
{
    public int LineaId { get; set; }
    public int EquipoId { get; set; }
    public string? ParteEquipo { get; set; }
    public string Detalle { get; set; } = string.Empty;
    public string TipoTarea { get; set; } = string.Empty;
    public string Criticidad { get; set; } = string.Empty;
}

public class ProgramarTareasRequest
{
    public List<long> TareaMantenimientoIds { get; set; } = new();
}

public class TareasHistorialFiltro
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int? LineaId { get; set; }
    public int? EquipoId { get; set; }
    public string? Criticidad { get; set; }
    public string? TipoTarea { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
}

/// <summary>
/// Opciones fijas de tipo y criticidad (los mismos valores que los enums de la API) y su badge.
/// </summary>
public static class TareaMantenimientoOpciones
{
    public static readonly (string Valor, string Texto)[] Tipos =
    [
        ("MECANICA", "Mecánica"),
        ("ELECTRICA", "Eléctrica"),
        ("ELECTRONICA", "Electrónica")
    ];

    public static readonly (string Valor, string Texto)[] Criticidades =
    [
        ("ALTA", "Alta"),
        ("MEDIA", "Media"),
        ("BAJA", "Baja")
    ];

    public static string BadgeCriticidad(string criticidad) => criticidad switch
    {
        "ALTA" => "badge bg-danger",
        "MEDIA" => "badge bg-warning text-dark",
        "BAJA" => "badge bg-secondary",
        _ => "badge bg-light text-dark"
    };

    public static string BadgeEstado(string estado) => estado switch
    {
        "PENDIENTE" => "badge bg-primary",
        "PROGRAMADA" => "badge bg-info text-dark",
        "CERRADA" => "badge bg-success",
        _ => "badge bg-secondary"
    };

    /// <summary>Mismos roles que la API para informar tareas.</summary>
    public static bool PuedeCargar(Func<string, bool> tieneRol) =>
        new[] { "OPERARIO", "SUPERVISOR", "ADMINISTRADOR" }.Any(tieneRol);

    /// <summary>Mismos roles que la API para programar y cerrar.</summary>
    public static bool PuedeGestionar(Func<string, bool> tieneRol) =>
        new[] { "MANTENIMIENTO", "SUPERVISOR", "ADMINISTRADOR" }.Any(tieneRol);
}
