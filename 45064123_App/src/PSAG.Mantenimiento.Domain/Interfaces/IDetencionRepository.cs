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

    /// <summary>
    /// Incluye las eliminadas. Sólo para acciones de administración (restaurar).
    /// </summary>
    Task<Detencion?> GetByIdIncluyendoEliminadasAsync(long detencionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Detencion>> GetByLineaAsync(int lineaId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Detencion>> GetByEstadoAsync(EstadoDetencion estado, CancellationToken cancellationToken = default);
    Task<IEnumerable<Detencion>> GetDetencionesAbiertasAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<Detencion>> GetDetencionesPorFechaAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default);
    Task<IEnumerable<Detencion>> GetDetencionesTurnoActualAsync(int lineaId, int turnoId, DateOnly fecha, CancellationToken cancellationToken = default);
    Task<string> GenerarCodigoDetencionAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetLineasConDetencionesAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detenciones que requieren análisis y todavía no están cerradas, más antiguas primero.
    /// </summary>
    Task<IEnumerable<Detencion>> GetAnalisisAbiertosAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Análisis abiertos asignados al usuario o a alguno de sus roles, más antiguos primero.
    /// </summary>
    Task<IEnumerable<Detencion>> GetAnalisisAbiertosAsignadosAAsync(string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cuenta los análisis abiertos asignados al usuario o a alguno de sus roles (mismo criterio que la lista).
    /// </summary>
    Task<int> CountAnalisisAbiertosParaAsync(string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    /// <summary>
    /// Análisis cerrados por el usuario, más recientes primero.
    /// </summary>
    Task<IEnumerable<Detencion>> GetAnalisisCerradosPorUsuarioAsync(string usuarioId, int limite, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detenciones cerradas con solución o causa cargada, para la base de soluciones.
    /// La búsqueda libre requiere que cada palabra aparezca en descripción, solución o causa.
    /// </summary>
    Task<IEnumerable<Detencion>> GetSolucionesAsync(
        string? busqueda = null,
        int? lineaId = null,
        int? equipoId = null,
        int? tipoFallaId = null,
        long? excluirDetencionId = null,
        int limite = 50,
        CancellationToken cancellationToken = default);
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
        bool? analisisPendiente = null,
        bool incluirEliminadas = false,
        string? ordenarPor = null,
        bool ordenDescendente = true,
        CancellationToken cancellationToken = default);
}
