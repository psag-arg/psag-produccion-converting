using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Alerta de cámara (T-610): congelada o sin video (las abre y cierra el servicio de cámara), sin configuración
/// local (la manda el servicio) o sin datos con el turno abierto (la abre el job de la API; T-798 etapa 4). Se cierra
/// (FechaHoraFin) cuando vuelve la imagen o llegan minutos. Nunca se borra.
/// </summary>
public class AlertaCamara
{
    public long AlertaCamaraId { get; set; }

    public int CamaraId { get; set; }

    public TipoAlertaCamara Tipo { get; set; }

    public DateTimeOffset FechaHoraInicio { get; set; }

    /// <summary>Null mientras sigue la alerta.</summary>
    public DateTimeOffset? FechaHoraFin { get; set; }

    /// <summary>Cámara + tipo + inicio. Único: un reenvío no duplica, sólo completa el fin.</summary>
    public string IdentificadorExterno { get; set; } = string.Empty;

    public string? Detalle { get; set; }

    public DateTimeOffset FechaHoraCreacion { get; set; }
    public DateTimeOffset FechaHoraActualizacion { get; set; }

    public bool Activa => FechaHoraFin == null;

    // Navegación
    public virtual Camara Camara { get; set; } = null!;
}
