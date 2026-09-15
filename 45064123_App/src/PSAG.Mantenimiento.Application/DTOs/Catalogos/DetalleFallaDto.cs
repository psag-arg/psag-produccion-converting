namespace PSAG.Mantenimiento.Application.DTOs.Catalogos;

/// <summary>
/// DTO para tercer nivel de clasificación de fallas.
/// Puede ser genérico (TipoFallaId=null y/o EquipoId=null) o específico.
/// </summary>
public class DetalleFallaDto
{
    public int DetalleFallaId { get; set; }
    public int? TipoFallaId { get; set; }
    public string? TipoFallaNombre { get; set; }
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool RequiereDescripcion { get; set; }
    public bool Activo { get; set; }
}

public class CrearDetalleFallaRequest
{
    public int? TipoFallaId { get; set; }
    public int? EquipoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool RequiereDescripcion { get; set; }
}

public class ActualizarDetalleFallaRequest
{
    public int? TipoFallaId { get; set; }
    public int? EquipoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool RequiereDescripcion { get; set; }
    public bool Activo { get; set; }
}

/// <summary>
/// Request para filtrar detalles de falla en cascada.
/// </summary>
public class DetalleFallaFiltroRequest
{
    public int? TipoFallaId { get; set; }
    public int? EquipoId { get; set; }
    public bool SoloActivos { get; set; } = true;
}
