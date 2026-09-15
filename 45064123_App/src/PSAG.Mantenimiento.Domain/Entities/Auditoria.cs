using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Registro de auditoría del sistema.
/// Esta tabla es de solo inserción - no se permiten actualizaciones ni eliminaciones.
/// </summary>
public class Auditoria
{
    public long AuditoriaId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public TipoAccionAuditoria Accion { get; set; }

    /// <summary>
    /// Nombre de la entidad afectada (ej: Evento, Linea, Usuario).
    /// </summary>
    public string Entidad { get; set; } = string.Empty;

    /// <summary>
    /// Identificador del registro afectado.
    /// </summary>
    public string? EntidadId { get; set; }

    /// <summary>
    /// Valores anteriores en formato JSON.
    /// </summary>
    public string? ValoresAnteriores { get; set; }

    /// <summary>
    /// Valores nuevos en formato JSON.
    /// </summary>
    public string? ValoresNuevos { get; set; }

    /// <summary>
    /// Dirección IP del cliente.
    /// </summary>
    public string? DireccionIp { get; set; }

    /// <summary>
    /// ID de correlación para trazabilidad.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Descripción adicional de la acción.
    /// </summary>
    public string? Descripcion { get; set; }
}
