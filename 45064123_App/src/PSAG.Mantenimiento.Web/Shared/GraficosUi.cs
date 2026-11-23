using System.Globalization;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// T-770 (iteración 2) · Lógica pura de los gráficos SVG del tablero (medidor de eficiencia, barras de producción y de
/// detenciones, línea de velocidad). Sin librerías ni CDN: la tablet de planta no tiene Internet. Las coordenadas van con
/// punto decimal (cultura invariante: con la de Argentina el SVG se rompe). Los colores son tokens de css/tema.css
/// (T-806: semáforo --psag-semaforo-*), definidos para el modo claro y el oscuro de Bootstrap 5.3.
/// </summary>
public static class GraficosUi
{
    /// <summary>Corte rojo/amarillo de la eficiencia (rojo &lt; 65 %). Mismo valor que la API (TableroIndicadoresReglas).</summary>
    public const decimal EficienciaRojoDebajoDe = 65m;

    /// <summary>Corte amarillo/verde de la eficiencia (verde &gt; 85 %).</summary>
    public const decimal EficienciaVerdeArribaDe = 85m;

    /// <summary>Fondo de escala del medidor: 0 a 100 % (más de 100 deja la aguja al tope; el número dice el valor real).</summary>
    public const decimal TopeMedidor = 100m;

    /// <summary>
    /// T-805 · EL semáforo de la eficiencia de la Web (dashboard, barra de turno, tablero, modales): rojo &lt; 65,
    /// amarillo 65–85 (inclusive), verde &gt; 85; sin dato gris. Mismos cortes que la API (TableroIndicadoresReglas).
    /// </summary>
    public static TableroUi.Nivel NivelEficiencia(decimal? porcentaje) => porcentaje switch
    {
        null => TableroUi.Nivel.SinDato,
        < EficienciaRojoDebajoDe => TableroUi.Nivel.Mal,
        > EficienciaVerdeArribaDe => TableroUi.Nivel.Bien,
        _ => TableroUi.Nivel.Atencion
    };

    /// <summary>Nivel que manda la API (ROJO, AMARILLO, VERDE, SIN_DATO).</summary>
    public static TableroUi.Nivel NivelDeApi(string? nivel) => nivel switch
    {
        "VERDE" => TableroUi.Nivel.Bien,
        "AMARILLO" => TableroUi.Nivel.Atencion,
        "ROJO" => TableroUi.Nivel.Mal,
        _ => TableroUi.Nivel.SinDato
    };

    /// <summary>Color de relleno SVG del nivel (tokens del semáforo: cambian solos en modo oscuro).</summary>
    public static string Color(TableroUi.Nivel nivel) => nivel switch
    {
        TableroUi.Nivel.Bien => "var(--psag-semaforo-verde)",
        TableroUi.Nivel.Atencion => "var(--psag-semaforo-amarillo)",
        TableroUi.Nivel.Mal => "var(--psag-semaforo-rojo)",
        TableroUi.Nivel.Neutro => "var(--psag-acento)",
        _ => "var(--bs-secondary-color)"
    };

