using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Nota de seguimiento de una detención.
/// Esta tabla es de solo inserción - no se permiten actualizaciones ni eliminaciones.
/// </summary>
public class DetencionNota
{
    public long DetencionNotaId { get; set; }
    public long DetencionId { get; set; }

    /// <summary>
    /// Contenido de la nota.
    /// </summary>
    public string Texto { get; set; } = string.Empty;

    public string UsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }

    /// <summary>
    /// Tipo de nota (Operativa, Técnica, Cierre).
    /// </summary>
    public TipoNota TipoNota { get; set; }

    /// <summary>
    /// Indica si esta nota es la nota de cierre de la detención.
    /// </summary>
    public bool EsNotaDeCierre { get; set; }

    // Navegación
    public virtual Detencion Detencion { get; set; } = null!;
}
