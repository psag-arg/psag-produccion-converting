using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Representa un producto que se fabrica en una línea.
/// El producto determina los estándares de parámetros a verificar.
/// </summary>
public class Producto : EntityBase
{
    public int ProductoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    // Navegación
    public virtual ICollection<LineaProducto> LineasProductos { get; set; } = new List<LineaProducto>();
    public virtual ICollection<EstandarParametro> Estandares { get; set; } = new List<EstandarParametro>();
    public virtual ICollection<RegistroProceso> RegistrosProceso { get; set; } = new List<RegistroProceso>();
}
