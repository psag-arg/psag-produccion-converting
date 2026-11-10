using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.ConteoCamara;
using PSAG.Mantenimiento.ConteoCamara.Analisis;
using PSAG.Mantenimiento.ConteoCamara.Salida;

namespace PSAG.Mantenimiento.ConteoCamara.Tests;

/// <summary>
/// Cámara 10 sintética: cuadros de 32x18 a 5 fps. Rodillo = cuarto izquierdo (textura que cambia en cada cuadro
/// si gira), mesa = mitad derecha (una manga = 3 cuadros brillantes). Ruido de ±3 en todo el cuadro, como una
/// imagen viva (con el rodillo y la mesa quietos la diferencia máxima sigue siendo mayor que 4).
/// </summary>
internal sealed class Camara10
{
    public const int Ancho = 32, Alto = 18, Fps = 5;
    public static readonly DateTimeOffset T0 = new(2026, 11, 3, 2, 25, 0, TimeSpan.FromHours(-3));

    private readonly Random _r = new(7);
    private int _n;

    public static OpcionesConteo Opciones()
    {
        var o = new OpcionesConteo { PuntoControl = "T2-CAM10", Metodo = MetodosConteo.Mangas };
        o.Fuente.AnchoDecodificado = Ancho;
        o.Fuente.AltoDecodificado = Alto;
        o.Fuente.Fps = Fps;
        o.Mangas = new OpcionesMangas
        {
            AnchoImagen = 2560, AltoImagen = 1440,
            Mesa = new Rectangulo { X = 1280, Y = 0, Ancho = 1280, Alto = 1440 },
            Rodillo = new Rectangulo { X = 0, Y = 0, Ancho = 640, Alto = 1440 }
        };
        return o;
    }

    /// <summary>
    /// Un cuadro. <paramref name="manga"/>: la mesa está tapada por una manga. <paramref name="movimientoRodillo"/>:
    /// rodillo girando con ese movimiento medio aproximado (damero que se invierte en cada cuadro); null = textura
    /// que cambia mucho (giro normal, ~60).
    /// </summary>
    public byte[] Cuadro(bool rodilloGira, bool manga, double? movimientoRodillo = null)
    {
        var f = new byte[Ancho * Alto];
        for (var y = 0; y < Alto; y++)
            for (var x = 0; x < Ancho; x++)
            {
                int v;
                if (x < 8 && rodilloGira && movimientoRodillo is { } mov)
                    v = 90 + (int)Math.Round(((x + y + _n) % 2 == 0 ? 1 : -1) * mov / 2);
                else if (x < 8) v = rodilloGira ? 60 + (x * 7 + y * 13 + _n * 29) % 120 : 90;
                else if (x >= 16) v = manga ? 200 : 50;
                else v = 120;
                f[y * Ancho + x] = (byte)Math.Clamp(v + _r.Next(-3, 4), 0, 255);
            }
        _n++;
        return f;
    }

    /// <summary>
    /// Pasa <paramref name="segundos"/> de video al analizador desde <paramref name="desde"/> segundos. Con
    /// <paramref name="periodo"/> &gt; 0 pasa una manga cada ese tiempo (3 cuadros); 0 = no pasan mangas.
    /// </summary>
    public double Pasar(IAnalizadorCuadros a, double desde, double segundos, double periodo, bool rodilloGira = true,
        double? movimientoRodillo = null)
    {
        var cuadros = (int)Math.Round(segundos * Fps);
        for (var i = 0; i < cuadros; i++)
        {
            var t = desde + (double)i / Fps;
            var manga = periodo > 0 && t % periodo < 0.6;
            a.Cuadro(Cuadro(rodilloGira, manga, movimientoRodillo), T0.AddSeconds(t));
        }
        return desde + segundos;
    }
}

internal sealed class Captura : ISalidaConteo
{
    public List<ResultadoVentana> Ventanas { get; } = [];
    public List<ConteoMinuto> Minutos_ { get; } = [];
    public List<ParadaDetectada> Paradas { get; } = [];
    public List<ParadaEnCurso> EnCurso { get; } = [];
    public List<AlertaVideo> Alertas { get; } = [];

