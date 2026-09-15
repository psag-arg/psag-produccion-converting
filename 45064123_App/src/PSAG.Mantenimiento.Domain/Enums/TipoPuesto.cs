namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Tipos de puesto operativo para asignación de inspecciones.
/// DEC-027: Los puestos operativos son distintos de los roles de ASP.NET Core Identity.
/// </summary>
public enum TipoPuesto
{
    MAQUINISTA = 1,
    AYUDANTE = 2,
    PACKAGING = 3
}

public static class TipoPuestoExtensions
{
    public static string ObtenerDescripcion(this TipoPuesto tipo) => tipo switch
    {
        TipoPuesto.MAQUINISTA => "Maquinista",
        TipoPuesto.AYUDANTE => "Ayudante",
        TipoPuesto.PACKAGING => "Operador Packaging",
        _ => tipo.ToString()
    };
}
