using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Reglas del análisis de falla: quién lo completa y qué hace falta para cerrarlo. Funciones puras.
/// </summary>
public static class AnalisisFallaReglas
{
    public const string RolAdministrador = "ADMINISTRADOR";

    /// <summary>
    /// Es responsable si la regla le asignó el usuario, o si tiene el rol responsable de la regla.
    /// </summary>
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
        return roles.Contains(RolAdministrador) || EsResponsable(detencion, usuarioId, roles);
    }

    /// <summary>
    /// Pendiente de análisis: la regla lo marcó y todavía no se cerró.
    /// </summary>
    public static bool EstaAbierto(Detencion detencion)
    {
        return detencion.RequiereAnalisis && detencion.EstadoActual != EstadoDetencion.CERRADA;
    }

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
