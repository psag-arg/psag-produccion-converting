using System.Text.Json;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.ConteoCamara;
using PSAG.Mantenimiento.ConteoCamara.Analisis;
using PSAG.Mantenimiento.ConteoCamara.Salida;
using PSAG.Mantenimiento.ConteoCamara.Video;

namespace PSAG.Mantenimiento.ConteoCamara.Tests;

/// <summary>Señales sintéticas: un rollo que pasa = un ciclo de brillo. 10 fps, ventanas de 10 s.</summary>
internal static class Senales
{
    public const double Fps = 10;
    public static readonly DateTimeOffset T0 = new(2026, 10, 28, 10, 15, 0, TimeSpan.FromHours(-3));

    /// <summary>Paso regular a <paramref name="hz"/> rollos/s: brillo 120 ± 40 con forma de pulso (más armónicos que un seno).</summary>
    public static double[] Marcha(double hz, int n = 100, double fase = 0, double ruido = 2, int semilla = 1)
    {
        var r = new Random(semilla);
        return Enumerable.Range(0, n).Select(i =>
        {
            var c = (i / Fps * hz + fase) % 1.0;
            return 120 + (c < 0.45 ? 40 : -30) + (r.NextDouble() - 0.5) * 2 * ruido;
        }).ToArray();
    }

    public static double[] Quieto(int n = 100, int semilla = 2)
    {
        var r = new Random(semilla);
        return Enumerable.Range(0, n).Select(_ => 150 + (r.NextDouble() - 0.5) * 3).ToArray();
    }

    /// <summary>Movimiento sin periodicidad (rollos pegados, a los tirones): ruido grande.</summary>
    public static double[] Irregular(int n = 100, int semilla = 3)
    {
        var r = new Random(semilla);
        double v = 120;
        return Enumerable.Range(0, n).Select(_ => v = Math.Clamp(v + (r.NextDouble() - 0.5) * 60, 40, 220)).ToArray();
    }

    public static OpcionesAnalisis Analisis() => new();

    public static ResultadoVentana Ventana(int indice, params EstadoCarril[] estados)
    {
        var inicio = T0.AddSeconds(10 * indice);
        return new ResultadoVentana(inicio, inicio.AddSeconds(10), 2.78,
            estados.Select((e, i) => new ResultadoCarril(i + 1, e, e == EstadoCarril.MARCHA ? 2.78 : 0, 30, 1)).ToList());
    }

    public static ResultadoVentana Marcha4(int i) => Ventana(i, EstadoCarril.MARCHA, EstadoCarril.MARCHA, EstadoCarril.MARCHA, EstadoCarril.MARCHA);
    public static ResultadoVentana Parada4(int i) => Ventana(i, EstadoCarril.PARADA, EstadoCarril.PARADA, EstadoCarril.PARADA, EstadoCarril.PARADA);
}

public class EstimadorVentanaTests
{
    private readonly EstimadorVentana _estimador = new(Senales.Analisis(), Senales.Fps);

    private ResultadoVentana Evaluar(params double[][] series) =>
        _estimador.Evaluar(Senales.T0, Senales.T0.AddSeconds(10), series.Select((s, i) => (i + 1, s)).ToList());

    [Theory]
    [InlineData(2.78)]
    [InlineData(2.5)]
    [InlineData(3.2)]
    public void CuatroCarrilesEnMarcha_DaLaFrecuenciaDeCorte(double hz)
    {
        var r = Evaluar(Senales.Marcha(hz, fase: 0), Senales.Marcha(hz, fase: 0.3, semilla: 5),
            Senales.Marcha(hz, fase: 0.6, semilla: 6), Senales.Marcha(hz, fase: 0.1, semilla: 7));

        Assert.Equal(EstadoCarril.MARCHA, r.EstadoLinea);
        Assert.InRange(r.FrecuenciaComunHz, hz - 0.05, hz + 0.05);
        Assert.All(r.Carriles, c => Assert.Equal(r.FrecuenciaComunHz, c.RollosPorSegundo));
        Assert.InRange(r.Rollos, 4 * hz * 10 - 3, 4 * hz * 10 + 3);
    }

    [Fact]
    public void TodosQuietos_EsParadaSinRollos()
    {
        var r = Evaluar(Senales.Quieto(), Senales.Quieto(semilla: 3), Senales.Quieto(semilla: 4), Senales.Quieto(semilla: 5));

        Assert.Equal(EstadoCarril.PARADA, r.EstadoLinea);
        Assert.Equal(0, r.Rollos);
    }

