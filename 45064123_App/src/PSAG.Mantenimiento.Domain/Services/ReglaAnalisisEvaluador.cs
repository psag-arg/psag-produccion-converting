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
        return reglas
            .Where(r => r.Activo
                && tipoFallaId.HasValue
                && r.TipoFallaId == tipoFallaId.Value
                && (r.LineaId == null || r.LineaId == lineaId)
                && (r.EquipoId == null || r.EquipoId == equipoId)
                && minutosEfectivos >= r.MinutosMinimos)
            .OrderByDescending(r => r.MinutosMinimos)
            .ThenBy(r => r.ReglaAnalisisId)
            .FirstOrDefault();
    }

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
