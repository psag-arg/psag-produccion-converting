using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Define el valor estándar de un parámetro para una combinación línea + producto.
/// "El que cambia el parámetro es el producto" - el mismo parámetro tiene
/// distintos estándares según qué producto esté corriendo.
/// </summary>
public class EstandarParametro : EntityBase
{
    public int EstandarParametroId { get; set; }
    public int LineaId { get; set; }
    public int ProductoId { get; set; }
    public int ParametroId { get; set; }

    /// <summary>
    /// Valor estándar en formato texto (para mostrar).
    /// Puede ser "152", "3.8 - 4.1 (bar)", "21-22", etc.
    /// </summary>
    public string? ValorEstandarTexto { get; set; }

    /// <summary>
    /// Valor mínimo del rango (para RANGO) o valor exacto (para NUMERICO).
    /// </summary>
    public decimal? ValorMin { get; set; }

    /// <summary>
    /// Valor máximo del rango (solo para RANGO).
    /// </summary>
    public decimal? ValorMax { get; set; }

    /// <summary>
    /// Tolerancia permitida para valores NUMERICO.
    /// Si el valor real está dentro de ValorMin ± Tolerancia, no es desvío.
    /// </summary>
    public decimal? Tolerancia { get; set; }

    public bool Activo { get; set; } = true;

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Producto Producto { get; set; } = null!;
    public virtual Parametro Parametro { get; set; } = null!;

    /// <summary>
    /// Verifica si un valor real está dentro del estándar.
    /// </summary>
    public bool EstaEnEstandar(decimal? valorReal)
    {
        if (!valorReal.HasValue)
            return true; // Sin valor no es desvío

        if (!ValorMin.HasValue)
            return true; // Sin estándar definido no es desvío

        // Rango: valor debe estar entre min y max
        if (ValorMax.HasValue)
        {
            return valorReal.Value >= ValorMin.Value && valorReal.Value <= ValorMax.Value;
        }

        // Numérico con tolerancia
        if (Tolerancia.HasValue)
        {
            return Math.Abs(valorReal.Value - ValorMin.Value) <= Tolerancia.Value;
        }

        // Numérico exacto
        return valorReal.Value == ValorMin.Value;
    }
}
