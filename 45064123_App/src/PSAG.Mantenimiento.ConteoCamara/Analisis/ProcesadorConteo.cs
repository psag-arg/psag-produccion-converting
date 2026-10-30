namespace PSAG.Mantenimiento.ConteoCamara.Analisis;

/// <summary>Lo que produce el procesador. El servicio lo manda a la API; el modo offline lo imprime.</summary>
public interface ISalidaConteo
{
    void Ventana(ResultadoVentana ventana);
    void Minutos(IReadOnlyList<ConteoMinuto> minutos);
    void Parada(ParadaDetectada parada);
}

/// <summary>
/// Une todo: cuadro gris reducido → brillo de la zona de cada carril → ventana de 10 s → estimador →
/// minutos por carril y paradas con histéresis. No sabe de dónde vienen los cuadros (RTSP o archivo).
/// </summary>
public sealed class ProcesadorConteo
{
    private readonly EstimadorVentana _estimador;
    private readonly DetectorParadas _detector;
    private readonly AcumuladorMinutos _minutos;
    private readonly ISalidaConteo _salida;
    private readonly (int Carril, int X0, int Y0, int Lado)[] _zonas;
    private readonly int _ancho;
    private readonly int _alto;
    private readonly double[][] _series;
    private int _cuadrosEnVentana;
    private DateTimeOffset _inicioVentana;

    public ProcesadorConteo(OpcionesConteo opciones, ISalidaConteo salida)
    {
        var fuente = opciones.Fuente;
        _ancho = fuente.AnchoDecodificado;
        _alto = fuente.AltoDecodificado;
        _zonas = ZonasDecodificadas(opciones.Zonas, _ancho, _alto);
        if (_zonas.Length == 0)
            throw new InvalidOperationException("No hay carriles configurados (ConteoCamara:Zonas:Carriles en zonas-camara.json).");

        _estimador = new EstimadorVentana(opciones.Analisis, fuente.Fps);
        _detector = new DetectorParadas(opciones.Analisis);
        _minutos = new AcumuladorMinutos(_zonas.Select(z => z.Carril));
        _salida = salida;
        _series = _zonas.Select(_ => new double[_estimador.MuestrasPorVentana]).ToArray();
    }

    public double Fps => _estimador.Fps;

    /// <summary>
    /// Pasa las zonas de la imagen original a la decodificada: centro escalado y lado escalado (mínimo 2 px).
    /// Devuelve la esquina superior izquierda ya recortada a la imagen.
    /// </summary>
    internal static (int Carril, int X0, int Y0, int Lado)[] ZonasDecodificadas(OpcionesZonas zonas, int ancho, int alto)
    {
        var escalaX = (double)ancho / zonas.AnchoImagen;
        var escalaY = (double)alto / zonas.AltoImagen;
        return zonas.Carriles
            .OrderBy(z => z.Carril)
            .Select(z =>
            {
                var lado = Math.Max(2, (int)Math.Round(z.Lado * escalaX));
                var cx = (int)Math.Floor(z.X * escalaX);
                var cy = (int)Math.Floor(z.Y * escalaY);
                var x0 = Math.Clamp(cx - lado / 2, 0, ancho - lado);
                var y0 = Math.Clamp(cy - lado / 2, 0, alto - lado);
                return (z.Carril, x0, y0, lado);
            })
            .ToArray();
    }

    internal static double BrilloMedio(ReadOnlySpan<byte> cuadro, int ancho, int x0, int y0, int lado)
    {
        long suma = 0;
        for (var y = y0; y < y0 + lado; y++)
        {
            var fila = cuadro.Slice(y * ancho + x0, lado);
            foreach (var b in fila) suma += b;
        }
        return (double)suma / (lado * lado);
    }

    /// <summary>Procesa un cuadro gris de AnchoDecodificado x AltoDecodificado, con su hora.</summary>
    public void Cuadro(ReadOnlySpan<byte> cuadro, DateTimeOffset hora)
    {
        if (cuadro.Length < _ancho * _alto)
            throw new ArgumentException("Cuadro incompleto.");

        if (_cuadrosEnVentana == 0)
            _inicioVentana = hora;

        for (var i = 0; i < _zonas.Length; i++)
        {
            var z = _zonas[i];
            _series[i][_cuadrosEnVentana] = BrilloMedio(cuadro, _ancho, z.X0, z.Y0, z.Lado);
        }
        _cuadrosEnVentana++;

        if (_cuadrosEnVentana == _estimador.MuestrasPorVentana)
        {
            var fin = _inicioVentana.AddSeconds(_cuadrosEnVentana / _estimador.Fps);
            CerrarVentana(fin);
        }
    }

    private void CerrarVentana(DateTimeOffset fin)
    {
        var series = _zonas.Select((z, i) => (z.Carril, (double[])_series[i].Clone())).ToList();
        _cuadrosEnVentana = 0;

        var ventana = _estimador.Evaluar(_inicioVentana, fin, series);
        _salida.Ventana(ventana);

        var parada = _detector.Procesar(ventana);
        if (parada != null) _salida.Parada(parada);

        var minutos = _minutos.Agregar(ventana);
        if (minutos.Count > 0) _salida.Minutos(minutos);
    }

    /// <summary>
    /// Se cortó el video: se descarta la ventana a medias y se cierra la parada abierta (FinIncierto).
    /// Con <paramref name="final"/> (terminó el archivo o se detiene el servicio) también se entregan los minutos
    /// pendientes; si no, quedan abiertos y se completan cuando vuelve el video (lo que falte es SIN_SENAL).
    /// </summary>
    public void CortarSenal(bool final)
    {
        _cuadrosEnVentana = 0;
        var parada = _detector.CortarSenal();
        if (parada != null) _salida.Parada(parada);
        if (!final) return;
        var minutos = _minutos.Vaciar();
        if (minutos.Count > 0) _salida.Minutos(minutos);
    }
}
