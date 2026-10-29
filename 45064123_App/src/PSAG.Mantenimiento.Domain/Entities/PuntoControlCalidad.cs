using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Punto de control de calidad de una línea: en general una cámara (ej.: cámara 1 a la salida de la
/// cortadora de la Tango 2). Lo administra el administrador desde Maestros.
/// El detector se identifica con el Codigo, que es único en todo el sistema.
/// </summary>
public class PuntoControlCalidad : CatalogoBase
{
    public int PuntoControlCalidadId { get; set; }
    public int LineaId { get; set; }

    /// <summary>
    /// Código que manda el detector (ej.: T2-CAM1). Único; se guarda en mayúsculas.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
}
