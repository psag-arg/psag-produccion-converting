namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Parametrización línea-producto (tabla PRD_ParametrosLineaProducto, ex LineasProductos) = PRESUPUESTO.
/// Por vigencia: el vigente para una fecha es el de mayor <see cref="VigenteDesde"/> menor o igual a esa fecha
/// (ver <see cref="Services.ParametroLineaProductoVigencia"/>). <see cref="Habilitado"/> dice si el producto
/// se puede producir en la línea (lo usa Control de Proceso para listar productos).
/// </summary>
public class ParametroLineaProducto
{
    public const int MinutosTurnoReferenciaPorDefecto = 480;

    public int ParametroLineaProductoId { get; set; }
    public int LineaId { get; set; }
    public int ProductoMaestroId { get; set; }
    public DateOnly VigenteDesde { get; set; }
    public bool Habilitado { get; set; } = true;

    /// <summary>Objetivo de mangas por turno. Null = sin objetivo.</summary>
    public int? MangasObjetivoTurno { get; set; }

    /// <summary>Objetivo de bolsones por turno. Null = no se mide (sin datos).</summary>
    public int? BolsonesObjetivoTurno { get; set; }

    public int MinutosTurnoReferencia { get; set; } = MinutosTurnoReferenciaPorDefecto;

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual ProductoMaestro ProductoMaestro { get; set; } = null!;
}
