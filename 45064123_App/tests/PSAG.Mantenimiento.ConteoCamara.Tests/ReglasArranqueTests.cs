using Microsoft.Extensions.Configuration;
using PSAG.Mantenimiento.ConteoCamara.Analisis;

namespace PSAG.Mantenimiento.ConteoCamara.Tests;

/// <summary>
/// Regla A (60 s de marcha para cerrar una parada) en el detector, con ventanas sintéticas de 10 s de un carril.
/// Caso real 13/11: la máquina paró 11:08:21, un operario limpió la mesa con aire 11:10-11:14 (la cámara vio
/// "marcha" de 20 y 40 s) y arrancó 11:21:58; antes la parada se partía en tres.
/// </summary>
public class ReglaArranqueDetectorTests
{
    private static DetectorParadas Nuevo(int arranque = 6) =>
        new(new OpcionesAnalisis { VentanasParaConfirmar = 2, VentanasParaConfirmarArranque = arranque, SegundosMinimosParada = 20 },
            TipoParada.SIN_MANGAS);

    private static ResultadoVentana V(int i, bool marcha) =>
        Senales.Ventana(i, marcha ? EstadoCarril.MARCHA : EstadoCarril.PARADA);

    /// <summary>Corre la secuencia ('M' marcha, 'P' parada, una letra por ventana de 10 s).</summary>
    private static (List<ParadaDetectada> Cerradas, List<ParadaEnCurso> EnCurso) Correr(DetectorParadas d, string secuencia, bool cortarAlFinal = true)
    {
        var cerradas = new List<ParadaDetectada>();
        var enCurso = new List<ParadaEnCurso>();
        for (var i = 0; i < secuencia.Length; i++)
        {
            if (d.Procesar(V(i, secuencia[i] == 'M')) is { } p) cerradas.Add(p);
            if (d.Abierta is { } a) enCurso.Add(a);
        }
        if (cortarAlFinal && d.CortarSenal(final: true) is { } u) cerradas.Add(u);
        return (cerradas, enCurso);
    }

    private static DateTimeOffset T(int ventana) => Senales.T0.AddSeconds(10 * ventana);

    [Fact]
    public void LimpiezaQueParecenArranquesDe20y40s_EsUnaSolaParada()
    {
        //                 marcha | parada 2 min    | 20 s M | P | 40 s M | parada        | arranque real
        var (cerradas, enCurso) = Correr(Nuevo(), "MMMMM" + "PPPPPPPPPPPP" + "MM" + "PP" + "MMMM" + "PPPPPPPPPPPP" + "MMMMMMMMMM");

        var p = Assert.Single(cerradas);
        Assert.Equal(T(5), p.Inicio);
        Assert.Equal(T(5 + 12 + 2 + 2 + 4 + 12), p.Fin);            // inicio de la marcha que sí se confirmó
        Assert.False(p.FinIncierto);
        Assert.All(enCurso, e => Assert.Equal(p.Inicio, e.Inicio));   // un solo identificador: una sola detención
    }

    [Fact]
    public void ConLaReglaApagada_LaMismaLimpiezaPartiaLaParada()
    {
        var (cerradas, _) = Correr(Nuevo(arranque: 0), "MMMMM" + "PPPPPPPPPPPP" + "MM" + "PP" + "MMMM" + "PPPPPPPPPPPP" + "MMMMMMMMMM");
        Assert.Equal(3, cerradas.Count);
    }

    [Fact]
    public void ArranqueRealDe40s_NoCierraLaParada()
    {
        var (cerradas, _) = Correr(Nuevo(), "MMM" + "PPPPPP" + "MMMM" + "PPPPPP" + "MMMMMMM");
        var p = Assert.Single(cerradas);
        Assert.Equal(T(3), p.Inicio);
        Assert.Equal(T(19), p.Fin);
    }

