namespace PSAG.Mantenimiento.ConteoCamara.Analisis;

/// <summary>Estado de un carril (o de la línea) en una ventana o en un minuto.</summary>
public enum EstadoCarril
{
    /// <summary>Pasan rollos a la frecuencia de la cortadora.</summary>
    MARCHA,
    /// <summary>El brillo del punto no cambia: no pasa nada.</summary>
    PARADA,
    /// <summary>Hay movimiento pero sin la frecuencia de corte: rollos pegados, avance a los tirones, operario tapando.</summary>
    ACUMULACION,
    /// <summary>No llegó video.</summary>
    SIN_SENAL
}

public sealed record ResultadoCarril(int Carril, EstadoCarril Estado, double RollosPorSegundo, double Amplitud, double ProporcionPico);

public sealed record ResultadoVentana(
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    double FrecuenciaComunHz,
    IReadOnlyList<ResultadoCarril> Carriles)
{
    public double Segundos => (Fin - Inicio).TotalSeconds;

    /// <summary>Rollos de la ventana sumando los carriles.</summary>
    public double Rollos => Carriles.Sum(c => c.RollosPorSegundo) * Segundos;

    /// <summary>Estado de la línea: todos en marcha, todos parados, o mezcla / irregular (acumulación).</summary>
    public EstadoCarril EstadoLinea =>
        Carriles.All(c => c.Estado == EstadoCarril.MARCHA) ? EstadoCarril.MARCHA
        : Carriles.All(c => c.Estado == EstadoCarril.PARADA) ? EstadoCarril.PARADA
        : EstadoCarril.ACUMULACION;
}

/// <summary>
/// Método validado en la prueba de factibilidad (period.py, window_rates): en cada ventana de 10 s se toma la
/// serie de brillo del punto de cada carril, se calcula el espectro (Hann, relleno x8) y se busca la frecuencia
/// común a todos los carriles (la cortadora corta los 4 a la vez). Un carril está en marcha si se mueve
/// (desvío &gt; AmplitudMinima) y tiene un pico fuerte cerca de esa frecuencia; rollos = frecuencia x segundos.
/// Diferencias con el prototipo: la frecuencia común se busca sólo con los carriles que se mueven
/// (un carril quieto no aporta ruido); el carril que se mueve sin la frecuencia de corte, o a menos del 80 % de la
/// velocidad normal, se informa como ACUMULACION (en el segundo caso los rollos se cuentan igual).
/// </summary>
public sealed class EstimadorVentana
{
    private readonly OpcionesAnalisis _opciones;
    private readonly double _fps;
    private readonly int _muestras;
    private readonly double[] _hann;
    private readonly double[] _frecuencias;
    private readonly double[,] _cos;
    private readonly double[,] _sin;

    public EstimadorVentana(OpcionesAnalisis opciones, double fps)
    {
        _opciones = opciones;
        _fps = fps;
        _muestras = (int)Math.Round(opciones.SegundosVentana * fps);
        if (_muestras < 16)
            throw new ArgumentException("La ventana necesita al menos 16 muestras (subí Fps o SegundosVentana).");

        _hann = new double[_muestras];
        for (var n = 0; n < _muestras; n++)
            _hann[n] = 0.5 - 0.5 * Math.Cos(2 * Math.PI * n / (_muestras - 1));

        // Grilla de la FFT con relleno x8 (igual que numpy rfft(n=8N)), sólo dentro de la banda.
        var relleno = 8 * _muestras;
        var lista = new List<double>();
        for (var k = 0; k <= relleno / 2; k++)
        {
            var f = k * fps / relleno;
            if (f > opciones.FrecuenciaMinimaHz && f < opciones.FrecuenciaMaximaHz)
                lista.Add(f);
        }
        _frecuencias = lista.ToArray();
        _cos = new double[_frecuencias.Length, _muestras];
        _sin = new double[_frecuencias.Length, _muestras];
        for (var i = 0; i < _frecuencias.Length; i++)
            for (var n = 0; n < _muestras; n++)
            {
                var w = 2 * Math.PI * _frecuencias[i] * n / fps;
                _cos[i, n] = Math.Cos(w);
                _sin[i, n] = Math.Sin(w);
            }
    }

    /// <summary>Muestras por ventana (SegundosVentana x Fps).</summary>
    public int MuestrasPorVentana => _muestras;

    public double Fps => _fps;

