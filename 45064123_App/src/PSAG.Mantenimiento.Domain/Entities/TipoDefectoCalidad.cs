using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Tipo de defecto de calidad (ej.: rabo del rollo mayor a 1 cm, mancha de grasa). El maestro arranca
/// vacío: se carga desde Maestros cuando llegue la ficha de criterios del cliente (ver docs/CALIDAD.md).
/// El detector lo identifica por el Codigo.
/// </summary>
public class TipoDefectoCalidad : CatalogoBase
{
    public int TipoDefectoCalidadId { get; set; }

    /// <summary>
    /// Código que manda el detector. Único; se guarda en mayúsculas.
    /// </summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>
    /// Unidad del dato medido (mm, paquetes...). Opcional: hay defectos que no se miden.
    /// Es la unidad que toma el registro si el detector no manda otra.
    /// </summary>
    public string? UnidadMedida { get; set; }
}
