using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// Evalúa qué regla de análisis aplica a una detención. Función pura: no toca la base.
/// </summary>
public static class ReglaAnalisisEvaluador
{
    /// <summary>
    /// Devuelve la regla ganadora o null. Se ignoran las inactivas. Si aplican varias,
    /// gana la de mayor MinutosMinimos y, en empate, la de menor Id.
    /// </summary>
    public static ReglaAnalisis? Evaluar(
        IEnumerable<ReglaAnalisis> reglas,
        int lineaId,
        int? equipoId,
        int? tipoFallaId,
        int minutosEfectivos)
    {
        return Candidatas(reglas, lineaId, equipoId, tipoFallaId)
            .Where(r => minutosEfectivos >= r.MinutosMinimos)
            .OrderByDescending(r => r.MinutosMinimos)
            .ThenBy(r => r.ReglaAnalisisId)
            .FirstOrDefault();
    }

    /// <summary>
    /// Por qué una detención no requiere análisis, con las mismas reglas que <see cref="Evaluar"/>.
    /// Si hoy le aplicaría una regla, es porque la regla se creó o cambió después de cargarla.
    /// </summary>
    public static string MotivoSinAnalisis(
        IEnumerable<ReglaAnalisis> reglas,
        int lineaId,
        int? equipoId,
        int? tipoFallaId,
        string? tipoFallaNombre,
        int minutosEfectivos)
    {
        const string Prefijo = "No requiere análisis: ";
        if (!tipoFallaId.HasValue)
            return Prefijo + "no tiene tipo de falla";

        var tipo = tipoFallaNombre ?? "el tipo de falla";
        var candidatas = Candidatas(reglas, lineaId, equipoId, tipoFallaId).ToList();
        if (candidatas.Count == 0)
            return Prefijo + $"«{tipo}» no tiene regla de análisis";

        var minimo = candidatas.Min(r => r.MinutosMinimos);
        return minutosEfectivos < minimo
            ? Prefijo + $"dura {minutosEfectivos} min y la regla de {tipo} pide {minimo} o más"
            : Prefijo + $"cuando se cargó no había una regla de {tipo} que le aplicara";
    }

    // Reglas activas del tipo de falla que alcanzan a la línea y al equipo, sin mirar los minutos.
    private static IEnumerable<ReglaAnalisis> Candidatas(
        IEnumerable<ReglaAnalisis> reglas,
        int lineaId,
        int? equipoId,
        int? tipoFallaId) =>
        reglas.Where(r => r.Activo
            && tipoFallaId.HasValue
            && r.TipoFallaId == tipoFallaId.Value
            && (r.LineaId == null || r.LineaId == lineaId)
            && (r.EquipoId == null || r.EquipoId == equipoId));

    /// <summary>
    /// Regla activa con las mismas condiciones (línea, equipo, tipo y minutos), sin contar la que se edita.
    /// Dos reglas así son ambiguas: sólo una puede ganar.
    /// </summary>
    public static ReglaAnalisis? BuscarDuplicada(
        IEnumerable<ReglaAnalisis> reglas,
        int? lineaId,
        int? equipoId,
        int tipoFallaId,
        int minutosMinimos,
        int? reglaAnalisisIdExcluida = null)
    {
        return reglas.FirstOrDefault(r => r.Activo
            && r.ReglaAnalisisId != reglaAnalisisIdExcluida
            && r.LineaId == lineaId
            && r.EquipoId == equipoId
            && r.TipoFallaId == tipoFallaId
            && r.MinutosMinimos == minutosMinimos);
    }
}
