namespace PSAG.Mantenimiento.Application.DTOs.Catalogos;

public class TipoFallaDto
{
    public int TipoFallaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public string? Color { get; set; }
    public bool Activo { get; set; }

    /// <summary>T-798: las detenciones de este tipo suman en indisponibilidad y eficiencia.</summary>
    public bool CuentaParaIndicadores { get; set; } = true;
}

public class CrearTipoFallaRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public string? Color { get; set; }

    /// <summary>T-798: por defecto, sí cuenta (lo conservador).</summary>
    public bool CuentaParaIndicadores { get; set; } = true;
}

public class ActualizarTipoFallaRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public string? Color { get; set; }
    public bool Activo { get; set; }

    /// <summary>T-798: null = no se cambia (clientes viejos que no lo mandan).</summary>
    public bool? CuentaParaIndicadores { get; set; }
}
