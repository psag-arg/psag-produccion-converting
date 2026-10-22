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
    /// Como GetByIdWithDetailsAsync, más los cambios y las fotos del análisis. Sólo para el análisis de falla.
    /// </summary>
    Task<Detencion?> GetByIdConAnalisisAsync(long detencionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Una foto del análisis de la detención (sin seguimiento), o null si no existe o es de otra detención.
    /// </summary>
    Task<AnalisisAdjunto?> GetAdjuntoAnalisisAsync(long detencionId, long adjuntoId, CancellationToken cancellationToken = default);

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
    /// La búsqueda libre requiere que cada palabra aparezca en código, descripción, solución, causa o plan.
    /// </summary>
    Task<IEnumerable<Detencion>> GetSolucionesAsync(
        string? busqueda = null,
        int? lineaId = null,
        int? equipoId = null,
        int? tipoFallaId = null,
        long? excluirDetencionId = null,
        int limite = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Total de la base de soluciones con los mismos filtros que GetSolucionesAsync (sin límite).
    /// </summary>
    Task<int> ContarSolucionesAsync(
        string? busqueda = null,
        int? lineaId = null,
        int? equipoId = null,
        int? tipoFallaId = null,
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
        int? minutosMinimos = null,
        CancellationToken cancellationToken = default);
}
