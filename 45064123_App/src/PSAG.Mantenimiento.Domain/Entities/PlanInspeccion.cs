using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Plan de inspección para un equipo específico de una línea.
/// DEC-027: Asignado a un puesto operativo (no a un rol).
/// R14: Operarios ven solo planes de su puesto y línea asignada.
/// </summary>
public class PlanInspeccion
{
    public int PlanInspeccionId { get; set; }

    /// <summary>
    /// Línea a la que pertenece este plan.
    /// </summary>
    public int LineaId { get; set; }
    public Linea Linea { get; set; } = null!;

    /// <summary>
    /// Equipo específico a inspeccionar (opcional - puede ser toda la línea).
    /// </summary>
    public int? EquipoId { get; set; }
    public Equipo? Equipo { get; set; }

    /// <summary>
    /// Puesto operativo responsable de esta inspección.
    /// </summary>
    public TipoPuesto PuestoResponsable { get; set; }

    /// <summary>
    /// Código único del plan.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>
    /// Nombre descriptivo del plan.
    /// </summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>
    /// Descripción detallada del plan.
    /// </summary>
    public string? Descripcion { get; set; }

    /// <summary>
    /// Frecuencia en días (1 = diario, 7 = semanal, etc.).
    /// R15: Sistema muestra qué ítems vencen hoy según frecuencia.
    /// </summary>
    public int FrecuenciaDias { get; set; } = 1;

    /// <summary>
    /// Orden de visualización.
    /// </summary>
    public int Orden { get; set; }

    /// <summary>
    /// Si el plan está activo.
    /// </summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Fecha de creación del plan.
    /// </summary>
    public DateTimeOffset FechaCreacion { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Ítems del checklist de este plan.
    /// </summary>
    public ICollection<ItemPlanInspeccion> Items { get; set; } = new List<ItemPlanInspeccion>();

    /// <summary>
    /// Ejecuciones históricas de este plan.
    /// </summary>
    public ICollection<EjecucionInspeccion> Ejecuciones { get; set; } = new List<EjecucionInspeccion>();
}
