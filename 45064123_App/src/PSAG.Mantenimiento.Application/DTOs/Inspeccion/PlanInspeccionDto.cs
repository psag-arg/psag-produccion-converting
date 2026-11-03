namespace PSAG.Mantenimiento.Application.DTOs.Inspeccion;

public class PlanInspeccionDto
{
    public int PlanInspeccionId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public string PuestoResponsable { get; set; } = string.Empty;
    public string PuestoResponsableDescripcion { get; set; } = string.Empty;
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int FrecuenciaDias { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
    public int CantidadItems { get; set; }
    public DateOnly? UltimaEjecucion { get; set; }
    public bool PendienteHoy { get; set; }
}

public class PlanInspeccionConItemsDto : PlanInspeccionDto
{
    public List<ItemPlanInspeccionDto> Items { get; set; } = new();
}

/// <summary>
/// Punto de inspección: una fila de la planilla (N°, Parte / Equipo, Tareas, Estándar, Método, Quién realiza, Frecuencia).
/// </summary>
public class ItemPlanInspeccionDto
{
    public int ItemPlanInspeccionId { get; set; }
    public int PlanInspeccionId { get; set; }
    public int NumeroItem { get; set; }
    public string? Codigo { get; set; }
    public string? ParteEquipo { get; set; }

    /// <summary>Tareas.</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Estándar.</summary>
    public string CondicionEstandar { get; set; } = string.Empty;
    public string Metodo { get; set; } = string.Empty;
    public string MetodoDescripcion { get; set; } = string.Empty;
    public string? MetodoDetalle { get; set; }
    public string QuienRealiza { get; set; } = string.Empty;
    public string QuienRealizaDescripcion { get; set; } = string.Empty;
    public int FrecuenciaDias { get; set; }
    public string FrecuenciaDescripcion { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; }

    /// <summary>Solo en la carga: última vez que se respondió y si vence hoy según la frecuencia.</summary>
    public DateOnly? UltimaRespuesta { get; set; }
    public DateOnly? ProximoVencimiento { get; set; }
    public bool VenceHoy { get; set; } = true;
}
