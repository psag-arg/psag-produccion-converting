using PSAG.Mantenimiento.Application.DTOs.Analisis;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;

namespace PSAG.Mantenimiento.Application.DTOs.Produccion;

// T-770 · Tablero del operador (tablet): todo lo del turno en proceso en una sola llamada (GET api/tablero/turno).
// Horas en hora de planta. "Sin datos" nunca es 0.

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

    /// <summary>Producto del tramo abierto (null = "a completar").</summary>
    public string? ProductoNombre { get; set; }
    public DateTimeOffset? ProductoDesde { get; set; }

    public string? OperadorUsuarioId { get; set; }
    public string? OperadorNombre { get; set; }

    /// <summary>Lo mira alguien que no es el operador del turno (supervisor por línea): sin botones de carga.</summary>
    public bool SoloLectura { get; set; }

    /// <summary>Cuándo se armó (hora de planta).</summary>
    public DateTimeOffset GeneradoEn { get; set; }

    /// <summary>Presupuesto vs. real del turno (lo mismo que GET api/indicadores/turno/{id}).</summary>
    public IndicadoresTurnoDto Indicadores { get; set; } = new();

    /// <summary>Mangas por hora del turno hasta ahora (objetivo prorrateado y real de la cámara oficial de mangas).</summary>
    public List<MangasHoraDto> MangasPorHora { get; set; } = [];

    /// <summary>Paradas de cámara del turno que superaron el umbral de detención.</summary>
    public ResumenParadasTableroDto DetencionesReales { get; set; } = new();

    /// <summary>Paradas de cámara del turno que NO llegaron al umbral (microcortes).</summary>
    public ResumenParadasTableroDto Microcortes { get; set; } = new();

    /// <summary>Detenciones cargadas en el turno (manuales y de cámara), con sus minutos efectivos.</summary>
    public List<DetencionTableroDto> Detenciones { get; set; } = [];

    /// <summary>Detenciones del turno pendientes de declaración (las creó la cámara).</summary>
    public List<PendienteDeclaracionDto> PendientesDeclarar { get; set; } = [];

    public CamaraTableroDto Camara { get; set; } = new();

    // ===== Los 4 indicadores de Ferretti (T-770 iteración 2) =====

    /// <summary>1) Eficiencia acumulada del turno (aguja contra el objetivo a la hora; el del turno completo, secundario).</summary>
    public EficienciaTableroDto Eficiencia { get; set; } = new();

    /// <summary>2) Producción acumulada del turno: mangas y bolsones contra el objetivo del turno.</summary>
    public ProduccionTableroDto Produccion { get; set; } = new();

    /// <summary>3) Velocidad m/min minuto a minuto (cámara oficial de mangas × metros por manga del tramo).</summary>
    public VelocidadTableroDto Velocidad { get; set; } = new();

    /// <summary>4) Detenciones del turno por tipo de falla, total y minutos pendientes de declaración.</summary>
    public DetencionesTipoTableroDto DetencionesPorTipo { get; set; } = new();
}

/// <summary>
/// Eficiencia = mangas producidas ÷ mangas objetivo. La aguja usa el objetivo "a la hora" (prorrateado hasta ahora: es la
/// eficiencia acumulada del turno, comparable desde el primer minuto); <see cref="ObjetivoTurno"/> es el del turno
/// completo (11.200 del SH 30) y <see cref="PorcentajeTurno"/> el avance contra él.
/// </summary>
public class EficienciaTableroDto
{
    /// <summary>Real ÷ objetivo a la hora × 100 (el de IndicadoresService). Null si no es OK.</summary>
    public decimal? Porcentaje { get; set; }

    /// <summary>OK, SIN_OBJETIVO o SIN_DATOS.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>ROJO (&lt; 65), AMARILLO (65–85), VERDE (&gt; 85) o SIN_DATO.</summary>
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
    /// <summary>Producto del tramo abierto (o del último, si el turno cerró). Null = "a completar".</summary>
    public string? ProductoNombre { get; set; }

