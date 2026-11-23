using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Reglas del turno de producción (T-798, 2.3 y 2.6). Funciones puras: no tocan la base.
/// Los turnos son globales (<c>ABM_Turnos</c>: T1 05–13, T2 13–21, T3 21–05). La fecha de producción es el día en
/// que empieza el turno: el T3 que empezó el 19/11 a las 21:00 sigue siendo del 19/11 a las 02:00 del 20/11.
/// </summary>
public static class TurnoProduccionReglas
{
    public const string MensajeSinTurno = "No hay un turno de planta a esta hora (revisá Configuración → Turnos).";

    public const string MensajeSinLineas =
        "No estás habilitado en ninguna línea, así que no podés tomar un turno. " +
        "Pedile al administrador que te habilite en Configuración → Maquinistas (o a tu supervisor que te cargue en el turno).";

    /// <summary>Turno de planta que contiene la hora de pared (al minuto) y su fecha de producción, o null si ninguno la cubre.</summary>
    public static (Turno Turno, DateOnly FechaProduccion)? TurnoQueContiene(IEnumerable<Turno> turnos, DateTime horaPared)
    {
        var hora = new TimeOnly(horaPared.Hour, horaPared.Minute);
        var turno = turnos.Where(t => t.Activo).OrderBy(t => t.Orden).ThenBy(t => t.HoraInicio).FirstOrDefault(t => t.ContieneHora(hora));
        return turno == null ? null : (turno, FechaProduccion(turno, horaPared));
    }

    /// <summary>
    /// Día de inicio del turno que contiene la hora de pared: si la hora es anterior a la de inicio, el turno empezó
    /// el día anterior (T3 21–05: a las 02:00 del 20/11 es del 19/11).
    /// </summary>
    public static DateOnly FechaProduccion(Turno turno, DateTime horaPared)
    {
        var hora = new TimeOnly(horaPared.Hour, horaPared.Minute);
        var dia = DateOnly.FromDateTime(horaPared);
        return hora >= turno.HoraInicio ? dia : dia.AddDays(-1);
    }

    /// <summary>Inicio y fin programados del turno que empieza ese día, en hora de pared de la planta.</summary>
    public static (DateTime Inicio, DateTime Fin) VentanaPared(DateOnly fechaProduccion, Turno turno) =>
        EficienciaCamaraCalculador.VentanaTurno(fechaProduccion, turno);

    /// <summary>Inicio y fin programados como instantes (con el offset de la planta en esa fecha).</summary>
    public static (DateTimeOffset Inicio, DateTimeOffset Fin) Ventana(DateOnly fechaProduccion, Turno turno, TimeZoneInfo zonaPlanta)
    {
        var (inicio, fin) = VentanaPared(fechaProduccion, turno);
        return (HoraPlanta.DesdeHoraDePared(inicio, zonaPlanta), HoraPlanta.DesdeHoraDePared(fin, zonaPlanta));
    }

    /// <summary>
    /// Minutos programados del turno ese día: la misma regla que la eficiencia y las horas programadas
    /// (<see cref="EficienciaCamaraCalculador.MinutosProgramadosTurno"/>: programación vigente de la línea + excepción
    /// del día). 0 = fuera de programación (el turno se abre igual, regla 6 de 2.6).
    /// </summary>
    public static int MinutosProgramados(
        DateOnly fechaProduccion, Turno turno, IEnumerable<Turno> turnos,
        IEnumerable<CalendarioLinea> programacionDeLaLinea, IEnumerable<TiempoProgramadoDia> excepcionesDeLaLinea) =>
        EficienciaCamaraCalculador.MinutosProgramadosTurno(fechaProduccion, turno, turnos, programacionDeLaLinea, excepcionesDeLaLinea);

    /// <summary>Cierre automático: el turno está ABIERTO y ya pasó FinProgramado + tolerancia.</summary>
    public static bool Vencido(TurnoProduccion turno, DateTimeOffset ahora, int toleranciaMinutos) =>
        turno.Estado == EstadoTurnoProduccion.ABIERTO && ahora >= turno.FinProgramado.AddMinutes(toleranciaMinutos);

