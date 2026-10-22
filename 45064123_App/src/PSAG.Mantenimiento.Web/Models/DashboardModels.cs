namespace PSAG.Mantenimiento.Web.Models;

public class DashboardFiltroRequest
{
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public int? LineaId { get; set; }
}

public class DashboardCompletoDto
{
    public DashboardResumenDto Resumen { get; set; } = new();
    public List<DetencionesPorLineaDto> DetencionesPorLinea { get; set; } = new();
    public List<DetencionesPorEquipoDto> TopEquiposConMayorParada { get; set; } = new();
    public List<DetencionesPorTipoFallaDto> DetencionesPorTipoFalla { get; set; } = new();
    public List<DetencionesPorDetalleFallaDto> TopDetallesFalla { get; set; } = new();
    public List<TendenciaDetencionesDto> TendenciaDiaria { get; set; } = new();
    public List<DetencionAbiertaDto> DetencionesAbiertas { get; set; } = new();
    public int TotalPendientesAnalisis { get; set; }

    // Eje 2: Variables de Proceso
    public ResumenProcesoDto ResumenProceso { get; set; } = new();
    public List<DesviacionParametroDto> TopDesviaciones { get; set; } = new();

    // Eje 3: Inspecciones
    public ResumenInspeccionesDto ResumenInspecciones { get; set; } = new();
    public List<HallazgoPendienteDto> HallazgosPendientes { get; set; } = new();
}

public class DashboardResumenDto
{
    public int TotalDetenciones { get; set; }
    public int DetencionesAbiertas { get; set; }
    public int DetencionesCerradas { get; set; }
    public int MinutosTotalesParada { get; set; }
    public double TiempoPromedioResolucionMinutos { get; set; }
    public double? MedianaTiempoResolucionMinutos { get; set; }
}

public class DetencionesPorLineaDto
{
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int CantidadDetenciones { get; set; }
    public int MinutosTotalesParada { get; set; }
}

public class DetencionesPorEquipoDto
{
    public int EquipoId { get; set; }
    public string EquipoNombre { get; set; } = string.Empty;
    public string LineaNombre { get; set; } = string.Empty;
    public int CantidadDetenciones { get; set; }
    public int MinutosTotalesParada { get; set; }
}

public class DetencionesPorTipoFallaDto
{
    public int TipoFallaId { get; set; }
    public string TipoFallaNombre { get; set; } = string.Empty;
    public int CantidadDetenciones { get; set; }
    public double Porcentaje { get; set; }
}

public class DetencionesPorDetalleFallaDto
{
    public int? DetalleFallaId { get; set; }
    public string DetalleFallaNombre { get; set; } = string.Empty;
    public int CantidadDetenciones { get; set; }
    public double Porcentaje { get; set; }
}

public class TendenciaDetencionesDto
{
    public DateTime Fecha { get; set; }
    public int CantidadDetenciones { get; set; }
    public int MinutosTotalesParada { get; set; }
}

public class DetencionAbiertaDto
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string LineaNombre { get; set; } = string.Empty;
    public string? EquipoNombre { get; set; }
    public string? TipoFallaNombre { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public int MinutosEfectivos { get; set; }
    public string Prioridad { get; set; } = string.Empty;
}

// Eje 2
public class ResumenProcesoDto
{
    public int TotalRegistrosHoy { get; set; }
    public int TotalValoresCapturados { get; set; }
    public int ValoresDentroEstandar { get; set; }
    public int ValoresFueraEstandar { get; set; }
    public double PorcentajeCumplimiento { get; set; }
}

