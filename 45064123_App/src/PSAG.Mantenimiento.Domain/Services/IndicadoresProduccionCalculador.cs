using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>Estado de un indicador de presupuesto vs. real.</summary>
public static class EstadoIndicador
{
    public const string Ok = "OK";
    /// <summary>No hay objetivo: tramo sin producto, parámetro sin objetivo o turno "a completar".</summary>
    public const string SinObjetivo = "SIN_OBJETIVO";
    /// <summary>Hay objetivo pero no hay real: nunca 0 % por falta de dato.</summary>
    public const string SinDatos = "SIN_DATOS";
}

/// <summary>
/// Real vs. objetivo de una magnitud sobre un conjunto de tramos (un tramo, un turno, un día, un producto...).
/// <see cref="Objetivo"/> y <see cref="Real"/> son las sumas de todos los tramos que tienen el dato; el
/// <see cref="Porcentaje"/> se calcula sólo con los tramos que tienen los dos (Σ real ÷ Σ objetivo, nunca promedio de %).
/// </summary>
public sealed record ComparacionProduccion(
    decimal? Objetivo,
    decimal? Real,
    decimal? Porcentaje,
    string Estado,
    bool Provisorio,
    int TramosSinDatos,
    int TramosSinObjetivo);

/// <summary>Velocidad de un tramo: mangas contadas en los minutos en marcha, esos minutos y los metros por manga (snapshot).</summary>
public sealed record VelocidadTramo(decimal Mangas, int MinutosMarcha, decimal? MetrosPorManga)
{
    public decimal? Metros => MetrosPorManga is { } m ? Mangas * m : null;

    /// <summary>m/min del tramo, o null sin metros por manga o sin minutos en marcha.</summary>
    public decimal? MetrosPorMinuto => IndicadoresProduccionCalculador.Velocidad([this]);
}

/// <summary>
/// Indicadores de presupuesto vs. real (T-798 2.10, etapa 5). Funciones puras: no tocan la base.
/// <list type="bullet">
/// <item>Objetivo del tramo = objetivo por turno (snapshot) × minutos del tramo ÷ minutos de referencia (snapshot); qué
/// minutos le tocan a cada tramo lo decide <see cref="ObjetivoTurnoReglas"/> (T-805).</item>
/// <item>Eficiencia (MANGAS) = Σ real ÷ Σ objetivo prorrateado; producción por magnitud (mangas y bolsones), igual.</item>
/// <item>Velocidad = mangas de los minutos en marcha × metros por manga ÷ minutos en marcha.</item>
/// <item>Indisponibilidad = Σ minutos de las detenciones que cuentan ÷ Σ minutos programados de los turnos.</item>
/// </list>
/// Qué cuenta como real: <see cref="ProduccionOficialReglas.RealParaIndicadores"/>; qué detenciones cuentan:
/// <see cref="DetencionIndicadores"/>.
/// </summary>
public static class IndicadoresProduccionCalculador
{
    /// <summary>Magnitudes con presupuesto (PRD_ParametrosLineaProducto): mangas y bolsones.</summary>
    public static readonly IReadOnlyList<string> MagnitudesConPresupuesto = [MagnitudProduccion.Mangas, MagnitudProduccion.Bolsones];

    /// <summary>Objetivo prorrateado del tramo; null si el parámetro no tiene objetivo para la magnitud (o referencia inválida).</summary>
    public static decimal? ObjetivoTramo(int? objetivoTurno, int minutosTurnoReferencia, decimal minutosTramo)
    {
        if (objetivoTurno is not { } objetivo || minutosTurnoReferencia <= 0)
            return null;
        return objetivo * Math.Max(0m, minutosTramo) / minutosTurnoReferencia;
    }

    /// <summary>Minutos programados del turno: el snapshot; si es 0 (sin snapshot o convertido), el recálculo desde la programación.</summary>
    public static int MinutosProgramados(int snapshot, Func<int> recalcular) => snapshot > 0 ? snapshot : recalcular();

