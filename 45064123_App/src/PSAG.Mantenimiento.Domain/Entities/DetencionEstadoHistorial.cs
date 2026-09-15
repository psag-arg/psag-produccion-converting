using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Registro histórico de cambios de estado de una detención.
/// Esta tabla es de solo inserción - no se permiten actualizaciones ni eliminaciones.
/// </summary>
public class DetencionEstadoHistorial
{
    public long DetencionEstadoHistorialId { get; set; }
    public long DetencionId { get; set; }

    /// <summary>
    /// Estado anterior al cambio. Null para el primer registro (creación).
    /// </summary>
    public EstadoDetencion? EstadoAnterior { get; set; }

    /// <summary>
    /// Nuevo estado asignado.
    /// </summary>
    public EstadoDetencion EstadoNuevo { get; set; }

    public DateTimeOffset FechaHora { get; set; }
    public string UsuarioId { get; set; } = string.Empty;

    /// <summary>
    /// Observación opcional del cambio de estado.
    /// </summary>
    public string? Observacion { get; set; }

    /// <summary>
    /// Origen de la acción (Web, API, Sistema).
    /// </summary>
    public string? OrigenAccion { get; set; }

    /// <summary>
    /// ID de correlación para trazabilidad.
    /// </summary>
    public string? CorrelationId { get; set; }

    // Navegación
    public virtual Detencion Detencion { get; set; } = null!;
}
