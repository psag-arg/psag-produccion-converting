using Microsoft.Extensions.Configuration;

namespace PSAG.Mantenimiento.Api.Configuration;

/// <summary>
/// Zona horaria de la planta (Planta:ZonaHoraria). Las horas que carga el operador se leen en esta zona,
/// no en la del servidor. Si el servidor no tiene la zona IANA, usa el id de Windows; si tampoco, falla al arrancar.
/// </summary>
public static class ZonaHorariaPlanta
{
    public const string Clave = "Planta:ZonaHoraria";
    public const string PorDefecto = "America/Argentina/Buenos_Aires";
    public const string IdWindowsRespaldo = "Argentina Standard Time";

    public static TimeZoneInfo Resolver(IConfiguration configuracion)
    {
        var id = configuracion[Clave];
        if (string.IsNullOrWhiteSpace(id))
            id = PorDefecto;

        if (TryBuscar(id, out var zona) || TryBuscar(IdWindowsRespaldo, out zona))
            return zona!;

        throw new InvalidOperationException(
            $"No se encontró la zona horaria '{id}' (ni '{IdWindowsRespaldo}') en el servidor. Revisá {Clave}.");
    }

    private static bool TryBuscar(string id, out TimeZoneInfo? zona)
    {
        try
        {
            zona = TimeZoneInfo.FindSystemTimeZoneById(id);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            zona = null;
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            zona = null;
            return false;
        }
    }
}
