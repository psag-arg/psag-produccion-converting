namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Relación muchos a muchos entre Líneas y Productos.
/// Define qué productos puede fabricar cada línea.
/// </summary>
public class LineaProducto
{
    public int LineaId { get; set; }
    public int ProductoId { get; set; }

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Producto Producto { get; set; } = null!;
}
