namespace PSAG.Mantenimiento.Web.Models;

// T-798 etapa 6 · Maestros de producción (api/productos-maestro y api/parametros-linea-producto). Espejo de los DTO
// de Application (PSAG.Mantenimiento.Application.DTOs.Produccion.MaestrosProduccionDtos).

public class ProductoMaestroDto
{
    public int ProductoMaestroId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public string? TipoProducto { get; set; }
    public decimal? MetrosPorManga { get; set; }
    public int? RollosPorManga { get; set; }
    public int? RollosPorPaquete { get; set; }
    public List<ProductoSkuDto> Skus { get; set; } = [];
}

public class ProductoSkuDto
{
    public const string TipoBolson = "BOLSON";
    public const string TipoPallet = "PALLET";

    public int ProductoSkuId { get; set; }
    public int ProductoMaestroId { get; set; }
    public string CodigoSku { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string TipoSku { get; set; } = TipoBolson;
    public int? PaquetesPorBolson { get; set; }
    public int? SkuContenidoId { get; set; }
    public string? SkuContenidoCodigo { get; set; }
    public int? BolsonesPorPallet { get; set; }
    public bool Activo { get; set; } = true;
}

public class ParametroLineaProductoDto
{
    public int ParametroLineaProductoId { get; set; }
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int ProductoMaestroId { get; set; }
    public string ProductoCodigo { get; set; } = string.Empty;
    public string ProductoNombre { get; set; } = string.Empty;
    public bool ProductoActivo { get; set; }
    public DateOnly VigenteDesde { get; set; }
    public bool Habilitado { get; set; } = true;
    public int? MangasObjetivoTurno { get; set; }
    public int? BolsonesObjetivoTurno { get; set; }
    public int MinutosTurnoReferencia { get; set; } = 480;

    /// <summary>VIGENTE, FUTURA o REEMPLAZADA.</summary>
    public string EstadoVigencia { get; set; } = string.Empty;
}
