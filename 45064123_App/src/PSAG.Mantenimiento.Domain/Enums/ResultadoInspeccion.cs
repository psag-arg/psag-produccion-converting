namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Resultado de un ítem de inspección.
/// R16: Si el resultado es NO_OK, la observación es obligatoria.
/// </summary>
public enum ResultadoInspeccion
{
    OK = 1,
    NO_OK = 2
}

public static class ResultadoInspeccionExtensions
{
    public static string ObtenerDescripcion(this ResultadoInspeccion resultado) => resultado switch
    {
        ResultadoInspeccion.OK => "OK",
        ResultadoInspeccion.NO_OK => "NO OK",
        _ => resultado.ToString()
    };
}
