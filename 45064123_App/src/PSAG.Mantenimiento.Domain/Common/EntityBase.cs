namespace PSAG.Mantenimiento.Domain.Common;

/// <summary>
/// Clase base para entidades con campos de auditoría.
/// </summary>
public abstract class EntityBase
{
    public DateTimeOffset FechaHoraCreacion { get; set; }
    public DateTimeOffset FechaHoraModificacion { get; set; }
    public string CreadoPorUsuarioId { get; set; } = string.Empty;
    public string ModificadoPorUsuarioId { get; set; } = string.Empty;
}

/// <summary>
/// Clase base para catálogos con soporte de activación/inactivación.
/// </summary>
public abstract class CatalogoBase : EntityBase
{
    public bool Activo { get; set; } = true;
}