    /// <summary>Compara real vs. objetivo sobre los tramos de una magnitud (ver <see cref="ComparacionProduccion"/>).</summary>
    public static ComparacionProduccion Comparar(IEnumerable<(decimal? Objetivo, RealProduccion Real)> tramos)
    {
        var lista = tramos.ToList();
        var conObjetivo = lista.Where(t => t.Objetivo != null).ToList();
        var conReal = lista.Where(t => t.Real.TieneDato).ToList();
        var pares = conObjetivo.Where(t => t.Real.TieneDato).ToList();

        decimal? objetivo = conObjetivo.Count == 0 ? null : Redondear(conObjetivo.Sum(t => t.Objetivo!.Value));
        decimal? real = conReal.Count == 0 ? null : conReal.Sum(t => t.Real.Cantidad!.Value);
        var sinDatos = conObjetivo.Count - pares.Count;
        var sinObjetivo = lista.Count - conObjetivo.Count;

        var objetivoPares = pares.Sum(t => t.Objetivo!.Value);
        if (conObjetivo.Count == 0 || (pares.Count > 0 && objetivoPares <= 0))
            return new ComparacionProduccion(objetivo, real, null, EstadoIndicador.SinObjetivo, conReal.Any(t => t.Real.Provisorio), sinDatos, sinObjetivo);
        if (pares.Count == 0)
            return new ComparacionProduccion(objetivo, real, null, EstadoIndicador.SinDatos, false, sinDatos, sinObjetivo);

        var porcentaje = Porcentaje(pares.Sum(t => t.Real.Cantidad!.Value), objetivoPares);
        return new ComparacionProduccion(objetivo, real, porcentaje, EstadoIndicador.Ok, pares.Any(t => t.Real.Provisorio), sinDatos, sinObjetivo);
    }

    /// <summary>
    /// Mangas y minutos en marcha de una cámara en [desde, hasta): un minuto está en marcha si alguna fila (carril) tiene
    /// SegundosMarcha &gt; 0; se suman las mangas de esos minutos.
    /// </summary>
    public static VelocidadTramo VelocidadDelTramo(
        IEnumerable<MinutoConteo> minutos, int camaraId, DateTimeOffset desde, DateTimeOffset hasta, decimal? metrosPorManga)
    {
        var enMarcha = minutos
            .Where(m => m.CamaraId == camaraId && m.Minuto >= desde && m.Minuto < hasta)
            .GroupBy(m => m.Minuto)
            .Where(g => g.Any(m => m.SegundosMarcha > 0))
            .ToList();
        return new VelocidadTramo(enMarcha.Sum(g => g.Sum(m => m.Cantidad)), enMarcha.Count, metrosPorManga);
    }

    /// <summary>m/min de varios tramos: Σ metros ÷ Σ minutos en marcha (sólo los tramos con metros por manga). Null sin datos.</summary>
    public static decimal? Velocidad(IEnumerable<VelocidadTramo> tramos)
    {
        var conMetros = tramos.Where(t => t.MetrosPorManga != null && t.MinutosMarcha > 0).ToList();
        var minutos = conMetros.Sum(t => t.MinutosMarcha);
        return minutos == 0 ? null : Redondear(conMetros.Sum(t => t.Metros!.Value) / minutos);
    }

    /// <summary>Indisponibilidad en %: minutos de parada que cuentan ÷ minutos programados. Null si no hay minutos programados.</summary>
    public static decimal? Indisponibilidad(int minutosParadaQueCuentan, int minutosProgramados) =>
        minutosProgramados <= 0 ? null : Porcentaje(minutosParadaQueCuentan, minutosProgramados);

    /// <summary>numerador ÷ denominador × 100 con un decimal.</summary>
    public static decimal Porcentaje(decimal numerador, decimal denominador) =>
        Math.Round(numerador * 100m / denominador, 1, MidpointRounding.AwayFromZero);

    private static decimal Redondear(decimal valor) => Math.Round(valor, 1, MidpointRounding.AwayFromZero);
}
