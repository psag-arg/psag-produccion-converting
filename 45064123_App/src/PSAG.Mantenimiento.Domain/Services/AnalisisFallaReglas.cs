using System.Linq.Expressions;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Reglas del análisis de falla: quién lo completa y qué hace falta para cerrarlo. Funciones puras.
/// </summary>
public static class AnalisisFallaReglas
{
    public static bool EsResponsable(Detencion detencion, string usuarioId, IEnumerable<string> roles)
    {
        return detencion.ResponsableUsuarioId == usuarioId
            || (detencion.ResponsableRol != null && roles.Contains(detencion.ResponsableRol));
    }

    /// <summary>
    /// Solución, causa y plan los completa el responsable o el administrador. El resto solo lee.
    /// </summary>
    public static bool PuedeEditar(Detencion detencion, string usuarioId, IEnumerable<string> roles)
    {
        return roles.Contains(RolesSistema.Administrador) || EsResponsable(detencion, usuarioId, roles);
    }

    /// <summary>
    /// Pendiente de análisis: la regla lo marcó y todavía no se cerró. Es la única definición de la condición;
    /// sirve para consultas (EF la traduce a SQL) y, compilada, para objetos en memoria.
    /// </summary>
    public static Expression<Func<Detencion, bool>> EstaAbiertoExpr { get; } =
        d => d.RequiereAnalisis && d.EstadoActual != EstadoDetencion.CERRADA;

    private static readonly Func<Detencion, bool> EstaAbiertoCompilado = EstaAbiertoExpr.Compile();

    public static bool EstaAbierto(Detencion detencion) => EstaAbiertoCompilado(detencion);

    /// <summary>
    /// Devuelve el motivo por el que el análisis no se puede cerrar, o null si está completo.
    /// </summary>
    public static string? MotivoNoCerrable(string? solucion, string? causa)
    {
        if (string.IsNullOrWhiteSpace(solucion))
            return "Para cerrar el análisis falta la solución.";

        if (string.IsNullOrWhiteSpace(causa))
            return "Para cerrar el análisis falta la causa de la falla.";

        return null;
    }
}
