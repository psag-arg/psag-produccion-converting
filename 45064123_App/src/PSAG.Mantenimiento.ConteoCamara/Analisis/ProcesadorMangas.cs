namespace PSAG.Mantenimiento.ConteoCamara.Analisis;

/// <summary>
/// Cámara 10 (rebobinadora Futura 2 / Tango 2). Dos señales, medidas como movimiento medio (diferencia absoluta
/// entre dos cuadros seguidos) dentro de una zona:
/// <list type="bullet">
/// <item><b>Mesa</b>: cada manga que la cruza da un pulso. Se cuentan con histéresis (sube de UmbralPasoAlto,
/// baja de UmbralPasoBajo) y una separación mínima, así un pulso cuenta una vez.</item>
/// <item><b>Rodillo</b>: girando tiene movimiento constante; quieto, cero.</item>
/// </list>
/// Estado de cada ventana de 10 s: PARADA si el rodillo estuvo quieto la mayor parte; si no, SIN_MANGAS (se
/// informa como ACUMULACION del "carril" 1) si no pasó ninguna manga; si no, MARCHA. El empalme no es parada:
/// el rodillo sigue y siguen pasando mangas, más espaciadas. Una ventana suelta sin mangas no abre parada
/// (histéresis de DetectorParadas: 2 ventanas) y lo que dura menos que el umbral de la cámara es un microcorte.
/// Las mangas se cuentan siempre y van a los minutos como "rollos" del carril 1.
/// </summary>
public sealed class ProcesadorMangas : IAnalizadorCuadros
{
    public const int Carril = 1;

    private readonly OpcionesMangas _o;
    private readonly ISalidaConteo _salida;
    private readonly DetectorParadas _detector;
    private readonly AcumuladorMinutos _minutos;
    private readonly int _ancho;
    private readonly int _alto;
    private readonly (int X0, int Y0, int X1, int Y1) _mesa;
    private readonly (int X0, int Y0, int X1, int Y1)? _rodillo;
    private readonly int _muestrasPorVentana;
    private readonly byte[] _anterior;
    private bool _hayAnterior;

    // Ventana en curso
    private int _cuadros;
    private DateTimeOffset _inicioVentana;
    private int _pasosVentana;
    private int _cuadrosRodilloQuieto;
    private int _cuadrosConRodillo;
    private double _sumaMesa;
    private double _sumaRodillo;

    // Contador de pasos
    private bool _enPaso;
    private DateTimeOffset? _ultimaManga;

    public ProcesadorMangas(OpcionesConteo opciones, ISalidaConteo salida)
    {
        _o = opciones.Mangas;
        _salida = salida;
        _ancho = opciones.Fuente.AnchoDecodificado;
        _alto = opciones.Fuente.AltoDecodificado;
        Fps = opciones.Fuente.Fps;
        _mesa = Escalar(_o.Mesa, _o, _ancho, _alto)
            ?? throw new InvalidOperationException("Falta la zona de la mesa (ConteoCamara:Mangas:Mesa).");
        _rodillo = _o.UsarRodillo && _o.Rodillo != null ? Escalar(_o.Rodillo, _o, _ancho, _alto) : null;
        _muestrasPorVentana = Math.Max(2, (int)Math.Round(opciones.Analisis.SegundosVentana * Fps));
        _detector = new DetectorParadas(opciones.Analisis, TipoParada.SIN_MANGAS);
        _minutos = new AcumuladorMinutos([Carril]);
        _anterior = new byte[_ancho * _alto];
    }

    public double Fps { get; }

    /// <summary>Total de mangas contadas desde que arrancó (para el resumen).</summary>
    public long MangasTotales { get; private set; }

    /// <summary>Zona de la imagen original a la decodificada, recortada a la imagen. Null si queda vacía.</summary>
    internal static (int X0, int Y0, int X1, int Y1)? Escalar(Rectangulo r, OpcionesMangas o, int ancho, int alto)
    {
        var ex = (double)ancho / o.AnchoImagen;
        var ey = (double)alto / o.AltoImagen;
        var x0 = Math.Clamp((int)Math.Floor(r.X * ex), 0, ancho);
        var y0 = Math.Clamp((int)Math.Floor(r.Y * ey), 0, alto);
        var x1 = Math.Clamp((int)Math.Ceiling((r.X + r.Ancho) * ex), 0, ancho);
        var y1 = Math.Clamp((int)Math.Ceiling((r.Y + r.Alto) * ey), 0, alto);
        return x1 > x0 && y1 > y0 ? (x0, y0, x1, y1) : null;
    }

