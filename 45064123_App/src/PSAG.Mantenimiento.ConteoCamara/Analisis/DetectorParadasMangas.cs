namespace PSAG.Mantenimiento.ConteoCamara.Analisis;

/// <summary>
/// Paradas de la cámara 10 (rebobinadora) por eventos, cuadro a cuadro (T-784). Reemplaza la histéresis de dos
/// ventanas de 10 s, que juntaba microparadas seguidas (un arranque de menos de 20 s no cortaba la parada) y
/// descartaba las de menos de 20 s (caso del 13/11, 11:08 a 11:14). Cada microparada queda con su inicio y fin reales:
/// <list type="bullet">
/// <item><b>Para</b> cuando pasan <c>SegundosSinMangasParaParada</c> (10) sin mangas, o con el rodillo quieto ese
/// tiempo seguido. El <b>inicio</b> es la última manga que salió (o el primer cuadro con el rodillo quieto).</item>
/// <item><b>Arranca</b> con <c>MangasParaReanudar</c> (2) mangas seguidas a menos de 10 s una de otra. El <b>fin</b> es
/// la primera de ellas. Una manga suelta (ruido, una mano que cruza la mesa) no corta la parada.</item>
/// <item>No hay duración mínima: lo que cumple la definición se informa. El umbral de detención lo decide la API.</item>
/// </list>
/// El empalme (mangas cada 7 s con el rodillo girando) no es parada. Tipo PARADA si el rodillo estuvo quieto
/// <c>SegundosRodilloQuietoParaParada</c> (5) seguidos durante la parada; si no, SIN_MANGAS.
/// Corte o imagen congelada: la parada queda suspendida; si el video vuelve antes de
/// <c>SegundosMaximosCorteParaRetomar</c> sigue la misma (mismo inicio e identificador). Si al volver la máquina ya
/// estaba produciendo, o el corte fue largo, se cierra en el último instante visto con FinIncierto.
/// </summary>
public sealed class DetectorParadasMangas
{
    private readonly double _segundosHueco;
    private readonly int _mangasParaReanudar;
    private readonly double _segundosRodilloParaTipo;
    private readonly double _segundosMaximosCorte;

    private DateTimeOffset? _primerCuadro;        // desde cuándo se mira sin interrupción (sin mangas todavía: inicio incierto)
    private DateTimeOffset _ultimoCuadro;
    private DateTimeOffset? _ultimaManga;
    private DateTimeOffset? _rodilloQuietoDesde;

    // Parada abierta
    private bool _detenida;
    private DateTimeOffset _inicio;
    private bool _inicioIncierto;
    private bool _huboRodilloQuieto;

    // Arranque por confirmar: primera manga de la racha y cuántas van.
    private DateTimeOffset? _primeraMangaArranque;
    private int _mangasArranque;

    // Corte de video con la parada abierta.
    private DateTimeOffset? _suspendidaHasta;
    private DateTimeOffset? _volvioEn;

    public DetectorParadasMangas(OpcionesMangas mangas, OpcionesAnalisis analisis)
    {
        _segundosHueco = Math.Max(1, mangas.SegundosSinMangasParaParada);
        _mangasParaReanudar = Math.Max(1, mangas.MangasParaReanudar);
        _segundosRodilloParaTipo = Math.Max(0, mangas.SegundosRodilloQuietoParaParada);
        _segundosMaximosCorte = analisis.SegundosMaximosCorteParaRetomar;
    }

    /// <summary>La parada que sigue abierta (hasta el último instante detenido), o null. Suspendida por un corte: null.</summary>
    public ParadaEnCurso? Abierta => _detenida && (_suspendidaHasta == null || _volvioEn != null)
        ? new ParadaEnCurso(_inicio, UltimoDetenido, Tipo, _inicioIncierto)
        : null;

    private TipoParada Tipo => _huboRodilloQuieto ? TipoParada.PARADA : TipoParada.SIN_MANGAS;

    /// <summary>Si se está confirmando un arranque, la primera manga (el fin que tendrá la parada); si no, el último cuadro.</summary>
    private DateTimeOffset UltimoDetenido => _primeraMangaArranque ?? _ultimoCuadro;