    [Fact]
    public void UnCarrilQuieto_EsAcumulacionYEseCarrilNoCuenta()
    {
        var r = Evaluar(Senales.Quieto(), Senales.Marcha(2.78, semilla: 5), Senales.Marcha(2.78, fase: 0.5, semilla: 6), Senales.Marcha(2.78, semilla: 7));

        Assert.Equal(EstadoCarril.ACUMULACION, r.EstadoLinea);
        Assert.Equal(EstadoCarril.PARADA, r.Carriles[0].Estado);
        Assert.Equal(0, r.Carriles[0].RollosPorSegundo);
        Assert.All(r.Carriles.Skip(1), c => Assert.Equal(EstadoCarril.MARCHA, c.Estado));
    }

    [Fact]
    public void CarrilQueSeMueveSinFrecuenciaDeCorte_EsAcumulacion()
    {
        var r = Evaluar(Senales.Irregular(), Senales.Marcha(2.78, semilla: 5), Senales.Marcha(2.78, semilla: 6), Senales.Marcha(2.78, semilla: 7));

        Assert.Equal(EstadoCarril.ACUMULACION, r.Carriles[0].Estado);
        Assert.Equal(0, r.Carriles[0].RollosPorSegundo);
    }

    [Fact]
    public void MarchaMuyLenta_EsAcumulacionPeroCuentaLosRollos()
    {
        var r = Evaluar(Senales.Marcha(1.2), Senales.Marcha(1.2, semilla: 5), Senales.Marcha(1.2, semilla: 6), Senales.Marcha(1.2, semilla: 7));

        Assert.Equal(EstadoCarril.ACUMULACION, r.EstadoLinea);
        Assert.All(r.Carriles, c => Assert.InRange(c.RollosPorSegundo, 1.15, 1.25));
    }

    [Fact]
    public void SerieDeOtroLargo_Falla()
    {
        Assert.Throws<ArgumentException>(() => Evaluar(Senales.Marcha(2.78, n: 50)));
    }
}

public class DetectorParadasTests
{
    private static DetectorParadas Nuevo(int confirmar = 2) =>
        new(new OpcionesAnalisis { VentanasParaConfirmar = confirmar, SegundosMinimosParada = 20 });

    [Fact]
    public void ParadaDe60s_SeInformaConInicioYFinExactos()
    {
        var d = Nuevo();
        var paradas = new List<ParadaDetectada>();
        var i = 0;
        for (; i < 5; i++) Agregar(d, Senales.Marcha4(i), paradas);
        for (; i < 11; i++) Agregar(d, Senales.Parada4(i), paradas);
        for (; i < 16; i++) Agregar(d, Senales.Marcha4(i), paradas);

        var p = Assert.Single(paradas);
        Assert.Equal(TipoParada.PARADA, p.Tipo);
        Assert.Equal(Senales.T0.AddSeconds(50), p.Inicio);
        Assert.Equal(Senales.T0.AddSeconds(110), p.Fin);
        Assert.Equal(60, p.DuracionSegundos);
        Assert.Equal([1, 2, 3, 4], p.Carriles);
        Assert.False(p.InicioIncierto);
        Assert.False(p.FinIncierto);
    }

    [Fact]
    public void VentanaSueltaDeUnCarrilRuidoso_NoAbreParada()
    {
        var d = Nuevo();
        var paradas = new List<ParadaDetectada>();
        for (var i = 0; i < 20; i++)
        {
            var v = i % 5 == 3
                ? Senales.Ventana(i, EstadoCarril.ACUMULACION, EstadoCarril.MARCHA, EstadoCarril.MARCHA, EstadoCarril.MARCHA)
                : Senales.Marcha4(i);
            Agregar(d, v, paradas);
        }
        Assert.Empty(paradas);
        Assert.False(d.Detenida);
    }

