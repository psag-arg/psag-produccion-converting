using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Vigencia de la parametrización línea-producto (PRD_ParametrosLineaProducto). Funciones puras.
/// Regla: para una línea y un producto, el vigente en una fecha es el registro con el mayor
/// VigenteDesde menor o igual a esa fecha. Si no hay ninguno, no hay parámetro vigente.
/// </summary>
public static class ParametroLineaProductoVigencia
{
    /// <summary>
    /// El parámetro vigente en la fecha para cada (línea, producto) presente en la lista, o ninguno si
    /// todavía no empezó su vigencia. No filtra por Habilitado: un vigente deshabilitado tapa a los anteriores.
    /// </summary>
    public static IReadOnlyList<ParametroLineaProducto> Vigentes(IEnumerable<ParametroLineaProducto> parametros, DateOnly fecha) =>
        parametros
            .Where(p => p.VigenteDesde <= fecha)
            .GroupBy(p => (p.LineaId, p.ProductoMaestroId))
            .Select(g => g.OrderByDescending(p => p.VigenteDesde).ThenByDescending(p => p.ParametroLineaProductoId).First())
            .ToList();

    /// <summary>
    /// El vigente en la fecha para una línea y un producto, o null.
    /// </summary>
    public static ParametroLineaProducto? Vigente(
        IEnumerable<ParametroLineaProducto> parametros, int lineaId, int productoMaestroId, DateOnly fecha) =>
        Vigentes(parametros.Where(p => p.LineaId == lineaId && p.ProductoMaestroId == productoMaestroId), fecha)
            .SingleOrDefault();

    /// <summary>
    /// Ids de los productos habilitados en la línea a esa fecha (vigente y Habilitado).
    /// </summary>
    public static IReadOnlyList<int> ProductosHabilitados(IEnumerable<ParametroLineaProducto> parametros, int lineaId, DateOnly fecha) =>
        Vigentes(parametros.Where(p => p.LineaId == lineaId), fecha)
            .Where(p => p.Habilitado)
            .Select(p => p.ProductoMaestroId)
            .ToList();
}
