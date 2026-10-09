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
}