    [Fact]
    public void ArranqueDe10s_NoCortaLaParada()
    {
        var d = Nuevo();
        var paradas = new List<ParadaDetectada>();
        var secuencia = new[] { true, true, false, false, false, true, false, false, false, false, true, true, true };
        // true = marcha
        for (var i = 0; i < secuencia.Length; i++)
            Agregar(d, secuencia[i] ? Senales.Marcha4(i) : Senales.Parada4(i), paradas);

        var p = Assert.Single(paradas);
        Assert.Equal(Senales.T0.AddSeconds(20), p.Inicio);
        Assert.Equal(Senales.T0.AddSeconds(100), p.Fin);
    }

    [Fact]
    public void SoloAlgunosCarriles_EsAcumulacionConEsosCarriles()
    {
        var d = Nuevo();
        var paradas = new List<ParadaDetectada>();
        var i = 0;
        for (; i < 3; i++) Agregar(d, Senales.Marcha4(i), paradas);
        for (; i < 7; i++)
            Agregar(d, Senales.Ventana(i, EstadoCarril.MARCHA, EstadoCarril.ACUMULACION, EstadoCarril.PARADA, EstadoCarril.MARCHA), paradas);
        for (; i < 10; i++) Agregar(d, Senales.Marcha4(i), paradas);

        var p = Assert.Single(paradas);
        Assert.Equal(TipoParada.ACUMULACION, p.Tipo);
        Assert.Equal([2, 3], p.Carriles);
    }

    [Fact]
    public void CorteDeSenal_CierraLaParadaConFinIncierto()
    {
        var d = Nuevo();
        var paradas = new List<ParadaDetectada>();
        var i = 0;
        for (; i < 3; i++) Agregar(d, Senales.Marcha4(i), paradas);
        for (; i < 6; i++) Agregar(d, Senales.Parada4(i), paradas);

        var p = d.CortarSenal();

        Assert.NotNull(p);
        Assert.True(p!.FinIncierto);
        Assert.Equal(Senales.T0.AddSeconds(30), p.Inicio);
        Assert.Equal(Senales.T0.AddSeconds(60), p.Fin);
        Assert.Null(d.Detenida);
    }

    [Fact]
    public void ArrancaConLaLineaParada_InicioIncierto()
    {
        var d = Nuevo();
        var paradas = new List<ParadaDetectada>();
        var i = 0;
        for (; i < 4; i++) Agregar(d, Senales.Parada4(i), paradas);
        for (; i < 7; i++) Agregar(d, Senales.Marcha4(i), paradas);

        var p = Assert.Single(paradas);
        Assert.True(p.InicioIncierto);
        Assert.Equal(Senales.T0, p.Inicio);
    }

    [Fact]
    public void ParadaMasCortaQueElMinimo_NoSeInforma()
    {
        var d = new DetectorParadas(new OpcionesAnalisis { VentanasParaConfirmar = 1, SegundosMinimosParada = 30 });
        var paradas = new List<ParadaDetectada>();
        Agregar(d, Senales.Marcha4(0), paradas);
        Agregar(d, Senales.Parada4(1), paradas);
        Agregar(d, Senales.Parada4(2), paradas);
        Agregar(d, Senales.Marcha4(3), paradas);
        Assert.Empty(paradas);
    }

    private static void Agregar(DetectorParadas d, ResultadoVentana v, List<ParadaDetectada> paradas)
    {
        if (d.Procesar(v) is { } p) paradas.Add(p);
    }
}

public class AcumuladorMinutosTests
{
    [Fact]
    public void SeisVentanasAlineadas_UnMinutoPorCarril()
    {
        var a = new AcumuladorMinutos([1, 2, 3, 4]);
        var salida = new List<ConteoMinuto>();
        for (var i = 0; i < 7; i++) salida.AddRange(a.Agregar(Senales.Marcha4(i)));

        Assert.Equal(4, salida.Count);
        Assert.All(salida, m =>
        {
            Assert.Equal(Senales.T0, m.Minuto);
            Assert.Equal(166.8, m.Rollos, 1);
            Assert.Equal(60, m.SegundosMarcha);
            Assert.Equal(EstadoCarril.MARCHA, m.Estado);
        });
    }

    [Fact]
    public void VentanaQueCruzaElMinuto_SeRepartePorTiempo()
    {
        var a = new AcumuladorMinutos([1]);
        var inicio = Senales.T0.AddSeconds(55);
        var v = new ResultadoVentana(inicio, inicio.AddSeconds(10), 2, [new ResultadoCarril(1, EstadoCarril.MARCHA, 2, 30, 1)]);

        var salida = a.Agregar(v).Concat(a.Vaciar()).ToList();

        Assert.Equal(2, salida.Count);
        Assert.Equal(10, salida[0].Rollos, 1);
        Assert.Equal(5, salida[0].SegundosMarcha);
        Assert.Equal(55, salida[0].SegundosSinSenal);
        Assert.Equal(10, salida[1].Rollos, 1);
        Assert.Equal(Senales.T0.AddMinutes(1), salida[1].Minuto);
    }

