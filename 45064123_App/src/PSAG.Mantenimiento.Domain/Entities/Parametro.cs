using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Parámetro de proceso a verificar.
/// Pertenece a un grupo y puede tener una sección dentro del grupo.
/// </summary>
public class Parametro : EntityBase
{
    public int ParametroId { get; set; }
    public int GrupoParametroId { get; set; }

    /// <summary>
    /// Sección dentro del grupo (ej: "DATOS PRODUCTO", "PRECORTE").
    /// Es el nivel intermedio de agrupación visual.
    /// </summary>
    public string? Seccion { get; set; }

    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? UnidadMedida { get; set; }

    /// <summary>
    /// Tipo de dato: NUMERICO, RANGO, TEXTO.
    /// Determina cómo se compara el valor real contra el estándar.
    /// </summary>
    public TipoDatoParametro TipoDato { get; set; } = TipoDatoParametro.NUMERICO;

    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    // Navegación
    public virtual GrupoParametro GrupoParametro { get; set; } = null!;
    public virtual ICollection<EstandarParametro> Estandares { get; set; } = new List<EstandarParametro>();
    public virtual ICollection<RegistroProcesoDetalle> RegistrosDetalle { get; set; } = new List<RegistroProcesoDetalle>();
}
