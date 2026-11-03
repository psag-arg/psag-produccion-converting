namespace PSAG.Mantenimiento.Application.DTOs.Inspeccion;

/// <summary>
/// Alta o edición de un plan de inspección (un equipo de la línea, ej. EMPAQUETADORA TMC) desde Maestros.
/// </summary>
public class GuardarPlanInspeccionRequest
{
    /// <summary>Solo se usa en el alta: un plan no cambia de línea.</summary>
    public int LineaId { get; set; }
    public int? EquipoId { get; set; }

    /// <summary>Opcional: si viene vacío se genera (INS-L{línea}-NN).</summary>
    public string? Codigo { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>Responsable de la inspección (PACKAGING, MECANICO...).</summary>
    public string PuestoResponsable { get; set; } = "PACKAGING";

    /// <summary>Frecuencia propuesta para los puntos nuevos.</summary>
    public int FrecuenciaDias { get; set; } = 1;
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Alta o edición de un punto del plan (una fila de la planilla).
/// </summary>
public class GuardarItemInspeccionRequest
{
    public int PlanInspeccionId { get; set; }

    /// <summary>N° de la planilla. 0 = el siguiente.</summary>
    public int NumeroItem { get; set; }

    /// <summary>Opcional: si viene vacío se genera ({código del plan}-NN).</summary>
    public string? Codigo { get; set; }
    public string ParteEquipo { get; set; } = string.Empty;
    public string Tareas { get; set; } = string.Empty;
    public string Estandar { get; set; } = string.Empty;
    public string? Metodo { get; set; }

    /// <summary>PACKAGING, MECANICO, MAQUINISTA o AYUDANTE.</summary>
    public string QuienRealiza { get; set; } = "PACKAGING";
    public int FrecuenciaDias { get; set; } = 1;

    /// <summary>0 = mismo que el N°.</summary>
    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
}
