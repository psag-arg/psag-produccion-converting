using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Punto de control de calidad de una línea (ej.: salida de la cortadora de la Tango 2). Lo administra el
/// administrador desde Maestros → Calidad. El detector se identifica con el Codigo, que es único en todo el sistema.
/// T-798 etapa 4: los datos de la cámara (canal, umbral, método...) pasaron a <see cref="Entities.Camara"/>; el punto
/// sólo <b>usa</b> una cámara (opcional), no la duplica.
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

    /// <summary>Cámara que usa el punto (ej.: la cámara 3 de la embolsadora). Opcional.</summary>
    public int? CamaraId { get; set; }

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Camara? Camara { get; set; }
}
