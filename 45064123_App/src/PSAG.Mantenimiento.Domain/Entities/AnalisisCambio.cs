namespace PSAG.Mantenimiento.Domain.Entities;

/// <summary>
/// Historial de ediciones de un análisis de falla ya cerrado: un registro por campo modificado
/// (o por foto agregada) con quién, cuándo, el valor anterior y el nuevo.
/// Esta tabla es de solo inserción - no se permiten actualizaciones ni eliminaciones.
/// </summary>
public class AnalisisCambio
{
    public const string CampoSolucion = "SOLUCION";
    public const string CampoCausa = "CAUSA";
    public const string CampoPlanAccion = "PLAN_ACCION";
    public const string CampoFoto = "FOTO";

    public long AnalisisCambioId { get; set; }
    public long DetencionId { get; set; }

    /// <summary>
    /// SOLUCION | CAUSA | PLAN_ACCION | FOTO.
    /// </summary>
    public string Campo { get; set; } = string.Empty;

    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }

    public string UsuarioId { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }

    // Navegación
    public virtual Detencion Detencion { get; set; } = null!;
}
