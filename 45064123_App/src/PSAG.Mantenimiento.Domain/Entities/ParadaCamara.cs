using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Parada detectada por la cámara (T-610), ya cerrada: el servicio la manda cuando la línea vuelve a marcha
/// (o cuando se corta el video, con FinIncierto). Insert-only. No reemplaza a la Detencion: sirve para ver
/// qué paradas no tienen detención cargada.
/// </summary>
public class ParadaCamara
{
    public long ParadaCamaraId { get; set; }

    public int LineaId { get; set; }
    public int PuntoControlCalidadId { get; set; }

    /// <summary>Turno materializado con la hora de inicio.</summary>
    public int? TurnoId { get; set; }

    public DateTimeOffset FechaHoraInicio { get; set; }
    public DateTimeOffset FechaHoraFin { get; set; }
    public int DuracionSegundos { get; set; }

    public TipoParadaCamara Tipo { get; set; }

    /// <summary>Carriles que no estuvieron en marcha durante la parada, separados por coma (ej.: "1,2,3,4").</summary>
    public string Carriles { get; set; } = string.Empty;

    /// <summary>El servicio arrancó con la línea ya parada: la parada pudo empezar antes.</summary>
    public bool InicioIncierto { get; set; }

    /// <summary>Se cortó el video antes de que la línea arrancara: la parada pudo seguir después.</summary>
    public bool FinIncierto { get; set; }

    /// <summary>Id del servicio (punto + inicio). Único: un reenvío no duplica.</summary>
    public string IdentificadorExterno { get; set; } = string.Empty;

    public DateTimeOffset FechaHoraCreacion { get; set; }

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual PuntoControlCalidad PuntoControl { get; set; } = null!;
    public virtual Turno? Turno { get; set; }
}
