namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>Un tramo de producto con sus metros por manga (snapshot), para la velocidad minuto a minuto.</summary>
public readonly record struct TramoMetraje(DateTimeOffset Desde, DateTimeOffset Hasta, decimal? MetrosPorManga);

/// <summary>Una detención del turno para el gráfico por tipo de falla.</summary>
/// <param name="TipoFallaId">Null = todavía sin tipo.</param>
/// <param name="Minutos">MinutosEfectivos (declarada) o los estimados de la parada de cámara (pendiente).</param>
/// <param name="Pendiente">Pendiente de declaración (va a la barra "Sin declarar").</param>
/// <param name="Cuenta">Suma en indicadores (<see cref="DetencionIndicadores"/>).</param>
public readonly record struct DetencionTablero(int? TipoFallaId, int Minutos, bool Pendiente, bool Cuenta);

/// <summary>Tipo de falla del maestro (ABM_TiposFalla) para el gráfico de detenciones.</summary>
public readonly record struct TipoFallaTablero(int TipoFallaId, string Nombre, int Orden, bool CuentaParaIndicadores, bool Activo);

/// <summary>Una barra del gráfico de detenciones. <see cref="TipoFallaId"/> null: "Sin declarar" o "Sin tipo".</summary>
public sealed record BarraDetencionTipo(int? TipoFallaId, string Nombre, int Minutos, int Cantidad, bool Cuenta, bool SinDeclarar);

/// <summary>Detenciones del turno por tipo de falla, con el total y los minutos pendientes de declaración.</summary>
/// <param name="MinutosTotal">Σ de las barras que cuentan para indicadores (incluye "Sin declarar").</param>
/// <param name="MinutosNoCuentan">Σ de las barras de tipos que no cuentan (paradas programadas): se muestran aparte.</param>
public sealed record ResumenDetencionesTipo(
    IReadOnlyList<BarraDetencionTipo> Barras, int MinutosTotal, int MinutosPendientes, int CantidadPendientes, int MinutosNoCuentan);

/// <summary>Cantidad y minutos de una de las cifras del bloque Detenciones del tablero.</summary>
public readonly record struct CifraDetenciones(int Cantidad, int Minutos);

/// <summary>
/// T-770 (ajustes 23/11) · Las 4 cifras del bloque Detenciones del tablero, más las programadas aparte.
/// <list type="bullet">
/// <item><b>Pendientes</b> de declarar: las que creó la cámara y nadie declaró; minutos desde la parada de cámara
/// (<see cref="TableroIndicadoresReglas.MinutosPendiente"/>).</item>
/// <item><b>Declaradas</b>: detenciones ya declaradas (manuales o de cámara) de tipos que cuentan para indicadores, con
/// sus MinutosEfectivos.</item>
/// <item><b>Totales</b> = Declaradas + Pendientes: las detenciones "reales" del turno (superaron el umbral o se
/// cargaron a mano).</item>
/// <item><b>Microcortes</b>: paradas de cámara que no llegaron al umbral; no crean detención y NO suman en Totales.</item>
/// <item><b>Programadas</b>: declaradas de tipos que no cuentan (paradas programadas); fuera de Totales, se muestran
/// aparte y chico.</item>
/// </list>
/// </summary>
public sealed record CifrasDetenciones(
    CifraDetenciones Pendientes, CifraDetenciones Declaradas, CifraDetenciones Totales, CifraDetenciones Microcortes, CifraDetenciones Programadas);

/// <summary>
/// T-770 (iteración 2) · Reglas puras de los 4 indicadores de Ferretti en el tablero (eficiencia, producción, velocidad y
/// detenciones). Los números de presupuesto vs. real siguen saliendo de <c>IndicadoresService</c>; acá sólo lo que el
/// tablero agrega: el semáforo de eficiencia, la serie de velocidad minuto a minuto y las
/// detenciones por tipo de falla con los minutos pendientes. El objetivo del turno (completo y a la hora) es el de
/// <see cref="ObjetivoTurnoReglas"/> (T-805), que calcula <c>IndicadoresService</c>.
/// <para>T-805 · <see cref="NivelEficiencia"/> es EL semáforo de eficiencia de la API (el de la Web, <c>GraficosUi.NivelEficiencia</c>,
/// usa los mismos cortes): ROJO &lt; 65, AMARILLO 65–85, VERDE &gt; 85.</para>
/// </summary>
public static class TableroIndicadoresReglas
{
    /// <summary>Debajo de este % la eficiencia es ROJA (pedido de Ferretti: rojo &lt; 65 %).</summary>
    public const decimal EficienciaRojoDebajoDe = 65m;

    /// <summary>Por encima de este % la eficiencia es VERDE (verde &gt; 85 %); entre 65 y 85 inclusive, AMARILLA.</summary>
    public const decimal EficienciaVerdeArribaDe = 85m;

