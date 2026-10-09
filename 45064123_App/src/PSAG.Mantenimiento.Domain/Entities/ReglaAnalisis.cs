using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Regla que decide si una detención requiere análisis de falla y quién lo responde.
/// Aplica si coinciden el tipo de falla, la línea y el equipo (null = cualquiera)
/// y los minutos efectivos alcanzan el mínimo. Ver ReglaAnalisisEvaluador.
/// </summary>
public class ReglaAnalisis : CatalogoBase
{
    public int ReglaAnalisisId { get; set; }

    /// <summary>
    /// Null = aplica a todas las líneas.
    /// </summary>
    public int? LineaId { get; set; }

    public int TipoFallaId { get; set; }

    /// <summary>
    /// Null = aplica a todos los equipos.
    /// </summary>
    public int? EquipoId { get; set; }

    /// <summary>
    /// Minutos efectivos a partir de los cuales aplica la regla.
    /// </summary>
    public int MinutosMinimos { get; set; }

    /// <summary>
    /// Exactamente uno de los dos: rol responsable o usuario responsable.
    /// </summary>
    public string? ResponsableRol { get; set; }
    public string? ResponsableUsuarioId { get; set; }

    // Navegación
    public virtual Linea? Linea { get; set; }
    public virtual Equipo? Equipo { get; set; }
    public virtual TipoFalla TipoFalla { get; set; } = null!;
}
