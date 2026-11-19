namespace PSAG.Mantenimiento.Web.Models;

// Agrupador de detenciones (reunión 17/11/2026): microcortes de cámara previos a una detención.
// Horas en hora de la planta (las convierte la API).

public class MicrocorteDto
{
    public long ParadaCamaraId { get; set; }
    public int PuntoControlCalidadId { get; set; }
    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset FechaHoraFin { get; set; }
    public int DuracionSegundos { get; set; }
    public string Tipo { get; set; } = string.Empty;
}

public class MicrocortesPreviosDto
{
    public List<MicrocorteDto> Microcortes { get; set; } = new();
    public string? Pregunta { get; set; }
    public string? Aviso { get; set; }
    public DateTimeOffset? Desde { get; set; }
    public DateTimeOffset? Hasta { get; set; }
}

/// <summary>Microcortes agrupados y tiempo agrupado. Sólo informativo: los minutos de la detención no cambian.</summary>
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

public class AgruparMicrocortesRequest
{
    public List<long> ParadaCamaraIds { get; set; } = new();
}
