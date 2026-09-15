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

public class ItemPlanInspeccionDto
{
    public int ItemPlanInspeccionId { get; set; }
    public int PlanInspeccionId { get; set; }
    public int NumeroItem { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public string CondicionEstandar { get; set; } = string.Empty;
    public string Metodo { get; set; } = string.Empty;
    public string MetodoDescripcion { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; }
}
