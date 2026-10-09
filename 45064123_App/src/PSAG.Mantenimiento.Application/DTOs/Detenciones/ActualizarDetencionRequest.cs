namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Request para actualizar una detención.
/// </summary>
public class ActualizarDetencionRequest
{
    /// <summary>
    /// Fecha del registro. Si es null, se mantiene la actual.
    /// </summary>
    public DateOnly? Fecha { get; set; }

    public int? EquipoId { get; set; }
    public int? TipoFallaId { get; set; }
    public int? DetalleFallaId { get; set; }

    /// <summary>
    /// Minutos declarados manualmente.
    /// </summary>
    public int? MinutosDeclarados { get; set; }

    /// <summary>
    /// Fecha/hora de parada.
    /// </summary>
    public DateTimeOffset? FechaHoraParada { get; set; }

    /// <summary>
    /// Fecha/hora de arranque.
    /// </summary>
    public DateTimeOffset? FechaHoraArranque { get; set; }

    /// <summary>
    /// Origen del tiempo: DECLARADO o CALCULADO.
    /// </summary>
    public string OrigenTiempo { get; set; } = "DECLARADO";

    /// <summary>
    /// Hora de inicio y fin de la carga manual (ver CrearDetencionRequest).
    /// </summary>
    public TimeOnly? HoraInicio { get; set; }
    public TimeOnly? HoraFin { get; set; }

    public string Descripcion { get; set; } = string.Empty;
    public string? AccionesRealizadas { get; set; }
    public string? ResolucionDescripcion { get; set; }

    /// <summary>
    /// RowVersion para control de concurrencia.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
