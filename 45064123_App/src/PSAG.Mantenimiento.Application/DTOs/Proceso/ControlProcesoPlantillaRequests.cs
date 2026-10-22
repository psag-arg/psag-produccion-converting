namespace PSAG.Mantenimiento.Application.DTOs.Proceso;

/// <summary>
/// Alta o edición de un grupo de la plantilla de Control de Proceso de una línea (T-545).
/// </summary>
public class GuardarGrupoControlRequest
{
    /// <summary>Solo se usa en el alta: un grupo no cambia de línea.</summary>
    public int LineaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Alta o edición de un punto de control (T-545).
/// </summary>
public class GuardarPuntoControlRequest
{
    public int GrupoParametroId { get; set; }
    public string? Seccion { get; set; }

    /// <summary>Opcional: si viene vacío se genera (CP-L{línea}-NNN).</summary>
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;

    /// <summary>NUMERICO, OK_NOOK o TEXTO (RANGO se acepta por compatibilidad).</summary>
    public string TipoDato { get; set; } = "NUMERICO";
    public string? UnidadMedida { get; set; }
    public decimal? ValorMin { get; set; }
    public decimal? ValorMax { get; set; }

    /// <summary>POR_TURNO, INICIO_TURNO, CAMBIO_PRODUCTO, CADA_HORA, CADA_2_HORAS, DIARIA.</summary>
    public string Frecuencia { get; set; } = "POR_TURNO";
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}
