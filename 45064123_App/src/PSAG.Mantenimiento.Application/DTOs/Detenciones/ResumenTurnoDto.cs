namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Respuesta del resumen de detenciones por turno.
/// </summary>
public class ResumenTurnoResponse
{
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public List<ResumenTurnoItem> Items { get; set; } = new();
    public int TotalMinutos { get; set; }
}

/// <summary>
/// Item del resumen agrupado por tipo de falla.
/// </summary>
public class ResumenTurnoItem
{
    public int? TipoFallaId { get; set; }
    public string TipoFallaNombre { get; set; } = string.Empty;
    public int CantidadDetenciones { get; set; }
    public int TotalMinutos { get; set; }
}