    /// <summary>
    /// SIN_APERTURA: el turno está programado (minutos &gt; 0), ya pasó su inicio + tolerancia y no hay registro.
    /// Sólo se miran los turnos que empezaron en las últimas 24 h (si el job estuvo parado, no se rellena el pasado).
    /// </summary>
    public static bool CorrespondeSinApertura(int minutosProgramados, DateTimeOffset inicioProgramado, DateTimeOffset ahora, int toleranciaMinutos, bool existeRegistro) =>
        !existeRegistro
        && minutosProgramados > 0
        && ahora >= inicioProgramado.AddMinutes(toleranciaMinutos)
        && inicioProgramado > ahora.AddHours(-24);

    /// <summary>Estado con que se crea un turno que pide un dato (ingesta, detención): ya terminado → CERRADO_AUTOMATICO.</summary>
    public static EstadoTurnoProduccion EstadoAlAsegurar(DateTimeOffset finProgramado, DateTimeOffset ahora) =>
        ahora >= finProgramado ? EstadoTurnoProduccion.CERRADO_AUTOMATICO : EstadoTurnoProduccion.ABIERTO;

    // ===================== Tramos (operadores y productos) =====================

    /// <summary>Operador vigente: el tramo no anulado sin Hasta (el último, si por error hubiera más de uno).</summary>
    public static TurnoProduccionOperador? OperadorVigente(IEnumerable<TurnoProduccionOperador> tramos) =>
        tramos.Where(t => !t.Anulado && t.Hasta == null).OrderByDescending(t => t.Desde).FirstOrDefault();

    /// <summary>
    /// Operador del turno a una hora (T-798 etapa 3: snapshot del operador de una detención). El tramo no anulado con
    /// Desde &lt;= instante &lt; Hasta (Hasta null = sigue). Antes del relevo, el saliente; desde el relevo, el entrante.
    /// Null si a esa hora nadie había tomado el turno.
    /// </summary>
    public static TurnoProduccionOperador? OperadorALaHora(IEnumerable<TurnoProduccionOperador> tramos, DateTimeOffset instante) =>
        tramos.Where(t => !t.Anulado && t.Desde <= instante && (t.Hasta == null || instante < t.Hasta))
            .OrderByDescending(t => t.Desde)
            .FirstOrDefault();

    /// <summary>Producto vigente: el tramo no anulado sin Hasta.</summary>
    public static TurnoProduccionProducto? ProductoVigente(IEnumerable<TurnoProduccionProducto> tramos) =>
        tramos.Where(t => !t.Anulado && t.Hasta == null).OrderByDescending(t => t.Desde).FirstOrDefault();

    /// <summary>
    /// ¿El tramo nuevo [desde, hasta) se superpone con alguno de los existentes no anulados? (Hasta null = abierto
    /// hasta el infinito.) Los <paramref name="ignorar"/> no cuentan: el que se cierra o se anula en la misma operación.
    /// </summary>
    public static bool SeSuperpone(
        IEnumerable<(long Id, DateTimeOffset Desde, DateTimeOffset? Hasta, bool Anulado)> existentes,
        DateTimeOffset desde, DateTimeOffset? hasta, IReadOnlyCollection<long>? ignorar = null) =>
        existentes.Any(e => !e.Anulado
                            && (ignorar == null || !ignorar.Contains(e.Id))
                            && desde < (e.Hasta ?? DateTimeOffset.MaxValue)
                            && e.Desde < (hasta ?? DateTimeOffset.MaxValue));

    /// <summary>Lista de tramos de operador para <see cref="SeSuperpone"/>.</summary>
    public static IEnumerable<(long Id, DateTimeOffset Desde, DateTimeOffset? Hasta, bool Anulado)> Intervalos(IEnumerable<TurnoProduccionOperador> tramos) =>
        tramos.Select(t => (t.TurnoProduccionOperadorId, t.Desde, t.Hasta, t.Anulado));

    /// <summary>Lista de tramos de producto para <see cref="SeSuperpone"/>.</summary>
    public static IEnumerable<(long Id, DateTimeOffset Desde, DateTimeOffset? Hasta, bool Anulado)> Intervalos(IEnumerable<TurnoProduccionProducto> tramos) =>
        tramos.Select(t => (t.TurnoProduccionProductoId, t.Desde, t.Hasta, t.Anulado));

