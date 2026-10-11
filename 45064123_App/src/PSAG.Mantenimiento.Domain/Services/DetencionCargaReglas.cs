using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// De dónde sale el turno de una detención nueva.
/// </summary>
public enum FuenteTurno
{
    /// <summary>La hora de inicio cargada.</summary>
    HoraInicio,
    /// <summary>El turno asignado al maquinista en la línea.</summary>
    Maquinista,
    /// <summary>La hora del momento de guardar (no hay horas ni turno asignado).</summary>
    HoraActual
}

/// <summary>
/// Reglas de la carga de una detención que no dependen de la base. Funciones puras.
/// </summary>
public static class DetencionCargaReglas
{
    public const string MensajeFechaFutura = "La fecha no puede ser posterior a hoy.";
    public const string MensajeLineaNoAsignada = "No tenés asignada esta línea. Pedile al administrador que te la asigne.";
    public const string MensajeMaquinistaSinTurno =
        "Este maquinista no tiene turno. Asignáselo en Configuración → Maquinistas o cargá la hora de inicio y fin.";

    /// <summary>
    /// Con hora de inicio y fin, manda la hora. Si no, el turno del maquinista (elegido o propio).
    /// Un maquinista elegido sin turno y sin horas no se puede resolver: devuelve null.
    /// </summary>
    public static FuenteTurno? ElegirFuenteTurno(bool conHoras, bool maquinistaElegido, bool maquinistaTieneTurno)
    {
        if (conHoras) return FuenteTurno.HoraInicio;
        if (maquinistaTieneTurno) return FuenteTurno.Maquinista;
        return maquinistaElegido ? null : FuenteTurno.HoraActual;
    }

    public static bool EsFechaFutura(DateOnly fecha, DateOnly hoy) => fecha > hoy;

    /// <summary>
    /// El operario sólo carga en sus líneas. Los demás roles que cargan no tienen esa restricción.
    /// </summary>
    public static bool PuedeCargarEnLinea(IEnumerable<string> roles, bool lineaAsignada)
    {
        var rolesUsuario = roles.ToList();
        var soloOperario = rolesUsuario.Contains(RolesSistema.Operario)
            && !rolesUsuario.Any(r => r is RolesSistema.Administrador or RolesSistema.Supervisor or RolesSistema.Mantenimiento);
        return !soloOperario || lineaAsignada;
    }
}
