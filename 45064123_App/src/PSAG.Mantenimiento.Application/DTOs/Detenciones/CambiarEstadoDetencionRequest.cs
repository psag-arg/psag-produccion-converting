namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Request para cambiar el estado de una detención.
/// </summary>
public class CambiarEstadoDetencionRequest
{
    public string NuevoEstado { get; set; } = string.Empty;
    public string? Observacion { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Request para cerrar una detención (opcional - flujo corto).
/// Estado: REGISTRADA → CERRADA.
/// </summary>
public class CerrarDetencionRequest
{
    public string? AccionesRealizadas { get; set; }
    public string ResolucionDescripcion { get; set; } = string.Empty;
    public string? ObservacionCierre { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Request para iniciar seguimiento de una detención (opcional).
/// Estado: REGISTRADA → EN_SEGUIMIENTO.
/// </summary>
public class IniciarSeguimientoRequest
{
    public string? Observacion { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
