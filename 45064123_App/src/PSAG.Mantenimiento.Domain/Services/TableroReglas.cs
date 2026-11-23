namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>Un tramo de producto del turno, para prorratear el objetivo de mangas por hora.</summary>
/// <param name="Factor">T-805: factor de <see cref="ReparticionObjetivo"/> (minutos programados ÷ minutos repartidos; 1 si no se escala).</param>
public readonly record struct TramoObjetivo(DateTimeOffset Desde, DateTimeOffset Hasta, int? ObjetivoTurno, int MinutosTurnoReferencia, decimal Factor = 1m);

/// <summary>Mangas de una hora de planta del turno: objetivo prorrateado y real de la cámara oficial.</summary>
public sealed record MangasHora(DateTimeOffset Hora, decimal? Objetivo, decimal Real, int MinutosConDatos, bool EnCurso);

/// <summary>
/// T-770 · Reglas puras del tablero del operador (tablet): quién sigue los cambios en línea de un turno, mangas por hora
/// y "sin datos" de la cámara. Los indicadores del turno (objetivo, real, eficiencia, velocidad, indisponibilidad) son
/// los de <c>IndicadoresService</c>: acá no se recalculan.
/// </summary>
public static class TableroReglas
{
    /// <summary>Nombre del grupo de SignalR de un turno de producción (todos los que miran ese turno).</summary>
    public static string GrupoTurno(long turnoProduccionId) => $"turno-{turnoProduccionId}";

    /// <summary>
    /// ¿Puede seguir en línea el turno <paramref name="turnoPedido"/>? Hace falta VER del tablero; el obligado a turno,
    /// además, sólo el suyo (el turno abierto en el que es el operador vigente).
    /// </summary>
    public static bool PuedeSeguir(bool tienePermiso, bool obligadoATurno, long? turnoPropio, long turnoPedido) =>
        tienePermiso && (!obligadoATurno || turnoPropio == turnoPedido);

    /// <summary>
    /// "Sin datos" de la cámara con el turno abierto: el último dato recibido es de hace más de
    /// <paramref name="minutosSinDatos"/> minutos (o no hay ninguno). Con el turno cerrado no se avisa.
    /// La misma tolerancia que la alerta SIN_DATOS (TurnosProduccion:MinutosSinDatosCamara, 5).
    /// </summary>
    public static bool SinDatos(bool turnoAbierto, DateTimeOffset? ultimoDato, DateTimeOffset ahora, int minutosSinDatos = CamaraReglas.MinutosSinDatosPorDefecto) =>
        turnoAbierto && (ultimoDato is not { } u || DetencionCamaraReglas.CorrespondeAlertaSinDatos(u, ahora, minutosSinDatos));

    /// <summary>Minutos enteros desde el último dato (null si no hubo ninguno; nunca negativo).</summary>
    public static int? MinutosDesde(DateTimeOffset? ultimoDato, DateTimeOffset ahora) =>
        ultimoDato is { } u ? (int)Math.Max(0, Math.Floor((ahora - u).TotalMinutes)) : null;

    /// <summary>
    /// Mangas por hora de planta entre <paramref name="inicio"/> y <paramref name="fin"/> (el turno hasta ahora): real =
    /// Σ minutos de la cámara oficial de mangas en esa hora; objetivo = Σ de cada tramo, objetivo por turno × minutos del
    /// tramo dentro de la hora ÷ minutos de referencia (prorrateo simple, sin el recorte a los minutos programados que
    /// hace el total del turno). Sin objetivo en ningún tramo de la hora = null ("sin objetivo", nunca 0).
    /// La primera y la última hora pueden ser parciales (turno que empieza 05:00 y "ahora" 07:20 → 05, 06 y 07 en curso).
    /// </summary>
    public static IReadOnlyList<MangasHora> MangasPorHora(
        IEnumerable<MinutoConteo> minutos, int? camaraId, IReadOnlyCollection<TramoObjetivo> tramos,
        DateTimeOffset inicio, DateTimeOffset fin, DateTimeOffset ahora, TimeZoneInfo zona)
    {
        var resultado = new List<MangasHora>();
        if (fin <= inicio)
            return resultado;

        var delaCamara = camaraId is { } id
            ? minutos.Where(m => m.CamaraId == id && m.Minuto >= inicio && m.Minuto < fin).ToList()
            : [];

        var inicioLocal = TimeZoneInfo.ConvertTime(inicio, zona);
        var hora = new DateTimeOffset(inicioLocal.Year, inicioLocal.Month, inicioLocal.Day, inicioLocal.Hour, 0, 0, inicioLocal.Offset);
        while (hora < fin)
        {
            var siguiente = hora.AddHours(1);
            var desde = hora < inicio ? inicio : hora;
            var hasta = siguiente > fin ? fin : siguiente;

            decimal? objetivo = null;
            foreach (var t in tramos)
            {
                if (t.ObjetivoTurno is not { } obj || t.MinutosTurnoReferencia <= 0)
                    continue;
                var d = t.Desde > desde ? t.Desde : desde;
                var h = t.Hasta < hasta ? t.Hasta : hasta;
                if (h <= d)
                    continue;
                objetivo = (objetivo ?? 0) + obj * (decimal)(h - d).TotalMinutes * t.Factor / t.MinutosTurnoReferencia;
            }

            var enLaHora = delaCamara.Where(m => m.Minuto >= desde && m.Minuto < hasta).ToList();
            resultado.Add(new MangasHora(
                TimeZoneInfo.ConvertTime(hora, zona),
                objetivo is { } o ? Math.Round(o, 0, MidpointRounding.AwayFromZero) : null,
                Math.Round(enLaHora.Sum(m => m.Cantidad), 0, MidpointRounding.AwayFromZero),
                enLaHora.Select(m => m.Minuto).Distinct().Count(),
                ahora >= hora && ahora < siguiente));
            hora = siguiente;
        }
        return resultado;
    }
}
