using System.Globalization;
using System.Text;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Lee los VALUES de los scripts de database/ (una fila "(...)," por línea) para que los tests carguen
/// exactamente lo que carga el script: textos N'...' concatenados con NCHAR(n), números y NULL.
/// </summary>
internal static class ScriptSql
{
    public static string[] Lineas(string script) => File.ReadAllLines(Ruta(script), Encoding.UTF8);

    public static string Texto(string script) => File.ReadAllText(Ruta(script), Encoding.UTF8);

    /// <summary>Filas "(...)," que siguen al INSERT indicado, hasta la que termina en ";".</summary>
    public static IEnumerable<List<string?>> FilasDe(string[] lineas, string insert, string script)
    {
        var desde = Array.FindIndex(lineas, l => l.TrimStart().StartsWith(insert, StringComparison.Ordinal));
        Assert.True(desde >= 0, $"No se encontró '{insert}' en {script}.");

        for (var i = desde + 1; i < lineas.Length; i++)
        {
            var linea = lineas[i].Trim();
            if (linea.Length == 0)
                continue;
            Assert.StartsWith("(", linea);
            var fin = linea.EndsWith(';');
            var cuerpo = linea.TrimEnd(',', ';');
            Assert.EndsWith(")", cuerpo);
            yield return Valores(cuerpo[1..^1]);
            if (fin)
                yield break;
        }
    }

    /// <summary>
    /// Valores T-SQL separados por coma: NULL, números, y textos N'...' concatenados con NCHAR(n).
    /// </summary>
    public static List<string?> Valores(string fila)
    {
        var valores = new List<string?>();
        var i = 0;
        while (i < fila.Length)
        {
            while (i < fila.Length && fila[i] == ' ') i++;
            var texto = new StringBuilder();
            string? crudo = null;
            var esTexto = false;
            while (i < fila.Length && fila[i] != ',')
            {
                if (fila[i] == ' ' || fila[i] == '+') { i++; continue; }
                if (fila[i] == 'N' && i + 1 < fila.Length && fila[i + 1] == '\'')
                {
                    esTexto = true;
                    i += 2;
                    while (true)
                    {
                        if (fila[i] == '\'' && i + 1 < fila.Length && fila[i + 1] == '\'') { texto.Append('\''); i += 2; }
                        else if (fila[i] == '\'') { i++; break; }
                        else texto.Append(fila[i++]);
                    }
                }
                else if (fila.AsSpan(i).StartsWith("NCHAR("))
                {
                    esTexto = true;
                    var cierre = fila.IndexOf(')', i);
                    texto.Append((char)int.Parse(fila[(i + 6)..cierre], CultureInfo.InvariantCulture));
                    i = cierre + 1;
                }
                else
                {
                    var j = i;
                    while (j < fila.Length && fila[j] != ',' && fila[j] != ' ') j++;
                    crudo = fila[i..j];
                    i = j;
                }
            }
            i++; // coma
            valores.Add(esTexto ? texto.ToString() : crudo == "NULL" ? null : crudo);
        }
        return valores;
    }

    public static string Ruta(string script)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            var ruta = Path.Combine(dir.FullName, "database", script);
            if (File.Exists(ruta))
                return ruta;
        }
        throw new FileNotFoundException($"No se encontró database/{script} subiendo desde {AppContext.BaseDirectory}.");
    }
}
