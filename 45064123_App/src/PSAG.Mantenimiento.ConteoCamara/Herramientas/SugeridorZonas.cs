using PSAG.Mantenimiento.ConteoCamara.Analisis;

namespace PSAG.Mantenimiento.ConteoCamara.Herramientas;

public sealed record SugerenciaZona(int Carril, int X, int Y, double Puntaje, double PeorVentana, double Amplitud, double Pertenencia, bool EsActual);

/// <summary>
/// Busca, alrededor del punto actual de cada carril, el punto donde la señal de paso de rollos es más limpia.
/// Usa la frecuencia común de las zonas actuales en cada ventana con la línea en marcha (≥ 3 carriles en marcha)
/// y puntúa cada candidato con el percentil 5 de "pico en la frecuencia de corte / pico máximo".
/// La búsqueda es chica a propósito (±48 px de la imagen original, medio rollo): como la cortadora corta los 4 carriles
/// a la vez, un punto del carril vecino puntúa igual de bien, y con un radio grande la búsqueda "salta" de carril.
/// Además en X se limita a media distancia al carril vecino, y cada candidato tiene que "pertenecer" al carril:
/// la parte irregular de su señal (lo que no es el ritmo de corte: huecos, rollos corridos, acumulación) tiene que
/// parecerse más a la del punto actual de ese carril que a la de los otros (correlación con desfase de hasta 3 s,
/// mínimo 0,5). Un punto del carril vecino puntúa igual por frecuencia pero no pasa este filtro.
/// El punto actual de cada carril tiene que estar bien ubicado (verificarlo una vez con la foto y calibracion.html).
/// Es numérica a propósito: no hace falta mirar las imágenes (no salen de la PC).
/// </summary>
public sealed class SugeridorZonas
{
    private readonly OpcionesConteo _opciones;
    private readonly EstimadorVentana _estimador;
    private readonly (int Carril, int Xc, int Yc, (int X, int Y, int X0, int Y0)[] Candidatos)[] _busqueda;
    private readonly int _lado;
    private readonly List<double[]> _muestras = [];   // por cuadro: todas las zonas candidatas en fila
    private readonly (int Carril, int X0, int Y0, int Lado)[] _actuales;

    public SugeridorZonas(OpcionesConteo opciones, int radioBusqueda = 48, int paso = 8)
    {
        _opciones = opciones;
        _estimador = new EstimadorVentana(opciones.Analisis, opciones.Fuente.Fps);
        var f = opciones.Fuente;
        var z = opciones.Zonas;
        var ordenados = z.Carriles.OrderBy(c => c.X).ToList();
        _actuales = ProcesadorConteo.ZonasDecodificadas(z, f.AnchoDecodificado, f.AltoDecodificado);
        _lado = _actuales.Length > 0 ? _actuales[0].Lado : 8;

        _busqueda = z.Carriles.OrderBy(c => c.Carril).Select(c =>
        {
            var i = ordenados.IndexOf(c);
            var izq = i > 0 ? (c.X - ordenados[i - 1].X) / 2 : radioBusqueda;
            var der = i < ordenados.Count - 1 ? (ordenados[i + 1].X - c.X) / 2 : radioBusqueda;
            var candidatos = new List<(int, int, int, int)>();
            var limiteIzq = Math.Min(radioBusqueda, izq - 1);
            var limiteDer = Math.Min(radioBusqueda, der - 1);
            for (var dx = -(limiteIzq / paso) * paso; dx <= limiteDer; dx += paso)
                for (var dy = -(radioBusqueda / paso) * paso; dy <= radioBusqueda; dy += paso)
                {
                    var x = c.X + dx; var y = c.Y + dy;
                    var unico = new OpcionesZonas { AnchoImagen = z.AnchoImagen, AltoImagen = z.AltoImagen, Carriles = [new ZonaCarril { Carril = c.Carril, X = x, Y = y, Lado = c.Lado }] };
                    var d = ProcesadorConteo.ZonasDecodificadas(unico, f.AnchoDecodificado, f.AltoDecodificado)[0];
                    candidatos.Add((x, y, d.X0, d.Y0));
                }
            return (c.Carril, c.X, c.Y, candidatos.ToArray());
        }).ToArray();
    }

    public void Cuadro(ReadOnlySpan<byte> cuadro)
    {
        var ancho = _opciones.Fuente.AnchoDecodificado;
        var fila = new double[_actuales.Length + _busqueda.Sum(b => b.Candidatos.Length)];
        var k = 0;
        foreach (var a in _actuales) fila[k++] = ProcesadorConteo.BrilloMedio(cuadro, ancho, a.X0, a.Y0, a.Lado);
        foreach (var b in _busqueda)
            foreach (var c in b.Candidatos) fila[k++] = ProcesadorConteo.BrilloMedio(cuadro, ancho, c.X0, c.Y0, _lado);
        _muestras.Add(fila);
    }

