using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Detalle de falla (tercer nivel del árbol de clasificación).
/// Se filtra por Equipo y/o TipoFalla. Si ambos son null, es genérico.
/// </summary>
public class DetalleFalla : CatalogoBase
{
    public int DetalleFallaId { get; set; }

    /// <summary>
    /// Equipo al que aplica este detalle. NULL = aplica a cualquier equipo.
    /// </summary>
    public int? EquipoId { get; set; }

    /// <summary>
    /// Tipo de falla al que aplica este detalle. NULL = aplica a cualquier tipo.
    /// </summary>
    public int? TipoFallaId { get; set; }

    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }

    /// <summary>
    /// Indica si se requiere descripción adicional al seleccionar este detalle.
    /// </summary>
    public bool RequiereDescripcion { get; set; }

    // Navegación
    public virtual Equipo? Equipo { get; set; }
    public virtual TipoFalla? TipoFalla { get; set; }
    public virtual ICollection<Detencion> Detenciones { get; set; } = new List<Detencion>();
}