    /// <summary>Todos los productos del turno, en orden (más de uno = hubo cambio de producto).</summary>
    public List<string> Productos { get; set; } = [];

    public BarraProduccionDto Mangas { get; set; } = new();
    public BarraProduccionDto Bolsones { get; set; } = new();
}

/// <summary>Una barra de producción: real acumulado contra el objetivo del turno completo.</summary>
public class BarraProduccionDto
{
    public string Magnitud { get; set; } = string.Empty;
    public decimal? Real { get; set; }
    public decimal? ObjetivoTurno { get; set; }
    public decimal? ObjetivoALaHora { get; set; }

    /// <summary>Real ÷ objetivo del turno × 100 (avance). Null sin alguno de los dos.</summary>
    public decimal? PorcentajeTurno { get; set; }

    /// <summary>Real ÷ objetivo a la hora × 100 (para el color).</summary>
    public decimal? PorcentajeALaHora { get; set; }

    /// <summary>OK, SIN_DATOS (no hay real: nunca 0 %) o SIN_OBJETIVO.</summary>
    public string Estado { get; set; } = string.Empty;

    /// <summary>Semáforo del real contra el objetivo a la hora (mismos cortes que la eficiencia).</summary>
    public string Nivel { get; set; } = string.Empty;

    public bool Provisorio { get; set; }
}

public class VelocidadTableroDto
{
    /// <summary>Minuto del primer valor de la serie (hora de planta).</summary>
    public DateTimeOffset Desde { get; set; }

    /// <summary>m/min de cada minuto desde <see cref="Desde"/> (índice i = Desde + i min). Null = sin datos; 0 = parada.</summary>
    public List<decimal?> MetrosPorMinuto { get; set; } = [];

    /// <summary>Eje X: el turno completo (inicio y fin programados, o reales si se pasaron).</summary>
    public DateTimeOffset EjeDesde { get; set; }
    public DateTimeOffset EjeHasta { get; set; }

    /// <summary>Velocidad en marcha del turno (la de IndicadoresService: sólo los minutos en marcha).</summary>
    public decimal? Promedio { get; set; }

    /// <summary>Último minuto con dato.</summary>
    public decimal? Actual { get; set; }

    /// <summary>Velocidad normal de referencia (670 m/min).</summary>
    public decimal Referencia { get; set; }

    /// <summary>Algún tramo del turno no tiene metros por manga en el parámetro (esos minutos quedan sin dato).</summary>
    public bool FaltaMetraje { get; set; }

    /// <summary>Tramos de producto (para marcar el cambio de producto en el gráfico).</summary>
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

    /// <summary>Minutos de detención del turno que cuentan (declaradas que cuentan + pendientes estimadas).</summary>
    public int MinutosTotal { get; set; }

    /// <summary>Minutos pendientes de declaración (desde la parada de cámara hasta el arranque detectado o ahora).</summary>
    public int MinutosPendientes { get; set; }
    public int CantidadPendientes { get; set; }

    /// <summary>Minutos de tipos que no cuentan para indicadores (paradas programadas): aparte del total.</summary>
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

/// <summary>GET api/tablero/turnos (modo gerencial): líneas al alcance y sus turnos de producción del día.</summary>
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

    /// <summary>Suma en indisponibilidad (tipo de falla que cuenta, o sin tipo todavía).</summary>
    public bool Cuenta { get; set; }
    public bool PendienteDeclaracion { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}

public class CamaraTableroDto
{
    /// <summary>La línea tiene alguna cámara de conteo habilitada.</summary>
    public bool TieneCamara { get; set; }

    /// <summary>Último dato recibido de las cámaras de conteo de la línea (hora de planta).</summary>
    public DateTimeOffset? UltimoDato { get; set; }

    public int? MinutosDesdeUltimoDato { get; set; }

    /// <summary>Turno abierto y más de 5 min sin datos (como la alerta SIN_DATOS).</summary>
    public bool SinDatos { get; set; }

    /// <summary>Alertas de cámara abiertas de la línea (congelada, sin video, sin datos…).</summary>
    public List<AlertaCamaraDto> Alertas { get; set; } = [];
}
