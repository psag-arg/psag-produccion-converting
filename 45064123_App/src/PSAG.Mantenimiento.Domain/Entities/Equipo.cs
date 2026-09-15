using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Representa un equipo (componente) dentro de una línea de producción.
/// En planta lo llaman "equipo"; "la máquina" se refiere a la línea completa.
/// </summary>
public class Equipo : CatalogoBase
{
    public int EquipoId { get; set; }
    public int LineaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual ICollection<Detencion> Detenciones { get; set; } = new List<Detencion>();
    public virtual ICollection<DetalleFalla> DetallesFalla { get; set; } = new List<DetalleFalla>();
}
