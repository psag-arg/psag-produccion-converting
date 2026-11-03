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
    public DateOnly? UltimaEjecucion { get; set; }
    public bool PendienteHoy { get; set; }
    public List<ItemPlanInspeccionDto> Items { get; set; } = new();
}

/// <summary>Una fila de la planilla de inspección.</summary>
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
    public DateOnly? UltimaRespuesta { get; set; }
    public DateOnly? ProximoVencimiento { get; set; }
    public bool VenceHoy { get; set; } = true;
}

public class EjecucionInspeccionDto
{
    public long EjecucionInspeccionId { get; set; }
    public int PlanInspeccionId { get; set; }
    public string PlanNombre { get; set; } = string.Empty;
    public string PlanCodigo { get; set; } = string.Empty;
    public string? PlanDescripcion { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public int TurnoId { get; set; }
    public string TurnoNombre { get; set; } = string.Empty;
    public string UsuarioId { get; set; } = string.Empty;
    public string? UsuarioNombre { get; set; }
    public DateOnly Fecha { get; set; }
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset? FechaHoraFin { get; set; }
    public bool Completada { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string EstadoDescripcion { get; set; } = string.Empty;
    public string? Puesto { get; set; }
    public string PuestoDescripcion { get; set; } = string.Empty;
    public string? UsuarioCierreId { get; set; }
    public string? UsuarioCierreNombre { get; set; }
    public int CantidadOk { get; set; }
    public int CantidadNoOk { get; set; }
    public int CantidadTotal { get; set; }
    public int CantidadVencenHoy { get; set; }
    public int CantidadVencenHoyRespondidos { get; set; }
    public bool PuedeCerrar { get; set; }
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
    public string? Codigo { get; set; }
    public string? ParteEquipo { get; set; }

    /// <summary>Tareas.</summary>
    public string ItemDescripcion { get; set; } = string.Empty;
    public string CondicionEstandar { get; set; } = string.Empty;
    public string Metodo { get; set; } = string.Empty;
    public string QuienRealiza { get; set; } = string.Empty;
    public string QuienRealizaDescripcion { get; set; } = string.Empty;
    public int FrecuenciaDias { get; set; }
    public string FrecuenciaDescripcion { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool VenceHoy { get; set; } = true;
    public DateOnly? UltimaRespuesta { get; set; }
    public DateOnly? ProximoVencimiento { get; set; }
    public bool FueraDePlantilla { get; set; }

    /// <summary>OK, NO_OK o null.</summary>
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

    /// <summary>PACKAGING, MECANICO... Null = todos los puntos.</summary>
    public string? Puesto { get; set; }
}

public class IniciarEjecucionResponse
{
    public long EjecucionInspeccionId { get; set; }
    public string PlanNombre { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public int CantidadItems { get; set; }
    public bool EsNuevo { get; set; }
    public bool Completada { get; set; }
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

public class CerrarEjecucionRequest
{
    public long EjecucionInspeccionId { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    public string? ObservacionesGenerales { get; set; }
}

public class ResolverHallazgoRequest
{
    public long HallazgoNoOkId { get; set; }
    public string ObservacionResolucion { get; set; } = string.Empty;
    public bool RequiereAccionCorrectiva { get; set; }
    public string? AccionCorrectivaDescripcion { get; set; }
}

/// <summary>Filtros del historial de inspecciones.</summary>
public class FiltroInspecciones
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int? LineaId { get; set; }
    public int? PlanInspeccionId { get; set; }
    public string? Puesto { get; set; }
    public int? FrecuenciaDias { get; set; }
    public string? Estado { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public bool SoloConNoOk { get; set; }
}

public class GuardarPlanInspeccionRequest
{
    public int LineaId { get; set; }
    public int? EquipoId { get; set; }
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string PuestoResponsable { get; set; } = "PACKAGING";
    public int FrecuenciaDias { get; set; } = 1;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

public class GuardarItemInspeccionRequest
{
    public int PlanInspeccionId { get; set; }
    public int NumeroItem { get; set; }
    public string? Codigo { get; set; }
    public string ParteEquipo { get; set; } = string.Empty;
    public string Tareas { get; set; } = string.Empty;
    public string Estandar { get; set; } = string.Empty;
    public string? Metodo { get; set; }
    public string QuienRealiza { get; set; } = "PACKAGING";
    public int FrecuenciaDias { get; set; } = 1;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}
