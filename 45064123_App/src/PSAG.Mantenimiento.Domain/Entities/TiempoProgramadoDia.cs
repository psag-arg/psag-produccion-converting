using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Excepción al tiempo programado estándar de una línea en una fecha: horas que la línea
/// debía trabajar ese día. Sin fila para la fecha, rige la suma de los turnos activos.
/// Una fila por línea y fecha.
/// </summary>
public class TiempoProgramadoDia : CatalogoBase
{
    public int TiempoProgramadoDiaId { get; set; }
    public int LineaId { get; set; }
    public DateOnly Fecha { get; set; }

    /// <summary>
    /// Horas programadas ese día. 0 = la línea no trabaja.
    /// </summary>
    public decimal Horas { get; set; }

    public string? Motivo { get; set; }

    /// <summary>
    /// EXCEPCION | HISTORICO.
    /// </summary>
    public string Origen { get; set; } = "EXCEPCION";

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
}
