namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>Parada de cámara corta (no llegó al umbral) candidata a agruparse, o ya agrupada. Horas de planta.</summary>
public class MicrocorteDto
{
    public long ParadaCamaraId { get; set; }
    public int PuntoControlCalidadId { get; set; }
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset FechaHoraFin { get; set; }
    public int DuracionSegundos { get; set; }
    public string Tipo { get; set; } = string.Empty;
}

/// <summary>
/// Microcortes de la misma cámara/línea en los 10 minutos previos al inicio de la detención, sin agrupar.
/// <see cref="Pregunta"/> es el texto para el operario ("Hubo 2 microcortes antes de esta parada (11:08, 11:12).
/// ¿Mismo motivo?"), null si no hay ninguno.
/// </summary>
public class MicrocortesPreviosDto
{
    public List<MicrocorteDto> Microcortes { get; set; } = new();
    public string? Pregunta { get; set; }

    /// <summary>Por qué no se buscaron (detención sin hora de parada), o null.</summary>
    public string? Aviso { get; set; }

    /// <summary>Ventana buscada, en hora de planta.</summary>
    public DateTimeOffset? Desde { get; set; }
    public DateTimeOffset? Hasta { get; set; }
}

/// <summary>
/// Microcortes agrupados activos de una detención y el tiempo agrupado (misma fórmula que mnt.vw_DetencionesAgrupadas).
/// Sólo informativo y para reportes: <see cref="MinutosEfectivos"/> de la detención NO cambia al agrupar.
/// </summary>
public class AgrupacionDetencionDto
{
    public long DetencionId { get; set; }
    public List<MicrocorteDto> Agrupados { get; set; } = new();
    public DateTimeOffset? InicioReal { get; set; }
    public DateTimeOffset? FinReal { get; set; }
    public int CantidadMicrocortes { get; set; }
    public int SegundosMicrocortes { get; set; }
    public int SegundosDetencion { get; set; }
    public int SegundosTotales { get; set; }
    public int MinutosEfectivos { get; set; }
}

/// <summary>Paradas de cámara a agrupar o desagrupar (ParadaCamaraId).</summary>
public class AgruparMicrocortesRequest
{
    public List<long> ParadaCamaraIds { get; set; } = new();
}
