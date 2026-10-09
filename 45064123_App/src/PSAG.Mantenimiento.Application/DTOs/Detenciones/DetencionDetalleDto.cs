namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// DTO con detalle completo de detención incluyendo notas e historial.
/// </summary>
public class DetencionDetalleDto : DatosAnalisisDetencion
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public int? TipoFallaId { get; set; }
    public string? TipoFallaNombre { get; set; }
    public int? DetalleFallaId { get; set; }
    public string? DetalleFallaNombre { get; set; }
    public int TurnoId { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }

    // Modelo de tiempo
    public int? MinutosDeclarados { get; set; }
    public DateTimeOffset? FechaHoraParada { get; set; }
    public DateTimeOffset? FechaHoraArranque { get; set; }
    public string OrigenTiempo { get; set; } = string.Empty;
    public int MinutosEfectivos { get; set; }

    public string Descripcion { get; set; } = string.Empty;
    public string EstadoActual { get; set; } = string.Empty;
    public string EstadoActualDescripcion { get; set; } = string.Empty;

    public string? AccionesRealizadas { get; set; }
    public string? ResolucionDescripcion { get; set; }

    public DateTimeOffset? FechaHoraCierre { get; set; }
    public string? CerradoPorUsuarioId { get; set; }
    public string? CerradoPorUsuarioNombre { get; set; }

    public DateTimeOffset FechaHoraCreacion { get; set; }
    public string CreadoPorUsuarioId { get; set; } = string.Empty;
    public string CreadoPorUsuarioNombre { get; set; } = string.Empty;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public List<DetencionNotaDto> Notas { get; set; } = new();
    public List<DetencionHistorialDto> HistorialEstados { get; set; } = new();
}
