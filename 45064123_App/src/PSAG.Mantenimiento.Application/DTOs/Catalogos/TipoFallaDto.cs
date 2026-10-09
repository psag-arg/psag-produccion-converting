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
}

public class CrearTipoFallaRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public string? Color { get; set; }
}

public class ActualizarTipoFallaRequest
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public string? Color { get; set; }
    public bool Activo { get; set; }
}
