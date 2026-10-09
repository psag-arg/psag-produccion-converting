namespace PSAG.Mantenimiento.Application.DTOs.Catalogos;

/// <summary>
/// Horas programadas de una línea en un día. Origen = ESTANDAR cuando no hay excepción
/// (rigen los turnos activos); EXCEPCION o HISTORICO cuando hay una fila activa.
/// </summary>
public class DiaProgramadoDto
{
    public DateOnly Fecha { get; set; }
    public decimal Horas { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public int? TiempoProgramadoDiaId { get; set; }
}

public class GuardarTiempoProgramadoDiaRequest
{
    public int LineaId { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Horas { get; set; }
    public string? Motivo { get; set; }
}
