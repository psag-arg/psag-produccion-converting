namespace PSAG.Mantenimiento.Web.Models;

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
    public DateTime? UltimaEjecucion { get; set; }
    public bool PendienteHoy { get; set; }
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

public class EjecucionInspeccionDto
{
    public long EjecucionInspeccionId { get; set; }
    public int PlanInspeccionId { get; set; }
    public string PlanNombre { get; set; } = string.Empty;
    public string PlanCodigo { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public int TurnoId { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public string UsuarioId { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset? FechaHoraFin { get; set; }
    public bool Completada { get; set; }
    public int CantidadOk { get; set; }
    public int CantidadNoOk { get; set; }
    public int CantidadTotal { get; set; }
    public string? ObservacionesGenerales { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<EjecucionInspeccionDetalleDto> Detalles { get; set; } = new();
}

public class EjecucionInspeccionDetalleDto
{
    public long EjecucionInspeccionDetalleId { get; set; }
    public long EjecucionInspeccionId { get; set; }
    public int ItemPlanInspeccionId { get; set; }
    public int NumeroItem { get; set; }
    public string ItemDescripcion { get; set; } = string.Empty;
    public string CondicionEstandar { get; set; } = string.Empty;
    public string Metodo { get; set; } = string.Empty;
    public string? Resultado { get; set; }
    public string? ResultadoDescripcion { get; set; }
    public string? Observacion { get; set; }
    public DateTimeOffset? FechaHoraRegistro { get; set; }
}

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
    public string ItemDescripcion { get; set; } = string.Empty;
    public string PlanNombre { get; set; } = string.Empty;
}

public class IniciarEjecucionRequest
{
    public int PlanInspeccionId { get; set; }
}

public class IniciarEjecucionResponse
{
    public long EjecucionInspeccionId { get; set; }
    public string PlanNombre { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public int CantidadItems { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class GuardarResultadosInspeccionRequest
{
    public long EjecucionInspeccionId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public List<ResultadoItemRequest> Resultados { get; set; } = new();
    public string? ObservacionesGenerales { get; set; }
    public bool Completar { get; set; }
}

public class ResultadoItemRequest
{
    public int ItemPlanInspeccionId { get; set; }
    public string Resultado { get; set; } = string.Empty;
    public string? Observacion { get; set; }
}

public class ResolverHallazgoRequest
{
    public long HallazgoNoOkId { get; set; }
    public string ObservacionResolucion { get; set; } = string.Empty;
    public bool RequiereAccionCorrectiva { get; set; }
    public string? AccionCorrectivaDescripcion { get; set; }
}
