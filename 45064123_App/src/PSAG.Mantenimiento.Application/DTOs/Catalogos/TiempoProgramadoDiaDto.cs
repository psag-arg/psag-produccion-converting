namespace PSAG.Mantenimiento.Application.DTOs.Catalogos;

/// <summary>
/// Horas programadas de una línea en un día. Origen = EXCEPCION o HISTORICO cuando hay una fila activa;
/// si no, BASE_LINEA (calendario base de la línea) o ESTANDAR (todos los turnos activos).
/// </summary>
public class DiaProgramadoDto
{
    public DateOnly Fecha { get; set; }
    public decimal Horas { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string? Turnos { get; set; }
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
