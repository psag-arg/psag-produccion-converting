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
