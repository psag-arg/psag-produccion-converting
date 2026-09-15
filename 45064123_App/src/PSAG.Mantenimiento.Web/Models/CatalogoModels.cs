namespace PSAG.Mantenimiento.Web.Models;

public class LineaDto
{
    public int LineaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

public class EquipoDto
{
    public int EquipoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int LineaId { get; set; }
    public string? LineaNombre { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

public class TipoFallaDto
{
    public int TipoFallaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

public class DetalleFallaDto
{
    public int DetalleFallaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int? TipoFallaId { get; set; }
    public string? TipoFallaNombre { get; set; }
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public int Orden { get; set; }
    public bool RequiereDescripcion { get; set; }
    public bool Activo { get; set; }
}

public class TurnoDto
{
    public int TurnoId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public TimeSpan HoraInicio { get; set; }
    public TimeSpan HoraFin { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

public class MaquinistaDto
{
    public int UsuarioLineaId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string NombreUsuario { get; set; } = string.Empty;
    public string EmailUsuario { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? TurnoId { get; set; }
    public string? TurnoNombre { get; set; }
    public DateTimeOffset FechaAsignacion { get; set; }
}