    [Fact]
    public void MinutoIncompleto_ElRestoEsSinSenalYSumaSesenta()
    {
        var a = new AcumuladorMinutos([1, 2, 3, 4]);
        a.Agregar(Senales.Marcha4(0));
        a.Agregar(Senales.Parada4(1));
        var salida = a.Vaciar();

        Assert.All(salida, m =>
        {
            Assert.Equal(10, m.SegundosMarcha);
            Assert.Equal(10, m.SegundosParada);
            Assert.Equal(40, m.SegundosSinSenal);
            Assert.Equal(60, m.SegundosMarcha + m.SegundosParada + m.SegundosAcumulacion + m.SegundosSinSenal);
            Assert.Equal(EstadoCarril.SIN_SENAL, m.Estado);
        });
    }
}

public class ProcesadorConteoTests
{
    private sealed class Captura : ISalidaConteo
    {
        public List<ResultadoVentana> Ventanas { get; } = [];
        public List<ConteoMinuto> Minutos { get; } = [];
        public List<ParadaDetectada> Paradas { get; } = [];
        void ISalidaConteo.Ventana(ResultadoVentana v) => Ventanas.Add(v);
        void ISalidaConteo.Minutos(IReadOnlyList<ConteoMinuto> m) => Minutos.AddRange(m);
        void ISalidaConteo.Parada(ParadaDetectada p) => Paradas.Add(p);
    }

    private static OpcionesConteo Opciones() => new()
    {
        Fuente = new OpcionesFuente { AnchoDecodificado = 64, AltoDecodificado = 48, Fps = 10 },
        Zonas = new OpcionesZonas
        {
            AnchoImagen = 2560, AltoImagen = 1920,
            Carriles = [new ZonaCarril { Carril = 1, X = 640, Y = 960, Lado = 160 }, new ZonaCarril { Carril = 2, X = 1920, Y = 960, Lado = 160 }]
        }
    };

    [Fact]
    public void ZonasSeEscalanALaImagenDecodificada()
    {
        var z = ProcesadorConteo.ZonasDecodificadas(Opciones().Zonas, 64, 48);
        Assert.Equal((1, 14, 22, 4), z[0]);
        Assert.Equal((2, 46, 22, 4), z[1]);
    }

    [Fact]
    public void CuadrosSinteticos_CuentaParaYVuelve()
    {
        var captura = new Captura();
        var p = new ProcesadorConteo(Opciones(), captura);
        var cuadro = new byte[64 * 48];
        var hz = 2.78;
        // 60 s de marcha, 40 s parada (los dos carriles), 60 s de marcha.
        for (var n = 0; n < 1600; n++)
        {
            var t = n / 10.0;
            byte brillo = t is >= 60 and < 100 ? (byte)150 : ((t * hz) % 1.0 < 0.45 ? (byte)170 : (byte)90);
            Array.Fill(cuadro, brillo);
            p.Cuadro(cuadro, Senales.T0.AddSeconds(t));
        }
        p.CortarSenal(final: true);

        Assert.Equal(16, captura.Ventanas.Count);
        var parada = Assert.Single(captura.Paradas);
        Assert.Equal(Senales.T0.AddSeconds(60), parada.Inicio);
        Assert.Equal(Senales.T0.AddSeconds(100), parada.Fin);
        Assert.Equal(TipoParada.PARADA, parada.Tipo);

        var primerMinuto = captura.Minutos.Where(m => m.Minuto == Senales.T0).ToList();
        Assert.Equal(2, primerMinuto.Count);
        Assert.All(primerMinuto, m => Assert.InRange(m.Rollos, 160, 174));
        // 160 s = minutos 0, 1 y 2 (este último con 20 s de datos), por 2 carriles.
        Assert.Equal(6, captura.Minutos.Count);
        var segundo = captura.Minutos.First(m => m.Minuto == Senales.T0.AddMinutes(1));
        Assert.Equal(40, segundo.SegundosParada);
        Assert.Equal(20, segundo.SegundosMarcha);
    }

