namespace PSAG.Mantenimiento.Web.Models;

public class UsuarioDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<LineaAsignadaDto> LineasAsignadas { get; set; } = Array.Empty<LineaAsignadaDto>();
    public bool Activo { get; set; }
    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset? UltimoAcceso { get; set; }
}

public class LineaAsignadaDto
{
    public int LineaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
}

public class CrearUsuarioRequest
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public IList<int> LineasAsignadas { get; set; } = new List<int>();
}

public class ActualizarUsuarioRequest
{
    public string Email { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public IList<string> Roles { get; set; } = new List<string>();
    public IList<int> LineasAsignadas { get; set; } = new List<int>();
    public bool Activo { get; set; }
}

public class EliminarUsuarioResponse
{
    public bool Eliminado { get; set; }
    public bool Desactivado { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}
