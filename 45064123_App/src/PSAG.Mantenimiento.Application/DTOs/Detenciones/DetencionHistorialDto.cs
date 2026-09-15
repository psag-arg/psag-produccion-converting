namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// DTO para historial de estados de una detención.
/// </summary>
public class DetencionHistorialDto
{
    public long DetencionEstadoHistorialId { get; set; }
    public string? EstadoAnterior { get; set; }
    public string? EstadoAnteriorDescripcion { get; set; }
    public string EstadoNuevo { get; set; } = string.Empty;
    public string EstadoNuevoDescripcion { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string UsuarioNombre { get; set; } = string.Empty;
    public string? Observacion { get; set; }
}

/// <summary>
/// DTO para notas de una detención.
/// </summary>
public class DetencionNotaDto
{
    public long DetencionNotaId { get; set; }
    public string Texto { get; set; } = string.Empty;
    public string UsuarioId { get; set; } = string.Empty;
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public string TipoNota { get; set; } = string.Empty;
    public bool EsNotaDeCierre { get; set; }
}

/// <summary>
/// Request para agregar una nota a una detención.
/// </summary>
public class AgregarNotaDetencionRequest
{
    public string Texto { get; set; } = string.Empty;
    public string TipoNota { get; set; } = "Operativa";
}