    /// <summary>Velocidad normal de marcha de referencia (m/min) que se marca en el gráfico (lámina 3 de Ferretti).</summary>
    public const decimal VelocidadReferenciaMetrosMinuto = 670m;

    /// <summary>Tope de la serie de velocidad (un turno largo con extensión no pasa de esto).</summary>
    public const int MinutosMaximosSerie = 24 * 60;

    public const string NivelRojo = "ROJO";
    public const string NivelAmarillo = "AMARILLO";
    public const string NivelVerde = "VERDE";
    public const string NivelSinDato = "SIN_DATO";

    public const string TextoSinDeclarar = "Sin declarar";
    public const string TextoSinTipo = "Sin tipo";

    /// <summary>Semáforo de la eficiencia: ROJO &lt; 65, AMARILLO 65–85, VERDE &gt; 85; SIN_DATO sin %.</summary>
    public static string NivelEficiencia(decimal? porcentaje) => porcentaje switch
    {
        null => NivelSinDato,
        < EficienciaRojoDebajoDe => NivelRojo,
        > EficienciaVerdeArribaDe => NivelVerde,
        _ => NivelAmarillo
    };

    /// <summary>real ÷ objetivo × 100 con un decimal; null si falta alguno o el objetivo no es positivo.</summary>
    public static decimal? Porcentaje(decimal? real, decimal? objetivo) =>
        real is { } r && objetivo is { } o && o > 0 ? IndicadoresProduccionCalculador.Porcentaje(r, o) : null;

    /// <summary>Minuto entero (segundos en 0) de una hora.</summary>
    public static DateTimeOffset Minuto(DateTimeOffset hora) =>
        new(hora.Year, hora.Month, hora.Day, hora.Hour, hora.Minute, 0, hora.Offset);

    /// <summary>
    /// Velocidad (m/min) minuto a minuto entre <paramref name="desde"/> y <paramref name="hasta"/>: mangas del minuto de la
    /// cámara oficial de mangas (Σ de sus carriles) × metros por manga del tramo vigente en ese minuto. Un minuto sin filas
    /// de la cámara, sin tramo o con un tramo sin metros por manga es null ("sin datos", nunca 0); un minuto con filas y
    /// 0 mangas (parada) es 0. El índice i es el minuto <c>Minuto(desde) + i</c>.
    /// </summary>
    public static IReadOnlyList<decimal?> VelocidadPorMinuto(
        IEnumerable<MinutoConteo> minutos, int? camaraId, IReadOnlyCollection<TramoMetraje> tramos, DateTimeOffset desde, DateTimeOffset hasta)
    {
        var inicio = Minuto(desde);
        if (hasta <= inicio)
            return [];
        var cantidad = (int)Math.Min(MinutosMaximosSerie, Math.Ceiling((hasta - inicio).TotalMinutes));
        var porMinuto = camaraId is { } id
            ? minutos.Where(m => m.CamaraId == id).GroupBy(m => m.Minuto.UtcTicks).ToDictionary(g => g.Key, g => g.Sum(m => m.Cantidad))
            : [];
        var ordenados = tramos.OrderBy(t => t.Desde).ToList();
        var serie = new List<decimal?>(cantidad);
        for (var i = 0; i < cantidad; i++)
        {
            var m = inicio.AddMinutes(i);
            if (!porMinuto.TryGetValue(m.UtcTicks, out var mangas))
            {
                serie.Add(null);
                continue;
            }
            var metros = MetrosDelMinuto(ordenados, m);
            serie.Add(metros is { } mt ? Math.Round(mangas * mt, 1, MidpointRounding.AwayFromZero) : null);
        }
        return serie;
    }

    /// <summary>Metros por manga del tramo vigente en el minuto (el que lo cubre al empezar; si ninguno, el que empieza dentro).</summary>
    private static decimal? MetrosDelMinuto(IReadOnlyList<TramoMetraje> ordenados, DateTimeOffset minuto)
    {
        var vigente = ordenados.LastOrDefault(t => t.Desde <= minuto && t.Hasta > minuto);
        if (vigente != default)
            return vigente.MetrosPorManga;
        var fin = minuto.AddMinutes(1);
        var dentro = ordenados.FirstOrDefault(t => t.Desde > minuto && t.Desde < fin);
        return dentro != default ? dentro.MetrosPorManga : null;
    }

