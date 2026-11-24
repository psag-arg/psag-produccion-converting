using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Turno de producción (T-798 etapa 2): apertura en el login del operario o por el supervisor, relevos, cambio de
/// producto, turno automático (el job abre y cierra el turno de MÁQUINA a sus horas; la sesión del operario tiene su
/// tolerancia), paradas que cruzan de turno y correcciones del supervisor (insert-only). Los permisos
/// por línea se validan acá (roles + líneas habilitadas del usuario en ABM_OperadoresLinea).
/// </summary>
public interface ITurnoProduccionService
{
    /// <summary>Qué preguntarle al operario que acaba de entrar (pantalla "Inicio de turno"). Vacío para otros roles.</summary>
    Task<PropuestaTurnoProduccionDto> ObtenerPropuestaAsync(string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    /// <summary>
    /// T-802: igual, para quien está obligado a turno (algún rol con ObligadoATurno) aunque no sea OPERARIO; si no tiene
    /// turno propio, Preguntar queda en true (no sigue de largo).
    /// </summary>
    Task<PropuestaTurnoProduccionDto> ObtenerPropuestaAsync(string usuarioId, IReadOnlyCollection<string> roles, bool obligadoATurno, CancellationToken cancellationToken = default);

    /// <summary>T-802: el turno abierto en el que el usuario es el operador vigente (a lo sumo uno), o null.</summary>
    Task<TurnoProduccionDto?> ObtenerMioAsync(string usuarioId, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Igual, pero con <paramref name="soloSiProgramado"/> no crea un turno fuera de programación (0 min): para continuar
    /// una parada en el turno siguiente (una línea no programada no se abre sola).
    /// </summary>
    Task<TurnoProduccion?> AsegurarTurnoAsync(int lineaId, DateTimeOffset instante, bool soloSiProgramado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turno automático (D.12): corta una detención de cámara pendiente cuya parada cruzó el fin del turno de máquina. La
    /// parte de cada turno termina a la hora de fin (su arranque detectado = el corte) y sigue otra, vinculada
    /// (DetencionAnteriorId), en el turno siguiente (si está programado o ya existe). Si la parada terminó
    /// (<paramref name="finParada"/>) dentro del turno de la última parte, le pone el arranque detectado. Idempotente: parte
    /// de la última parte de la cadena. <paramref name="ahora"/> = hasta cuándo se sabe que la máquina siguió parada (el
    /// "hasta" de la parada en curso que mandó la cámara, o la hora del cierre del turno). Devuelve cuántas partes nuevas creó.
    /// </summary>
    Task<int> ContinuarDetencionAsync(Detencion detencion, DateTimeOffset? finParada, DateTimeOffset? arranque, DateTimeOffset ahora, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turno automático (B.6): el turno ya cerrado en el que el usuario sigue en su tolerancia (tramo vigente y no pasó
    /// fin + ABM_Turnos.ToleranciaCierreOperadorMinutos), con línea y turno de planta; null si no hay.
    /// </summary>
    Task<TurnoProduccion?> ObtenerSesionEnToleranciaAsync(string usuarioId, CancellationToken cancellationToken = default);

    Task<SesionToleranciaDto?> ObtenerSesionEnToleranciaDtoAsync(string usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turno automático (B.8): "Terminar mi turno". Cierra el tramo del operador (SU sesión). Antes de la hora de fin el turno
    /// de máquina sigue ABIERTO sin operador; en la tolerancia, el tramo termina donde terminó el turno.
    /// </summary>
    Task<Result<TurnoProduccionDto>> TerminarMiTurnoAsync(long turnoProduccionId, string usuarioId, CancellationToken cancellationToken = default);

    Task<Result<TurnoProduccionDto>> CambiarProductoAsync(
        long turnoProduccionId, CambiarProductoTurnoRequest request, string usuarioId,
        IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default);

    /// <summary>El usuario (habilitado en la línea) releva al operador vigente de un turno abierto.</summary>
    Task<Result<TurnoProduccionDto>> RelevarAsync(
        long turnoProduccionId, string usuarioId, IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cierre manual del turno de máquina: sólo después de la hora de fin (si el job no lo cerró): CERRADO con FinReal =
    /// FinProgramado. Antes de hora, el operario usa <see cref="TerminarMiTurnoAsync"/>.
    /// </summary>
    Task<Result<TurnoProduccionDto>> CerrarAsync(
        long turnoProduccionId, string? observacion, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default);

    Task<Result<TurnoProduccionDto>> CorregirOperadorAsync(
        long turnoProduccionId, CorregirOperadorTurnoRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default);

    Task<Result<TurnoProduccionDto>> CorregirProductoAsync(
        long turnoProduccionId, CorregirProductoTurnoRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default);

    /// <summary>
    /// Producción oficial de un tramo cerrado (T-798 etapa 5), por el supervisor: VALIDAR lo de la cámara, CORREGIRLO
    /// (con motivo) o cargarlo a MANO. Insert-only: la fila nueva anula a la vigente. Devuelve la fila nueva.
    /// </summary>
    Task<Result<ProduccionTurnoDto>> RegistrarProduccionAsync(
        long turnoProduccionId, RegistrarProduccionTurnoRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default);

    /// <summary>Una vuelta del job: <see cref="CerrarVencidosAsync"/>, <see cref="AbrirProgramadosAsync"/> y <see cref="VencerSesionesAsync"/>.</summary>
    Task<ResultadoJobTurnos> ProcesarAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turno automático (B.5): cierra los ABIERTO cuya hora de fin pasó (CERRADO_AUTOMATICO, FinReal = FinProgramado) y corta
    /// las paradas que seguían abiertas. Devuelve cuántos cerró.
    /// </summary>
    Task<int> CerrarVencidosAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turno automático (A.1): abre (SISTEMA, sin operador, producto heredado) el turno vigente de cada línea PROGRAMADA que
    /// no tenga registro; los programados de las últimas 24 h que falten (job parado) se crean ya terminados. Idempotente.
    /// </summary>
    Task<(int Abiertos, int CreadosTerminados)> AbrirProgramadosAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default);

    /// <summary>Turno automático (B.6): cierra los tramos de operador de los turnos cerrados cuya tolerancia venció.</summary>
    Task<int> VencerSesionesAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default);

    /// <summary>El turno ABIERTO de la línea, o el de la hora actual si no hay abierto; null si no hay ninguno.</summary>
    Task<TurnoProduccionDto?> ObtenerActualAsync(int lineaId, CancellationToken cancellationToken = default);

    /// <summary>Listado del supervisor: cada línea × turno de planta del día, con el registro si existe.</summary>
    Task<TurnosProduccionDiaDto> ObtenerDiaAsync(
        DateOnly fecha, int? lineaId, IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas,
        CancellationToken cancellationToken = default);
}
