using PSAG.Mantenimiento.ConteoCamara.Analisis;

namespace PSAG.Mantenimiento.ConteoCamara.Tests;

/// <summary>
/// T-784: paradas de la cámara 10 por eventos (sin ventanas). Se alimenta el detector a 5 cuadros por segundo con las
/// mangas en los instantes indicados; el rodillo gira salvo que se diga lo contrario.
/// </summary>
public class DetectorParadasMangasTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 13, 11, 0, 0, TimeSpan.FromHours(-3));
    private const double Paso = 0.2;

    private readonly List<ParadaDetectada> _paradas = [];
    private readonly List<ParadaEnCurso> _enCurso = [];

    private static DetectorParadasMangas Detector(int mangasParaReanudar = 2) =>
        new(new OpcionesMangas { MangasParaReanudar = mangasParaReanudar }, new OpcionesAnalisis());

    /// <summary>Mangas cada 2,7 s (ritmo normal) entre <paramref name="desde"/> y <paramref name="hasta"/> (segundos desde T0).</summary>
    private static IEnumerable<double> Ritmo(double desde, double hasta)
    {
        for (var t = desde; t < hasta - 1e-9; t += 2.7)
            yield return Math.Round(t, 1);
    }

    /// <summary>Pasa cuadros de <paramref name="desde"/> a <paramref name="hasta"/> con mangas en esos segundos.</summary>
    private void Pasar(DetectorParadasMangas d, double desde, double hasta, IEnumerable<double> mangas, Func<double, bool>? rodilloQuieto = null)
    {
        var pendientes = new SortedSet<double>(mangas.Where(m => m >= desde - 1e-9 && m < hasta - 1e-9));
        for (var t = desde; t < hasta - 1e-9; t = Math.Round(t + Paso, 1))
        {
            var manga = pendientes.Count > 0 && pendientes.Min <= t + 1e-9;
            if (manga) pendientes.Remove(pendientes.Min);
            var p = d.Cuadro(T0.AddSeconds(t), manga, rodilloQuieto?.Invoke(t) ?? false);
            if (p != null) _paradas.Add(p);
            if (d.Abierta is { } a) _enCurso.Add(a);
        }
    }

    private static double S(DateTimeOffset instante) => Math.Round((instante - T0).TotalSeconds, 1);

    [Fact]
    public void MicroparadasSeguidas_CadaUnaConSuInicioYFinReales()
    {
        // Caso 13/11: 11:08-11:14 paradas de 30-50 s con arranques cortos (3 mangas, ~8 s) entre medio. Con la
        // histéresis de 20 s se juntaban en una o se perdían; ahora quedan todas, separadas.
        var d = Detector();
        var mangas = Ritmo(0, 60)                 // marcha; última manga 59.4
            .Concat(Ritmo(100, 108))              // parada 59.4 → 100; arranque de ~8 s
            .Concat(Ritmo(140, 146))              // parada 105.4 → 140
            .Concat(Ritmo(170, 240));             // parada 145.4 → 170; marcha
        Pasar(d, 0, 240, mangas);
        d.CortarSenal(final: true);

        Assert.Equal(3, _paradas.Count);
        Assert.Equal([(59.4, 100.0), (105.4, 140.0), (145.4, 170.0)], _paradas.Select(p => (S(p.Inicio), S(p.Fin))));
        Assert.All(_paradas, p =>
        {
            Assert.Equal(TipoParada.SIN_MANGAS, p.Tipo);
            Assert.False(p.InicioIncierto);
            Assert.False(p.FinIncierto);
        });
    }

    [Fact]
    public void MicroparadaDe11Segundos_SeInforma()
    {
        // Antes se descartaba todo lo de menos de 20 s.
        var d = Detector();
        Pasar(d, 0, 60, Ritmo(0, 29).Concat(Ritmo(37.6, 60)));   // última 27.0, vuelve 37.6: 10,6 s sin mangas
        Assert.Equal((27.0, 37.6), (S(_paradas.Single().Inicio), S(_paradas.Single().Fin)));
    }

    [Fact]
    public void HuecoDeMenosDe10Segundos_NoEsParada()
    {
        // Empalme: mangas cada 7 s con la máquina andando más lento.
        var d = Detector();
        Pasar(d, 0, 120, Ritmo(0, 29).Concat(Enumerable.Range(0, 9).Select(i => 30.0 + 7 * i)).Concat(Ritmo(93, 120)));
        d.CortarSenal(final: true);
        Assert.Empty(_paradas);
    }

    [Fact]
    public void UnaMangaSuelta_NoCortaLaParada()
    {
        var d = Detector();
        Pasar(d, 0, 200, Ritmo(0, 29).Append(80).Concat(Ritmo(150, 200)));   // la de 80 está sola
        var p = Assert.Single(_paradas);
        Assert.Equal((27.0, 150.0), (S(p.Inicio), S(p.Fin)));
    }

    [Fact]
    public void ConUnaMangaParaReanudar_CadaMangaCortaLaParada()
    {
        var d = Detector(mangasParaReanudar: 1);
        Pasar(d, 0, 200, Ritmo(0, 29).Append(80).Concat(Ritmo(150, 200)));
        Assert.Equal([(27.0, 80.0), (80.0, 150.0)], _paradas.Select(p => (S(p.Inicio), S(p.Fin))));
    }

    [Fact]
    public void RodilloQuietoDuranteLaParada_EsTipoParada()
    {
        var d = Detector();
        Pasar(d, 0, 120, Ritmo(0, 29).Concat(Ritmo(90, 120)), rodilloQuieto: t => t >= 28 && t < 89);
        var p = Assert.Single(_paradas);
        Assert.Equal(TipoParada.PARADA, p.Tipo);
        Assert.Equal((27.0, 90.0), (S(p.Inicio), S(p.Fin)));
    }

    [Fact]
    public void EnCurso_MismoInicioYHastaNoPasaLaPrimeraMangaDelArranque()
    {
        var d = Detector();
        Pasar(d, 0, 100, Ritmo(0, 29).Concat(Ritmo(80, 100)));
        Assert.NotEmpty(_enCurso);
        Assert.All(_enCurso, a => Assert.Equal(27.0, S(a.Inicio)));
        Assert.All(_enCurso, a => Assert.True(S(a.Hasta) <= 80.0, $"hasta {S(a.Hasta)}"));
        Assert.Equal(80.0, S(_paradas.Single().Fin));
    }

    [Fact]
    public void ArrancaConLaMaquinaParada_InicioIncierto()
    {
        var d = Detector();
        Pasar(d, 0, 60, Ritmo(40, 60));
        var p = Assert.Single(_paradas);
        Assert.True(p.InicioIncierto);
        Assert.Equal((0.0, 40.0), (S(p.Inicio), S(p.Fin)));
    }

    [Fact]
    public void CorteCortoConLaMaquinaTodaviaParada_EsLaMismaParada()
    {
        var d = Detector();
        Pasar(d, 0, 60, Ritmo(0, 29));
        d.CortarSenal(final: false);                         // 40 s sin video
        Pasar(d, 100, 200, Ritmo(150, 200));                 // vuelve parada; arranca a los 150
        var p = Assert.Single(_paradas);
        Assert.Equal((27.0, 150.0), (S(p.Inicio), S(p.Fin)));
        Assert.False(p.FinIncierto);
        Assert.All(_enCurso, a => Assert.Equal(27.0, S(a.Inicio)));   // un solo identificador
    }

    [Fact]
    public void CorteCortoYAlVolverYaProducia_SeCierraDondeSePerdioElVideoConFinIncierto()
    {
        var d = Detector();
        Pasar(d, 0, 60, Ritmo(0, 29));
        d.CortarSenal(final: false);
        Pasar(d, 100, 150, Ritmo(100, 150));
        var p = Assert.Single(_paradas);
        Assert.Equal((27.0, 59.8), (S(p.Inicio), S(p.Fin)));
        Assert.True(p.FinIncierto);
    }

    [Fact]
    public void CorteLargo_CierraConFinInciertoYLoQueSigueEmpiezaIncierto()
    {
        var d = Detector();
        Pasar(d, 0, 60, Ritmo(0, 29));
        d.CortarSenal(final: false);
        Pasar(d, 300, 400, Ritmo(350, 400));                 // 240 s sin video (más de 120)
        Assert.Equal(2, _paradas.Count);
        Assert.Equal((27.0, 59.8), (S(_paradas[0].Inicio), S(_paradas[0].Fin)));
        Assert.True(_paradas[0].FinIncierto);
        Assert.Equal((300.0, 350.0), (S(_paradas[1].Inicio), S(_paradas[1].Fin)));
        Assert.True(_paradas[1].InicioIncierto);
    }

    [Fact]
    public void Final_ConLaParadaAbierta_SeCierraConFinIncierto()
    {
        var d = Detector();
        Pasar(d, 0, 60, Ritmo(0, 29));
        var p = d.CortarSenal(final: true);
        Assert.NotNull(p);
        Assert.Equal((27.0, 59.8), (S(p!.Inicio), S(p.Fin)));
        Assert.True(p.FinIncierto);
    }
}
