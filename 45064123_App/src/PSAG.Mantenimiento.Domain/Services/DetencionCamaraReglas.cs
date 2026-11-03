using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>Maquinista elegido para una detención automática y por qué (queda en el historial).</summary>
public sealed record EleccionMaquinista(UsuarioLinea? Elegido, string Motivo);

/// <summary>
/// Reglas de la detención automática por cámara (T-610). Funciones puras: las usan la ingesta del servicio de
/// cámara, la declaración del operador y el aviso de fin de turno.
/// </summary>
public static class DetencionCamaraReglas
{
    /// <summary>Umbral por defecto (reunión 03/11/2026 [26:21]): 5 minutos, configurable por cámara.</summary>
    public const int MinutosUmbralPorDefecto = 5;

    public const int MinutosUmbralMinimo = 1;
    public const int MinutosUmbralMaximo = 240;

    /// <summary>Minutos antes del fin del turno en que se avisa al operador que le quedan detenciones sin declarar.</summary>
    public const int MinutosAvisoFinTurnoPorDefecto = 30;

    /// <summary>Duración máxima que se acepta al declarar con hora de arranque.</summary>
    public const int MaximoHorasDeclaradas = 12;

    /// <summary>Usuario técnico con el que queda creada la detención automática (CreadoPorUsuarioId).</summary>
    public const string UsuarioSistemaCamara = "SISTEMA-CAMARA";

    /// <summary>La detención sólo se crea si la parada llegó al umbral (a partir de 5 min, inclusive).</summary>
    public static bool SuperaUmbral(DateTimeOffset inicio, DateTimeOffset hasta, int minutosUmbral) =>
        hasta - inicio >= TimeSpan.FromMinutes(UmbralValido(minutosUmbral));

    /// <summary>Umbral dentro del rango admitido (un valor fuera de rango en la base vuelve al por defecto).</summary>
    public static int UmbralValido(int minutosUmbral) =>
        minutosUmbral is >= MinutosUmbralMinimo and <= MinutosUmbralMaximo ? minutosUmbral : MinutosUmbralPorDefecto;

    public static string? MotivoUmbralInvalido(int minutosUmbral) =>
        minutosUmbral is >= MinutosUmbralMinimo and <= MinutosUmbralMaximo
            ? null
            : $"El umbral de detención tiene que estar entre {MinutosUmbralMinimo} y {MinutosUmbralMaximo} minutos.";

    /// <summary>
    /// Maquinista del turno en que empezó la parada, aunque no haya nadie logueado. Regla:
    /// <list type="number">
    /// <item>Asignaciones de la línea para ese turno (Maestros → Maquinistas). Si hay una, esa.</item>
    /// <item>Si hay más de una (error de carga, como el turno mañana con dos maquinistas), la asignada más
    /// recientemente: suele ser la corrección. El motivo deja escritos los otros para que se revise.</item>
    /// <item>Si no hay ninguna para el turno, la única asignación de la línea sin turno fijo, si hay exactamente una.</item>
    /// <item>Si no, queda sin maquinista; al declarar, se completa con el operador que declara si está asignado a la línea.</item>
    /// </list>
    /// </summary>
    public static EleccionMaquinista ElegirMaquinista(IEnumerable<UsuarioLinea> asignacionesDeLaLinea, int? turnoId)
    {
        var todas = asignacionesDeLaLinea.ToList();
        var delTurno = turnoId == null ? [] : todas.Where(a => a.TurnoId == turnoId).ToList();

        if (delTurno.Count == 1)
            return new(delTurno[0], "Maquinista asignado al turno.");

        if (delTurno.Count > 1)
        {
            var ordenadas = delTurno.OrderByDescending(a => a.FechaAsignacion).ThenByDescending(a => a.UsuarioLineaId).ToList();
            var otros = string.Join(", ", ordenadas.Skip(1).Select(a => a.UsuarioId));
            return new(ordenadas[0], $"Hay {delTurno.Count} maquinistas asignados al turno; se tomó el asignado más recientemente. Revisar la asignación (otros: {otros}).");
        }

        var sinTurno = todas.Where(a => a.TurnoId == null).ToList();
        if (sinTurno.Count == 1)
            return new(sinTurno[0], "Sin maquinista para el turno; se tomó el único asignado a la línea sin turno fijo.");

        return new(null, sinTurno.Count > 1
            ? "Sin maquinista para el turno y varios asignados a la línea sin turno fijo: lo completa quien declara."
            : "Sin maquinista asignado al turno: lo completa quien declara.");
    }

    /// <summary>Texto de la detención automática que ve el operador mientras está pendiente (la descripción la escribe él).</summary>
    public static string ObservacionAlta(string codigoCamara, DateTimeOffset inicioPlanta, bool inicioIncierto) =>
        $"Detención creada por la cámara {codigoCamara}: parada desde {inicioPlanta:HH:mm:ss}" +
        (inicioIncierto ? " (la cámara arrancó con la máquina parada: pudo empezar antes)." : ".");

