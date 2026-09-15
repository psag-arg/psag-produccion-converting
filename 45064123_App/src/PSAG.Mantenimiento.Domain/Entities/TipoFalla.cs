using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Tipo de falla (primer nivel del árbol de clasificación).
/// Ejemplo: Mecánica, Eléctrica, Operativa, Calidad.
/// </summary>
public class TipoFalla : CatalogoBase
{
    public int TipoFallaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }

    /// <summary>
    /// Indica si se requiere descripción adicional al seleccionar este tipo (ej: "Otro").
    /// </summary>
    public bool RequiereDescripcion { get; set; }

    // Navegación
    public virtual ICollection<Detencion> Detenciones { get; set; } = new List<Detencion>();
    public virtual ICollection<DetalleFalla> DetallesFalla { get; set; } = new List<DetalleFalla>();
}
