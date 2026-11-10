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

    /// <summary>Una detención pendiente de declaración no se edita: se declara.</summary>
    public const string MensajeEditarPendiente =
        "La detención está pendiente de declaración: no se edita. Declarala desde Mis pendientes (Declarar).";

    /// <summary>
    /// Umbral en segundos, para probar en planta (reunión 10/11/2026). El servicio de cámara confirma una parada
    /// recién a los 20 s (2 ventanas de 10 s): el mínimo útil es 30 s. El máximo equivale a 240 minutos.
    /// </summary>
    public const int SegundosUmbralMinimo = 30;
    public const int SegundosUmbralMaximo = MinutosUmbralMaximo * 60;

    /// <summary>
    /// La detención sólo se crea si la parada llegó al umbral efectivo de la cámara, en segundos (inclusive; ver
    /// <see cref="UmbralSegundos(PuntoControlCalidad)"/>). Un valor fuera de rango vuelve al por defecto (5 min).
    /// </summary>
    public static bool SuperaUmbral(DateTimeOffset inicio, DateTimeOffset hasta, int segundosUmbral) =>
        hasta - inicio >= TimeSpan.FromSeconds(UmbralSegundosValido(segundosUmbral));

    /// <summary>
    /// Umbral efectivo de la cámara, en segundos: <c>SegundosUmbralDetencion</c> si está cargado (modo prueba); si
    /// no, <c>MinutosUmbralDetencion</c> x 60. ÚNICO lugar donde se decide el umbral: ingesta, log de paradas y
    /// eficiencia (microcortes) usan esto.
    /// </summary>
    public static int UmbralSegundos(PuntoControlCalidad punto) =>
        UmbralSegundos(punto.SegundosUmbralDetencion, punto.MinutosUmbralDetencion);

    public static int UmbralSegundos(int? segundosUmbral, int minutosUmbral) =>
        segundosUmbral is >= SegundosUmbralMinimo and <= SegundosUmbralMaximo
            ? segundosUmbral.Value
            : UmbralValido(minutosUmbral) * 60;

    /// <summary>Umbral en segundos dentro del rango admitido (fuera de rango vuelve al por defecto, 5 min).</summary>
    public static int UmbralSegundosValido(int segundosUmbral) =>
        segundosUmbral is >= SegundosUmbralMinimo and <= SegundosUmbralMaximo ? segundosUmbral : MinutosUmbralPorDefecto * 60;

    /// <summary>Umbral dentro del rango admitido (un valor fuera de rango en la base vuelve al por defecto).</summary>
    public static int UmbralValido(int minutosUmbral) =>
        minutosUmbral is >= MinutosUmbralMinimo and <= MinutosUmbralMaximo ? minutosUmbral : MinutosUmbralPorDefecto;

    public static string? MotivoUmbralInvalido(int minutosUmbral) =>
        minutosUmbral is >= MinutosUmbralMinimo and <= MinutosUmbralMaximo
            ? null
            : $"El umbral de detención tiene que estar entre {MinutosUmbralMinimo} y {MinutosUmbralMaximo} minutos.";

    /// <summary>
    /// Motivo por el que el umbral guardado desde Maestros no es válido, o null. Con segundos cargados (modo prueba)
    /// se validan los segundos (30 a 14400) y los minutos no se usan; sin segundos, los minutos (1 a 240).
    /// </summary>
    public static string? MotivoUmbralInvalido(int? segundosUmbral, int minutosUmbral) =>
        segundosUmbral is { } s
            ? s is >= SegundosUmbralMinimo and <= SegundosUmbralMaximo
                ? null
                : $"El umbral de detención en segundos tiene que estar entre {SegundosUmbralMinimo} y {SegundosUmbralMaximo} " +
                  $"(la cámara confirma una parada recién a los 20 s; para más de {MinutosUmbralMaximo} minutos no se admite)."
            : MotivoUmbralInvalido(minutosUmbral);

    /// <summary>Umbral legible para mostrar: "5 min" si es un número entero de minutos, si no "45 s".</summary>
    public static string TextoUmbral(int segundosUmbral) =>
        segundosUmbral % 60 == 0 ? $"{segundosUmbral / 60} min" : $"{segundosUmbral} s";

    /// <summary>Umbral efectivo legible de la cámara ("5 min", "45 s").</summary>
    public static string TextoUmbral(PuntoControlCalidad punto) => TextoUmbral(UmbralSegundos(punto));

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

    /// <summary>
    /// Hora de pared en que empezó el turno en curso que contiene <paramref name="ahoraPared"/>. Un nocturno
    /// (21:00 a 05:00) a las 02:00 empezó el día anterior: la fecha de inicio identifica a ESE turno (la noche
    /// siguiente es otro), igual que la eficiencia, que asigna el nocturno al día en que empieza.
    /// </summary>
    public static DateTime InicioTurnoEnCurso(Turno turno, DateTime ahoraPared)
    {
        var inicio = ahoraPared.Date.Add(turno.HoraInicio.ToTimeSpan());
        return TimeOnly.FromDateTime(ahoraPared) >= turno.HoraInicio ? inicio : inicio.AddDays(-1);
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
