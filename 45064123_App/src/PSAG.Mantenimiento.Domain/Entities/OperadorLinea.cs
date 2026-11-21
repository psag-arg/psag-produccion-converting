namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Habilitación de un usuario (operador) en una línea, con su turno habitual opcional
/// (tabla ABM_OperadoresLinea, ex UsuariosLineas). La PK sigue siendo UsuarioLineaId
/// (desde la etapa 3 de T-798 ya no la referencia DET_Detenciones: la detención guarda el usuario, OperadorUsuarioId).
/// </summary>
public class OperadorLinea
{
    public int UsuarioLineaId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public int? TurnoHabitualId { get; set; }
    public DateTimeOffset FechaAsignacion { get; set; }
    public string AsignadoPorUsuarioId { get; set; } = string.Empty;

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Turno? TurnoHabitual { get; set; }
}
