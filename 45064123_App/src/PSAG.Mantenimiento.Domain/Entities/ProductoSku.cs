namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// SKU de un producto maestro (tabla ABM_ProductosSku). No existe un SKU sin maestro.
/// Jerarquía: un SKU BOLSON lleva <see cref="PaquetesPorBolson"/> paquetes; un SKU PALLET lleva
/// <see cref="BolsonesPorPallet"/> bolsones del SKU bolsón <see cref="SkuContenidoId"/>.
/// Las cantidades aceptan nulo (dato todavía no informado). Lo controlan CHECKs por tipo.
/// </summary>
public class ProductoSku
{
    public const string TipoBolson = "BOLSON";
    public const string TipoPallet = "PALLET";

    public int ProductoSkuId { get; set; }
    public int ProductoMaestroId { get; set; }
    public string CodigoSku { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>BOLSON o PALLET.</summary>
    public string TipoSku { get; set; } = TipoBolson;

    /// <summary>Solo BOLSON. Único lugar del dato (también lo usa la cámara de calidad).</summary>
    public int? PaquetesPorBolson { get; set; }

    /// <summary>Solo PALLET: el SKU bolsón que lleva.</summary>
    public int? SkuContenidoId { get; set; }

    /// <summary>Solo PALLET.</summary>
    public int? BolsonesPorPallet { get; set; }

    public bool Activo { get; set; } = true;

    // Navegación
    public virtual ProductoMaestro ProductoMaestro { get; set; } = null!;
    public virtual ProductoSku? SkuContenido { get; set; }
}
