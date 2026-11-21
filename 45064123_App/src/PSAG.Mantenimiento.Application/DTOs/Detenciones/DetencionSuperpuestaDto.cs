namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// T-786: detención de la misma línea que se pisa con el horario de una carga manual (por ejemplo, la que creó la
/// cámara al reiniciarse después de un corte de energía). Se avisa antes de guardar; no bloquea.
/// </summary>
public class DetencionSuperpuestaDto
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string OrigenRegistro { get; set; } = string.Empty;
    public string EstadoActual { get; set; } = string.Empty;
    public string EstadoActualDescripcion { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraParada { get; set; }
    public DateTimeOffset? FechaHoraFin { get; set; }
    public int MinutosEfectivos { get; set; }
}