    [Fact]
    public void SinCarriles_NoArranca()
    {
        var o = Opciones();
        o.Zonas.Carriles.Clear();
        Assert.Throws<InvalidOperationException>(() => new ProcesadorConteo(o, new Captura()));
    }
}

public class ContratoYCredencialesTests
{
    [Fact]
    public void ElLoteDelServicioSeLeeConLosDtoDeLaApi()
    {
        var parada = new ParadaDetectada(Senales.T0, Senales.T0.AddSeconds(60), TipoParada.PARADA, [1, 2, 3, 4], false, true);
        var lote = new LoteConteo
        {
            PuntoControl = "T2-CAM09",
            Minutos = [MinutoLote.Desde(new ConteoMinuto(Senales.T0, 2, 166.8, 50, 10, 0, 0))],
            Paradas = [ParadaLote.Desde("T2-CAM09", parada)]
        };
        var json = JsonSerializer.Serialize(lote, LoteConteo.Json);

        var api = JsonSerializer.Deserialize<IngestaConteoCamaraRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal("T2-CAM09", api.PuntoControl);
        var m = Assert.Single(api.Minutos);
        Assert.Equal(Senales.T0, m.Minuto);
        Assert.Equal(166.8m, m.Rollos);
        Assert.Equal("MARCHA", m.Estado);
        var p = Assert.Single(api.Paradas);
        Assert.Equal("T2-CAM09-20261028T131500Z", p.IdentificadorExterno);
        Assert.Equal("PARADA", p.Tipo);
        Assert.True(p.FinIncierto);
        Assert.Equal([1, 2, 3, 4], p.Carriles);
    }

    [Fact]
    public void Credenciales_NuncaMuestranLaClave()
    {
        var archivo = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(archivo, ["# NVR", "NVR=http://10.0.0.1/", "NVR_USER=lector", "NVR_PASS=\"cl@ve:secreta/1\""]);
            var c = CredencialesNvr.Leer(archivo);

            Assert.Equal("10.0.0.1", c.Host);
            Assert.DoesNotContain("secreta", c.ToString());
            var url = c.UrlRtsp(554, "/Streaming/Channels/901");
            Assert.Equal("rtsp://lector:cl%40ve%3Asecreta%2F1@10.0.0.1:554/Streaming/Channels/901", url);
            Assert.DoesNotContain("secreta", c.Ocultar("error abriendo " + url + " con cl@ve:secreta/1"));
        }
        finally
        {
            File.Delete(archivo);
        }
    }

    [Fact]
    public void Credenciales_FaltaUnaClave_Falla()
    {
        var archivo = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(archivo, ["NVR=10.0.0.1", "NVR_USER=lector"]);
            Assert.Throws<InvalidOperationException>(() => CredencialesNvr.Leer(archivo));
        }
        finally
        {
            File.Delete(archivo);
        }
    }

    [Fact]
    public void ArgumentosDeFfmpeg_ReducenYEntreganGrisCrudo()
    {
        var args = FuenteFfmpeg.Argumentos(new OpcionesFuente(), "rtsp://x", esRtsp: true);
        var texto = string.Join(" ", args);
        Assert.Contains("-rtsp_transport tcp", texto);
        Assert.Contains("fps=10,scale=640:480", texto);
        Assert.Contains("-pix_fmt gray -f rawvideo pipe:1", texto);
        Assert.Contains("-an", args);
    }

    [Fact]
    public void RelojCuadros_EsParejoYReanclaSiSeDesfasa()
    {
        var ahora = Senales.T0;
        var reloj = new RelojCuadros(10, TimeZoneInfo.CreateCustomTimeZone("P", TimeSpan.FromHours(-3), "P", "P"), () => ahora);

        var (h0, r0) = reloj.Siguiente();
        ahora = ahora.AddMilliseconds(130);    // llegó un poco tarde: igual se fecha a 100 ms
        var (h1, _) = reloj.Siguiente();
        ahora = ahora.AddSeconds(30);          // hueco de 30 s
        var (h2, r2) = reloj.Siguiente();

        Assert.False(r0);
        Assert.Equal(h0.AddMilliseconds(100), h1);
        Assert.True(r2);
        Assert.Equal(ahora, h2);
    }
}