    [Fact]
    public void ArranqueRealDe70s_CierraLaParadaEnElInicioDelArranque()
    {
        var (cerradas, _) = Correr(Nuevo(), "MMM" + "PPPPPP" + "MMMMMMM" + "PPPPPP" + "MMMMMMM");
        Assert.Equal(2, cerradas.Count);
        Assert.Equal((T(3), T(9)), (cerradas[0].Inicio, cerradas[0].Fin));
        Assert.Equal((T(16), T(22)), (cerradas[1].Inicio, cerradas[1].Fin));
    }

    [Theory]
    [InlineData(5, 1)]   // 50 s en marcha: sigue la misma parada
    [InlineData(6, 2)]   // 60 s justos: cierra
    public void Borde_SeisVentanasCierran_CincoNo(int ventanasMarcha, int paradasEsperadas)
    {
        var (cerradas, _) = Correr(Nuevo(), "MM" + "PPPP" + new string('M', ventanasMarcha) + "PPPP" + "MMMMMM");
        Assert.Equal(paradasEsperadas, cerradas.Count);
    }

    [Fact]
    public void AbrirLaParada_SigueConDosVentanas()
    {
        var d = Nuevo();
        Correr(d, "MMMPP", cortarAlFinal: false);
        Assert.True(d.Detenida);
        Assert.Equal(T(3), d.Abierta!.Inicio);
    }

    [Fact]
    public void EnCurso_MientrasSeConfirmaElArranqueNoAvanzaYSiNoArrancaSigue()
    {
        var d = Nuevo();
        var (_, enCurso) = Correr(d, "MM" + "PPP" + "MMMM" + "PP", cortarAlFinal: false);
        // Durante los 40 s de "marcha" la parada en curso queda hasta el inicio de esa marcha (no infla la duración).
        // (la parada se confirma en la ventana 3: enCurso[k] es la ventana k + 3)
        Assert.Equal(T(5), enCurso[2].Hasta);
        Assert.Equal(T(5), enCurso[5].Hasta);
        // Volvió a parar: esos 40 s eran parte de la parada.
        Assert.Equal(T(11), enCurso[^1].Hasta);
        Assert.Single(enCurso.Select(e => e.Inicio).Distinct());
        Assert.Equal(0, d.VentanasConfirmandoArranque);
    }

    [Fact]
    public void VentanasConfirmandoArranque_CuentaSoloConLaParadaAbierta()
    {
        var d = Nuevo();
        d.Procesar(V(0, true)); d.Procesar(V(1, true));
        d.Procesar(V(2, false));
        Assert.Equal(0, d.VentanasConfirmandoArranque);              // candidata a parada, no a arranque
        d.Procesar(V(3, false));
        d.Procesar(V(4, true)); d.Procesar(V(5, true));
        Assert.Equal(2, d.VentanasConfirmandoArranque);
    }

    [Fact]
    public void CorteDeVideoConParadaAbierta_AlVolverEnMarchaTambienPide60s()
    {
        var d = Nuevo();
        Correr(d, "MM" + "PPPP", cortarAlFinal: false);
        d.CortarSenal(final: false);                                 // ffmpeg se reconecta
        var cerradas = new List<ParadaDetectada>();
        for (var i = 7; i < 11; i++)                                 // vuelve con 40 s de "marcha"
            if (d.Procesar(V(i, true)) is { } p) cerradas.Add(p);
        Assert.Empty(cerradas);
        for (var i = 11; i < 13; i++)                                // sigue parada: se retoma la misma
            if (d.Procesar(V(i, false)) is { } p) cerradas.Add(p);
        Assert.Empty(cerradas);
        Assert.Equal(T(2), d.Abierta!.Inicio);
    }

    [Fact]
    public void FinalDelVideoMientrasSeConfirmaElArranque_CierraDondeEmpezoElArranque()
    {
        var (cerradas, _) = Correr(Nuevo(), "MM" + "PPPP" + "MMM");
        var p = Assert.Single(cerradas);
        Assert.Equal(T(6), p.Fin);
        Assert.True(p.FinIncierto);
    }
}

/// <summary>
/// Reglas E y F en la cámara 10 con cuadros sintéticos (ver <see cref="Camara10"/>).
/// </summary>
public class ReglasCamara10Tests
{
    private readonly Camara10 _cam = new();
    private readonly Captura _salida = new();
    private readonly VigilanteVideo _cadena;

