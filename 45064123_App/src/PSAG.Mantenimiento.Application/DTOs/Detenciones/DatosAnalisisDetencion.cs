namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Campos de eliminación y análisis que muestran las vistas de detención.
/// Se completan desde la entidad en un único lugar de la API.
/// </summary>
public abstract class DatosAnalisisDetencion
{
    public bool Eliminada { get; set; }
    public bool RequiereAnalisis { get; set; }
    public string? ResponsableRol { get; set; }
    public string? ResponsableUsuarioId { get; set; }
    public string? CausaFalla { get; set; }
    public string? PlanAccion { get; set; }
    public string? AnalisisPorUsuarioId { get; set; }
    public DateTimeOffset? FechaHoraAnalisis { get; set; }
    public bool EquipoCritico { get; set; }
    public bool AnalisisPendiente { get; set; }
    public string? EstadoAnalisisDescripcion { get; set; }
}