    /// <summary>¿Los tramos no anulados están sin superposición entre sí? (Invariante: se verifica en los tests.)</summary>
    public static bool SinSuperposicion(IEnumerable<(long Id, DateTimeOffset Desde, DateTimeOffset? Hasta, bool Anulado)> tramos)
    {
        var vivos = tramos.Where(t => !t.Anulado).OrderBy(t => t.Desde).ToList();
        for (var i = 1; i < vivos.Count; i++)
        {
            if ((vivos[i - 1].Hasta ?? DateTimeOffset.MaxValue) > vivos[i].Desde)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Motivo por el que no se acepta un tramo corregido por el supervisor, o null. El tramo tiene que caer dentro del
    /// turno (inicio programado menos la tolerancia hasta el fin real o programado más la tolerancia), Desde &lt; Hasta,
    /// y sólo puede quedar abierto (Hasta null) si el turno está ABIERTO.
    /// </summary>
    public static string? MotivoTramoInvalido(TurnoProduccion turno, DateTimeOffset desde, DateTimeOffset? hasta, DateTimeOffset ahora, int toleranciaMinutos, string? motivo, int motivoMaximo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return "Escribí el motivo de la corrección.";
        if (motivo.Trim().Length > motivoMaximo)
            return $"El motivo no puede pasar de {motivoMaximo} caracteres.";
        if (hasta is { } h && h <= desde)
            return "La hora hasta tiene que ser posterior a la hora desde.";
        if (hasta == null && !turno.EstaAbierto)
            return "El turno no está abierto: indicá hasta qué hora.";
        var minimo = turno.InicioProgramado.AddMinutes(-toleranciaMinutos);
        var finTurno = turno.FinReal ?? turno.FinProgramado;
        var maximo = (finTurno > turno.FinProgramado ? finTurno : turno.FinProgramado).AddMinutes(toleranciaMinutos);
        if (desde < minimo || desde >= maximo || (hasta is { } h2 && h2 > maximo))
            return "El tramo tiene que caer dentro del horario del turno.";
        if (desde > ahora)
            return "No se puede cargar un tramo que todavía no empezó.";
        return null;
    }

    // ===================== Permisos =====================

    /// <summary>
    /// Quién corrige un turno de producción (operador, producto, cierre): administrador y mantenimiento, todas las
    /// líneas; supervisor, las suyas (sin líneas asignadas, todas, como en T-783). Pregunta abierta 9 del diseño.
    /// </summary>
    public static bool PuedeCorregir(IEnumerable<string> roles, IReadOnlyCollection<int> lineasAsignadas, int lineaId)
    {
        var lista = roles.ToList();
        return PuedeCorregir(TienePermisoCorregirPorRol(lista), lista, lineasAsignadas, lineaId);
    }

    /// <summary>Regla por rol anterior a T-802 (= matriz migrada de "Turnos de producción", MODIFICACION).</summary>
    public static bool TienePermisoCorregirPorRol(IEnumerable<string> roles) =>
        roles.Any(r => r is RolesSistema.Administrador or RolesSistema.Mantenimiento or RolesSistema.Supervisor);

    /// <summary>
    /// T-802: quién corrige es un permiso configurable (opción "Turnos de producción", MODIFICACION). Lo que queda en el
    /// código es el alcance por línea: administrador y mantenimiento, todas; el resto, sus líneas (sin líneas asignadas, todas).
    /// </summary>
    public static bool PuedeCorregir(bool permisoCorregir, IEnumerable<string> roles, IReadOnlyCollection<int> lineasAsignadas, int lineaId)
    {
        if (!permisoCorregir)
            return false;
        var lista = roles.ToList();
        if (lista.Contains(RolesSistema.Administrador) || lista.Contains(RolesSistema.Mantenimiento))
            return true;
        return lineasAsignadas.Count == 0 || lineasAsignadas.Contains(lineaId);
    }

    /// <summary>Quién opera el turno de una línea (tomar, cambiar producto, cerrar): el operario habilitado en la línea o quien puede corregir.</summary>
    public static bool PuedeOperar(IEnumerable<string> roles, IReadOnlyCollection<int> lineasAsignadas, int lineaId)
    {
        var lista = roles.ToList();
        return PuedeOperar(TienePermisoCorregirPorRol(lista), lista, lineasAsignadas, lineaId);
    }

    public static bool PuedeOperar(bool permisoCorregir, IEnumerable<string> roles, IReadOnlyCollection<int> lineasAsignadas, int lineaId)
    {
        var lista = roles.ToList();
        if (PuedeCorregir(permisoCorregir, lista, lineasAsignadas, lineaId))
            return true;
        return lista.Contains(RolesSistema.Operario) && lineasAsignadas.Contains(lineaId);
    }
}
