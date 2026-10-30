using System.Globalization;
using System.Text.Json;
using PSAG.Mantenimiento.ConteoCamara.Analisis;

namespace PSAG.Mantenimiento.ConteoCamara.Salida;

/// <summary>Junta todo en memoria para el modo offline / prueba en vivo y arma el resumen (mismas métricas que el informe).</summary>
public sealed class ResumenConsola : ISalidaConteo
{
    private readonly ISalidaConteo? _tambien;
    private readonly bool _eco;
    public List<ResultadoVentana> Ventanas { get; } = [];
    public List<ConteoMinuto> MinutosRecibidos { get; } = [];
    public List<ParadaDetectada> Paradas { get; } = [];

    public ResumenConsola(ISalidaConteo? tambien = null, bool eco = false)
    {
        _tambien = tambien;
        _eco = eco;
    }

    public void Ventana(ResultadoVentana ventana)
    {
        Ventanas.Add(ventana);
        if (_eco)
            Console.WriteLine($"{ventana.Inicio:HH:mm:ss}  {ventana.Rollos / ventana.Segundos * 60,5:0}/min  {ventana.EstadoLinea,-11} " +
                string.Join("  ", ventana.Carriles.Select(c => $"c{c.Carril}:{Corto(c.Estado)} a={c.Amplitud:0} p={c.ProporcionPico:0.00}")));
        _tambien?.Ventana(ventana);
    }

    public void Minutos(IReadOnlyList<ConteoMinuto> minutos)
    {
        MinutosRecibidos.AddRange(minutos);
        _tambien?.Minutos(minutos);
    }

    public void Parada(ParadaDetectada parada)
    {
        Paradas.Add(parada);
        if (_eco)
            Console.WriteLine($"  >> {parada.Tipo} {parada.Inicio:HH:mm:ss}-{parada.Fin:HH:mm:ss} ({parada.DuracionSegundos} s) carriles {string.Join(",", parada.Carriles)}");
        _tambien?.Parada(parada);
    }

    private static string Corto(EstadoCarril e) => e switch
    {
        EstadoCarril.MARCHA => "M",
        EstadoCarril.PARADA => "P",
        EstadoCarril.ACUMULACION => "A",
        _ => "-"
    };

    public object Resumen(string nombre, double segundosProceso)
    {
        var porVentana = Ventanas.Select(v => Math.Round(v.Rollos / v.Segundos * 60)).ToList();
        var duracion = Ventanas.Sum(v => v.Segundos);
        var carriles = Ventanas.FirstOrDefault()?.Carriles.Select(c => c.Carril).ToList() ?? [];
        return new
        {
            video = nombre,
            ventanas = Ventanas.Count,
            minutosDeVideo = Math.Round(duracion / 60, 2),
            segundosDeProceso = Math.Round(segundosProceso, 1),
            vecesTiempoReal = segundosProceso > 0 ? Math.Round(duracion / segundosProceso, 1) : 0,
            rollosMinPromedio = porVentana.Count > 0 ? Math.Round(porVentana.Average()) : 0,
            rollosMinMarchaPlena = Ventanas.Where(v => v.EstadoLinea == EstadoCarril.MARCHA).Select(v => v.Rollos / v.Segundos * 60).DefaultIfEmpty().Average() is var m ? Math.Round(m) : 0,
            ventanasMarchaPlena = Ventanas.Count(v => v.EstadoLinea == EstadoCarril.MARCHA),
            ventanasDetenidas = Ventanas.Count(v => v.EstadoLinea == EstadoCarril.PARADA),
            ventanasAcumulacion = Ventanas.Count(v => v.EstadoLinea == EstadoCarril.ACUMULACION),
            rollosEstimados = Math.Round(Ventanas.Sum(v => v.Rollos)),
            rollosEnMinutos = Math.Round(MinutosRecibidos.Sum(x => x.Rollos)),
            ventanasNoMarchaPorCarril = carriles.ToDictionary(
                c => $"carril{c}",
                c => Ventanas.Count(v => v.Carriles.First(x => x.Carril == c).Estado != EstadoCarril.MARCHA)),
            rollosPorCarril = carriles.ToDictionary(
                c => $"carril{c}",
                c => Math.Round(Ventanas.Sum(v => v.Carriles.First(x => x.Carril == c).RollosPorSegundo * v.Segundos))),
            rollosMinPorVentana = porVentana,
            paradas = Paradas.Select(p => new
            {
                tipo = p.Tipo.ToString(),
                inicio = p.Inicio.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                fin = p.Fin.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                segundos = p.DuracionSegundos,
                carriles = p.Carriles,
                p.InicioIncierto,
                p.FinIncierto
            })
        };
    }

    public static string AJson(object o) => JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true });
}
