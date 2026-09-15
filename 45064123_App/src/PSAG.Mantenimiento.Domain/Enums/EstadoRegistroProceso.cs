namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Estado de un registro de variables de proceso.
/// </summary>
public enum EstadoRegistroProceso
{
    /// <summary>
    /// Registro iniciado pero no completo.
    /// El operador puede completarlo en el mismo turno.
    /// </summary>
    PARCIAL = 1,

    /// <summary>
    /// Registro completo (todos los grupos cargados).
    /// </summary>
    COMPLETO = 2,

    /// <summary>
    /// Registro cerrado (no editable).
    /// </summary>
    CERRADO = 3
}

public static class EstadoRegistroProcesoExtensions
{
    public static string ObtenerDescripcion(this EstadoRegistroProceso estado)
    {
        return estado switch
        {
            EstadoRegistroProceso.PARCIAL => "Parcial",
            EstadoRegistroProceso.COMPLETO => "Completo",
            EstadoRegistroProceso.CERRADO => "Cerrado",
            _ => estado.ToString()
        };
    }

    public static bool PuedeSerEditado(this EstadoRegistroProceso estado)
    {
        return estado != EstadoRegistroProceso.CERRADO;
    }
}
