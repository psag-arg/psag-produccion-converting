namespace PSAG.Mantenimiento.ConteoCamara.Analisis;

/// <summary>Conteo de un carril en un minuto de reloj.</summary>
public sealed record ConteoMinuto(
    DateTimeOffset Minuto,
    int Carril,
    double Rollos,
    int SegundosMarcha,
    int SegundosParada,
    int SegundosAcumulacion,
    int SegundosSinSenal)
{
    /// <summary>Estado con más segundos en el minuto (a igualdad gana el peor: sin señal, parada, acumulación, marcha).</summary>
    public EstadoCarril Estado
    {
        get
        {
            var max = new[] { SegundosSinSenal, SegundosParada, SegundosAcumulacion, SegundosMarcha }.Max();
            if (SegundosSinSenal == max) return EstadoCarril.SIN_SENAL;
            if (SegundosParada == max) return EstadoCarril.PARADA;
            if (SegundosAcumulacion == max) return EstadoCarril.ACUMULACION;
            return EstadoCarril.MARCHA;
        }
    }
}

/// <summary>
/// Reparte cada ventana (10 s) entre los minutos de reloj que cubre, proporcional al tiempo, y entrega el minuto
/// cuando ya no puede recibir más datos. Lo que falta para completar 60 s es SIN_SENAL (arranque, cortes de video).
/// Los minutos se fechan en la zona de la hora que trae la ventana (la de la planta).
/// </summary>
public sealed class AcumuladorMinutos
{
    private sealed class Acumulado
    {
        public double Rollos, Marcha, Parada, Acumulacion;
    }

    private readonly SortedDictionary<DateTimeOffset, Dictionary<int, Acumulado>> _minutos = new();
    private readonly SortedSet<int> _carriles;

    public AcumuladorMinutos(IEnumerable<int> carriles) => _carriles = new SortedSet<int>(carriles);

    public static DateTimeOffset InicioMinuto(DateTimeOffset t) =>
        new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Offset);

    /// <summary>Agrega una ventana y devuelve los minutos que quedaron completos (anteriores al fin de la ventana).</summary>
    public IReadOnlyList<ConteoMinuto> Agregar(ResultadoVentana ventana)
    {
        var duracion = ventana.Segundos;
        if (duracion > 0)
        {
            var minuto = InicioMinuto(ventana.Inicio);
            while (minuto < ventana.Fin)
            {
                var siguiente = minuto.AddMinutes(1);
                var desde = ventana.Inicio > minuto ? ventana.Inicio : minuto;
                var hasta = ventana.Fin < siguiente ? ventana.Fin : siguiente;
                var segundos = (hasta - desde).TotalSeconds;
                if (segundos > 0)
                {
                    var porCarril = Obtener(minuto);
                    foreach (var c in ventana.Carriles)
                    {
                        if (!porCarril.TryGetValue(c.Carril, out var a))
                            porCarril[c.Carril] = a = new Acumulado();
                        a.Rollos += c.RollosPorSegundo * segundos;
                        switch (c.Estado)
                        {
                            case EstadoCarril.MARCHA: a.Marcha += segundos; break;
                            case EstadoCarril.PARADA: a.Parada += segundos; break;
                            case EstadoCarril.ACUMULACION: a.Acumulacion += segundos; break;
                        }
                    }
                }
                minuto = siguiente;
            }
        }

        return Entregar(hastaExclusivo: InicioMinuto(ventana.Fin));
    }

    /// <summary>Entrega todo lo pendiente (al cortar la señal o al terminar un video).</summary>
    public IReadOnlyList<ConteoMinuto> Vaciar() => Entregar(hastaExclusivo: DateTimeOffset.MaxValue);

    private Dictionary<int, Acumulado> Obtener(DateTimeOffset minuto)
    {
        if (!_minutos.TryGetValue(minuto, out var d))
            _minutos[minuto] = d = new Dictionary<int, Acumulado>();
        return d;
    }

    private List<ConteoMinuto> Entregar(DateTimeOffset hastaExclusivo)
    {
        var salida = new List<ConteoMinuto>();
        foreach (var minuto in _minutos.Keys.Where(m => m < hastaExclusivo).ToList())
        {
            var porCarril = _minutos[minuto];
            foreach (var carril in _carriles)
            {
                porCarril.TryGetValue(carril, out var a);
                a ??= new Acumulado();
                var marcha = (int)Math.Round(a.Marcha);
                var parada = (int)Math.Round(a.Parada);
                var acumulacion = (int)Math.Round(a.Acumulacion);
                var exceso = marcha + parada + acumulacion - 60;   // redondeos de ventanas partidas
                if (exceso > 0)
                {
                    if (marcha >= parada && marcha >= acumulacion) marcha -= exceso;
                    else if (parada >= acumulacion) parada -= exceso;
                    else acumulacion -= exceso;
                }
                var sinSenal = Math.Max(0, 60 - marcha - parada - acumulacion);
                salida.Add(new ConteoMinuto(minuto, carril, Math.Round(a.Rollos, 1), marcha, parada, acumulacion, sinSenal));
            }
            _minutos.Remove(minuto);
        }
        return salida;
    }
}
