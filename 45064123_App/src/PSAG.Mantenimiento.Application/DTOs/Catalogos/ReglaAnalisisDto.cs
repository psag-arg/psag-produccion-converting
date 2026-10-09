namespace PSAG.Mantenimiento.Application.DTOs.Catalogos;

public class ReglaAnalisisDto
{
    public int ReglaAnalisisId { get; set; }
    public int? LineaId { get; set; }
    public string? LineaNombre { get; set; }
    public int TipoFallaId { get; set; }
    public string TipoFallaNombre { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public int MinutosMinimos { get; set; }
    public string? ResponsableRol { get; set; }
    public string? ResponsableUsuarioId { get; set; }
    public string? ResponsableUsuarioNombre { get; set; }
    public bool Activo { get; set; }
}

public class CrearReglaAnalisisRequest
{
    public int? LineaId { get; set; }
    public int TipoFallaId { get; set; }
    public int? EquipoId { get; set; }
    public int MinutosMinimos { get; set; }
    public string? ResponsableRol { get; set; }
    public string? ResponsableUsuarioId { get; set; }
}

public class ActualizarReglaAnalisisRequest : CrearReglaAnalisisRequest
{
    public bool Activo { get; set; }
}