    public IReadOnlyList<SugerenciaZona> Sugerir(int mejoresPorCarril = 3)
    {
        var n = _estimador.MuestrasPorVentana;
        var ventanas = _muestras.Count / n;
        var f0s = new List<(int Ventana, double F0)>();
        for (var w = 0; w < ventanas; w++)
        {
            var series = _actuales.Select((a, i) => (a.Carril, Columna(i, w * n, n))).ToList();
            var r = _estimador.Evaluar(DateTimeOffset.MinValue, DateTimeOffset.MinValue.AddSeconds(1), series);
            if (r.Carriles.Count(c => c.Estado == EstadoCarril.MARCHA) >= Math.Min(3, _actuales.Length))
                f0s.Add((w, r.FrecuenciaComunHz));
        }
        if (f0s.Count == 0)
            throw new InvalidOperationException("No hubo ventanas con la línea en marcha: probá con un tramo con producción.");

        var referencias = Enumerable.Range(0, _actuales.Length).Select(i => PasaAlto(Columna(i, 0, _muestras.Count))).ToArray();
        var salida = new List<SugerenciaZona>();
        var col = _actuales.Length;
        var indicePropio = 0;
        foreach (var b in _busqueda)
        {
            var puntajes = new List<SugerenciaZona>();
            foreach (var c in b.Candidatos)
            {
                var proporciones = new List<double>();
                var amplitudes = new List<double>();
                foreach (var (w, f0) in f0s)
                {
                    var serie = Columna(col, w * n, n);
                    amplitudes.Add(EstimadorVentana.Desvio(serie));
                    proporciones.Add(_estimador.Proporcion(serie, f0));
                }
                var irregular = PasaAlto(Columna(col, 0, _muestras.Count));
                var correlaciones = referencias.Select(r => CorrelacionConDesfase(irregular, r, (int)(3 * _estimador.Fps))).ToArray();
                var pertenencia = correlaciones[indicePropio];
                var esDelCarril = pertenencia >= 0.5 && correlaciones.Max() <= pertenencia + 1e-9;
                col++;
                proporciones.Sort();
                var amp = amplitudes.Average();
                var p5 = proporciones[(int)Math.Floor(0.05 * (proporciones.Count - 1))];
                // Con poco contraste el punto es frágil: se penaliza lo que no supera 1,5 veces la amplitud mínima.
                var puntaje = amp >= 1.5 * _opciones.Analisis.AmplitudMinima ? p5 : p5 * 0.5;
                if (!esDelCarril) puntaje = 0;
                puntajes.Add(new SugerenciaZona(b.Carril, c.X, c.Y, Math.Round(puntaje, 3), Math.Round(proporciones[0], 3), Math.Round(amp, 1),
                    Math.Round(pertenencia, 2), c.X == b.Xc && c.Y == b.Yc));
            }
            indicePropio++;
            salida.AddRange(puntajes.Where(p => p.Puntaje > 0).OrderByDescending(p => p.Puntaje).Take(mejoresPorCarril));
            var actual = puntajes.FirstOrDefault(p => p.EsActual);
            if (actual != null && !salida.Contains(actual)) salida.Add(actual);
        }
        return salida;
    }

    /// <summary>Saca la tendencia lenta (media móvil de 1,5 s): queda el paso de rollos y sus irregularidades.</summary>
    internal static double[] PasaAlto(double[] s, int ventana = 15)
    {
        var salida = new double[s.Length];
        var suma = 0.0;
        var mitad = ventana / 2;
        var acumulado = new double[s.Length + 1];
        for (var i = 0; i < s.Length; i++) acumulado[i + 1] = acumulado[i] + s[i];
        for (var i = 0; i < s.Length; i++)
        {
            var a = Math.Max(0, i - mitad);
            var b = Math.Min(s.Length, i + mitad + 1);
            suma = (acumulado[b] - acumulado[a]) / (b - a);
            salida[i] = s[i] - suma;
        }
        return salida;
    }

    /// <summary>Máxima correlación de Pearson entre a y b desplazada hasta ±maxDesfase muestras.</summary>
    internal static double CorrelacionConDesfase(double[] a, double[] b, int maxDesfase)
    {
        var n = a.Length;
        if (n <= 2 * maxDesfase + 10) return 0;
        var mejor = double.MinValue;
        for (var d = -maxDesfase; d <= maxDesfase; d++)
        {
            double sa = 0, sb = 0, saa = 0, sbb = 0, sab = 0;
            var cuenta = 0;
            for (var i = maxDesfase; i < n - maxDesfase; i++)
            {
                var x = a[i];
                var y = b[i + d];
                sa += x; sb += y; saa += x * x; sbb += y * y; sab += x * y;
                cuenta++;
            }
            var cov = sab - sa * sb / cuenta;
            var va = saa - sa * sa / cuenta;
            var vb = sbb - sb * sb / cuenta;
            if (va <= 0 || vb <= 0) continue;
            var r = cov / Math.Sqrt(va * vb);
            if (r > mejor) mejor = r;
        }
        return mejor == double.MinValue ? 0 : mejor;
    }

    private double[] Columna(int indice, int desde, int cantidad)
    {
        var s = new double[cantidad];
        for (var i = 0; i < cantidad; i++) s[i] = _muestras[desde + i][indice];
        return s;
    }
}