public class DesviacionParametroDto
{
    public int ParametroId { get; set; }
    public string ParametroNombre { get; set; } = string.Empty;
    public string LineaNombre { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public decimal ValorCapturado { get; set; }
    public decimal? ValorEstandar { get; set; }
    public DateTimeOffset FechaHora { get; set; }
}

// Eje 3
public class ResumenInspeccionesDto
{
    public int InspeccionesPendientesHoy { get; set; }
    public int InspeccionesCompletadasHoy { get; set; }
    public int InspeccionesEnProgreso { get; set; }
    public int HallazgosNoOkPendientes { get; set; }
    public int HallazgosResueltosSemana { get; set; }
    public double PorcentajeOkSemana { get; set; }
}

public class HallazgoPendienteDto
{
    public long HallazgoNoOkId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string? EquipoNombre { get; set; }
    public string PlanNombre { get; set; } = string.Empty;
    public string ItemDescripcion { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public DateTimeOffset FechaHoraHallazgo { get; set; }
    public int DiasAbierto { get; set; }
    public bool RequiereAccionCorrectiva { get; set; }
}

// ============ REPORTES DE INDISPONIBILIDAD, FALLOS MAYORES Y PARETO ============

public class ReporteFiltroRequest
{
    public int? LineaId { get; set; }
    public int Anio { get; set; } = DateTime.Today.Year;
    public int? MesDesde { get; set; }
    public int? MesHasta { get; set; }
}

public class IndisponibilidadTipoFallaItem
{
    public int TipoFallaId { get; set; }
    public string TipoFallaNombre { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int MinutosTotales { get; set; }
    public double Porcentaje { get; set; }
}

public class IndisponibilidadMensualDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string MesNombre { get; set; } = string.Empty;
    public int MinutosProgramados { get; set; }
    public double HorasProgramadas { get; set; }
    public int MinutosTotalesParada { get; set; }
    public double IndisponibilidadTotal { get; set; }
    public List<IndisponibilidadTipoFallaItem> PorTipoFalla { get; set; } = new();
}

public class IndisponibilidadReporteDto
{
    public int? LineaId { get; set; }
    public string? LineaNombre { get; set; }
    public int Anio { get; set; }
    public List<IndisponibilidadMensualDto> Meses { get; set; } = new();
    public IndisponibilidadMensualDto Promedio { get; set; } = new();
}

public class FalloMayorTipoItem
{
    public int TipoFallaId { get; set; }
    public string TipoFallaNombre { get; set; } = string.Empty;
    public string? Color { get; set; }
    /// <summary>Entero por mes; en el promedio, con un decimal.</summary>
    public double CantidadEventos { get; set; }
    public int MinutosTotales { get; set; }
    public double Porcentaje { get; set; }
}

public class FallosMayoresMensualDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string MesNombre { get; set; } = string.Empty;
    /// <summary>Entero por mes; en el promedio, con un decimal.</summary>
    public double TotalEventos { get; set; }
    public int MinutosTotalesParada { get; set; }
    public int MinutosProgramados { get; set; }
    public double HorasProgramadas { get; set; }
    public double IndicadorPorcentaje { get; set; }
    public List<FalloMayorTipoItem> PorTipoFalla { get; set; } = new();
}

public class FallosMayoresReporteDto
{
    public int? LineaId { get; set; }
    public string? LineaNombre { get; set; }
    public int Anio { get; set; }
    public int UmbralMinutos { get; set; } = 60;
    public List<FallosMayoresMensualDto> Meses { get; set; } = new();
    public FallosMayoresMensualDto Promedio { get; set; } = new();
}

public class ParetoTipoFallaItem
{
    public int? TipoFallaId { get; set; }
    public string TipoFallaNombre { get; set; } = string.Empty;
    public int MinutosTotales { get; set; }
    public double PorcentajeDelTotal { get; set; }
    public double PorcentajeAcumulado { get; set; }
}

public class ParetoDetalleItem
{
    public int? EquipoId { get; set; }
    public string EquipoNombre { get; set; } = string.Empty;
    public int? DetalleFallaId { get; set; }
    public string DetalleFallaNombre { get; set; } = string.Empty;
    public int MinutosTotales { get; set; }
    public double PorcentajeDelTotal { get; set; }
}

public class ParetoEquipoGrupo
{
    public int? EquipoId { get; set; }
    public string EquipoNombre { get; set; } = string.Empty;
    public int MinutosTotales { get; set; }
    public double PorcentajeDelTotal { get; set; }
    public List<ParetoDetalleItem> Detalles { get; set; } = new();
}

public class ParetoDetencionesReporteDto
{
    public int? LineaId { get; set; }
    public string? LineaNombre { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public int? TurnoId { get; set; }
    public string? TurnoNombre { get; set; }
    public int TotalMinutos { get; set; }
    public int TotalDetenciones { get; set; }
    public List<ParetoTipoFallaItem> ResumenPorTipoFalla { get; set; } = new();
    public List<ParetoEquipoGrupo> DetallePorEquipo { get; set; } = new();
}

public class TiempoProgramadoDiaDto
{
    public DateOnly Fecha { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int Minutos { get; set; }
    public double Horas { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string? Turnos { get; set; }
    public string? Motivo { get; set; }
}
