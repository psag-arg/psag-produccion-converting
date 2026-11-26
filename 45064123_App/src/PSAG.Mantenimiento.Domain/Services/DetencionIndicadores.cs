using System.Linq.Expressions;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Clasificación de detenciones para indicadores (T-798 2.7 y 2.10). ÚNICO lugar de la regla:
/// <list type="bullet">
/// <item>Cuentan las detenciones cuyo tipo de falla tiene <c>CuentaParaIndicadores = 1</c>, y las que todavía no
/// tienen tipo (pendientes de declarar: lo conservador es contarlas).</item>
/// <item>Las de un tipo con <c>CuentaParaIndicadores = 0</c> (paradas programadas) se registran y se listan igual, pero
/// no suman en indisponibilidad ni en los minutos de parada (dashboard, acumulado del turno).</item>
/// </list>
/// </summary>
public static class DetencionIndicadores
{
    /// <summary>Para consultas de EF (se traduce a SQL con un LEFT JOIN a ABM_TiposFalla).</summary>
    public static readonly Expression<Func<Detencion, bool>> CuentaExpr =
        d => d.TipoFallaId == null || d.TipoFalla!.CuentaParaIndicadores;

    /// <summary>
    /// En memoria. La detención con tipo tiene que traer cargado su <see cref="Detencion.TipoFalla"/>: si no, falla
    /// fuerte en vez de contar mal.
    /// </summary>
    public static bool Cuenta(Detencion d)
    {
        if (d.TipoFallaId == null)
            return true;
        if (d.TipoFalla == null)
            throw new InvalidOperationException(
                $"La detención {d.Codigo} tiene tipo de falla pero no se cargó (Include TipoFalla) para clasificarla.");
        return d.TipoFalla.CuentaParaIndicadores;
    }

    /// <summary>Las que cuentan para indicadores.</summary>
    public static IEnumerable<Detencion> QueCuentan(this IEnumerable<Detencion> detenciones) => detenciones.Where(Cuenta);

    /// <summary>Σ MinutosEfectivos de las que cuentan para indicadores.</summary>
    public static int MinutosQueCuentan(this IEnumerable<Detencion> detenciones) =>
        detenciones.Where(Cuenta).Sum(d => d.MinutosEfectivos);
}