    public ReglasCamara10Tests() => _cadena = Analizadores.Crear(Opciones(), _salida);

    /// <summary>Cámara sintética con las reglas de producción: A = 6 ventanas, E = 90 %, F = sí.</summary>
    private static OpcionesConteo Opciones()
    {
        var o = Camara10.Opciones();
        o.Analisis.VentanasParaConfirmarArranque = 6;
        o.Mangas.FraccionRodilloGiraParaMarcha = 0.9;
        o.Mangas.DescontarMangasEnParada = true;
        return o;
    }

    private double Pasar(double desde, double segundos, double periodo, bool rodilloGira = true) =>
        _cam.Pasar(_cadena, desde, segundos, periodo, rodilloGira);

    /// <summary>
    /// Limpieza con aire: alguien se mueve sobre la mesa (pulsos como mangas, irregulares) y tapa el rodillo a
    /// ratos (se ve "girar" tramos de 2 a 5 s y quieto de 0,6 a 2,4 s). Con las reglas viejas (alcanzaba con el
    /// rodillo girando la mitad de la ventana) eso era marcha.
    /// </summary>
    private double Limpieza(double desde, double segundos, int semilla) => Limpieza(_cam, _cadena, desde, segundos, semilla);

    private static double Limpieza(Camara10 cam, IAnalizadorCuadros cadena, double desde, double segundos, int semilla)
    {
        var r = new Random(semilla);
        var cuadros = (int)Math.Round(segundos * Camara10.Fps);
        var gira = false;
        var resto = 0;
        for (var i = 0; i < cuadros; i++)
        {
            if (resto-- <= 0)
            {
                gira = !gira;
                resto = gira ? r.Next(10, 26) : r.Next(3, 13);       // ~70 % "girando": antes era marcha
            }
            var t = desde + (double)i / Camara10.Fps;
            var manga = t % 4.0 < 0.6 && r.NextDouble() < 0.7;
            cadena.Cuadro(cam.Cuadro(gira, manga), Camara10.T0.AddSeconds(t));
        }
        return desde + segundos;
    }

    private double MangasEntre(double desde, double hasta) => _salida.Ventanas
        .Where(v => v.Inicio >= Camara10.T0.AddSeconds(desde) && v.Inicio < Camara10.T0.AddSeconds(hasta)).Sum(v => v.Rollos);

