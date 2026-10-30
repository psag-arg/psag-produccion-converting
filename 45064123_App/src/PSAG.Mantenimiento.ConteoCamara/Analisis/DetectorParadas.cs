namespace PSAG.Mantenimiento.ConteoCamara.Analisis;

public enum TipoParada
{
    /// <summary>En algún momento estuvieron quietos los 4 carriles (la cortadora paró).</summary>
    PARADA,
    /// <summary>Nunca pararon todos: uno o más carriles quietos o irregulares mientras otros seguían.</summary>
    ACUMULACION
}

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

    public DetectorParadas(OpcionesAnalisis opciones) => _opciones = opciones;

    public bool? Detenida => _detenida;

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
                if (detenidaAhora)
                {
                    AbrirParada(_inicioCandidato, inicioIncierto: anterior == null);
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
    /// Se cortó el video (o se detiene el servicio): si había una parada abierta se cierra en el último dato
    /// con FinIncierto, y se vuelve a empezar sin estado confirmado.
    /// </summary>
    public ParadaDetectada? CortarSenal()
    {
        ParadaDetectada? cerrada = null;
        if (_detenida == true)
        {
            foreach (var v in _ventanasCandidatas) Acumular(v);
            cerrada = Cerrar(_ultimoFin, finIncierto: true);
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
            _huboParadaTotal ? TipoParada.PARADA : TipoParada.ACUMULACION,
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
