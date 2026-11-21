using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Turno de producción (T-798 etapa 2): apertura en el login del operario o por el supervisor, relevos, cambio de
/// producto, cierre manual y automático, SIN_APERTURA y correcciones del supervisor (insert-only). Los permisos
/// por línea se validan acá (roles + líneas habilitadas del usuario en ABM_OperadoresLinea).
/// </summary>
public interface ITurnoProduccionService
{
    /// <summary>Qué preguntarle al operario que acaba de entrar (pantalla "Inicio de turno"). Vacío para otros roles.</summary>
    Task<PropuestaTurnoProduccionDto> ObtenerPropuestaAsync(string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    /// <summary>
    /// Abre (o toma) el turno de la hora actual de la línea. Cierra como CERRADO_AUTOMATICO el ABIERTO anterior de la
    /// línea. Si ya está abierto, no duplica: con LOGIN agrega al usuario como operador (relevo si hay otro y
    /// <paramref name="relevar"/>; si no, devuelve RequiereConfirmarRelevo sin cambiar nada). El producto, si viene,
    /// abre el primer tramo (o cambia el vigente) con snapshot del parámetro vigente y habilitado.
    /// </summary>
    Task<Result<AperturaTurnoProduccionDto>> AbrirAsync(
        int lineaId, string usuarioId, OrigenAperturaTurno origen, int? productoMaestroId, bool relevar,
        IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default);

    /// <summary>
    /// El turno de esa línea para ese instante; si no existe, lo crea el SISTEMA (sin operador ni producto): ABIERTO si
    /// el turno no terminó (cerrando el ABIERTO anterior de la línea), CERRADO_AUTOMATICO si ya terminó. Null si ningún
    /// turno de planta cubre la hora o la línea no existe. Lo usan la ingesta de cámara y las detenciones (etapas 3 y 4).
    /// </summary>
    Task<TurnoProduccion?> AsegurarTurnoAsync(int lineaId, DateTimeOffset instante, CancellationToken cancellationToken = default);

    Task<Result<TurnoProduccionDto>> CambiarProductoAsync(
        long turnoProduccionId, CambiarProductoTurnoRequest request, string usuarioId,
        IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default);

    /// <summary>El usuario (habilitado en la línea) releva al operador vigente de un turno abierto.</summary>
    Task<Result<TurnoProduccionDto>> RelevarAsync(
        long turnoProduccionId, string usuarioId, IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas,
        CancellationToken cancellationToken = default);

    /// <summary>Cierre manual (operador o supervisor): CERRADO.</summary>
    Task<Result<TurnoProduccionDto>> CerrarAsync(
        long turnoProduccionId, string? observacion, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default);

    Task<Result<TurnoProduccionDto>> CorregirOperadorAsync(
        long turnoProduccionId, CorregirOperadorTurnoRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default);

    Task<Result<TurnoProduccionDto>> CorregirProductoAsync(
        long turnoProduccionId, CorregirProductoTurnoRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default);

    /// <summary>Cierra los ABIERTO con FinProgramado + tolerancia vencido (CERRADO_AUTOMATICO). Devuelve cuántos cerró.</summary>
    Task<int> CerrarVencidosAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default);

    /// <summary>Crea en SIN_APERTURA los turnos programados cuyo inicio + tolerancia pasó sin registro. Devuelve cuántos creó.</summary>
    Task<int> MarcarSinAperturaAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default);

    /// <summary>El turno ABIERTO de la línea, o el de la hora actual si no hay abierto; null si no hay ninguno.</summary>
    Task<TurnoProduccionDto?> ObtenerActualAsync(int lineaId, CancellationToken cancellationToken = default);

    /// <summary>Listado del supervisor: cada línea × turno de planta del día, con el registro si existe.</summary>
    Task<TurnosProduccionDiaDto> ObtenerDiaAsync(
        DateOnly fecha, int? lineaId, IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas,
        CancellationToken cancellationToken = default);
}
