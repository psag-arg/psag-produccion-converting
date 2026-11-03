using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Alerta de cámara congelada o sin video (T-610). La abre el servicio de cámara cuando deja de tener imagen
/// confiable y la cierra (FechaHoraFin) cuando vuelve. Nunca se borra. Mientras está abierta no se detectan
/// paradas, así que no se crean detenciones falsas.
/// </summary>
public class AlertaCamara
{
    public long AlertaCamaraId { get; set; }

    public int PuntoControlCalidadId { get; set; }
    public int LineaId { get; set; }

    public TipoAlertaCamara Tipo { get; set; }

    public DateTimeOffset FechaHoraInicio { get; set; }

    /// <summary>Null mientras sigue la alerta.</summary>
    public DateTimeOffset? FechaHoraFin { get; set; }

    /// <summary>Punto + tipo + inicio. Único: un reenvío no duplica, sólo completa el fin.</summary>
    public string IdentificadorExterno { get; set; } = string.Empty;

    public string? Detalle { get; set; }

    public DateTimeOffset FechaHoraCreacion { get; set; }
    public DateTimeOffset FechaHoraActualizacion { get; set; }

    public bool Activa => FechaHoraFin == null;

    // Navegación
    public virtual PuntoControlCalidad PuntoControl { get; set; } = null!;
    public virtual Linea Linea { get; set; } = null!;
}
