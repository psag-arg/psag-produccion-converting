using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Reglas del calendario base de horas programadas por línea. Funciones puras.
/// </summary>
public static class CalendarioLineaReglas
{
    /// <summary>
    /// El administrador carga el calendario y las excepciones de cualquier línea;
    /// el supervisor, sólo de las líneas que tiene asignadas.
    /// </summary>
    public static bool PuedeEditar(IEnumerable<string> roles, IEnumerable<int> lineasAsignadas, int lineaId)
    {
        var lista = roles.ToList();
        if (lista.Contains(RolesSistema.Administrador))
            return true;

        return lista.Contains(RolesSistema.Supervisor) && lineasAsignadas.Contains(lineaId);
    }

    /// <summary>
    /// Motivo por el que la grilla día × turno no se puede guardar, o null si es válida.
    /// Una grilla vacía es válida: la línea no trabaja ningún día desde esa fecha.
    /// </summary>
    public static string? MotivoInvalido(IEnumerable<(DayOfWeek Dia, int TurnoId)> grilla, IEnumerable<Turno> turnosActivos)
    {
        var activos = turnosActivos.Where(t => t.Activo).Select(t => t.TurnoId).ToHashSet();

        foreach (var (dia, turnoId) in grilla)
        {
            if (!Enum.IsDefined(dia))
                return "El día de la semana no es válido.";
            if (!activos.Contains(turnoId))
                return "Uno de los turnos elegidos no existe o está inactivo. Recargá la página.";
        }

        return null;
    }

    /// <summary>
    /// Arma las filas de la versión sin repetidos (el mismo turno marcado dos veces el mismo día).
    /// </summary>
    public static List<CalendarioLineaTurno> Filas(IEnumerable<(DayOfWeek Dia, int TurnoId)> grilla) =>
        grilla.Distinct()
            .OrderBy(g => g.Dia)
            .ThenBy(g => g.TurnoId)
            .Select(g => new CalendarioLineaTurno { DiaSemana = g.Dia, TurnoId = g.TurnoId })
            .ToList();
}
