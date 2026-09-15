namespace PSAG.Mantenimiento.Application.DTOs.Inspeccion;

public class HallazgoNoOkDto
{
    public long HallazgoNoOkId { get; set; }
    public long EjecucionInspeccionDetalleId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public string UsuarioReporteId { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraHallazgo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public int? NivelCriticidad { get; set; }
    public bool Resuelto { get; set; }
    public string? UsuarioResolucionId { get; set; }
    public DateTimeOffset? FechaHoraResolucion { get; set; }
    public string? ObservacionResolucion { get; set; }
    public bool RequiereAccionCorrectiva { get; set; }
    public string? AccionCorrectivaDescripcion { get; set; }

    // Información del ítem de inspección
    public string ItemDescripcion { get; set; } = string.Empty;
    public string PlanNombre { get; set; } = string.Empty;
}
