namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Método de inspección utilizado para verificar el estado del equipo.
/// </summary>
public enum MetodoInspeccion
{
    VISUAL = 1,
    TACTO = 2,
    MEDICION = 3
}

public static class MetodoInspeccionExtensions
{
    public static string ObtenerDescripcion(this MetodoInspeccion metodo) => metodo switch
    {
        MetodoInspeccion.VISUAL => "Visual",
        MetodoInspeccion.TACTO => "Tacto",
        MetodoInspeccion.MEDICION => "Medición",
        _ => metodo.ToString()
    };
}
