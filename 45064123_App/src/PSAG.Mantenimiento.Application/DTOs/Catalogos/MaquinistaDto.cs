namespace PSAG.Mantenimiento.Application.DTOs.Catalogos;

/// <summary>
/// Habilitación de un usuario en una línea (ABM_OperadoresLinea), sólo lectura (T-802): se carga desde el alta del
/// usuario (Configuración → Usuarios). No tiene turno: el turno lo declara el operario al abrirlo.
/// </summary>
public class MaquinistaDto
{
    public int UsuarioLineaId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string NombreUsuario { get; set; } = string.Empty;
    public string EmailUsuario { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public DateTimeOffset FechaAsignacion { get; set; }
}
