namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Detalle de un registro de proceso: valor real capturado para un parámetro.
/// Guarda el estándar vigente al momento del registro (snapshot) para
/// que la consulta histórica sea honesta.
/// </summary>
public class RegistroProcesoDetalle
{
    public long RegistroProcesoDetalleId { get; set; }
    public long RegistroProcesoId { get; set; }
    public int ParametroId { get; set; }

    /// <summary>
    /// SNAPSHOT del estándar vigente al momento del registro.
    /// NO se recalcula. Si mañana cambian el estándar, este registro
    /// sigue mostrando contra qué se comparó ESE DÍA.
    /// </summary>
    public string? ValorEstandarSnapshot { get; set; }

    /// <summary>
    /// Valor real numérico capturado.
    /// </summary>
    public decimal? ValorReal { get; set; }

    /// <summary>
    /// Valor real en texto (para parámetros de tipo TEXTO).
    /// </summary>
    public string? ValorRealTexto { get; set; }

    /// <summary>
    /// Indica si el valor está fuera del estándar.
    /// Se calcula en BACKEND al guardar, no lo manda el cliente.
    /// </summary>
    public bool FueraDeEstandar { get; set; }

    /// <summary>
    /// Observación obligatoria cuando FueraDeEstandar = true.
    /// Justifica por qué el valor difiere del estándar.
    /// </summary>
    public string? Observacion { get; set; }

    /// <summary>
    /// Fecha y hora en que se registró este valor.
    /// </summary>
    public DateTimeOffset FechaHoraRegistro { get; set; } = DateTimeOffset.UtcNow;

    // Navegación
    public virtual RegistroProceso RegistroProceso { get; set; } = null!;
    public virtual Parametro Parametro { get; set; } = null!;
}
