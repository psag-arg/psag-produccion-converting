namespace PSAG.Mantenimiento.Application.DTOs.Proceso;

public class GrupoParametroDto
{
    public int GrupoParametroId { get; set; }
    public int? LineaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; }

    public List<ParametroDto> Parametros { get; set; } = new();
}
