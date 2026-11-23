namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>Un tramo de producto del turno, con su fin ya resuelto (<see cref="ObjetivoTurnoReglas.FinTramo"/>).</summary>
public readonly record struct TramoParaObjetivo(DateTimeOffset Desde, DateTimeOffset Hasta);

/// <summary>
/// Cómo se reparten los minutos del turno entre sus tramos de producto (<see cref="ObjetivoTurnoReglas.Repartir"/>):
/// los intervalos de reloj que le tocan a cada tramo (su duración más los huecos sin producto que se le asignan) y el
/// factor que lleva la suma a los minutos programados.
/// </summary>
/// <param name="MinutosRepartidos">Σ de los intervalos.</param>
/// <param name="MinutosEscalados">A cuántos minutos se lleva esa suma (los programados si la pasan; si no, la misma).</param>
public sealed record ReparticionObjetivo(IReadOnlyList<IReadOnlyList<TramoParaObjetivo>> Intervalos, decimal MinutosRepartidos, decimal MinutosEscalados)
{
    /// <summary>Minutos escalados ÷ repartidos (1 si no se escala).</summary>
    public decimal Factor => MinutosRepartidos > 0 ? MinutosEscalados / MinutosRepartidos : 1m;

    private decimal Escalar(decimal minutos) =>
        MinutosRepartidos > 0 && MinutosEscalados != MinutosRepartidos ? minutos * MinutosEscalados / MinutosRepartidos : minutos;

    /// <summary>Minutos del tramo <paramref name="i"/> que cuentan para el objetivo del TURNO COMPLETO.</summary>
    public decimal MinutosTurno(int i) => Escalar(Intervalos[i].Sum(x => Minutos(x.Desde, x.Hasta)));

    /// <summary>
    /// Minutos del tramo <paramref name="i"/> que cuentan para el objetivo A LA HORA <paramref name="corte"/> (la misma
    /// repartición, cortada en ese momento). Sin corte (turno cerrado), los del turno completo.
    /// </summary>
    public decimal MinutosHasta(int i, DateTimeOffset? corte) => corte is not { } c
        ? MinutosTurno(i)
        : Escalar(Intervalos[i].Sum(x => Minutos(x.Desde, x.Hasta < c ? x.Hasta : c)));

    /// <summary>Minutos del tramo <paramref name="i"/> dentro de [desde, hasta) (objetivo por hora del tablero).</summary>
    public decimal MinutosEntre(int i, DateTimeOffset desde, DateTimeOffset hasta) =>
        Escalar(Intervalos[i].Sum(x => Minutos(x.Desde > desde ? x.Desde : desde, x.Hasta < hasta ? x.Hasta : hasta)));

    private static decimal Minutos(DateTimeOffset desde, DateTimeOffset hasta) =>
        hasta > desde ? (decimal)(hasta - desde).TotalMinutes : 0m;
}

