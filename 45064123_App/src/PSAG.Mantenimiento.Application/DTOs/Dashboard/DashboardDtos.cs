namespace PSAG.Mantenimiento.Application.DTOs.Dashboard;

public class DashboardFiltroRequest
{
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public int? LineaId { get; set; }
    public int? EquipoId { get; set; }
    public int? TipoFallaId { get; set; }
    public int? TurnoId { get; set; }
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
    public DateOnly Fecha { get; set; }
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
    public DateOnly Fecha { get; set; }
    public int MinutosEfectivos { get; set; }
    public string Prioridad { get; set; } = string.Empty;
}

public class DashboardCompletoDto
{
    public DashboardResumenDto Resumen { get; set; } = new();
    public IReadOnlyList<DetencionesPorLineaDto> DetencionesPorLinea { get; set; } = Array.Empty<DetencionesPorLineaDto>();
    public IReadOnlyList<DetencionesPorEquipoDto> TopEquiposConMayorParada { get; set; } = Array.Empty<DetencionesPorEquipoDto>();
    public IReadOnlyList<DetencionesPorTipoFallaDto> DetencionesPorTipoFalla { get; set; } = Array.Empty<DetencionesPorTipoFallaDto>();
    public IReadOnlyList<DetencionesPorDetalleFallaDto> TopDetallesFalla { get; set; } = Array.Empty<DetencionesPorDetalleFallaDto>();
    public IReadOnlyList<TendenciaDetencionesDto> TendenciaDiaria { get; set; } = Array.Empty<TendenciaDetencionesDto>();
    public IReadOnlyList<DetencionAbiertaDto> DetencionesAbiertas { get; set; } = Array.Empty<DetencionAbiertaDto>();

    // Eje 2: Variables de Proceso
    public ResumenProcesoDto ResumenProceso { get; set; } = new();
    public IReadOnlyList<DesviacionParametroDto> TopDesviaciones { get; set; } = Array.Empty<DesviacionParametroDto>();

    // Eje 3: Inspecciones
    public ResumenInspeccionesDto ResumenInspecciones { get; set; } = new();
    public IReadOnlyList<HallazgoPendienteDto> HallazgosPendientes { get; set; } = Array.Empty<HallazgoPendienteDto>();
}

// DTOs Eje 2: Variables de Proceso
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
    public decimal? ToleranciaInferior { get; set; }
    public decimal? ToleranciaSuperior { get; set; }
    public decimal Desviacion { get; set; }
    public DateTimeOffset FechaHora { get; set; }
}

// DTOs Eje 3: Inspecciones
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

/// <summary>
/// Filtro para reportes mensuales.
/// </summary>
public class ReporteFiltroRequest
{
    public int? LineaId { get; set; }
    public int Anio { get; set; } = DateTime.Today.Year;
    public int? MesDesde { get; set; }
    public int? MesHasta { get; set; }
}

/// <summary>
/// Item de indisponibilidad por tipo de falla para un mes.
/// </summary>
public class IndisponibilidadTipoFallaItem
{
    public int TipoFallaId { get; set; }
    public string TipoFallaNombre { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int MinutosTotales { get; set; }
    public double Porcentaje { get; set; }
}

/// <summary>
/// Datos de indisponibilidad para un mes.
/// </summary>
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

/// <summary>
/// Reporte completo de indisponibilidad.
/// </summary>
public class IndisponibilidadReporteDto
{
    public int? LineaId { get; set; }
    public string? LineaNombre { get; set; }
    public int Anio { get; set; }
    public List<IndisponibilidadMensualDto> Meses { get; set; } = new();
    public IndisponibilidadMensualDto Promedio { get; set; } = new();
}

/// <summary>
/// Item de fallos mayores por tipo para un mes.
/// </summary>
public class FalloMayorTipoItem
{
    public int TipoFallaId { get; set; }
    public string TipoFallaNombre { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int CantidadEventos { get; set; }
    public int MinutosTotales { get; set; }
}

/// <summary>
/// Datos de fallos mayores (>60 min) para un mes.
/// </summary>
public class FallosMayoresMensualDto
{
    public int Anio { get; set; }
    public int Mes { get; set; }
    public string MesNombre { get; set; } = string.Empty;
    public int TotalEventos { get; set; }
    public int MinutosTotalesParada { get; set; }
    public int MinutosProgramados { get; set; }
    public double HorasProgramadas { get; set; }
    public double IndicadorPorcentaje { get; set; }
    public List<FalloMayorTipoItem> PorTipoFalla { get; set; } = new();
}

/// <summary>
/// Reporte completo de fallos mayores.
/// </summary>
public class FallosMayoresReporteDto
{
    public int? LineaId { get; set; }
    public string? LineaNombre { get; set; }
    public int Anio { get; set; }
    public int UmbralMinutos { get; set; } = 60;
    public List<FallosMayoresMensualDto> Meses { get; set; } = new();
    public FallosMayoresMensualDto Promedio { get; set; } = new();
}

/// <summary>
/// Item del resumen de detenciones por tipo de falla (primera tabla del Pareto).
/// </summary>
public class ParetoTipoFallaItem
{
    public int? TipoFallaId { get; set; }
    public string TipoFallaNombre { get; set; } = string.Empty;
    public int MinutosTotales { get; set; }
    public double PorcentajeDelTotal { get; set; }
    public double PorcentajeAcumulado { get; set; }
}

/// <summary>
/// Item del detalle de detenciones por equipo y detalle de falla (segunda tabla del Pareto).
/// </summary>
public class ParetoDetalleItem
{
    public int? EquipoId { get; set; }
    public string EquipoNombre { get; set; } = string.Empty;
    public int? DetalleFallaId { get; set; }
    public string DetalleFallaNombre { get; set; } = string.Empty;
    public int MinutosTotales { get; set; }
    public double PorcentajeDelTotal { get; set; }
}

/// <summary>
/// Agrupacion de detalles por equipo.
/// </summary>
public class ParetoEquipoGrupo
{
    public int? EquipoId { get; set; }
    public string EquipoNombre { get; set; } = string.Empty;
    public int MinutosTotales { get; set; }
    public double PorcentajeDelTotal { get; set; }
    public List<ParetoDetalleItem> Detalles { get; set; } = new();
}

/// <summary>
/// Reporte completo de Pareto de detenciones.
/// </summary>
public class ParetoDetencionesReporteDto
{
    public int? LineaId { get; set; }
    public string? LineaNombre { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public int? TurnoId { get; set; }
    public string? TurnoNombre { get; set; }
    public int TotalMinutos { get; set; }
    public int TotalDetenciones { get; set; }
    public List<ParetoTipoFallaItem> ResumenPorTipoFalla { get; set; } = new();
    public List<ParetoEquipoGrupo> DetallePorEquipo { get; set; } = new();
}

/// <summary>
/// Tiempo programado de una línea en un día. Origen: ESTANDAR (suma de turnos activos),
/// EXCEPCION (calendario, con motivo) o HISTORICO (HS PROG cargado desde el Excel).
/// </summary>
public class TiempoProgramadoDiaDto
{
    public DateOnly Fecha { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int Minutos { get; set; }
    public double Horas { get; set; }
    public string Origen { get; set; } = string.Empty;
    public string? Motivo { get; set; }
    public int? TiempoProgramadoDiaId { get; set; }
}
