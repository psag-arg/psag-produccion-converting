namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Tipos de puesto operativo para asignación de inspecciones.
/// DEC-027: Los puestos operativos son distintos de los roles de ASP.NET Core Identity.
/// </summary>
public enum TipoPuesto
{
    MAQUINISTA = 1,
    AYUDANTE = 2,
    PACKAGING = 3,

    /// <summary>
    /// Mecánico de la línea (planilla de inspección de la empaquetadora TMC: cadena y carro pente cada 10 días).
    /// </summary>
    MECANICO = 4
}

public static class TipoPuestoExtensions
{
    public static string ObtenerDescripcion(this TipoPuesto tipo) => tipo switch
    {
        TipoPuesto.MAQUINISTA => "Maquinista",
        TipoPuesto.AYUDANTE => "Ayudante",
        TipoPuesto.PACKAGING => "Operador Packaging",
        TipoPuesto.MECANICO => "Mecánico",
        _ => tipo.ToString()
    };
}