    /// <summary>
    /// Minutos pendientes de declaración de una detención de cámara (su MinutosEfectivos todavía es 0): desde la parada
    /// (<c>FechaHoraParada</c>) hasta el arranque que vio la cámara (<c>FechaHoraArranqueDetectada</c>) o, si sigue parada,
    /// hasta <paramref name="siSigueParada"/> (ahora con el turno abierto; el fin del turno si ya cerró). Redondeo al
    /// minuto; nunca negativo; sin hora de parada, 0.
    /// </summary>
    public static int MinutosPendiente(DateTimeOffset? parada, DateTimeOffset? arranqueDetectada, DateTimeOffset siSigueParada)
    {
        if (parada is not { } p)
            return 0;
        var fin = arranqueDetectada ?? siSigueParada;
        return fin <= p ? 0 : (int)Math.Round((decimal)(fin - p).TotalMinutes, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Detenciones del turno por tipo de falla (maestro ABM_TiposFalla): una barra por cada tipo activo (en su orden, aunque
    /// tenga 0), más los inactivos que tengan detenciones, "Sin tipo" si hay declaradas sin tipo, y al final "Sin declarar"
    /// (las pendientes, siempre). El total suma las barras que cuentan para indicadores (las pendientes cuentan, como en
    /// <see cref="DetencionIndicadores"/>); los tipos que no cuentan (paradas programadas) se informan aparte.
    /// </summary>
    public static ResumenDetencionesTipo DetencionesPorTipo(IEnumerable<DetencionTablero> detenciones, IEnumerable<TipoFallaTablero> tipos)
    {
        var lista = detenciones.ToList();
        var declaradas = lista.Where(d => !d.Pendiente).ToList();
        var pendientes = lista.Where(d => d.Pendiente).ToList();
        var barras = new List<BarraDetencionTipo>();

        foreach (var tipo in tipos.OrderBy(t => t.Orden).ThenBy(t => t.Nombre))
        {
            var delTipo = declaradas.Where(d => d.TipoFallaId == tipo.TipoFallaId).ToList();
            if (!tipo.Activo && delTipo.Count == 0)
                continue;
            barras.Add(new BarraDetencionTipo(tipo.TipoFallaId, tipo.Nombre, delTipo.Sum(d => d.Minutos), delTipo.Count, tipo.CuentaParaIndicadores, false));
        }
        var conocidos = barras.Select(b => b.TipoFallaId).ToHashSet();
        var sinTipo = declaradas.Where(d => d.TipoFallaId == null || !conocidos.Contains(d.TipoFallaId)).ToList();
        if (sinTipo.Count > 0)
            barras.Add(new BarraDetencionTipo(null, TextoSinTipo, sinTipo.Sum(d => d.Minutos), sinTipo.Count, sinTipo.All(d => d.Cuenta), false));
        var minutosPendientes = pendientes.Sum(d => d.Minutos);
        barras.Add(new BarraDetencionTipo(null, TextoSinDeclarar, minutosPendientes, pendientes.Count, true, true));

        return new ResumenDetencionesTipo(
            barras,
            barras.Where(b => b.Cuenta).Sum(b => b.Minutos),
            minutosPendientes,
            pendientes.Count,
            barras.Where(b => !b.Cuenta).Sum(b => b.Minutos));
    }

    /// <summary>
    /// Las 4 cifras del bloque Detenciones (ver <see cref="CifrasDetenciones"/>): pendientes, declaradas, totales
    /// (declaradas + pendientes) y microcortes (aparte, no suman), más las programadas que no cuentan. Los minutos de
    /// los microcortes son sus segundos redondeados al minuto.
    /// </summary>
    public static CifrasDetenciones Cifras(IEnumerable<DetencionTablero> detenciones, int cantidadMicrocortes, int segundosMicrocortes)
    {
        var lista = detenciones.ToList();
        var pendientes = Cifra(lista.Where(d => d.Pendiente));
        var declaradas = Cifra(lista.Where(d => !d.Pendiente && d.Cuenta));
        var programadas = Cifra(lista.Where(d => !d.Pendiente && !d.Cuenta));
        var totales = new CifraDetenciones(pendientes.Cantidad + declaradas.Cantidad, pendientes.Minutos + declaradas.Minutos);
        var microcortes = new CifraDetenciones(
            Math.Max(0, cantidadMicrocortes), (int)Math.Round(Math.Max(0, segundosMicrocortes) / 60m, MidpointRounding.AwayFromZero));
        return new CifrasDetenciones(pendientes, declaradas, totales, microcortes, programadas);

        static CifraDetenciones Cifra(IEnumerable<DetencionTablero> ds)
        {
            var l = ds.ToList();
            return new CifraDetenciones(l.Count, l.Sum(d => d.Minutos));
        }
    }

    /// <summary>
    /// Alcance por línea del tablero que mira a otro (supervisor / gerencial), la misma regla de T-802 que la corrección
    /// de turnos: administrador y mantenimiento, todas las líneas; el resto, sus líneas (sin líneas asignadas, todas).
    /// </summary>
    public static bool AlcanzaLinea(IEnumerable<string> roles, IReadOnlyCollection<int> lineasAsignadas, int lineaId) =>
        TurnoProduccionReglas.PuedeCorregir(true, roles, lineasAsignadas, lineaId);
}
