using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

public interface IAuditoriaService
{
    Task RegistrarAsync(
        string usuarioId,
        TipoAccionAuditoria accion,
        string entidad,
        string? entidadId,
        object? valoresAnteriores,
        object? valoresNuevos,
        string? descripcion = null,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResult<AuditoriaDto>>> ObtenerPaginadoAsync(
        AuditoriaFiltroRequest filtro,
        CancellationToken cancellationToken = default);
}

public class AuditoriaDto
{
    public long AuditoriaId { get; set; }
    public string UsuarioId { get; set; } = string.Empty;
    public string UsuarioNombre { get; set; } = string.Empty;
    public DateTimeOffset FechaHora { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public string? EntidadId { get; set; }
    public string? Descripcion { get; set; }
    public string? DireccionIp { get; set; }
}

public class AuditoriaFiltroRequest : PaginationParams
{
    public string? UsuarioId { get; set; }
    public TipoAccionAuditoria? Accion { get; set; }
    public string? Entidad { get; set; }
    public DateTimeOffset? FechaDesde { get; set; }
    public DateTimeOffset? FechaHasta { get; set; }
}
