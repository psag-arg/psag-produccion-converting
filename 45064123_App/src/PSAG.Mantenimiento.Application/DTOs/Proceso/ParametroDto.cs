namespace PSAG.Mantenimiento.Application.DTOs.Proceso;

public class ParametroDto
{
    public int ParametroId { get; set; }
    public int GrupoParametroId { get; set; }
    public string? Seccion { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? UnidadMedida { get; set; }
    public string TipoDato { get; set; } = "NUMERICO";
    public string TipoDatoDescripcion { get; set; } = string.Empty;
    public decimal? ValorMin { get; set; }
    public decimal? ValorMax { get; set; }
    public string Frecuencia { get; set; } = "POR_TURNO";
    public string FrecuenciaDescripcion { get; set; } = string.Empty;
    public int Orden { get; set; }
    public bool Activo { get; set; }
}

public class ParametroConEstandarDto : ParametroDto
{
    public string? ValorEstandarTexto { get; set; }
    public decimal? ValorMin { get; set; }
    public decimal? ValorMax { get; set; }
    public decimal? Tolerancia { get; set; }
}
