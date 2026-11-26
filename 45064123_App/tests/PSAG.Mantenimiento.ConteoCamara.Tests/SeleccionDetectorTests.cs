using Microsoft.Extensions.Configuration;
using PSAG.Mantenimiento.ConteoCamara;
using PSAG.Mantenimiento.ConteoCamara.Analisis;

namespace PSAG.Mantenimiento.ConteoCamara.Tests;

/// <summary>Los casos de <see cref="ProcesadorMangasTests"/> con el detector cuadro a cuadro de T-784.</summary>
public class ProcesadorMangasDetectorMangasTests : ProcesadorMangasTests
{
    protected override string Detector => DetectoresParadas.Mangas;
}

/// <summary>
/// T-804: la cámara de mangas elige el detector de paradas por parámetro (Mangas:DetectorParadas). VENTANAS (reglas
/// A/E/F, validado con video del 13 y 14/11) es el default; MANGAS (T-784) se activa para probar. La cámara 8 sólo
/// cuenta con cualquiera de los dos.
/// </summary>
public class SeleccionDetectorTests
{
    private readonly Camara10 _cam = new();

    private static IConfiguration Config(string json) =>
        new ConfigurationBuilder().AddJsonStream(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json))).Build();

    [Fact]
    public void PorDefecto_EsVentanas()
    {
        Assert.Equal(DetectoresParadas.Ventanas, new OpcionesMangas().DetectorParadas);
        var p = new ProcesadorMangas(Camara10.Opciones(), new Captura());
        Assert.Equal(DetectoresParadas.Ventanas, p.Detector);
    }

    [Fact]
    public void SinElParametroEnLaConfiguracion_EsVentanas()
    {
        var c = Assert.Single(OpcionesConteo.CargarCamaras(Config("""
            { "ConteoCamara": { "Camaras": [ { "Camara": "T2-CAM10", "Metodo": "MANGAS" } ] } }
            """)));
        Assert.Equal(DetectoresParadas.Ventanas, c.Mangas.DetectorParadas);
    }

    [Fact]
    public void ElAppsettingsDelRepo_La10EnVentanasConLasReglasDeMain()
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        var c10 = OpcionesConteo.CargarCamaras(new ConfigurationBuilder().AddJsonFile(ruta).Build()).Single(c => c.Camara == "T2-CAM10");
        Assert.Equal(DetectoresParadas.Ventanas, c10.Mangas.DetectorParadas);
        Assert.Equal(6, c10.Analisis.VentanasParaConfirmarArranque);
        Assert.Equal(0.9, c10.Mangas.FraccionRodilloGiraParaMarcha);
        Assert.True(c10.Mangas.DescontarMangasEnParada);
        Assert.Equal(15, c10.Mangas.UmbralPasoAlto);
        Assert.Equal(2.0, c10.Mangas.SegundosMinimosEntrePasos);
        Assert.Equal(DetectoresParadas.Ventanas, new ProcesadorMangas(c10, new Captura()).Detector);
    }

    [Theory]
    [InlineData("MANGAS")]
    [InlineData("mangas")]
    [InlineData(" Mangas ")]
    public void Mangas_SeActivaPorParametro(string valor)
    {
        var c10 = Assert.Single(OpcionesConteo.CargarCamaras(Config($$"""
            { "ConteoCamara": { "Camaras": [ { "Camara": "T2-CAM10", "Metodo": "MANGAS",
              "Mangas": { "DetectorParadas": "{{valor}}" } } ] } }
            """)));
        var o = Camara10.Opciones();
        o.Mangas.DetectorParadas = c10.Mangas.DetectorParadas;
        Assert.Equal(DetectoresParadas.Mangas, new ProcesadorMangas(o, new Captura()).Detector);
    }

    [Fact]
    public void ValorDesconocido_NoArranca()
    {
        var o = Camara10.Opciones();
        o.Mangas.DetectorParadas = "CUADROS";
        var ex = Assert.Throws<InvalidOperationException>(() => new ProcesadorMangas(o, new Captura()));
        Assert.Contains("DetectorParadas", ex.Message);
    }

    [Theory]
    [InlineData("VENTANAS")]
    [InlineData("MANGAS")]
    public void Camara8_SinParadasConCualquierDetector(string detector)
    {
        var o = Camara10.Opciones();
        o.Camara = "T2-CAM08";
        o.Metodo = MetodosConteo.Bolsones;
        o.Mangas.UsarRodillo = false;
        o.Mangas.DetectorParadas = detector;
        var salida = new Captura();
        var cadena = Analizadores.Crear(o, salida);

        var t = _cam.Pasar(cadena, 0, 120, periodo: 6);
        t = _cam.Pasar(cadena, t, 6 * 60, periodo: 0);          // 6 min sin bolsones: no es parada
        _cam.Pasar(cadena, t, 60, periodo: 6);
        cadena.CortarSenal(final: true);

        Assert.InRange(salida.Minutos_.Sum(m => m.Rollos), 28, 32);
        Assert.Empty(salida.Paradas);
        Assert.Empty(salida.EnCurso);
        Assert.Null(new ProcesadorMangas(o, new Captura(), detectarParadas: false).Detector);
    }

    [Theory]
    [InlineData("VENTANAS")]
    [InlineData("MANGAS")]
    public void CamaraDeMangasSinDetectarParadas_SoloCuenta(string detector)
    {
        // Maestros → Cámaras puede decir que la 10 no detecta paradas: cuenta igual con cualquiera de los dos.
        var o = Camara10.Opciones();
        o.Mangas.DetectorParadas = detector;
        o.DetectaParadas = false;
        var salida = new Captura();
        var cadena = Analizadores.Crear(o, salida);

        var t = _cam.Pasar(cadena, 0, 60, periodo: 2.7);
        t = _cam.Pasar(cadena, t, 120, periodo: 0, rodilloGira: false);
        _cam.Pasar(cadena, t, 60, periodo: 2.7);
        cadena.CortarSenal(final: true);

        Assert.Empty(salida.Paradas);
        Assert.Empty(salida.EnCurso);
        Assert.InRange(salida.Minutos_.Sum(m => m.Rollos), 42, 46);   // 120 / 2,7 = 44
    }

    [Theory]
    [InlineData("VENTANAS", 0)]   // regla F: con la parada abierta no se cuentan
    [InlineData("MANGAS", 15)]    // T-784: se cuentan siempre (la regla F no aplica)
    public void MangasDuranteLaParada_ReglaFSoloConVentanas(string detector, int minimo)
    {
        var o = Camara10.Opciones();
        o.Analisis.VentanasParaConfirmarArranque = 6;
        o.Mangas.DetectorParadas = detector;
        var salida = new Captura();
        var cadena = Analizadores.Crear(o, salida);

        var t = _cam.Pasar(cadena, 0, 60, periodo: 2.7);
        t = _cam.Pasar(cadena, t, 60, periodo: 0, rodilloGira: false);
        t = _cam.Pasar(cadena, t, 60, periodo: 3, rodilloGira: false);   // algo se mueve en la mesa con el rodillo quieto
        t = _cam.Pasar(cadena, t, 60, periodo: 0, rodilloGira: false);
        _cam.Pasar(cadena, t, 60, periodo: 2.7);
        cadena.CortarSenal(final: true);

        var mangas = salida.Ventanas.Where(v => v.Inicio >= Camara10.T0.AddSeconds(120) && v.Inicio < Camara10.T0.AddSeconds(180)).Sum(v => v.Rollos);
        if (minimo == 0) Assert.Equal(0, mangas);
        else Assert.True(mangas >= minimo, $"{mangas} mangas");
        Assert.NotEmpty(salida.Paradas);
    }
}