    public void Ventana(ResultadoVentana ventana) => Ventanas.Add(ventana);
    public void Minutos(IReadOnlyList<ConteoMinuto> minutos) => Minutos_.AddRange(minutos);
    public void Parada(ParadaDetectada parada) => Paradas.Add(parada);
    public void ParadaEnCurso(ParadaEnCurso parada) => EnCurso.Add(parada);
    public void Alerta(AlertaVideo alerta) => Alertas.Add(alerta);
}

public class ProcesadorMangasTests
{
    private readonly Camara10 _cam = new();
    private readonly Captura _salida = new();
    private readonly VigilanteVideo _cadena;

    public ProcesadorMangasTests() => _cadena = Analizadores.Crear(Camara10.Opciones(), _salida);

    [Fact]
    public void EnMarcha_CuentaUnaMangaCadaPasoYNoHayParadas()
    {
        _cam.Pasar(_cadena, 0, 120, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        var mangas = _salida.Minutos_.Sum(m => m.Rollos);
        Assert.InRange(mangas, 43, 46);                       // 120 / 2,7 = 44,4
        Assert.Empty(_salida.Paradas);
        Assert.Empty(_salida.Alertas);
        Assert.All(_salida.Ventanas, v => Assert.Equal(EstadoCarril.MARCHA, v.EstadoLinea));
        Assert.All(_salida.Minutos_, m => Assert.Equal(ProcesadorMangas.Carril, m.Carril));
    }

    [Fact]
    public void SinMangasConRodilloGirando_EsParadaSinMangas_YSeInformaEnCursoAntesDelArranque()
    {
        var t = _cam.Pasar(_cadena, 0, 60, periodo: 2.7);
        t = _cam.Pasar(_cadena, t, 6 * 60, periodo: 0);      // 6 min sin mangas, el rodillo sigue
        _cam.Pasar(_cadena, t, 60, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        var parada = Assert.Single(_salida.Paradas);
        Assert.Equal(TipoParada.SIN_MANGAS, parada.Tipo);
        Assert.InRange(parada.DuracionSegundos, 340, 380);
        Assert.InRange((parada.Inicio - Camara10.T0).TotalSeconds, 55, 75);
        // Mientras seguía parada se informó como "en curso" pasando los 5 minutos: la API crea la detención sin esperar el arranque.
        Assert.Contains(_salida.EnCurso, p => p.Inicio == parada.Inicio && p.Hasta - p.Inicio >= TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void RodilloQuieto_EsParada()
    {
        var t = _cam.Pasar(_cadena, 0, 30, periodo: 2.7);
        t = _cam.Pasar(_cadena, t, 120, periodo: 0, rodilloGira: false);
        _cam.Pasar(_cadena, t, 30, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        var parada = Assert.Single(_salida.Paradas);
        Assert.Equal(TipoParada.PARADA, parada.Tipo);
        Assert.Empty(_salida.Alertas);  // máquina parada no es imagen congelada: el ruido y el reloj siguen cambiando
    }

    [Fact]
    public void Empalme_MangasMasEspaciadasConRodilloGirando_NoEsParada()
    {
        var t = _cam.Pasar(_cadena, 0, 60, periodo: 2.7);
        t = _cam.Pasar(_cadena, t, 120, periodo: 7);          // empalme: baja la velocidad, siguen saliendo mangas
        _cam.Pasar(_cadena, t, 60, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        Assert.Empty(_salida.Paradas);
    }

    /// <summary>
    /// Ferretti: al cambiar bobina la máquina baja la velocidad pero no para. Mangas cada 15-25 s y el rodillo
    /// girando a la mitad (movimiento ~10; normal ~20, umbral de giro 6) durante 40-60 s: nunca PARADA ni
    /// detención; a lo sumo un microcorte SIN_MANGAS (dos ventanas seguidas sin mangas) muy por debajo de 5 min.
    /// </summary>
    [Theory]
    [InlineData(15, 40)]
    [InlineData(15, 60)]
    [InlineData(20, 60)]
    [InlineData(25, 40)]
    [InlineData(25, 60)]
    public void EmpalmeDeBobina_RodilloLentoYMangasEspaciadas_NoEsParadaNiDetencion(double periodo, double segundos)
    {
        var t = _cam.Pasar(_cadena, 0, 60, periodo: 2.7);
        t = _cam.Pasar(_cadena, t, segundos, periodo, movimientoRodillo: 10);
        _cam.Pasar(_cadena, t, 60, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        Assert.DoesNotContain(_salida.Ventanas, v => v.EstadoLinea == EstadoCarril.PARADA);
        Assert.All(_salida.Ventanas.Where(v => v.Inicio >= Camara10.T0.AddSeconds(60) && v.Fin <= Camara10.T0.AddSeconds(60 + segundos)),
            v => Assert.InRange(v.Carriles[0].ProporcionPico, 8, 12));      // movimiento medio del rodillo lento
        // Con mangas cada 20 s o más, ventanas sin mangas alternan con ventanas con una: el SIN_MANGAS puede
        // durar todo el empalme (no se confirma el arranque hasta dos ventanas con mangas), nunca más.
        Assert.All(_salida.Paradas, p =>
        {
            Assert.Equal(TipoParada.SIN_MANGAS, p.Tipo);
            Assert.True(p.DuracionSegundos <= segundos, $"microcorte de {p.DuracionSegundos} s");
        });
        Assert.DoesNotContain(_salida.EnCurso, p => p.Tipo == TipoParada.PARADA || p.Hasta - p.Inicio >= TimeSpan.FromMinutes(5));
    }

    /// <summary>
    /// Límite del empalme: con el rodillo por debajo de UmbralRodilloGira (6) en la mitad de los cuadros de dos
    /// ventanas seguidas sí es PARADA. Con ~20 girando normal, eso es ir a menos del 30 % de la velocidad.
    /// </summary>
    [Fact]
    public void EmpalmeConRodilloDebajoDelUmbral_SiEsParada()
    {
        var t = _cam.Pasar(_cadena, 0, 60, periodo: 2.7);
        t = _cam.Pasar(_cadena, t, 60, periodo: 20, movimientoRodillo: 4);
        _cam.Pasar(_cadena, t, 60, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        Assert.Contains(_salida.Paradas, p => p.Tipo == TipoParada.PARADA);
    }

    [Fact]
    public void ImagenCongeladaConLaMaquinaParada_NoParteLaParada()
    {
        var t = _cam.Pasar(_cadena, 0, 60, periodo: 2.7);
        t = _cam.Pasar(_cadena, t, 120, periodo: 0, rodilloGira: false);
        var congelado = _cam.Cuadro(false, false);
        for (var i = 0; i < 40 * Camara10.Fps; i++)            // 40 s congelada, la máquina sigue parada
            _cadena.Cuadro(congelado, Camara10.T0.AddSeconds(t + (double)i / Camara10.Fps));
        t += 40;
        t = _cam.Pasar(_cadena, t, 200, periodo: 0, rodilloGira: false);
        _cam.Pasar(_cadena, t, 60, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        var parada = Assert.Single(_salida.Paradas);
        Assert.Equal(TipoParada.PARADA, parada.Tipo);
        Assert.False(parada.FinIncierto);
        Assert.InRange((parada.Inicio - Camara10.T0).TotalSeconds, 59, 71);
        Assert.InRange(parada.DuracionSegundos, 350, 370);    // 120 + 40 + 200 s
        Assert.All(_salida.EnCurso, p => Assert.Equal(parada.Inicio, p.Inicio));   // un solo identificador
        Assert.Contains(_salida.EnCurso, p => p.Hasta - p.Inicio >= TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void Microcorte_QuedaComoParadaCorta()
    {
        var t = _cam.Pasar(_cadena, 0, 60, periodo: 2.7);
        t = _cam.Pasar(_cadena, t, 40, periodo: 0);
        _cam.Pasar(_cadena, t, 60, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        var parada = Assert.Single(_salida.Paradas);
        Assert.InRange(parada.DuracionSegundos, 20, 60);      // al log; la API no crea detención (< 5 min)
        Assert.DoesNotContain(_salida.EnCurso, p => p.Hasta - p.Inicio >= TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ImagenCongelada_DaAlertaYNoParada()
    {
        var t = _cam.Pasar(_cadena, 0, 60, periodo: 2.7);
        var congelado = _cam.Cuadro(true, false);
        for (var i = 0; i < 8 * 60 * Camara10.Fps; i++)       // 8 minutos de cuadros idénticos
            _cadena.Cuadro(congelado, Camara10.T0.AddSeconds(t + (double)i / Camara10.Fps));
        t += 8 * 60;
        _cam.Pasar(_cadena, t, 60, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        Assert.Empty(_salida.Paradas);
        Assert.DoesNotContain(_salida.EnCurso, p => p.Hasta - p.Inicio >= TimeSpan.FromMinutes(1));
        var abierta = Assert.Single(_salida.Alertas, a => a.Fin == null);
        var cerrada = Assert.Single(_salida.Alertas, a => a.Fin != null);
        Assert.Equal(TipoAlertaVideo.CONGELADA, abierta.Tipo);
        Assert.InRange((abierta.Inicio - Camara10.T0).TotalSeconds, 59, 62);
        Assert.Equal(abierta.Inicio, cerrada.Inicio);
        Assert.InRange((cerrada.Fin!.Value - Camara10.T0).TotalSeconds, 539, 542);
    }

    [Fact]
    public void ImagenCongelada_UnCuadroClaveDistintoNoCierraLaAlerta()
    {
        var t = _cam.Pasar(_cadena, 0, 30, periodo: 2.7);
        var congelado = _cam.Cuadro(true, false);
        var apenasDistinto = (byte[])congelado.Clone();
        apenasDistinto[0] = (byte)(apenasDistinto[0] + 20);   // como un cuadro clave del códec
        for (var i = 0; i < 120 * Camara10.Fps; i++)
            _cadena.Cuadro(i == 300 ? apenasDistinto : congelado, Camara10.T0.AddSeconds(t + (double)i / Camara10.Fps));
        _cam.Pasar(_cadena, t + 120, 30, periodo: 2.7);
        _cadena.CortarSenal(final: true);

        Assert.Single(_salida.Alertas, a => a.Fin == null);
        Assert.Single(_salida.Alertas, a => a.Fin != null);
        Assert.Empty(_salida.Paradas);
    }

    [Fact]
    public void Vigilante_NoAtrasaElConteo_EntregaTodoAlCortar()
    {
        _cam.Pasar(_cadena, 0, 30, periodo: 2.7);
        _cadena.CortarSenal(final: true);
        Assert.Equal(3, _salida.Ventanas.Count);
    }

    [Fact]
    public void Zonas_SeEscalanALaResolucionDecodificada()
    {
        var o = new OpcionesMangas { AnchoImagen = 2560, AltoImagen = 1440 };
        var z = ProcesadorMangas.Escalar(new Rectangulo { X = 1120, Y = 160, Ancho = 960, Alto = 560 }, o, 320, 180)!.Value;
        Assert.Equal((140, 20, 260, 90), z);
        Assert.Null(ProcesadorMangas.Escalar(new Rectangulo { X = 3000, Y = 0, Ancho = 10, Alto = 10 }, o, 320, 180));
    }
}

public class AlertaSinVideoTests
{
    [Fact]
    public void SeAbrePasadoElLimiteYSeCierraConElPrimerCuadro()
    {
        var zona = TimeZoneInfo.CreateCustomTimeZone("P", TimeSpan.FromHours(-3), "P", "P");
        var ahora = Camara10.T0;
        var alerta = new AlertaSinVideo(60, zona, () => ahora);
        var ultimo = Camara10.T0;

        ahora = ahora.AddSeconds(30);
        Assert.Null(alerta.Revisar(ultimo));
        ahora = ahora.AddSeconds(40);
        var abierta = alerta.Revisar(ultimo);
        Assert.NotNull(abierta);
        Assert.Equal(TipoAlertaVideo.SIN_VIDEO, abierta!.Tipo);
        Assert.Equal(ultimo, abierta.Inicio);
        Assert.Null(alerta.Revisar(ultimo));                 // no se repite

        var cerrada = alerta.Volvio(ahora);
        Assert.Equal(ahora, cerrada!.Fin);
        Assert.Null(alerta.Volvio(ahora));
    }
}

public class ConfiguracionCamarasTests
{
    private static IConfiguration Config(string json) =>
        new ConfigurationBuilder().AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))).Build();

    [Fact]
    public void SinLista_EsUnaSolaCamaraComoAntes()
    {
        var camaras = OpcionesConteo.CargarCamaras(Config("""
            { "ConteoCamara": { "PuntoControl": "T2-CAM09", "Fuente": { "RutaRtsp": "/Streaming/Channels/901" },
              "Zonas": { "Carriles": [ { "Carril": 1, "X": 1092, "Y": 984 } ] } } }
            """));
        var c = Assert.Single(camaras);
        Assert.Equal("T2-CAM09", c.PuntoControl);
        Assert.False(c.EsMangas);
        Assert.Equal("pendientes", c.Api.CarpetaPendientes);
        Assert.Single(c.Zonas.Carriles);
    }

    [Fact]
    public void ConLista_CadaCamaraHeredaLoComunYPisaLoSuyo()
    {
        var camaras = OpcionesConteo.CargarCamaras(Config("""
            { "ConteoCamara": {
                "PuntoControl": "T2-CAM09",
                "Fuente": { "RutaRtsp": "/Streaming/Channels/901", "ArchivoCredenciales": "C:/s/nvr.env", "Fps": 10 },
                "Zonas": { "Carriles": [ { "Carril": 1, "X": 1092, "Y": 984 } ] },
                "Api": { "UrlBase": "http://api", "CarpetaPendientes": "pendientes" },
                "Camaras": [
                  { "PuntoControl": "T2-CAM09", "Metodo": "CARRILES" },
                  { "PuntoControl": "T2-CAM10", "Metodo": "MANGAS", "Activa": false,
                    "Fuente": { "RutaRtsp": "/Streaming/Channels/1001", "Fps": 5, "AnchoDecodificado": 320, "AltoDecodificado": 180 } }
                ] } }
            """));

        Assert.Equal(2, camaras.Count);
        var c09 = camaras[0];
        var c10 = camaras[1];
        Assert.Equal("/Streaming/Channels/901", c09.Fuente.RutaRtsp);
        Assert.Single(c09.Zonas.Carriles);                                   // sigue leyendo zonas-camara.json
        Assert.Equal("pendientes", c09.Api.CarpetaPendientes);               // no quedan lotes huérfanos
        Assert.True(c09.Activa);
        Assert.True(c10.EsMangas);
        Assert.False(c10.Activa);
        Assert.Equal("/Streaming/Channels/1001", c10.Fuente.RutaRtsp);
        Assert.Equal(5, c10.Fuente.Fps);
        Assert.Equal("C:/s/nvr.env", c10.Fuente.ArchivoCredenciales);       // heredado
        Assert.Equal("http://api", c10.Api.UrlBase);
        Assert.Equal(Path.Combine("pendientes", "T2-CAM10"), c10.Api.CarpetaPendientes);
    }

    [Fact]
    public void ElAppsettingsDelRepoTraeLa09ActivaYLa10Apagada()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var camaras = OpcionesConteo.CargarCamaras(new ConfigurationBuilder().AddJsonFile(ruta).Build());
        Assert.Collection(camaras,
            c => { Assert.Equal("T2-CAM09", c.PuntoControl); Assert.True(c.Activa); Assert.False(c.EsMangas); },
            c => { Assert.Equal("T2-CAM10", c.PuntoControl); Assert.False(c.Activa); Assert.True(c.EsMangas); });
    }

    [Fact]
    public void ElLoteConParadasEnCursoYAlertasSeLeeConLosDtoDeLaApi()
    {
        var lote = new LoteConteo
        {
            PuntoControl = "T2-CAM10",
            ParadasEnCurso = [ParadaEnCursoLote.Desde("T2-CAM10", new ParadaEnCurso(Camara10.T0, Camara10.T0.AddMinutes(6), TipoParada.SIN_MANGAS, false))],
            Alertas = [AlertaLote.Desde(new AlertaVideo(TipoAlertaVideo.CONGELADA, Camara10.T0, null, "x"))]
        };
        var json = JsonSerializer.Serialize(lote, LoteConteo.Json);
        var api = JsonSerializer.Deserialize<IngestaConteoCamaraRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        var p = Assert.Single(api.ParadasEnCurso);
        Assert.Equal("T2-CAM10-20261103T052500Z", p.IdentificadorExterno);
        Assert.Equal("SIN_MANGAS", p.Tipo);
        Assert.Equal(TimeSpan.FromMinutes(6), p.Hasta - p.Inicio);
        var a = Assert.Single(api.Alertas);
        Assert.Equal("CONGELADA", a.Tipo);
        Assert.Null(a.Fin);
    }
}
