using System.Text.RegularExpressions;
using PSAG.Mantenimiento.Web.Shared;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>
/// T-806 · Tema global (claro "Redondo vivo" / oscuro "Industrial oscuro") en UN archivo (wwwroot/css/tema.css): cada
/// token que usa el código está definido en los dos modos de Bootstrap 5.3, el semáforo y los colores por tema tienen los
/// valores pedidos, y las fuentes van empaquetadas (sin CDN).
/// </summary>
public class TemaWebTests
{
    private static string Web(params string[] partes)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var ruta = Path.Combine([dir.FullName, "src", "PSAG.Mantenimiento.Web", .. partes]);
            if (File.Exists(ruta))
                return ruta;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("No encontré " + string.Join("/", partes) + " subiendo desde " + AppContext.BaseDirectory);
    }

    private static string Tema() => File.ReadAllText(Web("wwwroot", "css", "tema.css"));

    /// <summary>Cuerpo del bloque de un modo: ":root, [data-bs-theme=\"light\"] { ... }" o "[data-bs-theme=\"dark\"] { ... }".</summary>
    private static string Bloque(string modo)
    {
        var css = Tema();
        var m = Regex.Match(css, @"(^|\n)(:root,\s*)?\[data-bs-theme=""" + modo + @"""\]\s*\{(?<cuerpo>[^}]*)\}");
        Assert.True(m.Success, "No está el bloque del modo " + modo);
        return m.Groups["cuerpo"].Value;
    }

    private static string? Valor(string bloque, string token)
    {
        var m = Regex.Match(bloque, @"(?<![\w-])" + Regex.Escape(token) + @"\s*:\s*(?<v>[^;]+);");
        return m.Success ? m.Groups["v"].Value.Trim() : null;
    }

    private static string Token(string varCss) => Regex.Match(varCss, @"var\((?<t>--[\w-]+)\)").Groups["t"].Value;

    [Theory]
    [InlineData("light", "#f43f6b", "#6246ff", "#06b6d4", "#ab47ff")]
    [InlineData("dark", "#ff6b6b", "#4d8bff", "#36d1dc", "#b98bff")]
    public void ColorPorTema_EnLosDosModos(string modo, string eficiencia, string produccion, string velocidad, string detenciones)
    {
        var b = Bloque(modo);
        Assert.Equal(eficiencia, Valor(b, Token(TableroUi.ColorTema(TableroUi.Tema.Eficiencia))));
        Assert.Equal(produccion, Valor(b, Token(TableroUi.ColorTema(TableroUi.Tema.Produccion))));
        Assert.Equal(velocidad, Valor(b, Token(TableroUi.ColorTema(TableroUi.Tema.Velocidad))));
        Assert.Equal(detenciones, Valor(b, Token(TableroUi.ColorTema(TableroUi.Tema.Detenciones))));
    }

    [Theory]
    [InlineData("light", "#12b76a", "#f5a524", "#f43f6b")]
    [InlineData("dark", "#2ee6a0", "#ffcf3f", "#ff6b6b")]
    public void Semaforo_EnLosDosModos_YBootstrapLoUsa(string modo, string verde, string amarillo, string rojo)
    {
        var b = Bloque(modo);
        Assert.Equal(verde, Valor(b, Token(GraficosUi.Color(TableroUi.Nivel.Bien))));
        Assert.Equal(amarillo, Valor(b, Token(GraficosUi.Color(TableroUi.Nivel.Atencion))));
        Assert.Equal(rojo, Valor(b, Token(GraficosUi.Color(TableroUi.Nivel.Mal))));
        // Las clases de Bootstrap (bg-success-subtle, text-warning-emphasis…) siguen al mismo semáforo
        Assert.Equal("var(--psag-semaforo-verde)", Valor(b, "--bs-success"));
        Assert.Equal("var(--psag-semaforo-amarillo)", Valor(b, "--bs-warning"));
        Assert.Equal("var(--psag-semaforo-rojo)", Valor(b, "--bs-danger"));
    }

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public void TodoTokenUsadoEstaDefinidoEnCadaModo(string modo)
    {
        var b = Bloque(modo);
        var fuentes = Directory.GetFiles(Path.GetDirectoryName(Path.GetDirectoryName(Web("wwwroot", "index.html")))!, "*.*", SearchOption.AllDirectories)
            .Where(f => (f.EndsWith(".razor") || f.EndsWith(".css") || f.EndsWith(".cs"))
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        var usados = fuentes.SelectMany(f => Regex.Matches(File.ReadAllText(f), @"var\((--psag-[\w-]+)\)").Select(m => m.Groups[1].Value))
            .Distinct().ToList();
        Assert.NotEmpty(usados);
        // Tipografía, radios de pill y similares se definen una sola vez en :root (valen para los dos modos)
        var comunes = Bloque("light");
        Assert.All(usados, t => Assert.True(Valor(b, t) != null || Valor(comunes, t) != null, $"{t} no está definido para {modo}"));
    }

    [Fact]
    public void Fuentes_Empaquetadas_SinCdn()
    {
        var css = Tema();
        var urls = Regex.Matches(css, @"url\(""?(?<u>[^"")]+)""?\)").Select(m => m.Groups["u"].Value).Where(u => !u.StartsWith("data:")).ToList();
        Assert.NotEmpty(urls);
        Assert.All(urls, u => Assert.StartsWith("../fonts/", u));
        Assert.All(urls, u => Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(Web("wwwroot", "css", "tema.css"))!, u)), u));
        Assert.True(File.Exists(Web("wwwroot", "fonts", "OFL-Inter.txt")));
        Assert.True(File.Exists(Web("wwwroot", "fonts", "OFL-SpaceGrotesk.txt")));
    }

    [Fact]
    public void IndexCargaElTema_YSigueAlSistemaPorDefecto()
    {
        var index = File.ReadAllText(Web("wwwroot", "index.html"));
        Assert.True(index.IndexOf("bootstrap.min.css", StringComparison.Ordinal) < index.IndexOf("css/tema.css", StringComparison.Ordinal));
        Assert.True(index.IndexOf("css/tema.css", StringComparison.Ordinal) < index.IndexOf("css/app.css", StringComparison.Ordinal));
        Assert.Contains("prefers-color-scheme: dark", index);
        Assert.Contains("data-bs-theme", index);
    }
}
