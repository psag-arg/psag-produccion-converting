using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Agrupa parámetros de proceso para organizar la captura.
/// Ej: "PARÁMETROS - Grupo Gofrador", "CALIDAD DE MANGA".
/// Puede ser específico de una línea o global (LineaId null).
/// </summary>
public class GrupoParametro : EntityBase
{
    public int GrupoParametroId { get; set; }

    /// <summary>
    /// Si es null, aplica a todas las líneas.
    /// </summary>
    public int? LineaId { get; set; }

    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;

    // Navegación
    public virtual Linea? Linea { get; set; }
    public virtual ICollection<Parametro> Parametros { get; set; } = new List<Parametro>();
}
