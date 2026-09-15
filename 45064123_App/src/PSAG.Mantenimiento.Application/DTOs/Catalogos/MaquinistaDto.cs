namespace PSAG.Mantenimiento.Application.DTOs.Catalogos;

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

public class CrearMaquinistaRequest
{
    public string UsuarioId { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public int? TurnoId { get; set; }
}

public class ActualizarMaquinistaRequest
{
    public int LineaId { get; set; }
    public int? TurnoId { get; set; }
}
