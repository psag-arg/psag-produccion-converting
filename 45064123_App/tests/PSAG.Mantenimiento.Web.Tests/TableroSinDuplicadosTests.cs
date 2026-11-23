using System.Text.RegularExpressions;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>
/// T-770 (ajustes 23/11) · El tablero no repite KPIs: cada tema (Eficiencia, Producción, Velocidad, Detenciones) en UN SOLO
/// bloque con su color de acento; las tarjetas viejas que repetían el mismo número ya no están. La página tiene muchos
/// servicios inyectados, así que se revisa su marcado (TableroOperador.razor); los componentes se dibujan en
/// GraficosRenderTests.
/// </summary>
public class TableroSinDuplicadosTests
{
    private static string Pagina()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var ruta = Path.Combine(dir.FullName, "src", "PSAG.Mantenimiento.Web", "Pages", "Produccion", "TableroOperador.razor");
            if (File.Exists(ruta))
                return File.ReadAllText(ruta);
            dir = dir.Parent;
        }
        throw new FileNotFoundException("No encontré TableroOperador.razor subiendo desde " + AppContext.BaseDirectory);
    }

    /// <summary>El marcado sin los comentarios de Razor (ahí se nombran las tarjetas que se sacaron).</summary>
    private static string Marcado() => Regex.Replace(Pagina(), @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);

    [Theory]
    [InlineData("kpi-mangas")]
    [InlineData("kpi-eficiencia")]
    [InlineData("kpi-bolsones")]
    [InlineData("kpi-velocidad")]
    [InlineData("kpi-detenciones")]
    [InlineData("kpi-microcortes")]
    [InlineData("kpi-pendientes")]
    [InlineData("Eficiencia parcial")]
    [InlineData("Mangas (obj. a la hora)")]
    [InlineData("Bolsones (real / obj.)")]
    public void NoQuedanLasTarjetasViejas(string viejo)
    {
        Assert.DoesNotContain(viejo, Marcado());
    }

    [Fact]
    public void CadaTemaEnUnSoloBloque_ConSuColor()
    {
        var m = Marcado();
        var temas = Regex.Matches(m, @"<TarjetaGrafico[^>]*Tema=""TableroUi\.Tema\.(\w+)""").Select(x => x.Groups[1].Value).ToList();
        Assert.Equal(["Eficiencia", "Produccion", "Velocidad", "Detenciones"], temas);
        Assert.Equal(4, Regex.Matches(m, "<TarjetaGrafico").Count);

        // En "Más datos del turno" sólo queda lo que no es de los 4 temas: la indisponibilidad.
        var kpis = Regex.Matches(m, @"<TarjetaKpi[^>]*TestId=""([\w-]+)""").Select(x => x.Groups[1].Value).ToList();
        Assert.Equal(["kpi-indisponibilidad"], kpis);

        // Detenciones: las 4 cifras adentro del bloque (y las barras por tipo, sólo en los modales)
        Assert.Contains("<CifrasDetenciones", m);
        Assert.Equal(1, Regex.Matches(m, "<CifrasDetenciones").Count);
        Assert.Equal(1, Regex.Matches(m, "<MedidorEficiencia").Count);
        Assert.Equal(1, Regex.Matches(m, "<BarrasProduccion").Count);
        // Los modales llevan el color de su tema
        Assert.Contains("TableroUi.EstiloAcento(tema)", m);
    }

    [Fact]
    public void ColoresDeAcento_DistintosYSinPisarElSemaforo()
    {
        var colores = Enum.GetValues<TableroUi.Tema>().Select(TableroUi.ColorTema).ToList();
        Assert.Equal(4, colores.Distinct().Count());
        Assert.All(colores, c => Assert.StartsWith("var(--bs-", c));
        var semaforo = new[] { TableroUi.Nivel.Bien, TableroUi.Nivel.Atencion, TableroUi.Nivel.Mal }.Select(GraficosUi.Color).ToList();
        Assert.All(colores, c => Assert.DoesNotContain(c, semaforo));
    }

    [Theory]
    [InlineData(TableroUi.Cifra.Pendientes, TableroUi.ModalCifra.Declarar)]
    [InlineData(TableroUi.Cifra.Declaradas, TableroUi.ModalCifra.SoloLectura)]
    [InlineData(TableroUi.Cifra.Totales, TableroUi.ModalCifra.SoloLectura)]
    [InlineData(TableroUi.Cifra.Microcortes, TableroUi.ModalCifra.SoloLectura)]
    public void CadaCifraAbreSuModal(TableroUi.Cifra cifra, TableroUi.ModalCifra esperado)
    {
        Assert.Equal(esperado, TableroUi.ModalDeCifra(cifra));
    }

    [Fact]
    public void LaPaginaMapeaCadaCifraASuModal()
    {
        var m = Pagina();
        Assert.Contains("TableroUi.Cifra.Pendientes => Detalle.Pendientes", m);
        Assert.Contains("TableroUi.Cifra.Declaradas => Detalle.Declaradas", m);
        Assert.Contains("TableroUi.Cifra.Microcortes => Detalle.Microcortes", m);
        // El formulario de declaración y el botón "Declarar" sólo están en el modal de pendientes / declarar.
        var totales = m[m.IndexOf("case Detalle.Totales:", StringComparison.Ordinal)..m.IndexOf("case Detalle.Pendientes:", StringComparison.Ordinal)];
        Assert.DoesNotContain("AbrirDeclarar", totales);
        Assert.DoesNotContain("FormularioDeclaracion", totales);
    }

    [Theory]
    [InlineData("FALLA PROCESO", "FALLA PROCESO")]
    [InlineData("Mantenimiento preventivo", "MANTENIMIENTO PREVENTIVO")]
    public void EtiquetaTipo_EnteraEnMayusculas(string nombre, string esperado)
    {
        Assert.Equal(esperado, GraficosUi.EtiquetaTipo(nombre));
    }

    [Fact]
    public void EtiquetaTipo_MuyLarga_SeRecortaConPuntos()
    {
        var e = GraficosUi.EtiquetaTipo(new string('X', 80));
        Assert.Equal(GraficosUi.LargoEtiquetaTipo, e.Length);
        Assert.EndsWith("…", e);
    }

    [Fact]
    public void TextoProgramadas()
    {
        Assert.Null(TableroUi.TextoProgramadas(new()));
        Assert.Equal("+ 2 programadas (1 h 05 min, no cuentan)", TableroUi.TextoProgramadas(new() { Cantidad = 2, Minutos = 65 }));
    }
}
