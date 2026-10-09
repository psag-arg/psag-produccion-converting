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
    /// <summary>
    /// Roles que una regla puede asignar como responsables de un análisis.
    /// </summary>
    public static readonly IReadOnlyList<string> RolesResponsables =
        [RolesSistema.Operario, RolesSistema.Supervisor, RolesSistema.Mantenimiento];

    public static bool EsResponsable(Detencion detencion, string usuarioId, IEnumerable<string> roles)
    {
        return detencion.ResponsableUsuarioId == usuarioId
            || (detencion.ResponsableRol != null && roles.Contains(detencion.ResponsableRol));
    }

    /// <summary>
    /// Muestra las pestañas de análisis en Mis pendientes: administrador, cualquier rol responsable,
    /// o al menos un análisis abierto asignado al usuario por id. <paramref name="analisisAsignados"/>
    /// son los análisis abiertos que el usuario ve (mismo criterio que la lista).
    /// </summary>
    public static bool MuestraAnalisis(string usuarioId, IEnumerable<string> roles, IEnumerable<Detencion> analisisAsignados)
    {
        var rolesUsuario = roles.ToList();
        if (rolesUsuario.Contains(RolesSistema.Administrador) || rolesUsuario.Any(rol => RolesResponsables.Contains(rol)))
            return true;

        return analisisAsignados.Any(d => d.ResponsableUsuarioId == usuarioId);
    }

    /// <summary>
    /// Solución, causa y plan los completa el responsable o el administrador. El resto solo lee.
    /// </summary>
    public static bool PuedeEditar(Detencion detencion, string usuarioId, IEnumerable<string> roles)
    {
        return roles.Contains(RolesSistema.Administrador) || EsResponsable(detencion, usuarioId, roles);
    }

    /// <summary>
    /// Análisis abierto: la regla lo marcó y está REGISTRADA (sin empezar) o EN_SEGUIMIENTO (en curso).
    /// PENDIENTE_JUSTIFICACION no es un análisis. Es la única definición de la condición; sirve para consultas
    /// (EF la traduce a SQL) y, compilada, para objetos en memoria. Si cambia, cambia EstaAbierto a la par.
    /// </summary>
    public static Expression<Func<Detencion, bool>> EstaAbiertoExpr { get; } =
        d => d.RequiereAnalisis
            && (d.EstadoActual == EstadoDetencion.REGISTRADA || d.EstadoActual == EstadoDetencion.EN_SEGUIMIENTO);

    private static readonly Func<Detencion, bool> EstaAbiertoCompilado = EstaAbiertoExpr.Compile();

    public static bool EstaAbierto(Detencion detencion) => EstaAbiertoCompilado(detencion);

    /// <summary>
    /// Asignado al usuario: por id o por uno de sus roles. Es EsResponsable en forma de expresión, para que
    /// EF la traduzca a SQL. Lista y conteo de Mis pendientes la usan juntos, así no pueden divergir.
    /// </summary>
    public static Expression<Func<Detencion, bool>> AsignadaAExpr(string usuarioId, IReadOnlyCollection<string> roles)
    {
        var rolesArray = roles.ToArray();
        return d => d.ResponsableUsuarioId == usuarioId
            || (d.ResponsableRol != null && rolesArray.Contains(d.ResponsableRol));
    }

    /// <summary>
    /// Si una regla deja de aplicar, la marca de análisis y su responsable sólo se quitan cuando el análisis
    /// no empezó: sigue REGISTRADA, sin solución, causa ni plan, y sin fecha de análisis. Si ya hay trabajo, se conserva.
    /// </summary>
    public static bool PuedeQuitarMarcaAnalisis(Detencion detencion) =>
        detencion.EstadoActual == EstadoDetencion.REGISTRADA
        && string.IsNullOrWhiteSpace(detencion.AccionesRealizadas)
        && string.IsNullOrWhiteSpace(detencion.CausaFalla)
        && string.IsNullOrWhiteSpace(detencion.PlanAccion)
        && !detencion.FechaHoraAnalisis.HasValue;

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
