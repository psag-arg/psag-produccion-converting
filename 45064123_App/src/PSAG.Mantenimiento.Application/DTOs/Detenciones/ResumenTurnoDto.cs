namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Respuesta del resumen de detenciones por turno.
/// </summary>
public class ResumenTurnoResponse
{
    public string TurnoNombre { get; set; } = string.Empty;

    /// <summary>Fecha de producción del turno (el T3 después de medianoche sigue siendo del día de las 21:00).</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Turnos de producción sumados (uno con línea; los del turno de planta actual de cada línea sin línea).</summary>
    public List<long> TurnoProduccionIds { get; set; } = new();

    public List<ResumenTurnoItem> Items { get; set; } = new();

    /// <summary>Minutos de parada del turno: sólo los que cuentan para indicadores (T-798 2.7).</summary>
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

    /// <summary>Minutos de las detenciones del tipo (se listan todas, cuenten o no para indicadores).</summary>
    public int TotalMinutos { get; set; }

    /// <summary>False si el tipo es una parada programada (CuentaParaIndicadores = 0): no suma en el total.</summary>
    public bool CuentaParaIndicadores { get; set; } = true;
}
