namespace PSAG.Mantenimiento.ConteoCamara.Analisis;

public enum TipoParada
{
    /// <summary>En algún momento estuvieron quietos los 4 carriles (la cortadora paró).</summary>
    PARADA,
    /// <summary>Nunca pararon todos: uno o más carriles quietos o irregulares mientras otros seguían.</summary>
    ACUMULACION,
    /// <summary>Cámara 10: el rodillo siguió girando pero no pasaron mangas por la mesa.</summary>
    SIN_MANGAS
}

/// <summary>Parada que sigue abierta: la máquina todavía no arrancó. Se informa para crear la detención sin esperar el fin.</summary>
public sealed record ParadaEnCurso(DateTimeOffset Inicio, DateTimeOffset Hasta, TipoParada Tipo, bool InicioIncierto);

/// <summary>Una parada detectada por la cámara, ya cerrada.</summary>
public sealed record ParadaDetectada(
    DateTimeOffset Inicio,
    DateTimeOffset Fin,
    TipoParada Tipo,
    IReadOnlyList<int> Carriles,
    bool InicioIncierto,
    bool FinIncierto)
{
    public int DuracionSegundos => (int)Math.Round((Fin - Inicio).TotalSeconds);
}

/// <summary>
/// Paradas con histéresis. La línea tiene dos estados confirmados: en marcha (los 4 carriles en marcha) o detenida
/// (cualquier otra cosa). Para cambiar hacen falta <c>VentanasParaConfirmar</c> ventanas seguidas del otro estado;
/// el cambio se fecha al inicio de la primera de esas ventanas, así la histéresis no corre la hora.
/// Una ventana suelta de un carril ruidoso no abre una parada, y un arranque de 10 s no la corta.
/// </summary>
public sealed class DetectorParadas
{
    private readonly OpcionesAnalisis _opciones;
    private readonly TipoParada _tipoParcial;

    private bool? _detenida;                  // null = todavía no se confirmó nada (arranque o después de un corte)
    private bool _candidatoDetenida;
    private int _ventanasCandidato;
    private DateTimeOffset _inicioCandidato;
    private readonly List<ResultadoVentana> _ventanasCandidatas = [];

    // Parada abierta
    private DateTimeOffset _inicioParada;
    private bool _inicioIncierto;
    private bool _huboParadaTotal;
    private readonly SortedSet<int> _carriles = [];
    private DateTimeOffset _ultimoFin;

    // Parada que quedó abierta al cortarse el video, con su último instante detenido: si el video vuelve pronto
    // y la línea sigue detenida se retoma la misma (mismo inicio e identificador) en vez de partirla en dos.
    private DateTimeOffset? _suspendidaHasta;

    /// <param name="tipoParcial">Tipo de la parada si nunca hubo una ventana PARADA: ACUMULACION en la cortadora,
    /// SIN_MANGAS en la rebobinadora.</param>
    public DetectorParadas(OpcionesAnalisis opciones, TipoParada tipoParcial = TipoParada.ACUMULACION)
    {
        _opciones = opciones;
        _tipoParcial = tipoParcial;
    }

    public bool? Detenida => _detenida;

    /// <summary>La parada confirmada que sigue abierta, hasta el último instante detenido, o null.</summary>
    public ParadaEnCurso? Abierta => _detenida == true
        ? new ParadaEnCurso(_inicioParada, UltimoDetenido, _huboParadaTotal ? TipoParada.PARADA : _tipoParcial, _inicioIncierto)
        : null;

    /// <summary>
    /// Último instante con la línea detenida. Si se está confirmando un arranque, el inicio de la primera ventana
    /// en marcha (el Fin que tendrá la parada cerrada), no el final de la ventana candidata: si no, una parada de
    /// 4:50 se informaría en curso como de 5:00 y la API crearía una detención que la cerrada no confirma.
    /// </summary>
    private DateTimeOffset UltimoDetenido => _detenida == true && _ventanasCandidato > 0 ? _inicioCandidato : _ultimoFin;

