using System.Globalization;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>
/// T-770 iteración 2 · Lógica pura de los gráficos SVG del tablero: ángulo de la aguja, colores (cortes 65/85), escalas,
/// polilínea de velocidad (sin datos corta la línea) y formatos (punto decimal en el SVG aunque la cultura sea es-AR).
/// </summary>
public class GraficosUiTests
{
    [Theory]
    [InlineData(0.0, 180.0)]
    [InlineData(50.0, 90.0)]
    [InlineData(100.0, 0.0)]
    [InlineData(68.0, 57.6)]
    [InlineData(130.0, 0.0)]     // pasado de 100: aguja al tope
    [InlineData(-5.0, 180.0)]
    public void AnguloAguja(double porcentaje, double grados) =>
        Assert.Equal(grados, GraficosUi.AnguloAguja((decimal)porcentaje), 6);

    [Fact]
    public void AnguloAguja_SinDato_EnCero_YPuntoArriba()
    {
        Assert.Equal(180d, GraficosUi.AnguloAguja(null));
        var (x, y) = GraficosUi.Punto(110, 112, 84, 90);
        Assert.Equal(110d, x, 6);
        Assert.Equal(28d, y, 6);   // arriba: y de SVG crece hacia abajo
    }

    [Theory]
    [InlineData(null, TableroUi.Nivel.SinDato)]
    [InlineData(64.9, TableroUi.Nivel.Mal)]
    [InlineData(65.0, TableroUi.Nivel.Atencion)]
    [InlineData(85.0, TableroUi.Nivel.Atencion)]
    [InlineData(85.1, TableroUi.Nivel.Bien)]
    public void NivelEficiencia_CortesDeFerretti(double? porcentaje, TableroUi.Nivel esperado) =>
        Assert.Equal(esperado, GraficosUi.NivelEficiencia(porcentaje is { } p ? (decimal)p : null));

    [Fact]
    public void Colores_SonVariablesDeBootstrap()
    {
        Assert.Equal("var(--bs-danger)", GraficosUi.Color(TableroUi.Nivel.Mal));
        Assert.Equal("var(--bs-warning)", GraficosUi.Color(TableroUi.Nivel.Atencion));
        Assert.Equal("var(--bs-success)", GraficosUi.Color(TableroUi.Nivel.Bien));
        Assert.Equal("var(--bs-secondary-color)", GraficosUi.Color(TableroUi.Nivel.SinDato));
        Assert.Equal(TableroUi.Nivel.Bien, GraficosUi.NivelDeApi("VERDE"));
        Assert.Equal(TableroUi.Nivel.Atencion, GraficosUi.NivelDeApi("AMARILLO"));
        Assert.Equal(TableroUi.Nivel.Mal, GraficosUi.NivelDeApi("ROJO"));
        Assert.Equal(TableroUi.Nivel.SinDato, GraficosUi.NivelDeApi("SIN_DATO"));
    }

    [Fact]
    public void Bandas_CubrenDe0A100ConLosCortes()
    {
        Assert.Equal([(0m, 65m, TableroUi.Nivel.Mal), (65m, 85m, TableroUi.Nivel.Atencion), (85m, 100m, TableroUi.Nivel.Bien)], GraficosUi.BandasEficiencia);
        var path = GraficosUi.Banda(110, 112, 96, 64, 0, 65);
        Assert.StartsWith("M 14 112 A 96 96 0 0 1 ", path);   // empieza a la izquierda (0 %)
        Assert.EndsWith(" Z", path);
    }

    [Theory]
    [InlineData(0.5, 1.0)]
    [InlineData(7.0, 10.0)]
    [InlineData(11200.0, 20000.0)]
    [InlineData(737.0, 1000.0)]
    [InlineData(2300.0, 2500.0)]
    [InlineData(45.0, 50.0)]
    [InlineData(100.0, 100.0)]
    public void EscalaRedonda(double maximo, double esperado) =>
        Assert.Equal((decimal)esperado, GraficosUi.EscalaRedonda((decimal)maximo));

    [Fact]
    public void Escalas_DeVelocidadYDetenciones()
    {
        Assert.Equal(1000m, GraficosUi.EscalaVelocidad([600m, null, 650m], 670m));    // la referencia +10 % entra
        Assert.Equal(1000m, GraficosUi.EscalaVelocidad([], 670m));
        Assert.Equal(100m, GraficosUi.EscalaVelocidad([], 0m));
        Assert.Equal(10m, GraficosUi.EscalaDetenciones([0, 0]));
        Assert.Equal(50m, GraficosUi.EscalaDetenciones([20, 45, 5]));
        Assert.Equal([0m, 250m, 500m, 750m, 1000m], GraficosUi.Marcas(1000m, 4));
    }

    [Fact]
    public void Polilineas_SinDatosCortaLaLinea_YLaParadaVaAbajo()
    {
        // Eje de 4 minutos, 0..100 de ancho y 0..50 de alto, máximo 1000 m/min
        var tramos = GraficosUi.Polilineas([500m, 0m, null, 1000m], 0, 4, 1000m, 0, 100, 0, 50);
        Assert.Equal(["0,25 25,50", "75,0 75,0"], tramos);   // el minuto suelto se dibuja como punto

        // Con desplazamiento (la serie empieza 2 min después del inicio del eje)
        Assert.Equal(["50,25 75,25"], GraficosUi.Polilineas([500m, 500m], 2, 4, 1000m, 0, 100, 0, 50));
        Assert.Empty(GraficosUi.Polilineas([null, null], 0, 4, 1000m, 0, 100, 0, 50));
    }

    [Fact]
    public void Formatos_PuntoDecimalEnElSvg_YTextosEnCastellano()
    {
        var anterior = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("es-AR");
        try
        {
            Assert.Equal("12.5", GraficosUi.N(12.5));
            Assert.Equal("68 %", GraficosUi.TextoPorcentaje(68.4m));
            Assert.Equal("Sin datos", GraficosUi.TextoPorcentaje(null));
            Assert.Equal("Sin objetivo", GraficosUi.TextoPorcentaje(null, "SIN_OBJETIVO"));
            Assert.Equal("Obj: 11.200 mangas", GraficosUi.TextoObjetivoBarra(11200m, "mangas"));
            Assert.Equal("Sin objetivo", GraficosUi.TextoObjetivoBarra(null, "bolsones"));
            Assert.Equal("7.450 (66 %)", GraficosUi.TextoRealBarra(7450m, 66.4m));
            Assert.Equal("Sin datos", GraficosUi.TextoRealBarra(null, null));     // nunca 0 %
            Assert.Equal("1.250 min", GraficosUi.TextoMinutos(1250));
            Assert.Equal("2,5", GraficosUi.TextoMarca(2.5m));
            Assert.Equal("1.500", GraficosUi.TextoMarca(1500m));
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    [Fact]
    public void MarcasHora_HorasEnterasDelEje()
    {
        var desde = new DateTimeOffset(2026, 11, 19, 13, 0, 0, TimeSpan.FromHours(-3));
        var marcas = GraficosUi.MarcasHora(desde, desde.AddHours(8), 2);
        Assert.Equal(["13:00", "15:00", "17:00", "19:00", "21:00"], marcas.Select(m => m.ToString("HH:mm")));
        Assert.Equal(["14:00"], GraficosUi.MarcasHora(desde.AddMinutes(10), desde.AddMinutes(70)).Select(m => m.ToString("HH:mm")));
        Assert.Equal(480, GraficosUi.MinutosEntre(desde, desde.AddHours(8)));
        Assert.Equal(0, GraficosUi.MinutosEntre(desde.AddHours(1), desde));
    }
}
