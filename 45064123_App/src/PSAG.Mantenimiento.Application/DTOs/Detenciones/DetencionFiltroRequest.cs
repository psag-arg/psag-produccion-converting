using PSAG.Mantenimiento.Application.Common;

namespace PSAG.Mantenimiento.Application.DTOs.Detenciones;

/// <summary>
/// Filtros para búsqueda de detenciones.
/// </summary>
public class DetencionFiltroRequest : PaginationParams
{
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? Estado { get; set; }
    public int? LineaId { get; set; }
    public int? EquipoId { get; set; }
    public int? TipoFallaId { get; set; }
    public int? DetalleFallaId { get; set; }
    public int? TurnoId { get; set; }
    public string? CreadoPorUsuarioId { get; set; }
    public string? CerradoPorUsuarioId { get; set; }
    public string? Busqueda { get; set; }
    public bool? SoloAbiertos { get; set; }
    public bool? SoloCerrados { get; set; }
    public int? DuracionMinimaMinutos { get; set; }
    public int? DuracionMaximaMinutos { get; set; }
}
