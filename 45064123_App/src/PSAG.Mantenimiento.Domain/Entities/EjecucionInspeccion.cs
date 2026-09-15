namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Registro de ejecución de un plan de inspección.
/// R18: INSERT-ONLY - las ejecuciones no se borran ni modifican.
/// </summary>
public class EjecucionInspeccion
{
    public long EjecucionInspeccionId { get; set; }

    /// <summary>
    /// Plan que se ejecutó.
    /// </summary>
    public int PlanInspeccionId { get; set; }
    public PlanInspeccion PlanInspeccion { get; set; } = null!;

    /// <summary>
    /// Turno en que se realizó la inspección.
    /// </summary>
    public int TurnoId { get; set; }
    public Turno Turno { get; set; } = null!;

    /// <summary>
    /// Usuario que realizó la inspección.
    /// </summary>
    public string UsuarioId { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de la inspección.
    /// </summary>
    public DateOnly Fecha { get; set; }

    /// <summary>
    /// Fecha y hora de inicio de la ejecución.
    /// </summary>
    public DateTimeOffset FechaHoraInicio { get; set; }

    /// <summary>
    /// Fecha y hora de fin de la ejecución.
    /// </summary>
    public DateTimeOffset? FechaHoraFin { get; set; }

    /// <summary>
    /// Indica si la inspección está completa (todos los ítems respondidos).
    /// </summary>
    public bool Completada { get; set; }

    /// <summary>
    /// Cantidad de ítems con resultado OK.
    /// </summary>
    public int CantidadOk { get; set; }

    /// <summary>
    /// Cantidad de ítems con resultado NO_OK.
    /// </summary>
    public int CantidadNoOk { get; set; }

    /// <summary>
    /// Observaciones generales de la ejecución.
    /// </summary>
    public string? ObservacionesGenerales { get; set; }

    /// <summary>
    /// Control de concurrencia.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Detalles de cada ítem inspeccionado.
    /// </summary>
    public ICollection<EjecucionInspeccionDetalle> Detalles { get; set; } = new List<EjecucionInspeccionDetalle>();
}
