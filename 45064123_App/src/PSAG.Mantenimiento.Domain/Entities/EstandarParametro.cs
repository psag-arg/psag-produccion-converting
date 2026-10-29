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
    /// Mínimo aceptable. Con ValorMax es un rango; con Tolerancia es el valor nominal
    /// (Tolerancia 0 = valor exacto); solo, es un piso (≥). NULL = sin límites: el texto
    /// queda como referencia y no marca desvío. Ver RangoControl.DesdeEstandar.
    /// </summary>
    public decimal? ValorMin { get; set; }

    /// <summary>
    /// Máximo aceptable del rango (inclusivo).
    /// </summary>
    public decimal? ValorMax { get; set; }

    /// <summary>
    /// Tolerancia sobre ValorMin cuando no hay ValorMax.
    /// Si el valor real está dentro de ValorMin ± Tolerancia, no es desvío.
    /// </summary>
    public decimal? Tolerancia { get; set; }

    public bool Activo { get; set; } = true;

    // Navegación
    public virtual Linea Linea { get; set; } = null!;
    public virtual Producto Producto { get; set; } = null!;
    public virtual Parametro Parametro { get; set; } = null!;
}
