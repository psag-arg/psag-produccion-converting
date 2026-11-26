using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Producto maestro (tabla ABM_ProductosMaestro, ex Productos): lo que se fabrica en una línea,
/// independiente de cómo se empaque. Determina los estándares de parámetros a verificar.
/// Los SKU (bolsón, pallet) cuelgan de acá: ver <see cref="ProductoSku"/>.
/// </summary>
public class ProductoMaestro : EntityBase
{
    public int ProductoMaestroId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    /// <summary>Tipo de producto (texto libre por ahora: higiénico, cocina...).</summary>
    public string? TipoProducto { get; set; }

    /// <summary>Metros por manga (30 / 50). Null si no se conoce.</summary>
    public decimal? MetrosPorManga { get; set; }

    /// <summary>Rollos por manga (28 en SH 30). Null si no se conoce.</summary>
    public int? RollosPorManga { get; set; }

    /// <summary>Rollos por paquete (el "x 4").</summary>
    public int? RollosPorPaquete { get; set; }

    // Navegación
    public virtual ICollection<ParametroLineaProducto> ParametrosLineaProducto { get; set; } = new List<ParametroLineaProducto>();
    public virtual ICollection<ProductoSku> Skus { get; set; } = new List<ProductoSku>();
    public virtual ICollection<EstandarParametro> Estandares { get; set; } = new List<EstandarParametro>();
    public virtual ICollection<RegistroProceso> RegistrosProceso { get; set; } = new List<RegistroProceso>();
}
