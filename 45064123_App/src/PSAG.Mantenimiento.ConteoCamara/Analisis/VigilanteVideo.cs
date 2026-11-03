namespace PSAG.Mantenimiento.ConteoCamara.Analisis;

public enum TipoAlertaVideo
{
    /// <summary>Cuadros idénticos: la cámara o el grabador repiten la misma imagen (el reloj no avanza).</summary>
    CONGELADA,
    /// <summary>No llegan cuadros del grabador.</summary>
    SIN_VIDEO
}

/// <summary>Alerta de video. Sin <see cref="Fin"/> está abierta; se vuelve a mandar con fin al cerrarse.</summary>
public sealed record AlertaVideo(TipoAlertaVideo Tipo, DateTimeOffset Inicio, DateTimeOffset? Fin, string? Detalle = null);

/// <summary>
/// Se pone entre el video y el analizador. Retiene los últimos <c>SegundosCongelado</c> de cuadros: si en ese
/// tiempo ningún píxel cambió más de <c>DiferenciaMaximaIdentico</c> (una imagen viva siempre cambia: el reloj de
/// la cámara avanza cada segundo y hay ruido), la imagen está congelada. Esos cuadros se descartan y el
/// analizador corta la señal, así una imagen congelada nunca se toma como "máquina parada" y no se crean
/// detenciones falsas. Abre una alerta CONGELADA y la cierra cuando la imagen vuelve a cambiar.
/// </summary>
public sealed class VigilanteVideo : IAnalizadorCuadros
{
    private readonly IAnalizadorCuadros _analizador;
    private readonly ISalidaConteo _salida;
    private readonly int _diferenciaMaxima;
    private readonly int _cuadrosParaCongelado;
    private readonly int _largoCuadro;
    private readonly Queue<(byte[] Cuadro, DateTimeOffset Hora)> _retenidos = new();
    private readonly Stack<byte[]> _libres = new();
    private byte[]? _anterior;
    private int _identicosSeguidos;
    private DateTimeOffset? _congeladaDesde;
    private readonly int _cuadrosParaVolver;
    private int _cambiosSeguidos;
    private DateTimeOffset _primerCambio;

    public VigilanteVideo(IAnalizadorCuadros analizador, ISalidaConteo salida, OpcionesVigilancia opciones, int largoCuadro)
    {
        _analizador = analizador;
        _salida = salida;
        _diferenciaMaxima = Math.Max(0, opciones.DiferenciaMaximaIdentico);
        _cuadrosParaCongelado = Math.Max(2, (int)Math.Round(opciones.SegundosCongelado * analizador.Fps));
        _largoCuadro = largoCuadro;
        _cuadrosParaVolver = Math.Max(2, (int)Math.Round(analizador.Fps));
    }

    public double Fps => _analizador.Fps;

    public bool Congelada => _congeladaDesde != null;

    /// <summary>Hora del último cuadro recibido (vivo o congelado). Null si todavía no llegó ninguno.</summary>
    public DateTimeOffset? UltimoCuadro { get; private set; }

    /// <summary>Diferencia máxima de un píxel entre dos cuadros, cortando apenas supera el límite.</summary>
    internal static bool Identicos(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b, int diferenciaMaxima)
    {
        for (var i = 0; i < a.Length; i++)
            if (Math.Abs(a[i] - b[i]) > diferenciaMaxima)
                return false;
        return true;
    }

    public void Cuadro(ReadOnlySpan<byte> cuadro, DateTimeOffset hora)
    {
        var actual = cuadro[.._largoCuadro];
        var identico = _anterior != null && Identicos(actual, _anterior, _diferenciaMaxima);
        _anterior ??= new byte[_largoCuadro];
        actual.CopyTo(_anterior);
        UltimoCuadro = hora;

        if (_congeladaDesde is { } desde)
        {
            if (identico)
            {
                _cambiosSeguidos = 0;
                return;
            }
            // Un cuadro clave del códec puede cambiar apenas una imagen congelada: la imagen "volvió" recién
            // cuando cambia durante 1 s seguido. Se cierra la alerta en el primero de esos cuadros.
            if (_cambiosSeguidos++ == 0) _primerCambio = hora;
            if (_cambiosSeguidos < _cuadrosParaVolver) return;
            _salida.Alerta(new AlertaVideo(TipoAlertaVideo.CONGELADA, desde, _primerCambio, "La imagen volvió a cambiar."));
            _congeladaDesde = null;
            _identicosSeguidos = 0;
            _cambiosSeguidos = 0;
            Retener(actual, hora);
            return;
        }

        _identicosSeguidos = identico ? _identicosSeguidos + 1 : 0;
        Retener(actual, hora);

        if (_identicosSeguidos >= _cuadrosParaCongelado)
        {
            // Los cuadros de la racha (todos menos el último vivo) no se analizan.
            var vivos = _retenidos.Count - _identicosSeguidos;
            while (_retenidos.Count > 0 && vivos-- > 0)
                Entregar(_retenidos.Dequeue());
            var primeroCongelado = _retenidos.Count > 0 ? _retenidos.Peek().Hora : hora;
            while (_retenidos.Count > 0)
                _libres.Push(_retenidos.Dequeue().Cuadro);
            _analizador.CortarSenal(final: false);
            _congeladaDesde = primeroCongelado;
            _salida.Alerta(new AlertaVideo(TipoAlertaVideo.CONGELADA, primeroCongelado, null,
                "Imagen congelada: el reloj de la cámara no avanza (cuadros idénticos). No se detectan paradas mientras dure."));
            return;
        }

        while (_retenidos.Count > _cuadrosParaCongelado)
            Entregar(_retenidos.Dequeue());
    }

    private void Retener(ReadOnlySpan<byte> cuadro, DateTimeOffset hora)
    {
        var copia = _libres.Count > 0 ? _libres.Pop() : new byte[_largoCuadro];
        cuadro.CopyTo(copia);
        _retenidos.Enqueue((copia, hora));
    }

    private void Entregar((byte[] Cuadro, DateTimeOffset Hora) item)
    {
        _analizador.Cuadro(item.Cuadro, item.Hora);
        _libres.Push(item.Cuadro);
    }

    /// <summary>
    /// Se cortó el video o se detiene el servicio: lo retenido se analiza (si no era una racha congelada) y se
    /// corta la señal. Una alerta CONGELADA abierta queda abierta hasta que vuelva una imagen viva.
    /// </summary>
    public void CortarSenal(bool final)
    {
        if (_congeladaDesde == null)
            while (_retenidos.Count > 0)
                Entregar(_retenidos.Dequeue());
        else
            while (_retenidos.Count > 0)
                _libres.Push(_retenidos.Dequeue().Cuadro);
        _anterior = null;
        _identicosSeguidos = 0;
        _analizador.CortarSenal(final);
        if (final && _congeladaDesde is { } desde && UltimoCuadro is { } ultimo)
        {
            _salida.Alerta(new AlertaVideo(TipoAlertaVideo.CONGELADA, desde, ultimo, "Terminó el video o se detuvo el servicio con la imagen congelada."));
            _congeladaDesde = null;
        }
    }
}