    /// <summary>
    /// Un cuadro: si pasó una manga y si el rodillo está quieto (null sin zona de rodillo o sin cuadro anterior).
    /// Devuelve la parada si con este cuadro se cerró una.
    /// </summary>
    public ParadaDetectada? Cuadro(DateTimeOffset hora, bool manga, bool? rodilloQuieto)
    {
        ParadaDetectada? cerrada = null;
        if (_suspendidaHasta is { } suspendida && _volvioEn == null)
        {
            _volvioEn = hora;
            if ((hora - suspendida).TotalSeconds > _segundosMaximosCorte)
            {
                cerrada = Cerrar(suspendida, finIncierto: true);
                _primerCuadro = null;
                _ultimaManga = null;
            }
        }

        _primerCuadro ??= hora;
        _ultimoCuadro = hora;
        if (rodilloQuieto == true) _rodilloQuietoDesde ??= hora;
        else _rodilloQuietoDesde = null;

        if (!_detenida)
        {
            if (manga)
            {
                _ultimaManga = hora;
                return cerrada;
            }
            var anclaMangas = _ultimaManga ?? _primerCuadro.Value;
            var porMangas = (hora - anclaMangas).TotalSeconds >= _segundosHueco;
            var porRodillo = _rodilloQuietoDesde is { } q && (hora - q).TotalSeconds >= _segundosHueco;
            if (porMangas || porRodillo)
            {
                var inicio = porMangas && porRodillo ? Min(anclaMangas, _rodilloQuietoDesde!.Value)
                    : porMangas ? anclaMangas : _rodilloQuietoDesde!.Value;
                Abrir(inicio, inicioIncierto: _ultimaManga == null && inicio == _primerCuadro);
                _huboRodilloQuieto = porRodillo;
            }
            return cerrada;
        }

        if (_rodilloQuietoDesde is { } quietoDesde && (hora - quietoDesde).TotalSeconds >= _segundosRodilloParaTipo)
            _huboRodilloQuieto = true;

        if (manga)
        {
            if (_primeraMangaArranque != null && (hora - _ultimaManga!.Value).TotalSeconds <= _segundosHueco)
                _mangasArranque++;
            else
            {
                _primeraMangaArranque = hora;
                _mangasArranque = 1;
            }
            _ultimaManga = hora;
            if (_mangasArranque >= _mangasParaReanudar && rodilloQuieto != true)
                cerrada = Arrancar() ?? cerrada;
        }
        else if (_primeraMangaArranque != null && (hora - _ultimaManga!.Value).TotalSeconds > _segundosHueco)
        {
            // La manga suelta no era el arranque: sigue la misma parada.
            _primeraMangaArranque = null;
            _mangasArranque = 0;
        }
        return cerrada;
    }

    /// <summary>
    /// Se cortó o se congeló el video. Con la parada abierta queda suspendida hasta que vuelva el video; con
    /// <paramref name="final"/> (terminó el archivo o se detiene el servicio) se cierra con FinIncierto.
    /// </summary>
    public ParadaDetectada? CortarSenal(bool final)
    {
        ParadaDetectada? cerrada = null;
        if (_detenida)
        {
            if (final)
                cerrada = Cerrar(_suspendidaHasta is { } s && _volvioEn == null ? s : UltimoDetenido, finIncierto: true);
            else if (_suspendidaHasta == null || _volvioEn != null)
            {
                // Segundo corte sin cuadros en el medio: queda la primera suspensión.
                _suspendidaHasta = UltimoDetenido;
                _volvioEn = null;
            }
        }
        _rodilloQuietoDesde = null;
        _primeraMangaArranque = null;
        _mangasArranque = 0;
        if (!_detenida)
        {
            // Sin video no se sabe qué pasó: lo próximo se mide desde que vuelva.
            _primerCuadro = null;
            _ultimaManga = null;
        }
        return cerrada;
    }

    private void Abrir(DateTimeOffset inicio, bool inicioIncierto)
    {
        _detenida = true;
        _inicio = inicio;
        _inicioIncierto = inicioIncierto;
        _huboRodilloQuieto = false;
        _primeraMangaArranque = null;
        _mangasArranque = 0;
    }

    private ParadaDetectada? Arrancar()
    {
        var fin = _primeraMangaArranque!.Value;
        var finIncierto = false;
        if (_suspendidaHasta is { } suspendida && _volvioEn is { } volvio && (fin - volvio).TotalSeconds < _segundosHueco)
        {
            // Cuando volvió el video la máquina ya estaba produciendo: arrancó durante el corte.
            fin = suspendida;
            finIncierto = true;
        }
        return Cerrar(fin, finIncierto);
    }

    private ParadaDetectada? Cerrar(DateTimeOffset fin, bool finIncierto)
    {
        var parada = fin > _inicio
            ? new ParadaDetectada(_inicio, fin, Tipo, [ProcesadorMangas.Carril], _inicioIncierto, finIncierto)
            : null;
        _detenida = false;
        _suspendidaHasta = null;
        _volvioEn = null;
        _primeraMangaArranque = null;
        _mangasArranque = 0;
        _huboRodilloQuieto = false;
        return parada;
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
}
