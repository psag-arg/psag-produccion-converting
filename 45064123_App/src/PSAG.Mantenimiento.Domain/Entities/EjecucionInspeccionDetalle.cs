using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Detalle de un ítem inspeccionado dentro de una ejecución.
/// R16: Si Resultado es NO_OK, la Observacion es obligatoria.
/// </summary>
public class EjecucionInspeccionDetalle
{
    public long EjecucionInspeccionDetalleId { get; set; }

    /// <summary>
    /// Ejecución a la que pertenece este detalle.
    /// </summary>
    public long EjecucionInspeccionId { get; set; }
    public EjecucionInspeccion EjecucionInspeccion { get; set; } = null!;

    /// <summary>
    /// Ítem del plan que se inspeccionó.
    /// </summary>
    public int ItemPlanInspeccionId { get; set; }
    public ItemPlanInspeccion ItemPlanInspeccion { get; set; } = null!;

    /// <summary>
    /// Resultado de la inspección.
    /// </summary>
    public ResultadoInspeccion Resultado { get; set; }

    /// <summary>
    /// Observación del inspector.
    /// Obligatoria si Resultado = NO_OK.
    /// </summary>
    public string? Observacion { get; set; }

    /// <summary>
    /// Fecha y hora en que se registró este detalle.
    /// </summary>
    public DateTimeOffset FechaHoraRegistro { get; set; } = DateTimeOffset.UtcNow;
}
