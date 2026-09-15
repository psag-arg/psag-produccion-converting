namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Asignación de usuarios (maquinistas) a líneas de producción con su turno.
/// </summary>
public class UsuarioLinea
{
    public int UsuarioLineaId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public int? TurnoId { get; set; }
    public DateTimeOffset FechaAsignacion { get; set; }
    public string AsignadoPorUsuarioId { get; set; } = string.Empty;

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Turno? Turno { get; set; }
}