    /// <summary>Número para atributos SVG: punto decimal, hasta 2 decimales.</summary>
    public static string N(double valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>Valor entre 0 y 1 de <paramref name="valor"/> sobre <paramref name="maximo"/> (null o máximo inválido = 0).</summary>
    public static double Proporcion(decimal? valor, decimal maximo) =>
        valor is not { } v || maximo <= 0 ? 0 : (double)Math.Clamp(v / maximo, 0m, 1m);

    // ===================== Medidor semicircular =====================

    /// <summary>
    /// Ángulo de la aguja en grados (convención matemática: 180 = izquierda = 0 %, 90 = arriba = 50 %, 0 = derecha = 100 %).
    /// Recorta a 0..<see cref="TopeMedidor"/>; sin dato, la aguja descansa en 0 %.
    /// </summary>
    public static double AnguloAguja(decimal? porcentaje) =>
        180d - 180d * Proporcion(porcentaje is { } p ? Math.Max(0, p) : 0, TopeMedidor);

    /// <summary>Punto del círculo de centro (cx, cy) y radio r en el ángulo dado (y de SVG crece hacia abajo).</summary>
    public static (double X, double Y) Punto(double cx, double cy, double r, double grados)
    {
        var rad = grados * Math.PI / 180d;
        return (cx + r * Math.Cos(rad), cy - r * Math.Sin(rad));
    }

    /// <summary>Path SVG de una banda del medidor entre dos % (sector de corona entre rInt y rExt).</summary>
    public static string Banda(double cx, double cy, double rExt, double rInt, decimal desdePorcentaje, decimal hastaPorcentaje)
    {
        var a1 = AnguloAguja(desdePorcentaje);
        var a2 = AnguloAguja(hastaPorcentaje);
        var (x1, y1) = Punto(cx, cy, rExt, a1);
        var (x2, y2) = Punto(cx, cy, rExt, a2);
        var (x3, y3) = Punto(cx, cy, rInt, a2);
        var (x4, y4) = Punto(cx, cy, rInt, a1);
        // Barrido horario en pantalla (de izquierda a derecha por arriba) = sweep-flag 1; nunca más de media vuelta.
        return $"M {N(x1)} {N(y1)} A {N(rExt)} {N(rExt)} 0 0 1 {N(x2)} {N(y2)} L {N(x3)} {N(y3)} A {N(rInt)} {N(rInt)} 0 0 0 {N(x4)} {N(y4)} Z";
    }

    /// <summary>Las tres bandas del medidor: (desde %, hasta %, nivel).</summary>
    public static IReadOnlyList<(decimal Desde, decimal Hasta, TableroUi.Nivel Nivel)> BandasEficiencia { get; } =
    [
        (0m, EficienciaRojoDebajoDe, TableroUi.Nivel.Mal),
        (EficienciaRojoDebajoDe, EficienciaVerdeArribaDe, TableroUi.Nivel.Atencion),
        (EficienciaVerdeArribaDe, TopeMedidor, TableroUi.Nivel.Bien)
    ];

    /// <summary>"68 %" (sin decimales, como la lámina) o "Sin datos" / "Sin objetivo".</summary>
    public static string TextoPorcentaje(decimal? porcentaje, string estado = "OK") =>
        porcentaje is { } p && estado == "OK"
            ? Math.Round(p, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.CurrentCulture) + " %"
            : estado == "SIN_OBJETIVO" ? EficienciaUi.TextoSinObjetivo : EficienciaUi.TextoSinDatos;

    // ===================== Escalas =====================

    /// <summary>Máximo "redondo" (1, 2, 2,5, 5 o 10 × 10^n) mayor o igual a <paramref name="maximo"/>; mínimo 1.</summary>
    public static decimal EscalaRedonda(decimal maximo)
    {
        if (maximo <= 1)
            return 1;
        var potencia = (decimal)Math.Pow(10, Math.Floor(Math.Log10((double)maximo)));
        foreach (var paso in new[] { 1m, 2m, 2.5m, 5m, 10m })
        {
            if (paso * potencia >= maximo)
                return paso * potencia;
        }
        return 10 * potencia;
    }

    /// <summary>Marcas del eje de 0 a <paramref name="maximo"/> en <paramref name="partes"/> partes iguales.</summary>
    public static IReadOnlyList<decimal> Marcas(decimal maximo, int partes = 5) =>
        partes <= 0 ? [0m] : Enumerable.Range(0, partes + 1).Select(i => maximo * i / partes).ToList();

    /// <summary>Etiqueta de una marca del eje: "1.500", "2,5".</summary>
    public static string TextoMarca(decimal valor) =>
        valor == Math.Round(valor) ? valor.ToString("N0", CultureInfo.CurrentCulture) : valor.ToString("N1", CultureInfo.CurrentCulture);

    // ===================== Barras =====================

    /// <summary>Escala común de las barras de detenciones: el máximo redondo de la barra más larga (sin minutos, 10).</summary>
    public static decimal EscalaDetenciones(IEnumerable<int> minutos)
    {
        var max = minutos.DefaultIfEmpty(0).Max();
        return max <= 0 ? 10 : EscalaRedonda(max);
    }

    /// <summary>
    /// Barras del gráfico de detenciones (ajustes 23/11): SOLO las que tienen minutos y cuentan para indicadores (las
    /// programadas se informan aparte); "Sin declarar" según <paramref name="incluirSinDeclarar"/>.
    /// </summary>
    public static IReadOnlyList<BarraTipoFallaDto> BarrasConMinutos(IEnumerable<BarraTipoFallaDto> barras, bool incluirSinDeclarar = true) =>
        barras.Where(b => b.Minutos > 0 && b.Cuenta && (incluirSinDeclarar || !b.SinDeclarar)).ToList();

    /// <summary>Largo máximo de la etiqueta de un tipo (va en su propio renglón, sobre la barra: entra entera en el ancho).</summary>
    public const int LargoEtiquetaTipo = 48;

    /// <summary>Nombre del tipo de falla en mayúsculas, recortado con "…" sólo si pasa de <see cref="LargoEtiquetaTipo"/>.</summary>
    public static string EtiquetaTipo(string nombre)
    {
        var n = (nombre ?? string.Empty).Trim().ToUpperInvariant();
        return n.Length <= LargoEtiquetaTipo ? n : n[..(LargoEtiquetaTipo - 1)].TrimEnd() + "…";
    }

    /// <summary>"Obj: 11.200 mangas" / "Sin objetivo".</summary>
    public static string TextoObjetivoBarra(decimal? objetivo, string unidad) =>
        objetivo is { } o ? $"Obj: {EficienciaUi.Numero(o)} {unidad}" : EficienciaUi.TextoSinObjetivo;

    /// <summary>"7.450 (66 %)" o "Sin datos" (sin real nunca es 0 %).</summary>
    public static string TextoRealBarra(decimal? real, decimal? porcentaje) =>
        real is not { } r ? EficienciaUi.TextoSinDatos
        : porcentaje is { } p ? $"{EficienciaUi.Numero(r)} ({Math.Round(p, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.CurrentCulture)} %)"
        : EficienciaUi.Numero(r);

    // ===================== Línea de velocidad =====================

    /// <summary>Escala Y de la velocidad: lo mayor entre la serie y la referencia (+10 %), redondeado; mínimo 100 m/min.</summary>
    public static decimal EscalaVelocidad(IEnumerable<decimal?> serie, decimal referencia)
    {
        var max = serie.Where(v => v != null).Select(v => v!.Value).DefaultIfEmpty(0).Max();
        var tope = Math.Max(max, referencia * 1.1m);
        return Math.Max(100m, EscalaRedonda(tope));
    }

    /// <summary>
    /// Tramos de la polilínea de velocidad (cada uno "x,y x,y ..."): un punto por minuto con dato; un minuto sin dato
    /// (null) corta la línea (sin datos no es 0). <paramref name="desplazamiento"/> = minutos desde el inicio del eje al
    /// primer valor; <paramref name="minutosEje"/> = largo del eje X.
    /// </summary>
    public static IReadOnlyList<string> Polilineas(
        IReadOnlyList<decimal?> serie, int desplazamiento, int minutosEje, decimal maximoY, double x0, double ancho, double y0, double alto)
    {
        var tramos = new List<string>();
        if (minutosEje <= 0 || maximoY <= 0)
            return tramos;
        var actual = new List<string>();
        for (var i = 0; i < serie.Count; i++)
        {
            if (serie[i] is not { } v)
            {
                Cortar();
                continue;
            }
            var x = x0 + ancho * Math.Clamp((desplazamiento + i) / (double)minutosEje, 0, 1);
            var y = y0 + alto - alto * Proporcion(v, maximoY);
            actual.Add($"{N(x)},{N(y)}");
        }
        Cortar();
        return tramos;

        void Cortar()
        {
            if (actual.Count == 1)
                actual.Add(actual[0]);   // un minuto suelto igual se ve (como un punto)
            if (actual.Count > 0)
                tramos.Add(string.Join(" ", actual));
            actual.Clear();
        }
    }

    /// <summary>Horas enteras del eje X entre desde y hasta (para las marcas "06:00", "07:00"…), cada <paramref name="cadaHoras"/>.</summary>
    public static IReadOnlyList<DateTimeOffset> MarcasHora(DateTimeOffset desde, DateTimeOffset hasta, int cadaHoras = 1)
    {
        var marcas = new List<DateTimeOffset>();
        var h = new DateTimeOffset(desde.Year, desde.Month, desde.Day, desde.Hour, 0, 0, desde.Offset);
        if (h < desde)
            h = h.AddHours(1);
        for (; h <= hasta && marcas.Count < 48; h = h.AddHours(cadaHoras))
            marcas.Add(h);
        return marcas;
    }

    /// <summary>Minutos enteros entre dos horas (nunca negativo).</summary>
    public static int MinutosEntre(DateTimeOffset desde, DateTimeOffset hasta) =>
        hasta <= desde ? 0 : (int)Math.Round((hasta - desde).TotalMinutes, MidpointRounding.AwayFromZero);

    /// <summary>"612 m/min" o "-".</summary>
    public static string TextoVelocidad(decimal? v) => EficienciaUi.TextoVelocidad(v);

    /// <summary>"125 min" (las detenciones se muestran en minutos, como la lámina).</summary>
    public static string TextoMinutos(int minutos) => minutos.ToString("N0", CultureInfo.CurrentCulture) + " min";
}
