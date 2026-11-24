namespace PSAG.Mantenimiento.Web.Models;

// T-770 · Tablero del operador (GET api/tablero/turno): espejo de los DTO de la API. Los números los calcula la API.

public class TableroTurnoDto
{
    public long TurnoProduccionId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public string TurnoCodigo { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public DateOnly FechaProduccion { get; set; }
    public string Estado { get; set; } = string.Empty;
    public bool Abierto { get; set; }
    public DateTimeOffset InicioProgramado { get; set; }
    public DateTimeOffset FinProgramado { get; set; }
    public string? ProductoNombre { get; set; }
    public DateTimeOffset? ProductoDesde { get; set; }
    public string? OperadorUsuarioId { get; set; }
    public string? OperadorNombre { get; set; }
    public bool SoloLectura { get; set; }

    /// <summary>Turno automático: abierto sin operador / producto supuesto / avisos para el supervisor.</summary>
    public bool SinOperador { get; set; }
    public bool ProductoSinConfirmar { get; set; }
    public List<string> Alertas { get; set; } = [];
    public DateTimeOffset GeneradoEn { get; set; }
    public IndicadoresTurnoDto Indicadores { get; set; } = new();
    public List<MangasHoraDto> MangasPorHora { get; set; } = [];
    public ResumenParadasTableroDto DetencionesReales { get; set; } = new();
    public ResumenParadasTableroDto Microcortes { get; set; } = new();
    public List<DetencionTableroDto> Detenciones { get; set; } = [];
    public List<PendienteDeclaracionDto> PendientesDeclarar { get; set; } = [];
    public CamaraTableroDto Camara { get; set; } = new();
    public EficienciaTableroDto Eficiencia { get; set; } = new();
    public ProduccionTableroDto Produccion { get; set; } = new();
    public VelocidadTableroDto Velocidad { get; set; } = new();
    public DetencionesTipoTableroDto DetencionesPorTipo { get; set; } = new();
    public CifrasDetencionesTableroDto CifrasDetenciones { get; set; } = new();
}

/// <summary>Cantidad y minutos de una cifra del bloque Detenciones.</summary>
public class CifraDetencionesDto
{
    public int Cantidad { get; set; }
    public int Minutos { get; set; }
}

/// <summary>
/// Las 4 cifras del bloque Detenciones: pendientes de declarar, declaradas, totales (= declaradas + pendientes) y
/// microcortes (aparte, no suman en totales); más las programadas que no cuentan (se muestran chico).
/// </summary>
public class CifrasDetencionesTableroDto
{
    public CifraDetencionesDto Pendientes { get; set; } = new();
    public CifraDetencionesDto Declaradas { get; set; } = new();
    public CifraDetencionesDto Totales { get; set; } = new();
    public CifraDetencionesDto Microcortes { get; set; } = new();
    public CifraDetencionesDto Programadas { get; set; } = new();
}

public class MangasHoraDto
{
    public DateTimeOffset Hora { get; set; }
    public decimal? Objetivo { get; set; }
    public decimal Real { get; set; }
    public int MinutosConDatos { get; set; }
    public bool EnCurso { get; set; }
}

public class ResumenParadasTableroDto
{
    public int Cantidad { get; set; }
    public int Segundos { get; set; }
    public int Minutos { get; set; }
    public List<ParadaCamaraDto> Items { get; set; } = [];
}

public class DetencionTableroDto
{
    public long DetencionId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public DateTimeOffset? FechaHoraParada { get; set; }
    public DateTimeOffset? FechaHoraArranque { get; set; }
    public int MinutosEfectivos { get; set; }
    public string EstadoActual { get; set; } = string.Empty;
    public string EstadoActualDescripcion { get; set; } = string.Empty;
    public string? EquipoNombre { get; set; }
    public string? TipoFallaNombre { get; set; }
    public string OrigenRegistro { get; set; } = string.Empty;
    public bool Cuenta { get; set; }
    public bool PendienteDeclaracion { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    public int? SegundosDetectados { get; set; }
    public bool VieneDelTurnoAnterior { get; set; }
}

public class CamaraTableroDto
{
    public bool TieneCamara { get; set; }
    public DateTimeOffset? UltimoDato { get; set; }
    public int? MinutosDesdeUltimoDato { get; set; }
    public bool SinDatos { get; set; }
    public List<AlertaCamaraDto> Alertas { get; set; } = [];
}

// T-770 iteración 2 · Los 4 indicadores de Ferretti y el selector del modo gerencial.

public class EficienciaTableroDto
{
    public decimal? Porcentaje { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Nivel { get; set; } = string.Empty;
    public bool Provisorio { get; set; }
    public decimal? Real { get; set; }
    public decimal? ObjetivoALaHora { get; set; }
    public decimal? ObjetivoTurno { get; set; }
    public decimal? PorcentajeTurno { get; set; }
    public decimal UmbralRojo { get; set; }
    public decimal UmbralVerde { get; set; }
}

public class ProduccionTableroDto
{
    public string? ProductoNombre { get; set; }
    public List<string> Productos { get; set; } = [];
    public BarraProduccionDto Mangas { get; set; } = new();
    public BarraProduccionDto Bolsones { get; set; } = new();
}

public class BarraProduccionDto
{
    public string Magnitud { get; set; } = string.Empty;
    public decimal? Real { get; set; }
    public decimal? ObjetivoTurno { get; set; }
    public decimal? ObjetivoALaHora { get; set; }
    public decimal? PorcentajeTurno { get; set; }
    public decimal? PorcentajeALaHora { get; set; }
    public string Estado { get; set; } = string.Empty;
    public string Nivel { get; set; } = string.Empty;
    public bool Provisorio { get; set; }
}

public class VelocidadTableroDto
{
    public DateTimeOffset Desde { get; set; }
    public List<decimal?> MetrosPorMinuto { get; set; } = [];
    public DateTimeOffset EjeDesde { get; set; }
    public DateTimeOffset EjeHasta { get; set; }
    public decimal? Promedio { get; set; }
    public decimal? Actual { get; set; }
    public decimal Referencia { get; set; }
    public bool FaltaMetraje { get; set; }
    public List<TramoVelocidadDto> Tramos { get; set; } = [];
}

public class TramoVelocidadDto
{
    public DateTimeOffset Desde { get; set; }
    public string ProductoNombre { get; set; } = string.Empty;
    public decimal? MetrosPorManga { get; set; }
}

public class DetencionesTipoTableroDto
{
    public List<BarraTipoFallaDto> Barras { get; set; } = [];
    public int MinutosTotal { get; set; }
    public int MinutosPendientes { get; set; }
    public int CantidadPendientes { get; set; }
    public int MinutosNoCuentan { get; set; }
}

public class BarraTipoFallaDto
{
    public int? TipoFallaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Minutos { get; set; }
    public int Cantidad { get; set; }
    public bool Cuenta { get; set; }
    public bool SinDeclarar { get; set; }
}

public class TurnosTableroDto
{
    public DateOnly Fecha { get; set; }
    public List<LineaTableroDto> Lineas { get; set; } = [];
    public List<TurnoTableroItemDto> Turnos { get; set; } = [];
}

public class LineaTableroDto
{
    public int LineaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class TurnoTableroItemDto
{
    public long TurnoProduccionId { get; set; }
    public int LineaId { get; set; }
    public string TurnoCodigo { get; set; } = string.Empty;
    public string TurnoNombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public bool Abierto { get; set; }
    public DateTimeOffset InicioProgramado { get; set; }
    public DateTimeOffset FinProgramado { get; set; }
}
