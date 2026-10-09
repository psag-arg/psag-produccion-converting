namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Request para eliminar (borrado lógico) una detención. El motivo es obligatorio.
/// </summary>
public class EliminarDetencionRequest
{
    public string Motivo { get; set; } = string.Empty;
}