    /// <summary>Procesa una ventana; devuelve la parada si con esta ventana se confirmó el fin de una.</summary>
    public ParadaDetectada? Procesar(ResultadoVentana ventana)
    {
        var detenidaAhora = ventana.EstadoLinea != EstadoCarril.MARCHA;
        ParadaDetectada? cerrada = null;

        if (_detenida == detenidaAhora)
        {
            // Sigue igual: lo que parecía un cambio era ruido. Las ventanas candidatas se suman a la parada abierta.
            if (_detenida == true)
                foreach (var v in _ventanasCandidatas) Acumular(v);
            ReiniciarCandidato();
            if (_detenida == true) Acumular(ventana);
        }
        else
        {
            if (_ventanasCandidato == 0 || _candidatoDetenida != detenidaAhora)
            {
                if (_detenida == true)
                    foreach (var v in _ventanasCandidatas) Acumular(v);
                ReiniciarCandidato();
                _candidatoDetenida = detenidaAhora;
                _inicioCandidato = ventana.Inicio;
            }
            _ventanasCandidato++;
            _ventanasCandidatas.Add(ventana);

            if (_ventanasCandidato >= Math.Max(1, _opciones.VentanasParaConfirmar))
            {
                var anterior = _detenida;
                _detenida = detenidaAhora;
                var retomada = false;
                if (_suspendidaHasta is { } suspendida)
                {
                    // Volvió el video después de un corte con una parada abierta. Si la línea sigue detenida y el
                    // corte fue corto es la misma parada; si arrancó (o el corte fue largo) se cierra con fin incierto.
                    _suspendidaHasta = null;
                    retomada = detenidaAhora
                        && (_inicioCandidato - suspendida).TotalSeconds <= _opciones.SegundosMaximosCorteParaRetomar;
                    if (!retomada) cerrada = Cerrar(suspendida, finIncierto: true);
                }
                if (detenidaAhora)
                {
                    if (!retomada) AbrirParada(_inicioCandidato, inicioIncierto: anterior == null);
                    foreach (var v in _ventanasCandidatas) Acumular(v);
                }
                else if (anterior == true)
                {
                    cerrada = Cerrar(_inicioCandidato, finIncierto: false);
                }
                ReiniciarCandidato();
            }
        }

        _ultimoFin = ventana.Fin;
        return cerrada;
    }

    /// <summary>
    /// Se cortó o se congeló el video: si había una parada abierta queda suspendida hasta que vuelva el video
    /// (ver <see cref="Procesar"/>) y se vuelve a empezar sin estado confirmado. Con <paramref name="final"/>
    /// (terminó el archivo o se detiene el servicio) la parada abierta o suspendida se cierra en el último
    /// instante detenido con FinIncierto.
    /// </summary>
    public ParadaDetectada? CortarSenal(bool final)
    {
        ParadaDetectada? cerrada = null;
        if (_detenida == true)
        {
            if (final) cerrada = Cerrar(UltimoDetenido, finIncierto: true);
            else _suspendidaHasta = UltimoDetenido;
        }
        else if (_suspendidaHasta is { } suspendida)
        {
            // Segundo corte antes de saber cómo volvió la línea: sigue suspendida (salvo que sea el final).
            if (final)
            {
                _suspendidaHasta = null;
                cerrada = Cerrar(suspendida, finIncierto: true);
            }
        }
        else if (_detenida == false && _ventanasCandidato > 0 && _candidatoDetenida)
        {
            // Iba a confirmarse una parada que el corte dejó a medias: se informa si alcanza la duración mínima.
            AbrirParada(_inicioCandidato, inicioIncierto: false);
            foreach (var v in _ventanasCandidatas) Acumular(v);
            cerrada = Cerrar(_ultimoFin, finIncierto: true);
        }
        _detenida = null;
        ReiniciarCandidato();
        return cerrada;
    }

    private void AbrirParada(DateTimeOffset inicio, bool inicioIncierto)
    {
        _inicioParada = inicio;
        _inicioIncierto = inicioIncierto;
        _huboParadaTotal = false;
        _carriles.Clear();
    }

    private void Acumular(ResultadoVentana ventana)
    {
        if (ventana.EstadoLinea == EstadoCarril.PARADA) _huboParadaTotal = true;
        foreach (var c in ventana.Carriles)
            if (c.Estado != EstadoCarril.MARCHA) _carriles.Add(c.Carril);
    }

    private ParadaDetectada? Cerrar(DateTimeOffset fin, bool finIncierto)
    {
        var parada = new ParadaDetectada(
            _inicioParada, fin,
            _huboParadaTotal ? TipoParada.PARADA : _tipoParcial,
            _carriles.ToList(), _inicioIncierto, finIncierto);
        _carriles.Clear();
        return parada.DuracionSegundos >= _opciones.SegundosMinimosParada ? parada : null;
    }

    private void ReiniciarCandidato()
    {
        _ventanasCandidato = 0;
        _ventanasCandidatas.Clear();
    }
}
