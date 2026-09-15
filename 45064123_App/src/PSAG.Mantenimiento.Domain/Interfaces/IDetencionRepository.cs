using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Domain.Interfaces;

/// <summary>
/// Repositorio especializado para detenciones.
/// </summary>
public interface IDetencionRepository : IRepository<Detencion>
{
    Task<Detencion?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken = default);
    Task<Detencion?> GetByIdWithDetailsAsync(long detencionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Detencion>> GetByLineaAsync(int lineaId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Detencion>> GetByEstadoAsync(EstadoDetencion estado, CancellationToken cancellationToken = default);
    Task<IEnumerable<Detencion>> GetDetencionesAbiertasAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Detencion>> GetDetencionesPorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default);
    Task<IEnumerable<Detencion>> GetDetencionesTurnoActualAsync(int lineaId, int turnoId, DateOnly fecha, CancellationToken cancellationToken = default);
    Task<string> GenerarCodigoDetencionAsync(CancellationToken cancellationToken = default);
    Task<(IEnumerable<Detencion> Items, int TotalCount)> GetPaginadoAsync(
        int page,
        int pageSize,
        int? lineaId = null,
        int? equipoId = null,
        EstadoDetencion? estado = null,
        int? tipoFallaId = null,
        int? detalleFallaId = null,
        int? turnoId = null,
        DateOnly? fechaDesde = null,
        DateOnly? fechaHasta = null,
        string? busqueda = null,
        string? ordenarPor = null,
        bool ordenDescendente = true,
        CancellationToken cancellationToken = default);
}
