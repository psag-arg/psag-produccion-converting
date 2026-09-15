using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Ítem individual dentro de un plan de inspección (checklist).
/// Cada ítem representa un punto de verificación con su condición estándar.
/// </summary>
public class ItemPlanInspeccion
{
    public int ItemPlanInspeccionId { get; set; }

    /// <summary>
    /// Plan al que pertenece este ítem.
    /// </summary>
    public int PlanInspeccionId { get; set; }
    public PlanInspeccion PlanInspeccion { get; set; } = null!;

    /// <summary>
    /// Número de ítem dentro del plan (para referencia).
    /// </summary>
    public int NumeroItem { get; set; }

    /// <summary>
    /// Descripción del punto a inspeccionar.
    /// Ej: "Verificar tensión de la banda transportadora"
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Condición estándar/ideal que debe cumplirse.
    /// Ej: "Tensión uniforme, sin holguras visibles"
    /// </summary>
    public string CondicionEstandar { get; set; } = string.Empty;

    /// <summary>
    /// Método de inspección a utilizar.
    /// </summary>
    public MetodoInspeccion Metodo { get; set; } = MetodoInspeccion.VISUAL;

    /// <summary>
    /// Orden de visualización dentro del plan.
    /// </summary>
    public int Orden { get; set; }

    /// <summary>
    /// Si el ítem está activo.
    /// </summary>
    public bool Activo { get; set; } = true;
}
