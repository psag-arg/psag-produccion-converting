namespace PSAG.Mantenimiento.Application.DTOs.Produccion;

// T-798 etapa 6 · Maestros de producción: productos maestro con sus SKU (bolsón / pallet) y la parametrización
// línea + producto (presupuesto, por vigencia). Sólo el administrador los modifica; nada se borra.

public class ProductoMaestroDto
{
    public int ProductoMaestroId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
    public string? TipoProducto { get; set; }
    public decimal? MetrosPorManga { get; set; }
    public int? RollosPorManga { get; set; }
    public int? RollosPorPaquete { get; set; }

    /// <summary>SKU del maestro (también los inactivos; la pantalla los filtra).</summary>
    public List<ProductoSkuDto> Skus { get; set; } = [];
}

public class GuardarProductoMaestroRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public string? TipoProducto { get; set; }
    public decimal? MetrosPorManga { get; set; }
    public int? RollosPorManga { get; set; }
    public int? RollosPorPaquete { get; set; }
    public bool Activo { get; set; } = true;
}

public class ProductoSkuDto
{
    public int ProductoSkuId { get; set; }
    public int ProductoMaestroId { get; set; }
    public string CodigoSku { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>BOLSON o PALLET.</summary>
    public string TipoSku { get; set; } = string.Empty;
    public int? PaquetesPorBolson { get; set; }
    public int? SkuContenidoId { get; set; }
    public string? SkuContenidoCodigo { get; set; }
    public int? BolsonesPorPallet { get; set; }
    public bool Activo { get; set; }
}

public class GuardarProductoSkuRequest
{
    public int ProductoMaestroId { get; set; }
    public string CodigoSku { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public string TipoSku { get; set; } = string.Empty;
    public int? PaquetesPorBolson { get; set; }
    public int? SkuContenidoId { get; set; }
    public int? BolsonesPorPallet { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>Estado de una fila de la parametrización respecto de hoy.</summary>
public static class EstadoVigenciaParametro
{
    /// <summary>Es la que rige hoy para su línea y producto.</summary>
    public const string Vigente = "VIGENTE";
    /// <summary>Empieza después de hoy.</summary>
    public const string Futura = "FUTURA";
    /// <summary>La reemplazó otra más nueva.</summary>
    public const string Reemplazada = "REEMPLAZADA";
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
    public bool Habilitado { get; set; }
    public int? MangasObjetivoTurno { get; set; }
    public int? BolsonesObjetivoTurno { get; set; }
    public int MinutosTurnoReferencia { get; set; }

    /// <summary>VIGENTE, FUTURA o REEMPLAZADA (ver <see cref="EstadoVigenciaParametro"/>).</summary>
    public string EstadoVigencia { get; set; } = string.Empty;
}

public class GuardarParametroLineaProductoRequest
{
    public int LineaId { get; set; }
    public int ProductoMaestroId { get; set; }
    public DateOnly VigenteDesde { get; set; }
    public bool Habilitado { get; set; } = true;
    public int? MangasObjetivoTurno { get; set; }
    public int? BolsonesObjetivoTurno { get; set; }
    public int MinutosTurnoReferencia { get; set; } = 480;
}