/// <summary>
/// T-805 · LA regla del objetivo del turno (una sola, para el dashboard, la barra de turno, el tablero del operador y el
/// gerencial y sus modales). Funciones puras.
/// <list type="number">
/// <item>Cada tramo de producto se prorratea por sus minutos: objetivo por turno (snapshot) × minutos ÷ minutos de
/// referencia (<see cref="IndicadoresProduccionCalculador.ObjetivoTramo"/>).</item>
/// <item>Los minutos del turno programado SIN tramo de producto (producto declarado tarde, huecos entre tramos, cierre
/// antes de hora) se asignan al tramo MÁS CERCANO: el hueco del principio al primero, el del final al último y uno del
/// medio se parte por la mitad entre el anterior y el siguiente. Así un turno entero de SH 30 da 11.200 mangas /
/// 6.500 bolsones aunque el producto se haya declarado a las 13:05. (Decisión T-805; alternativa pendiente de
/// Emiliano: un objetivo "sin producto" que se muestre aparte.)</item>
/// <item>Si los minutos repartidos pasan los programados (cierre tarde, día cortado por una excepción) se escalan para
/// sumar los programados (como decidió la etapa 5). Fuera de programación (0 min programados) no se rellenan huecos y
/// cuenta la duración de cada tramo.</item>
/// <item>Turno sin ningún tramo: no hay repartición ("sin objetivo").</item>
/// <item>Objetivo A LA HORA = la misma repartición cortada en "ahora" (minutos transcurridos DEL TURNO, no del tramo),
/// con el mismo "ahora" para todos los números de una respuesta.</item>
/// </list>
/// </summary>
public static class ObjetivoTurnoReglas
{
    /// <summary>
    /// Fin de un tramo para el objetivo: su Hasta; abierto, el fin real del turno si ya cerró o, con el turno abierto, el
    /// fin programado (o ahora, si ya se pasó). Nunca antes de su Desde.
    /// </summary>
    public static DateTimeOffset FinTramo(DateTimeOffset desde, DateTimeOffset? hasta, DateTimeOffset? finReal, DateTimeOffset finProgramado, DateTimeOffset ahora)
    {
        var fin = hasta ?? finReal ?? (ahora > finProgramado ? ahora : finProgramado);
        return fin > desde ? fin : desde;
    }

    /// <summary>
    /// Reparte los minutos del turno [<paramref name="inicioTurno"/>, <paramref name="finTurno"/>) (horario programado)
    /// entre los tramos (en orden de Desde, con el fin resuelto). Ver las reglas en <see cref="ObjetivoTurnoReglas"/>.
    /// </summary>
    public static ReparticionObjetivo Repartir(
        IReadOnlyList<TramoParaObjetivo> tramos, DateTimeOffset inicioTurno, DateTimeOffset finTurno, int minutosProgramados)
    {
        var intervalos = tramos.Select(t => new List<TramoParaObjetivo> { t with { Hasta = t.Hasta > t.Desde ? t.Hasta : t.Desde } }).ToList();
        if (intervalos.Count == 0)
            return new ReparticionObjetivo([], 0m, 0m);

        if (minutosProgramados > 0 && finTurno > inicioTurno)
        {
            // Hueco del principio → primer tramo
            var primero = tramos[0].Desde;
            if (primero > inicioTurno)
                intervalos[0].Add(new TramoParaObjetivo(inicioTurno, Menor(primero, finTurno)));

            // Huecos del medio → se parten por la mitad entre el tramo anterior y el siguiente
            var finHastaAhora = intervalos[0][0].Hasta;
            for (var i = 1; i < tramos.Count; i++)
            {
                var desde = Mayor(finHastaAhora, inicioTurno);
                var hasta = Menor(tramos[i].Desde, finTurno);
                if (hasta > desde)
                {
                    var mitad = desde + TimeSpan.FromTicks((hasta - desde).Ticks / 2);
                    intervalos[i - 1].Add(new TramoParaObjetivo(desde, mitad));
                    intervalos[i].Add(new TramoParaObjetivo(mitad, hasta));
                }
                finHastaAhora = Mayor(finHastaAhora, intervalos[i][0].Hasta);
            }

            // Hueco del final → último tramo
            var desdeFinal = Mayor(finHastaAhora, inicioTurno);
            if (finTurno > desdeFinal)
                intervalos[^1].Add(new TramoParaObjetivo(desdeFinal, finTurno));
        }

        var total = intervalos.Sum(l => l.Sum(x => (decimal)(x.Hasta - x.Desde).TotalMinutes));
        var escalados = minutosProgramados > 0 && total > minutosProgramados ? minutosProgramados : total;
        return new ReparticionObjetivo(intervalos.Select(l => (IReadOnlyList<TramoParaObjetivo>)l.OrderBy(x => x.Desde).ToList()).ToList(), total, escalados);
    }

    private static DateTimeOffset Menor(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    private static DateTimeOffset Mayor(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
