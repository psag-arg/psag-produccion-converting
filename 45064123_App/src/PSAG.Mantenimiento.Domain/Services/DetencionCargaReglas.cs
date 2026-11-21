using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Reglas de la carga de una detención que no dependen de la base. Funciones puras.
/// </summary>
public static class DetencionCargaReglas
{
    public const string MensajeFechaFutura = "La fecha no puede ser posterior a hoy.";
    public const string MensajeLineaNoAsignada = "No tenés asignada esta línea. Pedile al administrador que te la asigne.";
    public const string MensajeOperarioSinLinea =
        "Un operario necesita al menos una línea asignada para poder cargar detenciones.";
    public const string MensajeSinTurnoALaHora =
        "No hay un turno de planta para la hora de la parada. Revisá los horarios en Configuración → Turnos.";
    public const string MensajeOperadorNoHabilitado =
        "El maquinista elegido no está habilitado en la línea de la detención (Configuración → Maquinistas).";

    /// <summary>
    /// Instante que decide el turno de producción de una detención manual (T-798 etapa 3): la hora desde (con su
    /// fecha) si se cargó; si no, el momento de la carga.
    /// </summary>
    public static DateTimeOffset InstanteTurno(DateTimeOffset? fechaHoraParada, DateTimeOffset ahora) => fechaHoraParada ?? ahora;

    /// <summary>
    /// Operador (usuario) de una detención manual: el elegido a mano (admin/supervisor); si no, el operador del turno
    /// a esa hora; si no hay, quien carga si está habilitado en la línea; si no, ninguno.
    /// </summary>
    public static string? ElegirOperador(string? elegido, string? operadorDelTurno, string? usuarioQueCarga, bool cargaHabilitadoEnLinea) =>
        elegido ?? operadorDelTurno ?? (cargaHabilitadoEnLinea ? usuarioQueCarga : null);

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

    /// <summary>
    /// Un usuario que sólo es operario y no tiene líneas no puede cargar nada: se avisa al darlo de alta.
    /// </summary>
    public static bool OperarioSinLineas(IEnumerable<string> roles, int cantidadLineas) =>
        !PuedeCargarEnLinea(roles, cantidadLineas > 0);

    /// <summary>
    /// T-786: fin de una detención para ver si se superpone con otra: el arranque declarado; si no, el que vio la
    /// cámara; si no (pendiente de declaración), la parada más sus minutos (0 = sólo el instante de la parada).
    /// </summary>
    public static DateTimeOffset? FinParaSuperposicion(Detencion detencion) =>
        detencion.FechaHoraParada is not { } parada
            ? null
            : detencion.FechaHoraArranque ?? detencion.FechaHoraArranqueDetectada ?? parada.AddMinutes(detencion.MinutosEfectivos);

    /// <summary>
    /// La detención (con hora de parada) cae dentro de [desde, hasta): se pisa con una carga manual de ese horario.
    /// Las cargadas sólo con minutos, sin horario, no se pueden comparar y no cuentan.
    /// </summary>
    public static bool SeSuperpone(Detencion detencion, DateTimeOffset desde, DateTimeOffset hasta) =>
        detencion.FechaHoraParada is { } parada
        && parada < hasta
        && (parada >= desde || FinParaSuperposicion(detencion) > desde);
}