    [Fact]
    public void LimpiezaQueTapaLaCamara_UnaSolaParadaYSinMangas()
    {
        var t = Pasar(0, 60, periodo: 2.7);
        t = Pasar(t, 120, periodo: 0, rodilloGira: false);           // paró
        t = Limpieza(t, 240, semilla: 11);                           // 4 min limpiando con aire
        t = Pasar(t, 300, periodo: 0, rodilloGira: false);           // sigue parada
        var arranque = t;
        Pasar(t, 120, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        var p = Assert.Single(_salida.Paradas);
        Assert.Equal(TipoParada.PARADA, p.Tipo);
        Assert.InRange((p.Inicio - Camara10.T0).TotalSeconds, 59, 71);
        Assert.InRange((p.Fin - Camara10.T0).TotalSeconds, arranque - 1, arranque + 11);
        Assert.Equal(0, MangasEntre(70, arranque - 10));             // ninguna manga de la limpieza
        Assert.InRange(MangasEntre(arranque, arranque + 120), 42, 46); // las del arranque sí (120 / 2,7 = 44)
        // Minutos: nada de marcha mientras estuvo parada; los segundos suman 60.
        Assert.All(_salida.Minutos_.Where(m => m.Minuto >= Camara10.T0.AddMinutes(2) && m.Minuto < Camara10.T0.AddSeconds(arranque - 60)),
            m => Assert.Equal(0, m.SegundosMarcha));
        Assert.All(_salida.Minutos_, m => Assert.Equal(60, m.SegundosMarcha + m.SegundosParada + m.SegundosAcumulacion + m.SegundosSinSenal));
        Assert.All(_salida.EnCurso, e => Assert.Equal(p.Inicio, e.Inicio));
    }

    [Fact]
    public void ReglasViejas_LaMismaLimpiezaPartiaLaParadaYContabaMangas()
    {
        var salida = new Captura();
        var o = Camara10.Opciones();
        o.Mangas.FraccionRodilloGiraParaMarcha = 0;                  // como antes de las reglas E y F
        o.Mangas.DescontarMangasEnParada = false;
        var cadena = Analizadores.Crear(o, salida);
        var cam = new Camara10();
        var t = cam.Pasar(cadena, 0, 60, 2.7);
        t = cam.Pasar(cadena, t, 120, 0, rodilloGira: false);
        t = Limpieza(cam, cadena, t, 240, semilla: 11);
        t = cam.Pasar(cadena, t, 300, 0, rodilloGira: false);
        cam.Pasar(cadena, t, 120, 2.7);
        cadena.CortarSenal(final: true);

        Assert.True(salida.Paradas.Count >= 2, $"{salida.Paradas.Count} paradas");
        Assert.True(salida.Ventanas.Where(v => v.Inicio >= Camara10.T0.AddSeconds(190) && v.Inicio < Camara10.T0.AddSeconds(410)).Sum(v => v.Rollos) > 5);
    }

    [Fact]
    public void MangasDuranteLaParada_NoSeCuentan()
    {
        var t = Pasar(0, 60, periodo: 2.7);
        t = Pasar(t, 60, periodo: 0, rodilloGira: false);
        t = Pasar(t, 60, periodo: 3, rodilloGira: false);            // se mueve algo en la mesa con el rodillo quieto
        t = Pasar(t, 60, periodo: 0, rodilloGira: false);
        Pasar(t, 60, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        Assert.Single(_salida.Paradas);
        Assert.Equal(0, MangasEntre(70, 180));
    }

    [Fact]
    public void ArranqueRealDe40s_QuedaDentroDeLaParadaYSusMangasNoCuentan()
    {
        var t = Pasar(0, 60, periodo: 2.7);
        t = Pasar(t, 120, periodo: 0, rodilloGira: false);
        t = Pasar(t, 40, periodo: 2.7);                               // arranca 40 s y vuelve a parar
        t = Pasar(t, 120, periodo: 0, rodilloGira: false);
        Pasar(t, 120, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        var p = Assert.Single(_salida.Paradas);
        Assert.InRange(p.DuracionSegundos, 270, 290);                 // 120 + 40 + 120
        Assert.Equal(0, MangasEntre(70, 290));                        // riesgo aceptado: esas ~15 mangas se pierden
    }

    [Fact]
    public void ArranqueRealDe70s_CierraLaParadaYCuentaSusMangas()
    {
        var t = Pasar(0, 60, periodo: 2.7);
        t = Pasar(t, 120, periodo: 0, rodilloGira: false);
        var arranque = t;
        t = Pasar(t, 70, periodo: 2.7);
        t = Pasar(t, 120, periodo: 0, rodilloGira: false);
        Pasar(t, 120, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        Assert.Equal(2, _salida.Paradas.Count);
        Assert.InRange((_salida.Paradas[0].Fin - Camara10.T0).TotalSeconds, arranque - 1, arranque + 11);
        Assert.InRange(MangasEntre(arranque, arranque + 70), 24, 27);  // 70 / 2,7 = 26
    }

    [Fact]
    public void ReglaE_RodilloQuietoUnCuartoDeLaVentana_NoEsMarcha()
    {
        // 10 s: 7,5 s girando con mangas y 2,5 s quieto (la máquina para en el medio de la ventana).
        var t = Pasar(0, 30, periodo: 2.7);
        t = Pasar(t, 7.5, periodo: 2.7);
        Pasar(t, 2.5, periodo: 0, rodilloGira: false);
        _cadena.CortarSenal(final: true);

        Assert.Equal(EstadoCarril.PARADA, _salida.Ventanas[^1].EstadoLinea);
        Assert.True(_salida.Ventanas[^1].Rollos > 0);                 // ventana que abre la parada: sus mangas cuentan
    }

    [Fact]
    public void FinalDelVideoConArranqueSinConfirmar_CuentaEsasMangas()
    {
        var t = Pasar(0, 60, periodo: 2.7);
        t = Pasar(t, 120, periodo: 0, rodilloGira: false);
        Pasar(t, 30, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        var p = Assert.Single(_salida.Paradas);
        Assert.True(p.FinIncierto);
        Assert.Equal(21, _salida.Ventanas.Count);                    // ninguna ventana quedó retenida
        Assert.InRange(MangasEntre(180, 210), 10, 12);
    }

    [Theory]
    [InlineData(15, 40)]
    [InlineData(20, 60)]
    [InlineData(25, 60)]
    public void EmpalmeDeBobina_ConLasReglasNuevas_NoEsParadaNiDetencion(double periodo, double segundos)
    {
        var t = _cam.Pasar(_cadena, 0, 60, periodo: 2.7);
        t = _cam.Pasar(_cadena, t, segundos, periodo, movimientoRodillo: 10);
        _cam.Pasar(_cadena, t, 90, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        Assert.DoesNotContain(_salida.Ventanas, v => v.EstadoLinea == EstadoCarril.PARADA);
        Assert.All(_salida.Paradas, p => Assert.Equal(TipoParada.SIN_MANGAS, p.Tipo));
        Assert.DoesNotContain(_salida.EnCurso, p => p.Hasta - p.Inicio >= TimeSpan.FromMinutes(5));
    }
}

public class ConfiguracionReglasCamaraTests
{
    [Fact]
    public void ElAppsettingsDelRepo_La10TraeLasReglasNuevasYLa09NoCambia()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var camaras = OpcionesConteo.CargarCamaras(new ConfigurationBuilder().AddJsonFile(ruta).Build());
        var c09 = camaras.Single(c => c.Camara == "T2-CAM09");
        var c10 = camaras.Single(c => c.Camara == "T2-CAM10");

        Assert.Equal(0, c09.Analisis.VentanasParaConfirmarArranque);
        Assert.Equal(2, c09.Analisis.VentanasParaConfirmar);

        Assert.Equal(2, c10.Analisis.VentanasParaConfirmar);
        Assert.Equal(6, c10.Analisis.VentanasParaConfirmarArranque);
        Assert.Equal(0.9, c10.Mangas.FraccionRodilloGiraParaMarcha);
        Assert.True(c10.Mangas.DescontarMangasEnParada);
        Assert.Equal(c10.Mangas.UmbralPasoAlto, new OpcionesMangas().UmbralPasoAlto);
        Assert.Equal(c10.Mangas.SegundosMinimosEntrePasos, new OpcionesMangas().SegundosMinimosEntrePasos);
        Assert.Equal(15, c10.Mangas.UmbralPasoAlto);
        Assert.Equal(2.0, c10.Mangas.SegundosMinimosEntrePasos);
        Assert.Equal(DetectoresParadas.Ventanas, c10.Mangas.DetectorParadas);   // T-804: A/E/F es lo productivo
    }

    [Fact]
    public void ElLocalDelServidorSoloPisaActiva_ConservaLasReglas()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var local = """{ "ConteoCamara": { "Api": { "UrlBase": "http://x" }, "Camaras": [ { "PuntoControl": "T2-CAM09" }, { "PuntoControl": "T2-CAM10", "Activa": true } ] } }""";
        var conf = new ConfigurationBuilder().AddJsonFile(ruta)
            .AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(local))).Build();
        var c10 = OpcionesConteo.CargarCamaras(conf).Single(c => c.Camara == "T2-CAM10");
        Assert.True(c10.ActivaIgnorada);                             // T-798: "Activa" ya no se usa (Maestros → Cámaras)
        Assert.Equal(6, c10.Analisis.VentanasParaConfirmarArranque);
        Assert.True(c10.Mangas.DescontarMangasEnParada);
        Assert.Equal(DetectoresParadas.Ventanas, c10.Mangas.DetectorParadas);
    }
}
