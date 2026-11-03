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
    /// Código del punto (ej. T2-EMP-01). Lo usa el script de carga para no duplicar; si se carga desde
    /// Maestros sin código, se genera con el código del plan.
    /// </summary>
    public string? Codigo { get; set; }

    /// <summary>
    /// Columna "Parte / Equipo" de la planilla (ej. "Difusores de rollos").
    /// </summary>
    public string? ParteEquipo { get; set; }

    /// <summary>
    /// Columna "Tareas" de la planilla: qué hay que inspeccionar.
    /// Ej: "Inspeccionar el estado de: 1) Correa de mando 2) Poleas"
    /// </summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>
    /// Columna "Estándar": condición que debe cumplirse.
    /// Ej: "Tensión uniforme, sin holguras visibles"
    /// </summary>
    public string CondicionEstandar { get; set; } = string.Empty;

    /// <summary>
    /// Método de inspección a utilizar.
    /// </summary>
    public MetodoInspeccion Metodo { get; set; } = MetodoInspeccion.VISUAL;

    /// <summary>
    /// Columna "Método" tal como la escribe la planilla (ej. "Control visual / tacto",
    /// "Destensar banda y girar rodillos con guantes"). Es lo que se muestra.
    /// </summary>
    public string? MetodoDetalle { get; set; }

    /// <summary>
    /// Columna "Quién realiza" (OP. PACKAGING, MECÁNICO...).
    /// </summary>
    public TipoPuesto QuienRealiza { get; set; } = TipoPuesto.PACKAGING;

    /// <summary>
    /// Columna "Frecuencia" en días: 1 = diaria, 10 = cada 10 días.
    /// Un punto vence cuando pasaron estos días desde la última vez que se respondió.
    /// </summary>
    public int FrecuenciaDias { get; set; } = 1;

    /// <summary>
    /// Orden de visualización dentro del plan.
    /// </summary>
    public int Orden { get; set; }

    /// <summary>
    /// Si el ítem está activo.
    /// </summary>
    public bool Activo { get; set; } = true;
}
