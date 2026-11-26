namespace PSAG.Mantenimiento.Web.Models;

/// <summary>Cámara del maestro único (Maestros → Cámaras, T-798 etapa 4). Espejo del DTO de la API.</summary>
public class CamaraDto
{
    public int CamaraId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int LineaId { get; set; }
    public string LineaNombre { get; set; } = string.Empty;
    public int? EquipoId { get; set; }
    public string? EquipoNombre { get; set; }
    public int? NumeroCanalGrabador { get; set; }
    public string Funcion { get; set; } = "CONTEO";
    public string? Magnitud { get; set; }
    public string? MetodoDeteccion { get; set; }
    public int? CantidadCarriles { get; set; }
    public bool DetectaParadas { get; set; }
    public bool CreaDetenciones { get; set; }
    public int? UmbralDetencionSegundos { get; set; }
    public int UmbralSegundos { get; set; } = 300;
    public string UmbralTexto { get; set; } = "5 min";
    public bool CuentaProduccionOficial { get; set; }
    public bool Habilitada { get; set; }
    public bool Activo { get; set; } = true;
    public DateTimeOffset? UltimoDatoRecibido { get; set; }
    public int? MinutosDesdeUltimoDato { get; set; }

    /// <summary>VERDE, AMARILLO, ROJO o SIN_DATOS (lo calcula la API).</summary>
    public string EstadoUltimoDato { get; set; } = string.Empty;
}