    /// <summary>
    /// Motivo por el que la declaración no se puede guardar, o null. Mismas exigencias que una detención manual
    /// (descripción obligatoria, minutos positivos) más la hora de arranque, que tiene que ser posterior a la parada
    /// y no futura.
    /// </summary>
    public static string? MotivoDeclaracionInvalida(
        Detencion detencion, string? descripcion, DateTimeOffset? arranque, int? minutosDeclarados, DateTimeOffset ahora)
    {
        if (detencion.EstadoActual != EstadoDetencion.PENDIENTE_JUSTIFICACION)
            return "La detención ya fue declarada.";
        if (string.IsNullOrWhiteSpace(descripcion))
            return "La descripción es obligatoria.";
        if (descripcion.Trim().Length > 2000)
            return "La descripción no puede exceder 2000 caracteres.";
        if (arranque is { } a)
        {
            if (detencion.FechaHoraParada is { } p && a <= p)
                return "La hora de arranque tiene que ser posterior a la hora de parada.";
            // HH:mm menor que la parada se toma como del día siguiente; más de 12 h es casi seguro un error de tipeo.
            if (detencion.FechaHoraParada is { } p2 && a - p2 > TimeSpan.FromHours(MaximoHorasDeclaradas))
                return $"La hora de arranque es anterior a la de parada o la parada duraría más de {MaximoHorasDeclaradas} h. Revisala.";
            if (a > ahora.AddMinutes(10))
                return "La hora de arranque no puede ser futura.";
            return null;
        }
        if (minutosDeclarados is not > 0)
            return "Completá la hora de arranque o los minutos de parada.";
        return null;
    }

    /// <summary>
    /// Minutos efectivos al declarar: con hora de arranque, la diferencia con la parada (CALCULADO, igual que una
    /// detención manual con horario); si no, los minutos que tipea el operador (DECLARADO).
    /// </summary>
    public static (OrigenTiempo Origen, int Minutos) TiempoDeclarado(DateTimeOffset? parada, DateTimeOffset? arranque, int? minutosDeclarados)
    {
        if (parada is { } p && arranque is { } a && a > p)
            return (OrigenTiempo.CALCULADO, Math.Max(1, (int)(a - p).TotalMinutes));
        return (OrigenTiempo.DECLARADO, Math.Max(1, minutosDeclarados ?? 1));
    }

    /// <summary>
    /// Minutos que faltan para el fin del turno que contiene <paramref name="horaPlanta"/>, o null si la hora no
    /// es de ese turno. Contempla turnos que cruzan medianoche.
    /// </summary>
    public static int? MinutosParaFinDeTurno(Turno turno, TimeOnly horaPlanta)
    {
        if (!turno.ContieneHora(horaPlanta))
            return null;
        var ahora = horaPlanta.ToTimeSpan();
        var fin = turno.HoraFin.ToTimeSpan();
        var falta = fin > ahora ? fin - ahora : TimeSpan.FromDays(1) - ahora + fin;
        return (int)Math.Ceiling(falta.TotalMinutes);
    }

    /// <summary>Hay que avisar: faltan <paramref name="minutosAntes"/> minutos o menos para el fin del turno y hay pendientes.</summary>
    public static bool CorrespondeAvisoFinTurno(int? minutosParaFin, int pendientes, int minutosAntes) =>
        pendientes > 0 && minutosParaFin is { } m && m <= minutosAntes;

    /// <summary>Quién ve la solapa "Pendiente de declaración": operario, supervisor y administrador.</summary>
    public static bool VeDeclaracion(IEnumerable<string> roles) =>
        roles.Any(r => r is Common.RolesSistema.Operario or Common.RolesSistema.Supervisor or Common.RolesSistema.Administrador);

    /// <summary>
    /// Filtro por línea de las pendientes de declaración: cada uno ve las de sus líneas asignadas; el administrador
    /// y el supervisor que no tienen líneas asignadas ven todas (el operario sin líneas no ve ninguna).
    /// </summary>
    public static bool VeTodasLasLineasParaDeclarar(IEnumerable<string> roles, int lineasAsignadas) =>
        lineasAsignadas == 0 && roles.Any(r => r is Common.RolesSistema.Administrador or Common.RolesSistema.Supervisor);

    /// <summary>Identificador único de la alerta: punto + tipo + inicio (UTC), como las paradas.</summary>
    public static string IdentificadorAlerta(string codigoPunto, TipoAlertaCamara tipo, DateTimeOffset inicio) =>
        $"{codigoPunto}-{tipo}-{inicio.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}";
}
