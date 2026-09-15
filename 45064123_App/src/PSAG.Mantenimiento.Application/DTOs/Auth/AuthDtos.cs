namespace PSAG.Mantenimiento.Application.DTOs.Auth;

public class LoginRequest
{
    public string Usuario { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTimeOffset Expiration { get; set; }
    public UsuarioInfoDto Usuario { get; set; } = null!;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class UsuarioInfoDto
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string NombreCompleto { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<int> LineasAsignadas { get; set; } = Array.Empty<int>();
    public bool RequiereCambioPassword { get; set; }
}

public class CambiarPasswordRequest
{
    public string PasswordActual { get; set; } = string.Empty;
    public string PasswordNuevo { get; set; } = string.Empty;
    public string ConfirmarPasswordNuevo { get; set; } = string.Empty;
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
