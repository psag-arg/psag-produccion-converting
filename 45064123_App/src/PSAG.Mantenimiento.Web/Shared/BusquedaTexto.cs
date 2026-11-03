using System.Globalization;
using System.Text;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Búsqueda de los buscadores con tipeo: sin importar mayúsculas ni acentos, y por palabras sueltas
/// en cualquier orden ("hig 30" encuentra "Higiénico 30 mts"; "electrica" encuentra "Falla Eléctrica").
/// </summary>
public static class BusquedaTexto
{
    public static bool Coincide(string? texto, string? busqueda)
    {
        if (string.IsNullOrWhiteSpace(busqueda)) return true;
        if (string.IsNullOrEmpty(texto)) return false;

        var normal = Normalizar(texto);
        return Normalizar(busqueda)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .All(normal.Contains);
    }

    public static string Normalizar(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