    /// <summary>Diferencia absoluta media entre dos cuadros dentro de la zona.</summary>
    internal static double Movimiento(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> anterior, int ancho, (int X0, int Y0, int X1, int Y1) z)
    {
        long suma = 0;
        for (var y = z.Y0; y < z.Y1; y++)
        {
            var a = actual.Slice(y * ancho + z.X0, z.X1 - z.X0);
            var b = anterior.Slice(y * ancho + z.X0, z.X1 - z.X0);
            for (var x = 0; x < a.Length; x++) suma += Math.Abs(a[x] - b[x]);
        }
        return (double)suma / ((z.X1 - z.X0) * (z.Y1 - z.Y0));
    }

    public void Cuadro(ReadOnlySpan<byte> cuadro, DateTimeOffset hora)
    {
        if (cuadro.Length < _ancho * _alto)
            throw new ArgumentException("Cuadro incompleto.");

        if (_cuadros == 0)
        {
            _inicioVentana = hora;
            _pasosVentana = _cuadrosRodilloQuieto = _cuadrosConRodillo = 0;
            _sumaMesa = _sumaRodillo = 0;
        }

        if (_hayAnterior)
        {
            var mesa = Movimiento(cuadro, _anterior, _ancho, _mesa);
            _sumaMesa += mesa;
            if (!_enPaso && mesa >= _o.UmbralPasoAlto
                && (_ultimaManga is not { } u || (hora - u).TotalSeconds >= _o.SegundosMinimosEntrePasos))
            {
                _enPaso = true;
                _ultimaManga = hora;
                _pasosVentana++;
                MangasTotales++;
            }
            else if (_enPaso && mesa <= _o.UmbralPasoBajo)
                _enPaso = false;

            if (_rodillo is { } r)
            {
                var rod = Movimiento(cuadro, _anterior, _ancho, r);
                _sumaRodillo += rod;
                _cuadrosConRodillo++;
                if (rod < _o.UmbralRodilloGira) _cuadrosRodilloQuieto++;
            }
        }
        cuadro[.._anterior.Length].CopyTo(_anterior);
        _hayAnterior = true;
        _cuadros++;

        if (_cuadros >= _muestrasPorVentana)
            CerrarVentana(_inicioVentana.AddSeconds(_cuadros / Fps));
    }

    private void CerrarVentana(DateTimeOffset fin)
    {
        var segundos = (fin - _inicioVentana).TotalSeconds;
        var rodilloQuieto = _rodillo != null && _cuadrosConRodillo > 0
            && (double)_cuadrosRodilloQuieto / _cuadrosConRodillo >= _o.FraccionRodilloQuieto;
        var estado = rodilloQuieto ? EstadoCarril.PARADA
            : _pasosVentana == 0 ? EstadoCarril.ACUMULACION
            : EstadoCarril.MARCHA;
        var diffs = Math.Max(1, _cuadros - 1);
        var ventana = new ResultadoVentana(_inicioVentana, fin, segundos > 0 ? _pasosVentana / segundos : 0,
        [
            new ResultadoCarril(Carril, estado, segundos > 0 ? _pasosVentana / segundos : 0,
                Amplitud: _sumaMesa / diffs,
                ProporcionPico: _cuadrosConRodillo > 0 ? _sumaRodillo / _cuadrosConRodillo : -1)
        ]);
        _cuadros = 0;

        _salida.Ventana(ventana);
        var parada = _detector.Procesar(ventana);
        if (parada != null) _salida.Parada(parada);
        if (_detector.Abierta is { } abierta) _salida.ParadaEnCurso(abierta);
        var minutos = _minutos.Agregar(ventana);
        if (minutos.Count > 0) _salida.Minutos(minutos);
    }

    public void CortarSenal(bool final)
    {
        _cuadros = 0;
        _hayAnterior = false;
        _enPaso = false;
        _ultimaManga = null;
        var parada = _detector.CortarSenal();
        if (parada != null) _salida.Parada(parada);
        if (!final) return;
        var minutos = _minutos.Vaciar();
        if (minutos.Count > 0) _salida.Minutos(minutos);
    }
}