    /// <summary>
    /// Evalúa una ventana. <paramref name="series"/>: por carril, el brillo medio del punto en cada cuadro
    /// (exactamente <see cref="MuestrasPorVentana"/> valores).
    /// </summary>
    public ResultadoVentana Evaluar(DateTimeOffset inicio, DateTimeOffset fin, IReadOnlyList<(int Carril, double[] Serie)> series)
    {
        var espectros = new double[series.Count][];
        var amplitudes = new double[series.Count];
        for (var c = 0; c < series.Count; c++)
        {
            var serie = series[c].Serie;
            if (serie.Length != _muestras)
                throw new ArgumentException($"El carril {series[c].Carril} trae {serie.Length} muestras y la ventana es de {_muestras}.");
            amplitudes[c] = Desvio(serie);
            espectros[c] = Espectro(serie);
        }

        // Frecuencia común: suma de los espectros normalizados de los carriles que se mueven.
        var f0 = 0.0;
        var suma = new double[_frecuencias.Length];
        var hayMovimiento = false;
        for (var c = 0; c < series.Count; c++)
        {
            if (amplitudes[c] <= _opciones.AmplitudMinima) continue;
            hayMovimiento = true;
            var max = espectros[c].Max() + 1e-9;
            for (var i = 0; i < suma.Length; i++) suma[i] += espectros[c][i] / max;
        }
        if (hayMovimiento && suma.Length > 0)
            f0 = _frecuencias[IndiceMaximo(suma)];

        var carriles = new List<ResultadoCarril>(series.Count);
        for (var c = 0; c < series.Count; c++)
        {
            var espectro = espectros[c];
            var max = espectro.Length > 0 ? espectro.Max() : 0;
            var cerca = 0.0;
            for (var i = 0; i < espectro.Length; i++)
                if (Math.Abs(_frecuencias[i] - f0) < _opciones.ToleranciaHz && espectro[i] > cerca)
                    cerca = espectro[i];
            var proporcion = max > 0 ? cerca / max : 0;

            EstadoCarril estado;
            double rps = 0;
            if (amplitudes[c] <= _opciones.AmplitudMinima)
                estado = EstadoCarril.PARADA;
            else if (f0 > 0 && proporcion >= _opciones.ProporcionPicoMinima)
            {
                // Pasan rollos a ritmo regular. Si es mucho más lento que lo normal es acumulación (se cuentan igual).
                estado = f0 >= _opciones.FraccionMinimaNominal * _opciones.FrecuenciaNominalHz ? EstadoCarril.MARCHA : EstadoCarril.ACUMULACION;
                rps = f0;
            }
            else
                estado = EstadoCarril.ACUMULACION;

            carriles.Add(new ResultadoCarril(series[c].Carril, estado, rps, amplitudes[c], proporcion));
        }

        return new ResultadoVentana(inicio, fin, f0, carriles);
    }

    /// <summary>Pico cerca de <paramref name="f0"/> sobre el pico máximo de la banda (para buscar mejores zonas).</summary>
    public double Proporcion(double[] serie, double f0)
    {
        var espectro = Espectro(serie);
        var max = espectro.Length > 0 ? espectro.Max() : 0;
        var cerca = 0.0;
        for (var i = 0; i < espectro.Length; i++)
            if (Math.Abs(_frecuencias[i] - f0) < _opciones.ToleranciaHz && espectro[i] > cerca)
                cerca = espectro[i];
        return max > 0 ? cerca / max : 0;
    }

    private double[] Espectro(double[] serie)
    {
        var media = serie.Average();
        var x = new double[_muestras];
        for (var n = 0; n < _muestras; n++) x[n] = (serie[n] - media) * _hann[n];

        var salida = new double[_frecuencias.Length];
        for (var i = 0; i < _frecuencias.Length; i++)
        {
            double re = 0, im = 0;
            for (var n = 0; n < _muestras; n++)
            {
                re += x[n] * _cos[i, n];
                im -= x[n] * _sin[i, n];
            }
            salida[i] = Math.Sqrt(re * re + im * im);
        }
        return salida;
    }

    internal static double Desvio(double[] serie)
    {
        var media = serie.Average();
        var acumulado = 0.0;
        foreach (var v in serie) acumulado += (v - media) * (v - media);
        return Math.Sqrt(acumulado / serie.Length);
    }

    private static int IndiceMaximo(double[] valores)
    {
        var indice = 0;
        for (var i = 1; i < valores.Length; i++)
            if (valores[i] > valores[indice]) indice = i;
        return indice;
    }
}
