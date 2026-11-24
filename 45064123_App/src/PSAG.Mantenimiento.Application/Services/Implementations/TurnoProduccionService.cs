using PSAG.Mantenimiento.Domain.Permisos;
using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

/// <summary>
/// Turno de producción (T-798 etapa 2, docs/diseno/T-798_modelo_produccion.md 2.3 y 2.6). Rehace T-783 sobre el
/// evento turno: no hay MaquinistasTurno; el operador es un tramo de PRD_TurnosProduccionOperadores.
/// <para>Decisiones (no estaban especificadas):</para>
/// <list type="bullet">
/// <item>Turno automático (docs/diseno/T-798 "Turno de máquina vs. sesión del operario"): el TURNO es de la máquina. El job
/// lo abre a la hora de inicio si la línea está programada (origen SISTEMA, "sin operador", con el producto HEREDADO del
/// turno anterior como supuesto) y lo cierra a la hora de fin exacta (FinReal = FinProgramado). Al cerrarlo, los tramos de
/// PRODUCTO se cierran en FinReal, pero el tramo del OPERADOR sigue (su sesión): durante la tolerancia del turno
/// (ABM_Turnos.ToleranciaCierreOperadorMinutos) puede declarar lo suyo; vencida, el job le cierra el tramo (Hasta = FinReal).
/// Las paradas de cámara que siguen abiertas se cortan en el fin del turno (<see cref="ContinuarDetencionAsync"/>).</item>
/// <item>Cierre al abrir el siguiente (el job no corrió, "lo pisa"): FinReal = el menor entre ahora y FinProgramado.</item>
/// <item>Un turno CERRADO / CERRADO_AUTOMATICO no se reabre desde el login (lo corrige el supervisor).</item>
/// <item>Si lo abre un SUPERVISOR, no queda él como operador: el operador se carga con "Corregir operador".</item>
/// <item>AsegurarTurnoAsync: InicioReal = instante del dato que lo pidió; si el turno ya terminó, se crea
/// CERRADO_AUTOMATICO con FinReal = FinProgramado. Si existe (en cualquier estado) se devuelve tal cual.</item>
/// <item>SIN_APERTURA (histórico, ya no se genera): si alguien lo abre pasa a ABIERTO con el origen y el usuario de quien lo abre.</item>
/// </list>
/// </summary>
public class TurnoProduccionService : ITurnoProduccionService
{
    /// <summary>Usuario de auditoría de lo que crea o cierra el sistema (job, ingesta).</summary>
    public const string UsuarioSistema = "SISTEMA";

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeZoneInfo _zonaPlanta;
    private readonly TurnosProduccionOpciones _opciones;
    private readonly ILogger<TurnoProduccionService> _logger;
    private readonly Func<DateTimeOffset> _ahora;

    /// <summary>Transacción en curso de este servicio (las operaciones se anidan: abrir → cerrar el anterior → asegurar el siguiente).</summary>
    private int _nivelTransaccion;

    /// <summary>Permisos configurables del usuario de la request (T-802); null fuera de una request (tests, jobs): regla por rol.</summary>
    private IPermisosUsuarioActual? _permisos;

    public TurnoProduccionService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta, TurnosProduccionOpciones opciones,
        ILogger<TurnoProduccionService> logger, IPermisosUsuarioActual? permisos = null)
        : this(unitOfWork, zonaPlanta, opciones, logger, () => DateTimeOffset.UtcNow)
    {
        _permisos = permisos;
    }

    /// <summary>
    /// T-793 / T-802: "quién corrige un turno" es el permiso MODIFICACION de "Turnos de producción". El alcance por línea
    /// sigue en <see cref="TurnoProduccionReglas.PuedeCorregir(bool, IEnumerable{string}, IReadOnlyCollection{int}, int)"/>.
    /// </summary>
    private Task<bool> PermisoCorregirAsync(IReadOnlyCollection<string> roles, CancellationToken cancellationToken) =>
        _permisos.PermiteAsync(Opciones.ProduccionTurnos, AccionPermiso.Modificar,
            () => TurnoProduccionReglas.TienePermisoCorregirPorRol(roles), cancellationToken);

    /// <summary>Con reloj inyectable, para los tests.</summary>
    public TurnoProduccionService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta, TurnosProduccionOpciones opciones,
        ILogger<TurnoProduccionService> logger, Func<DateTimeOffset> ahora)
    {
        _unitOfWork = unitOfWork;
        _zonaPlanta = zonaPlanta;
        _opciones = opciones;
        _logger = logger;
        _ahora = ahora;
    }

    // ===================== Login del operario =====================

    public async Task<PropuestaTurnoProduccionDto> ObtenerPropuestaAsync(
        string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default) =>
        await ObtenerPropuestaAsync(usuarioId, roles, false, cancellationToken);

    public async Task<PropuestaTurnoProduccionDto> ObtenerPropuestaAsync(
        string usuarioId, IReadOnlyCollection<string> roles, bool obligadoATurno, CancellationToken cancellationToken = default)
    {
        var propuesta = new PropuestaTurnoProduccionDto { ObligadoATurno = obligadoATurno };
        if (!roles.Contains(RolesSistema.Operario) && !obligadoATurno)
            return propuesta;

        // T-802: un solo turno abierto por operario a la vez.
        var miTurno = await OtroTurnoAbiertoAsync(usuarioId, null, cancellationToken);
        propuesta.MiTurnoProduccionId = miTurno?.TurnoProduccionId;
        propuesta.MiLineaNombre = miTurno?.Linea?.Nombre;
        // Turno automático: su turno anterior, si todavía está en la tolerancia para declarar lo suyo.
        if (await ObtenerSesionEnToleranciaAsync(usuarioId, cancellationToken) is { } sesion)
            propuesta.MiSesionEnTolerancia = await SesionDtoAsync(sesion, cancellationToken);

        var turnos = (await _unitOfWork.Turnos.GetActivosAsync(cancellationToken)).ToList();
        var actual = TurnoProduccionReglas.TurnoQueContiene(turnos, HoraPlanta.Ahora(_zonaPlanta, _ahora()).DateTime);
        if (actual is not var (turno, fecha))
        {
            // Sin turno de planta a esta hora no hay nada que tomar (el obligado a turno se queda con el aviso).
            if (obligadoATurno && miTurno == null)
            {
                propuesta.Aviso = TurnoProduccionReglas.MensajeSinTurno;
                propuesta.Preguntar = true;
            }
            return propuesta;
        }
        propuesta.TurnoId = turno.TurnoId;
        propuesta.TurnoCodigo = turno.Codigo;
        propuesta.TurnoNombre = turno.Nombre;
        propuesta.FechaProduccion = fecha;

        var habilitaciones = await _unitOfWork.OperadoresLinea.FindAsync(a => a.UsuarioId == usuarioId, cancellationToken);
        var lineas = (await _unitOfWork.Lineas.GetAllAsync(cancellationToken)).Where(l => l.Activo).ToDictionary(l => l.LineaId);
        var lineasUsuario = habilitaciones.Select(a => a.LineaId).Distinct().Where(lineas.ContainsKey)
            .Select(id => lineas[id]).OrderBy(l => l.Orden).ThenBy(l => l.Nombre).ToList();
        if (lineasUsuario.Count == 0)
        {
            propuesta.Aviso = TurnoProduccionReglas.MensajeSinLineas;
            propuesta.Preguntar = true;
            return propuesta;
        }

        foreach (var linea in lineasUsuario)
        {
            var tp = await _unitOfWork.TurnosProduccion.GetPorClaveAsync(linea.LineaId, turno.TurnoId, fecha, cancellationToken);
            var operador = tp == null ? null : TurnoProduccionReglas.OperadorVigente(tp.Operadores);
            var producto = tp == null ? null : TurnoProduccionReglas.ProductoVigente(tp.Productos);
            var ocupado = miTurno != null && miTurno.LineaId != linea.LineaId;
            var productos = await ProductosHabilitadosAsync(linea.LineaId, fecha, cancellationToken);
            propuesta.Lineas.Add(new OpcionTurnoProduccionDto
            {
                LineaId = linea.LineaId,
                LineaNombre = linea.Nombre,
                TurnoProduccionId = tp?.TurnoProduccionId,
                Estado = tp?.Estado.ToString(),
                PuedeTomar = (tp == null || !tp.EstaCerrado) && !ocupado,
                MotivoNoPuedeTomar = ocupado
                    ? TurnoProduccionReglas.MensajeOtroTurnoAbierto(miTurno!.Linea?.Nombre ?? "otra línea", miTurno.Turno?.Nombre ?? "turno")
                    : null,
                SoyOperadorActual = operador?.OperadorUsuarioId == usuarioId,
                OperadorActualUsuarioId = operador?.OperadorUsuarioId,
                OperadorActualDesde = HoraPlanta.En(operador?.Desde, _zonaPlanta),
                ProductoActualId = producto?.ProductoMaestroId,
                ProductoActualNombre = producto?.ProductoMaestro?.Nombre,
                ProductoSinConfirmar = producto?.SinConfirmar == true,
                SinOperador = tp is { EstaAbierto: true } && operador == null,
                SinObjetivos = productos.Count == 0,
                Productos = productos
            });
        }

        // Turno automático: el que ya es el operador igual tiene que elegir el producto (o confirmar el heredado).
        static bool FaltaProducto(OpcionTurnoProduccionDto o) =>
            o.Productos.Count > 0 && (o.ProductoActualId == null || o.ProductoSinConfirmar);

        // Se pregunta si hay una línea para tomar, o si ya es el operador pero falta elegir o confirmar el producto.
        propuesta.Preguntar = propuesta.Lineas.Any(o => o.PuedeTomar && (!o.SoyOperadorActual || FaltaProducto(o)));
        // El obligado a turno sin turno propio no sigue de largo: se queda en la pantalla (aunque no tenga nada para tomar).
        if (obligadoATurno && miTurno == null)
            propuesta.Preguntar = true;
        // Con turno propio en una línea no se le ofrece tomar otra: sólo se pregunta si falta el producto del suyo.
        if (miTurno != null)
            propuesta.Preguntar = propuesta.Lineas.Any(o => o.SoyOperadorActual && o.PuedeTomar && FaltaProducto(o));
        return propuesta;
    }

    public async Task<Result<AperturaTurnoProduccionDto>> AbrirAsync(
        int lineaId, string usuarioId, OrigenAperturaTurno origen, int? productoMaestroId, bool relevar,
        IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default)
    {
        if (origen == OrigenAperturaTurno.SISTEMA)
            return Result<AperturaTurnoProduccionDto>.Failure("VALIDATION", "El sistema abre turnos con AsegurarTurnoAsync, no desde la pantalla.");

        var linea = await _unitOfWork.Lineas.GetByIdAsync(lineaId, cancellationToken);
        if (linea == null || !linea.Activo)
            return Result<AperturaTurnoProduccionDto>.Failure("NOT_FOUND", "La línea no existe o no está activa.");

        if (origen == OrigenAperturaTurno.LOGIN)
        {
            if (await _unitOfWork.OperadoresLinea.GetByUsuarioYLineaAsync(usuarioId, lineaId, cancellationToken) == null)
                return Result<AperturaTurnoProduccionDto>.Failure("FORBIDDEN",
                    $"No estás habilitado en {linea.Nombre}: no podés tomar su turno. Pedile al administrador que te habilite.");
        }
        else if (!TurnoProduccionReglas.PuedeCorregir(await PermisoCorregirAsync(roles, cancellationToken), roles, lineasAsignadas, lineaId))
        {
            return Result<AperturaTurnoProduccionDto>.Failure("FORBIDDEN", $"No podés abrir el turno de {linea.Nombre}.");
        }

        var ahora = _ahora();
        var turnos = (await _unitOfWork.Turnos.GetActivosAsync(cancellationToken)).ToList();
        if (TurnoProduccionReglas.TurnoQueContiene(turnos, HoraPlanta.Ahora(_zonaPlanta, ahora).DateTime) is not var (turno, fecha))
            return Result<AperturaTurnoProduccionDto>.Failure("VALIDATION", TurnoProduccionReglas.MensajeSinTurno);

        // T-802: el turno lo declara el operario al abrirlo (es el turno en curso por hora) y no puede tener dos abiertos.
        if (origen == OrigenAperturaTurno.LOGIN)
        {
            if (await OtroTurnoAbiertoAsync(usuarioId, lineaId, cancellationToken) is { } otroAbierto)
                return Result<AperturaTurnoProduccionDto>.Failure("CONFLICT",
                    TurnoProduccionReglas.MensajeOtroTurnoAbierto(otroAbierto.Linea?.Nombre ?? "otra línea", otroAbierto.Turno?.Nombre ?? "turno"));
        }

        // Turno automático (C.10): el operario entra con producto (confirma el heredado o elige otro). Excepción: la línea
        // no tiene productos con objetivo vigentes → entra igual (la máquina trabaja) y queda la alerta para el supervisor.
        var habilitados = await ProductosHabilitadosAsync(lineaId, fecha, cancellationToken);
        var sinObjetivos = habilitados.Count == 0;
        if (origen == OrigenAperturaTurno.LOGIN && productoMaestroId == null && !sinObjetivos)
            return Result<AperturaTurnoProduccionDto>.Failure(CodigoProductoRequerido, TurnoProduccionReglas.MensajeProductoRequerido);

        ParametroLineaProducto? parametro = null;
        if (productoMaestroId is { } productoId)
        {
            parametro = await ParametroHabilitadoAsync(lineaId, productoId, fecha, cancellationToken);
            if (parametro == null)
                return Result<AperturaTurnoProduccionDto>.Failure("VALIDATION", MensajeProductoNoHabilitado(linea.Nombre));
        }

        var tp = await _unitOfWork.TurnosProduccion.GetPorClaveAsync(lineaId, turno.TurnoId, fecha, cancellationToken);
        if (tp is { EstaCerrado: true })
            return Result<AperturaTurnoProduccionDto>.Failure("VALIDATION",
                $"El {turno.Nombre} de {linea.Nombre} ya está cerrado. Si hay que corregirlo, pedíselo al supervisor.");

        var resultado = new AperturaTurnoProduccionDto();
        if (sinObjetivos)
            resultado.Alerta = TurnoProduccionReglas.MensajeSinObjetivos(linea.Nombre) + " Avisale al supervisor: podés trabajar igual.";

        // Relevo sin confirmar: no se cambia nada, el cliente tiene que preguntarle al usuario.
        if (origen == OrigenAperturaTurno.LOGIN && tp is { EstaAbierto: true }
            && TurnoProduccionReglas.OperadorVigente(tp.Operadores) is { } otro && otro.OperadorUsuarioId != usuarioId && !relevar)
        {
            resultado.RequiereConfirmarRelevo = true;
            resultado.ReemplazadoUsuarioId = otro.OperadorUsuarioId;
            resultado.Turno = Mapear(tp, sinObjetivos);
            return Result<AperturaTurnoProduccionDto>.Success(resultado);
        }

        TurnoProduccion? anteriorCerrado = null;
        var habiaOperador = false;
        await EnTransaccionAsync(async () =>
        {
            if (tp == null || tp.Estado == EstadoTurnoProduccion.SIN_APERTURA)
            {
                // "Lo pisa" (B.7): si el turno anterior de la línea sigue abierto (el job no corrió), se cierra a su hora.
                anteriorCerrado = await CerrarAbiertoDeLaLineaAsync(lineaId, tp?.TurnoProduccionId, ahora, turno, fecha, cancellationToken);
                resultado.TurnoAnteriorCerradoId = anteriorCerrado?.TurnoProduccionId;
                if (tp == null)
                {
                    tp = await NuevoTurnoAsync(lineaId, turno, fecha, turnos, EstadoTurnoProduccion.ABIERTO, origen, usuarioId, ahora, cancellationToken);
                    await _unitOfWork.TurnosProduccion.AddAsync(tp, cancellationToken);
                }
                else
                {
                    tp.Estado = EstadoTurnoProduccion.ABIERTO;
                    tp.OrigenApertura = origen;
                    tp.InicioReal = ahora;
                    tp.AbiertoPorUsuarioId = usuarioId;
                    tp.ModificadoPorUsuarioId = usuarioId;
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            if (origen == OrigenAperturaTurno.LOGIN)
            {
                var vigente = TurnoProduccionReglas.OperadorVigente(tp.Operadores);
                habiaOperador = vigente != null;
                if (vigente?.OperadorUsuarioId == usuarioId)
                {
                    resultado.YaEraOperador = true;
                }
                else
                {
                    // Entrar a un turno termina la sesión que le quedara en tolerancia en otro turno ya cerrado.
                    await TerminarSesionesEnToleranciaAsync(usuarioId, tp.TurnoProduccionId, cancellationToken);
                    if (vigente != null)
                    {
                        vigente.Hasta = ahora;
                        resultado.ReemplazadoUsuarioId = vigente.OperadorUsuarioId;
                    }
                    tp.Operadores.Add(NuevoTramoOperador(usuarioId, ahora, OrigenTramoOperador.LOGIN, usuarioId, ahora, null));
                    // T-802: se toca el turno para que dos relevos simultáneos choquen por RowVersion (además del índice
                    // único de un solo operador vigente por turno, script 40).
                    tp.ModificadoPorUsuarioId = usuarioId;
                }
            }

            if (parametro != null)
            {
                var vigente = TurnoProduccionReglas.ProductoVigente(tp.Productos);
                if (vigente?.ProductoMaestroId == parametro.ProductoMaestroId)
                {
                    // Confirma el heredado: sin tramo nuevo (C.10).
                    if (vigente.SinConfirmar)
                    {
                        vigente.ConfirmadoPorUsuarioId = usuarioId;
                        vigente.FechaHoraConfirmacion = ahora;
                        tp.ModificadoPorUsuarioId = usuarioId;
                        resultado.ProductoConfirmado = true;
                    }
                }
                else
                {
                    // Cambia: tramo nuevo desde ahora. Si el anterior era un heredado sin confirmar, queda como supuesto
                    // (lo ve el supervisor) para lo que va del turno.
                    var desde = vigente != null && vigente.Desde >= ahora ? vigente.Desde.AddSeconds(1) : ahora;
                    if (vigente != null)
                        vigente.Hasta = desde;
                    tp.Productos.Add(NuevoTramoProducto(parametro, desde, null,
                        vigente == null ? OrigenTramoProducto.LOGIN : OrigenTramoProducto.CAMBIO, usuarioId, ahora, null));
                }
            }

            await RegistrarProduccionTramosCerradosAsync(tp, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Las paradas que seguían abiertas en el turno que se cerró continúan en el siguiente (D.12).
            if (anteriorCerrado != null)
                await ContinuarDetencionesDelTurnoAsync(anteriorCerrado, ahora, cancellationToken);
            return true;
        }, cancellationToken);

        _logger.LogInformation(
            "Turno de producción {TurnoProduccionId} ({Turno} {Fecha}) de la línea {LineaId} tomado por {UsuarioId} ({Origen}); había operador: {HabiaOperador}, releva a: {Reemplazado}, anterior cerrado: {Anterior}, producto confirmado: {Confirmado}",
            tp!.TurnoProduccionId, turno.Codigo, fecha, lineaId, usuarioId, origen, habiaOperador, resultado.ReemplazadoUsuarioId, resultado.TurnoAnteriorCerradoId, resultado.ProductoConfirmado);

        resultado.Turno = Mapear((await _unitOfWork.TurnosProduccion.GetConDetalleAsync(tp.TurnoProduccionId, cancellationToken))!, sinObjetivos);
        return Result<AperturaTurnoProduccionDto>.Success(resultado);
    }

    /// <summary>Código del 400 cuando el operario quiere entrar sin producto (extensión "codigo" del ProblemDetails).</summary>
    public const string CodigoProductoRequerido = "PRODUCTO_REQUERIDO";

    /// <summary>
    /// T-802: turno ABIERTO de otra línea (no <paramref name="exceptoLineaId"/>) en el que el usuario es el operador
    /// vigente, o null. Los de la misma línea no cuentan: abrir el turno en curso cierra el anterior de la línea.
    /// </summary>
    private async Task<TurnoProduccion?> OtroTurnoAbiertoAsync(string usuarioId, int? exceptoLineaId, CancellationToken cancellationToken) =>
        (await _unitOfWork.TurnosProduccion.GetAbiertosDeOperadorAsync(usuarioId, cancellationToken))
            .FirstOrDefault(t => t.LineaId != exceptoLineaId);

    public async Task<TurnoProduccionDto?> ObtenerMioAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        var mio = (await _unitOfWork.TurnosProduccion.GetAbiertosDeOperadorAsync(usuarioId, cancellationToken)).FirstOrDefault();
        if (mio == null)
            return null;
        var detalle = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(mio.TurnoProduccionId, cancellationToken);
        if (detalle == null)
            return null;
        var sinObjetivos = (await ProductosHabilitadosAsync(detalle.LineaId, detalle.FechaProduccion, cancellationToken)).Count == 0;
        return Mapear(detalle, sinObjetivos);
    }

    // ===================== Sesión del operario (tolerancia) =====================

    public async Task<TurnoProduccion?> ObtenerSesionEnToleranciaAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        var ahora = _ahora();
        return (await _unitOfWork.TurnosProduccion.GetCerradosConSesionDeOperadorAsync(usuarioId, cancellationToken))
            .Where(t => TurnoProduccionReglas.SesionEnTolerancia(t, ToleranciaDe(t), ahora))
            .OrderByDescending(t => t.FinReal ?? t.FinProgramado)
            .FirstOrDefault();
    }

    public async Task<SesionToleranciaDto?> ObtenerSesionEnToleranciaDtoAsync(string usuarioId, CancellationToken cancellationToken = default) =>
        await ObtenerSesionEnToleranciaAsync(usuarioId, cancellationToken) is { } t ? await SesionDtoAsync(t, cancellationToken) : null;

    private async Task<SesionToleranciaDto> SesionDtoAsync(TurnoProduccion t, CancellationToken cancellationToken)
    {
        var pendientes = (await _unitOfWork.Detenciones.FindAsync(
                d => d.TurnoProduccionId == t.TurnoProduccionId && d.EstadoActual == EstadoDetencion.PENDIENTE_JUSTIFICACION, cancellationToken))
            .Count();
        return new SesionToleranciaDto
        {
            TurnoProduccionId = t.TurnoProduccionId,
            LineaId = t.LineaId,
            LineaNombre = t.Linea?.Nombre ?? string.Empty,
            TurnoNombre = t.Turno?.Nombre ?? string.Empty,
            FechaProduccion = t.FechaProduccion,
            FinTurno = HoraPlanta.En(t.FinReal ?? t.FinProgramado, _zonaPlanta)!.Value,
            ToleranciaHasta = HoraPlanta.En(TurnoProduccionReglas.FinTolerancia(t, ToleranciaDe(t)), _zonaPlanta)!.Value,
            PendientesDeclarar = pendientes
        };
    }

    /// <summary>Tolerancia del operario del turno (ABM_Turnos); sin el turno de planta cargado, la por defecto.</summary>
    private static int ToleranciaDe(TurnoProduccion t) => t.Turno?.ToleranciaCierreOperadorMinutos ?? Turno.ToleranciaOperadorPorDefecto;

    public async Task<Result<TurnoProduccionDto>> TerminarMiTurnoAsync(long turnoProduccionId, string usuarioId, CancellationToken cancellationToken = default)
    {
        var tp = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(turnoProduccionId, cancellationToken);
        if (tp == null)
            return NoEncontrado();
        var vigente = TurnoProduccionReglas.OperadorVigente(tp.Operadores);
        if (vigente == null || vigente.OperadorUsuarioId != usuarioId)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", "No sos el operador de este turno (ya lo terminaste o te relevaron).");
        if (!tp.EstaAbierto && !tp.EstaCerrado)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", "El turno no está abierto.");

        var ahora = _ahora();
        // Antes de la hora de fin: termina SU tramo; el turno de máquina sigue abierto "sin operador" (B.8). Después (en la
        // tolerancia): el tramo termina donde terminó el turno de máquina.
        var hasta = tp.EstaAbierto ? ahora : tp.FinReal ?? tp.FinProgramado;
        vigente.Hasta = hasta > vigente.Desde ? hasta : vigente.Desde;
        tp.ModificadoPorUsuarioId = usuarioId;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Turno de producción {TurnoProduccionId}: {UsuarioId} terminó su turno (turno de máquina {Estado})",
            turnoProduccionId, usuarioId, tp.Estado);
        return await RecargarAsync(turnoProduccionId, cancellationToken);
    }

    /// <summary>Cierra los tramos que el usuario tenga vigentes en turnos ya cerrados (al entrar a otro turno).</summary>
    private async Task TerminarSesionesEnToleranciaAsync(string usuarioId, long exceptoTurnoProduccionId, CancellationToken cancellationToken)
    {
        foreach (var t in await _unitOfWork.TurnosProduccion.GetCerradosConSesionDeOperadorAsync(usuarioId, cancellationToken))
        {
            if (t.TurnoProduccionId == exceptoTurnoProduccionId)
                continue;
            var detalle = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(t.TurnoProduccionId, cancellationToken);
            if (detalle == null)
                continue;
            foreach (var o in detalle.Operadores.Where(o => !o.Anulado && o.Hasta == null && o.OperadorUsuarioId == usuarioId))
            {
                var fin = detalle.FinReal ?? detalle.FinProgramado;
                o.Hasta = fin > o.Desde ? fin : o.Desde;
            }
        }
    }

    // ===================== Turno pedido por un dato (ingesta, detenciones) =====================

    public Task<TurnoProduccion?> AsegurarTurnoAsync(int lineaId, DateTimeOffset instante, CancellationToken cancellationToken = default) =>
        AsegurarTurnoAsync(lineaId, instante, false, cancellationToken);

    public async Task<TurnoProduccion?> AsegurarTurnoAsync(int lineaId, DateTimeOffset instante, bool soloSiProgramado, CancellationToken cancellationToken = default)
    {
        var turnos = (await _unitOfWork.Turnos.GetActivosAsync(cancellationToken)).ToList();
        if (TurnoProduccionReglas.TurnoQueContiene(turnos, HoraPlanta.Ahora(_zonaPlanta, instante).DateTime) is not var (turno, fecha))
            return null;

        var existente = await _unitOfWork.TurnosProduccion.GetPorClaveAsync(lineaId, turno.TurnoId, fecha, cancellationToken);
        if (existente != null)
            return existente;

        var linea = await _unitOfWork.Lineas.GetByIdAsync(lineaId, cancellationToken);
        if (linea == null)
            return null;

        var ahora = _ahora();
        var (_, fin) = TurnoProduccionReglas.Ventana(fecha, turno, _zonaPlanta);
        var estado = TurnoProduccionReglas.EstadoAlAsegurar(fin, ahora);

        try
        {
            var nuevo = await EnTransaccionAsync(async () =>
            {
                TurnoProduccion? anterior = null;
                if (estado == EstadoTurnoProduccion.ABIERTO)
                    anterior = await CerrarAbiertoDeLaLineaAsync(lineaId, null, ahora, turno, fecha, cancellationToken);

                var t = await NuevoTurnoAsync(lineaId, turno, fecha, turnos, estado, OrigenAperturaTurno.SISTEMA, null, instante, cancellationToken);
                if (soloSiProgramado && t.MinutosProgramados <= 0)
                    return null;
                if (estado == EstadoTurnoProduccion.CERRADO_AUTOMATICO)
                {
                    t.FinReal = t.FinProgramado;
                    t.ObservacionCierre = "Creado por el sistema para un dato de un turno ya terminado.";
                }
                // Turno automático (A.3): un turno programado que abre el sistema hereda el producto del anterior (supuesto).
                if (t.MinutosProgramados > 0)
                    await HeredarProductoAsync(t, ahora, cancellationToken);
                await _unitOfWork.TurnosProduccion.AddAsync(t, cancellationToken);
                await RegistrarProduccionTramosCerradosAsync(t, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (anterior != null)
                    await ContinuarDetencionesDelTurnoAsync(anterior, ahora, cancellationToken);
                return t;
            }, cancellationToken);

            if (nuevo != null)
                _logger.LogInformation(
                    "Turno de producción {TurnoProduccionId} ({Turno} {Fecha}) de la línea {LineaId} creado por el sistema en {Estado} para un dato de {Instante}",
                    nuevo.TurnoProduccionId, turno.Codigo, fecha, lineaId, estado, instante);
            return nuevo;
        }
        catch (Exception ex)
        {
            // Puede ser que otro proceso (otra ingesta, el login) lo haya creado al mismo tiempo (índice único):
            // quien llama reintenta y lo encuentra.
            _logger.LogWarning(ex, "No se pudo crear el turno de producción de la línea {LineaId} para {Instante}", lineaId, instante);
            throw;
        }
    }

    /// <summary>
    /// Turno automático (A.3): agrega al turno nuevo (todavía sin tramos) un tramo HEREDADO con el producto con que terminó
    /// el turno anterior de la línea, con snapshot del parámetro VIGENTE a la fecha del turno nuevo y sin confirmar. Si el
    /// anterior no tenía producto, o el producto ya no tiene parámetro vigente y habilitado en la línea, no hereda.
    /// </summary>
    private async Task HeredarProductoAsync(TurnoProduccion nuevo, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        if (nuevo.Productos.Any(p => !p.Anulado))
            return;
        var anterior = await _unitOfWork.TurnosProduccion.GetAnteriorDeLineaAsync(
            nuevo.LineaId, nuevo.FechaProduccion, nuevo.InicioProgramado, _opciones.DiasHerenciaProducto, cancellationToken);
        var ultimo = anterior?.Productos.Where(p => !p.Anulado).OrderBy(p => p.Desde).ThenBy(p => p.TurnoProduccionProductoId).LastOrDefault();
        if (ultimo == null)
            return;
        var parametro = await ParametroHabilitadoAsync(nuevo.LineaId, ultimo.ProductoMaestroId, nuevo.FechaProduccion, cancellationToken);
        if (parametro == null)
        {
            _logger.LogInformation("Línea {LineaId}: el producto {ProductoMaestroId} del turno anterior ya no tiene parámetro vigente: el turno nuevo queda sin producto",
                nuevo.LineaId, ultimo.ProductoMaestroId);
            return;
        }
        var desde = nuevo.InicioReal is { } ir && ir < nuevo.InicioProgramado ? ir : nuevo.InicioProgramado;
        DateTimeOffset? hasta = nuevo.EstaCerrado ? nuevo.FinReal ?? nuevo.FinProgramado : null;
        var tramo = NuevoTramoProducto(parametro, desde, hasta, OrigenTramoProducto.HEREDADO, UsuarioSistema, ahora,
            $"Heredado del {anterior!.Turno?.Nombre ?? "turno"} del {anterior.FechaProduccion:dd/MM/yyyy}: sin confirmar.");
        nuevo.Productos.Add(tramo);
    }

    // ===================== Operación del turno =====================

    public async Task<Result<TurnoProduccionDto>> CambiarProductoAsync(
        long turnoProduccionId, CambiarProductoTurnoRequest request, string usuarioId,
        IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default)
    {
        var tp = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(turnoProduccionId, cancellationToken);
        if (tp == null)
            return NoEncontrado();
        if (!TurnoProduccionReglas.PuedeOperar(await PermisoCorregirAsync(roles, cancellationToken), roles, lineasAsignadas, tp.LineaId))
            return Result<TurnoProduccionDto>.Failure("FORBIDDEN", $"No podés operar el turno de {tp.Linea.Nombre}.");
        if (!tp.EstaAbierto)
            return Result<TurnoProduccionDto>.Failure("VALIDATION",
                "Sólo se cambia el producto de un turno abierto. Para un turno cerrado, usá \"Corregir producto\".");

        var parametro = await ParametroHabilitadoAsync(tp.LineaId, request.ProductoMaestroId, tp.FechaProduccion, cancellationToken);
        if (parametro == null)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", MensajeProductoNoHabilitado(tp.Linea.Nombre));

        var ahora = _ahora();
        var desde = request.Desde ?? ahora;
        if (desde > ahora)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", "La hora del cambio no puede ser posterior a ahora.");
        if (desde < tp.InicioProgramado.AddMinutes(-_opciones.ToleranciaCierreAutomaticoMinutos))
            return Result<TurnoProduccionDto>.Failure("VALIDATION", "La hora del cambio tiene que caer dentro del turno.");

        var vigente = TurnoProduccionReglas.ProductoVigente(tp.Productos);
        if (vigente?.ProductoMaestroId == request.ProductoMaestroId)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", "La línea ya está con ese producto.");
        if (vigente != null && desde <= vigente.Desde)
            return Result<TurnoProduccionDto>.Failure("VALIDATION",
                $"La hora del cambio tiene que ser posterior al inicio del producto actual ({HoraPlanta.En(vigente.Desde, _zonaPlanta):HH:mm}).");
        var ignorar = vigente == null ? null : new[] { vigente.TurnoProduccionProductoId };
        if (TurnoProduccionReglas.SeSuperpone(TurnoProduccionReglas.Intervalos(tp.Productos), desde, null, ignorar))
            return Result<TurnoProduccionDto>.Failure("VALIDATION", "El cambio se superpone con otro tramo de producto del turno.");

        if (vigente != null)
            vigente.Hasta = desde;
        tp.Productos.Add(NuevoTramoProducto(parametro, desde, null, OrigenTramoProducto.CAMBIO, usuarioId, ahora, null));
        tp.ModificadoPorUsuarioId = usuarioId;
        await RegistrarProduccionTramosCerradosAsync(tp, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Turno de producción {TurnoProduccionId}: cambio de producto a {ProductoMaestroId} desde {Desde} por {UsuarioId}",
            turnoProduccionId, request.ProductoMaestroId, desde, usuarioId);
        return await RecargarAsync(turnoProduccionId, cancellationToken);
    }

    public async Task<Result<TurnoProduccionDto>> RelevarAsync(
        long turnoProduccionId, string usuarioId, IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas,
        CancellationToken cancellationToken = default)
    {
        var tp = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(turnoProduccionId, cancellationToken);
        if (tp == null)
            return NoEncontrado();
        if (await _unitOfWork.OperadoresLinea.GetByUsuarioYLineaAsync(usuarioId, tp.LineaId, cancellationToken) == null)
            return Result<TurnoProduccionDto>.Failure("FORBIDDEN", $"No estás habilitado en {tp.Linea.Nombre}: no podés tomar su turno.");
        if (!tp.EstaAbierto)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", "El turno no está abierto.");

        var vigente = TurnoProduccionReglas.OperadorVigente(tp.Operadores);
        if (vigente?.OperadorUsuarioId == usuarioId)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", "Ya sos el operador de este turno.");
        if (await OtroTurnoAbiertoAsync(usuarioId, tp.LineaId, cancellationToken) is { } otro)
            return Result<TurnoProduccionDto>.Failure("CONFLICT",
                TurnoProduccionReglas.MensajeOtroTurnoAbierto(otro.Linea?.Nombre ?? "otra línea", otro.Turno?.Nombre ?? "turno"));

        var ahora = _ahora();
        if (vigente != null)
            vigente.Hasta = ahora;
        tp.Operadores.Add(NuevoTramoOperador(usuarioId, ahora, OrigenTramoOperador.LOGIN, usuarioId, ahora, null));
        tp.ModificadoPorUsuarioId = usuarioId;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Turno de producción {TurnoProduccionId}: {UsuarioId} releva a {Anterior}", turnoProduccionId, usuarioId, vigente?.OperadorUsuarioId);
        return await RecargarAsync(turnoProduccionId, cancellationToken);
    }

    public async Task<Result<TurnoProduccionDto>> CerrarAsync(
        long turnoProduccionId, string? observacion, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default)
    {
        var tp = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(turnoProduccionId, cancellationToken);
        if (tp == null)
            return NoEncontrado();
        if (!TurnoProduccionReglas.PuedeOperar(await PermisoCorregirAsync(roles, cancellationToken), roles, lineasAsignadas, tp.LineaId))
            return Result<TurnoProduccionDto>.Failure("FORBIDDEN", $"No podés cerrar el turno de {tp.Linea.Nombre}.");
        if (!tp.EstaAbierto)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", "El turno no está abierto.");
        if (observacion?.Trim().Length > TurnoProduccion.ObservacionMaxima)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", $"La observación no puede pasar de {TurnoProduccion.ObservacionMaxima} caracteres.");

        var ahora = _ahora();
        // Turno automático (B.8): el turno de MÁQUINA no se cierra antes de hora (el operario termina SU turno; el cierre
        // anticipado por supervisor no se pidió). Pasada la hora (el job no corrió), se cierra a la hora de fin.
        if (ahora < tp.FinProgramado)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", TurnoProduccionReglas.MensajeTurnoNoTerminado);
        await EnTransaccionAsync(async () =>
        {
            Cerrar(tp, EstadoTurnoProduccion.CERRADO, tp.FinProgramado, usuarioId, string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim());
            await RegistrarProduccionTramosCerradosAsync(tp, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await ContinuarDetencionesDelTurnoAsync(tp, ahora, cancellationToken);
            return true;
        }, cancellationToken);

        _logger.LogInformation("Turno de producción {TurnoProduccionId} cerrado a mano por {UsuarioId}", turnoProduccionId, usuarioId);
        return await RecargarAsync(turnoProduccionId, cancellationToken);
    }

    // ===================== Correcciones del supervisor (insert-only) =====================

    public async Task<Result<TurnoProduccionDto>> CorregirOperadorAsync(
        long turnoProduccionId, CorregirOperadorTurnoRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default)
    {
        var tp = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(turnoProduccionId, cancellationToken);
        if (tp == null)
            return NoEncontrado();
        if (!TurnoProduccionReglas.PuedeCorregir(await PermisoCorregirAsync(roles, cancellationToken), roles, lineasAsignadas, tp.LineaId))
            return Result<TurnoProduccionDto>.Failure("FORBIDDEN", $"No podés corregir el turno de {tp.Linea.Nombre}.");
        if (string.IsNullOrWhiteSpace(request.OperadorUsuarioId)
            || await _unitOfWork.OperadoresLinea.GetByUsuarioYLineaAsync(request.OperadorUsuarioId, tp.LineaId, cancellationToken) == null)
            return Result<TurnoProduccionDto>.Failure("VALIDATION",
                $"El operador elegido no está habilitado en {tp.Linea.Nombre} (Configuración → Usuarios).");
        // T-802: si el tramo queda vigente (sin "hasta") en un turno abierto, el operador no puede estar vigente en otro.
        if (tp.EstaAbierto && request.Hasta == null
            && await OtroTurnoAbiertoAsync(request.OperadorUsuarioId, tp.LineaId, cancellationToken) is { } otroAbierto)
            return Result<TurnoProduccionDto>.Failure("CONFLICT",
                TurnoProduccionReglas.MensajeOperadorConOtroTurno(otroAbierto.Linea?.Nombre ?? "otra línea", otroAbierto.Turno?.Nombre ?? "turno"));

        var ahora = _ahora();
        var motivo = TurnoProduccionReglas.MotivoTramoInvalido(tp, request.Desde, request.Hasta, ahora,
            _opciones.ToleranciaCierreAutomaticoMinutos, request.Motivo, TurnoProduccionOperador.MotivoMaximo);
        if (motivo != null)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", motivo);

        TurnoProduccionOperador? anulado = null;
        if (request.AnulaTramoId is { } anulaId)
        {
            anulado = tp.Operadores.FirstOrDefault(o => o.TurnoProduccionOperadorId == anulaId && !o.Anulado);
            if (anulado == null)
                return Result<TurnoProduccionDto>.Failure("VALIDATION", "El tramo que querés reemplazar no existe o ya fue corregido.");
        }

        // Un tramo nuevo sin "hasta" releva al vigente (que se cierra a la hora desde), como en el login.
        var vigente = TurnoProduccionReglas.OperadorVigente(tp.Operadores);
        var cerrarVigente = request.Hasta == null && vigente != null && vigente != anulado && vigente.Desde < request.Desde;
        var intervalos = TurnoProduccionReglas.Intervalos(tp.Operadores)
            .Select(i => cerrarVigente && i.Id == vigente!.TurnoProduccionOperadorId ? (i.Id, i.Desde, (DateTimeOffset?)request.Desde, i.Anulado) : i)
            .ToList();
        var ignorar = anulado == null ? null : new[] { anulado.TurnoProduccionOperadorId };
        if (TurnoProduccionReglas.SeSuperpone(intervalos, request.Desde, request.Hasta, ignorar))
            return Result<TurnoProduccionDto>.Failure("VALIDATION",
                "El tramo se superpone con otro operador del turno. Elegí qué tramo reemplaza o ajustá las horas.");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (cerrarVigente)
                vigente!.Hasta = request.Desde;
            // Índice único de un solo operador vigente por turno (T-802): mientras se inserta el reemplazo, el tramo anulado
            // se marca anulado por sí mismo (misma transacción); enseguida queda anulado por el nuevo.
            if (anulado != null)
                anulado.AnuladoPorId = anulado.TurnoProduccionOperadorId;
            var nuevo = NuevoTramoOperador(request.OperadorUsuarioId, request.Desde, OrigenTramoOperador.SUPERVISOR, usuarioId, ahora, request.Motivo!.Trim());
            nuevo.Hasta = request.Hasta;
            tp.Operadores.Add(nuevo);
            tp.ModificadoPorUsuarioId = usuarioId;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (anulado != null)
            {
                anulado.AnuladoPorId = nuevo.TurnoProduccionOperadorId;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        _logger.LogInformation("Turno de producción {TurnoProduccionId}: operador corregido por {UsuarioId} ({Operador}, anula {Anulado})",
            turnoProduccionId, usuarioId, request.OperadorUsuarioId, request.AnulaTramoId);
        return await RecargarAsync(turnoProduccionId, cancellationToken);
    }

    public async Task<Result<TurnoProduccionDto>> CorregirProductoAsync(
        long turnoProduccionId, CorregirProductoTurnoRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default)
    {
        var tp = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(turnoProduccionId, cancellationToken);
        if (tp == null)
            return NoEncontrado();
        if (!TurnoProduccionReglas.PuedeCorregir(await PermisoCorregirAsync(roles, cancellationToken), roles, lineasAsignadas, tp.LineaId))
            return Result<TurnoProduccionDto>.Failure("FORBIDDEN", $"No podés corregir el turno de {tp.Linea.Nombre}.");

        var parametro = await ParametroHabilitadoAsync(tp.LineaId, request.ProductoMaestroId, tp.FechaProduccion, cancellationToken);
        if (parametro == null)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", MensajeProductoNoHabilitado(tp.Linea.Nombre));

        var ahora = _ahora();
        var motivo = TurnoProduccionReglas.MotivoTramoInvalido(tp, request.Desde, request.Hasta, ahora,
            _opciones.ToleranciaCierreAutomaticoMinutos, request.Motivo, TurnoProduccionProducto.MotivoMaximo);
        if (motivo != null)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", motivo);

        TurnoProduccionProducto? anulado = null;
        if (request.AnulaTramoId is { } anulaId)
        {
            anulado = tp.Productos.FirstOrDefault(p => p.TurnoProduccionProductoId == anulaId && !p.Anulado);
            if (anulado == null)
                return Result<TurnoProduccionDto>.Failure("VALIDATION", "El tramo que querés reemplazar no existe o ya fue corregido.");
        }

        var vigente = TurnoProduccionReglas.ProductoVigente(tp.Productos);
        var cerrarVigente = request.Hasta == null && vigente != null && vigente != anulado && vigente.Desde < request.Desde;
        var intervalos = TurnoProduccionReglas.Intervalos(tp.Productos)
            .Select(i => cerrarVigente && i.Id == vigente!.TurnoProduccionProductoId ? (i.Id, i.Desde, (DateTimeOffset?)request.Desde, i.Anulado) : i)
            .ToList();
        var ignorar = anulado == null ? null : new[] { anulado.TurnoProduccionProductoId };
        if (TurnoProduccionReglas.SeSuperpone(intervalos, request.Desde, request.Hasta, ignorar))
            return Result<TurnoProduccionDto>.Failure("VALIDATION",
                "El tramo se superpone con otro producto del turno. Elegí qué tramo reemplaza o ajustá las horas.");

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (cerrarVigente)
            {
                vigente!.Hasta = request.Desde;
                // Turno automático (C.11): el supervisor revisó el heredado (lo dejó hasta la hora del cambio): queda confirmado.
                if (vigente.SinConfirmar)
                {
                    vigente.ConfirmadoPorUsuarioId = usuarioId;
                    vigente.FechaHoraConfirmacion = ahora;
                }
            }
            var nuevo = NuevoTramoProducto(parametro, request.Desde, request.Hasta, OrigenTramoProducto.SUPERVISOR, usuarioId, ahora, request.Motivo!.Trim());
            tp.Productos.Add(nuevo);
            tp.ModificadoPorUsuarioId = usuarioId;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (anulado != null)
            {
                anulado.AnuladoPorId = nuevo.TurnoProduccionProductoId;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            await RegistrarProduccionTramosCerradosAsync(tp, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        _logger.LogInformation("Turno de producción {TurnoProduccionId}: producto corregido por {UsuarioId} ({Producto}, anula {Anulado})",
            turnoProduccionId, usuarioId, request.ProductoMaestroId, request.AnulaTramoId);
        return await RecargarAsync(turnoProduccionId, cancellationToken);
    }

    // ===================== Producción oficial (T-798 etapa 5) =====================

    public async Task<Result<ProduccionTurnoDto>> RegistrarProduccionAsync(
        long turnoProduccionId, RegistrarProduccionTurnoRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default)
    {
        static Result<ProduccionTurnoDto> Invalido(string mensaje) => Result<ProduccionTurnoDto>.Failure("VALIDATION", mensaje);

        var tp = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(turnoProduccionId, cancellationToken);
        if (tp == null)
            return Result<ProduccionTurnoDto>.Failure("NOT_FOUND", "El turno de producción no existe.");
        if (!TurnoProduccionReglas.PuedeCorregir(await PermisoCorregirAsync(roles, cancellationToken), roles, lineasAsignadas, tp.LineaId))
            return Result<ProduccionTurnoDto>.Failure("FORBIDDEN", $"No podés registrar la producción del turno de {tp.Linea.Nombre}.");

        var magnitud = request.Magnitud?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!MagnitudProduccion.Todas.Contains(magnitud))
            return Invalido("La magnitud tiene que ser MANGAS, ROLLOS, PAQUETES, BOLSONES o PALLETS.");
        var accion = request.Accion?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!AccionProduccionTurno.Todas.Contains(accion))
            return Invalido("La acción tiene que ser VALIDAR, CORREGIR o MANUAL.");

        var tramo = tp.Productos.FirstOrDefault(p => p.TurnoProduccionProductoId == request.TurnoProduccionProductoId && !p.Anulado);
        if (tramo == null)
            return Invalido("El tramo de producto no es de este turno o ya fue corregido.");
        if (tramo.Hasta == null)
            return Invalido("El tramo todavía está abierto: la producción se registra cuando cierra (cambio de producto o cierre del turno).");

        var motivo = string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim();
        if (motivo?.Length > ProduccionTurno.MotivoMaximo)
            return Invalido($"El motivo no puede pasar de {ProduccionTurno.MotivoMaximo} caracteres.");
        decimal? cantidad = request.Cantidad is { } c ? Math.Round(c, 1, MidpointRounding.AwayFromZero) : null;
        if (accion != AccionProduccionTurno.Validar && (cantidad is not { } valor || valor < 0 || valor > 99_999_999_999.9m))
            return Invalido("Indicá la cantidad (cero o más).");

        var vigente = ProduccionOficialReglas.Vigente(tramo.Producciones, magnitud);
        var ahora = _ahora();
        var nueva = new ProduccionTurno
        {
            Magnitud = magnitud,
            EstadoValidacion = EstadoValidacionProduccion.VALIDADA,
            ValidadoPorUsuarioId = usuarioId,
            FechaHoraValidacion = ahora,
            RegistradoPorUsuarioId = usuarioId,
            FechaHoraRegistro = ahora
        };
        switch (accion)
        {
            case AccionProduccionTurno.Validar:
                if (vigente == null || vigente.Origen != OrigenProduccionTurno.CAMARA)
                    return Invalido("No hay producción de cámara para validar en este tramo: cargala a mano.");
                if (vigente.EstadoValidacion == EstadoValidacionProduccion.VALIDADA)
                    return Invalido("La producción de este tramo ya está validada.");
                if (vigente.EstadoValidacion == EstadoValidacionProduccion.SIN_DATOS || vigente.CantidadCamara == null)
                    return Invalido("La cámara no tuvo datos suficientes en el tramo: corregí con el dato del contador.");
                nueva.Origen = OrigenProduccionTurno.CAMARA;
                nueva.CantidadOficial = vigente.CantidadCamara;
                nueva.Motivo = motivo;
                break;
            case AccionProduccionTurno.Corregir:
                if (vigente == null || vigente.Origen == OrigenProduccionTurno.MANUAL)
                    return Invalido("No hay producción de cámara para corregir en este tramo: cargala a mano.");
                if (motivo == null)
                    return Invalido("Escribí el motivo de la corrección.");
                nueva.Origen = OrigenProduccionTurno.CAMARA_CORREGIDA;
                nueva.CantidadOficial = cantidad;
                nueva.Motivo = motivo;
                break;
            default:
                nueva.Origen = OrigenProduccionTurno.MANUAL;
                nueva.CantidadOficial = cantidad;
                nueva.Motivo = motivo;
                break;
        }
        if (nueva.Origen != OrigenProduccionTurno.MANUAL)
        {
            nueva.CantidadCamara = vigente!.CantidadCamara;
            nueva.MinutosConDatos = vigente.MinutosConDatos;
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            tramo.Producciones.Add(nueva);
            tp.ModificadoPorUsuarioId = usuarioId;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (vigente != null)
            {
                vigente.AnuladoPorId = nueva.ProduccionTurnoId;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        _logger.LogInformation(
            "Turno de producción {TurnoProduccionId}, tramo {TramoId}: producción {Magnitud} {Accion} = {Cantidad} por {UsuarioId} (anula {Anulada})",
            turnoProduccionId, tramo.TurnoProduccionProductoId, magnitud, accion, nueva.CantidadOficial, usuarioId, vigente?.ProduccionTurnoId);
        return Result<ProduccionTurnoDto>.Success(ProduccionTurnoDto.De(nueva, _zonaPlanta));
    }

    /// <summary>
    /// Al cerrar tramos de producto (cambio de producto, cierre manual o automático, corrección): por cada tramo cerrado
    /// y cada magnitud con cámara oficial en la línea que todavía no tenga fila, crea la producción de la cámara
    /// (Σ minutos del tramo, minutos con datos, PENDIENTE o SIN_DATOS según la cobertura; ProduccionOficialReglas).
    /// Sin cámara para la magnitud no se crea fila ("sin datos"). Agrega las filas al turno (quien llama guarda).
    /// </summary>
    private async Task RegistrarProduccionTramosCerradosAsync(TurnoProduccion tp, CancellationToken cancellationToken)
    {
        var cerrados = tp.Productos.Where(p => !p.Anulado && p.Hasta is { } h && h > p.Desde).ToList();
        if (cerrados.Count == 0)
            return;
        var camaras = (await _unitOfWork.Camaras.GetOficialesAsync(cancellationToken))
            .Where(c => c.LineaId == tp.LineaId && c.Magnitud != null)
            .ToList();
        var faltantes = cerrados
            .SelectMany(t => camaras.Where(c => !t.Producciones.Any(p => p.Magnitud == c.Magnitud)).Select(c => (Tramo: t, Camara: c)))
            .ToList();
        if (faltantes.Count == 0)
            return;

        var minutos = await _unitOfWork.ConteosCamaraMinuto.GetMinutosAsync(
            faltantes.Select(f => f.Camara.CamaraId).Distinct().ToList(),
            faltantes.Min(f => f.Tramo.Desde), faltantes.Max(f => f.Tramo.Hasta!.Value), cancellationToken);
        var ahora = _ahora();
        foreach (var (tramo, camara) in faltantes)
        {
            var hasta = tramo.Hasta!.Value;
            var (cantidad, conDatos) = ProduccionOficialReglas.ConteoDelTramo(minutos, camara.CamaraId, tramo.Desde, hasta);
            var fila = ProduccionOficialReglas.FilaDeCamara(cantidad, conDatos,
                ProduccionOficialReglas.MinutosTramo(tramo.Desde, hasta), _opciones.CoberturaMinimaProduccion);
            tramo.Producciones.Add(new ProduccionTurno
            {
                Magnitud = camara.Magnitud!,
                CantidadCamara = fila.CantidadCamara,
                MinutosConDatos = fila.MinutosConDatos,
                CantidadOficial = fila.CantidadOficial,
                Origen = OrigenProduccionTurno.CAMARA,
                EstadoValidacion = fila.Estado,
                RegistradoPorUsuarioId = UsuarioSistema,
                FechaHoraRegistro = ahora
            });
            _logger.LogInformation(
                "Turno de producción {TurnoProduccionId}, tramo {TramoId}: producción de cámara {Camara} {Magnitud} = {Cantidad} ({Minutos} min con datos, {Estado})",
                tp.TurnoProduccionId, tramo.TurnoProduccionProductoId, camara.Codigo, camara.Magnitud, fila.CantidadCamara, conDatos, fila.Estado);
        }
    }

    // ===================== Job: turno automático =====================

    public async Task<ResultadoJobTurnos> ProcesarAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        var cerrados = await CerrarVencidosAsync(ahora, cancellationToken);
        var (abiertos, terminados) = await AbrirProgramadosAsync(ahora, cancellationToken);
        var sesiones = await VencerSesionesAsync(ahora, cancellationToken);
        return new ResultadoJobTurnos(cerrados, abiertos, terminados, sesiones);
    }

    public async Task<int> CerrarVencidosAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        var vencidos = (await _unitOfWork.TurnosProduccion.GetAbiertosAsync(cancellationToken))
            .Where(t => TurnoProduccionReglas.Vencido(t, ahora))
            .ToList();
        foreach (var t in vencidos)
        {
            await EnTransaccionAsync(async () =>
            {
                // B.5: el turno de máquina cierra a la hora exacta; el tramo del operador sigue (tolerancia).
                Cerrar(t, EstadoTurnoProduccion.CERRADO_AUTOMATICO, t.FinProgramado, null,
                    $"Cerrado automáticamente a la hora de fin ({HoraPlanta.En(t.FinProgramado, _zonaPlanta):HH:mm}).");
                await RegistrarProduccionTramosCerradosAsync(t, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await ContinuarDetencionesDelTurnoAsync(t, ahora, cancellationToken);
                return true;
            }, cancellationToken);
        }
        if (vencidos.Count > 0)
            _logger.LogInformation("Turnos de producción cerrados a la hora de fin: {Ids}", string.Join(", ", vencidos.Select(v => v.TurnoProduccionId)));
        return vencidos.Count;
    }

    public async Task<(int Abiertos, int CreadosTerminados)> AbrirProgramadosAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        var hoy = HoraPlanta.Hoy(_zonaPlanta, ahora);
        var desde = hoy.AddDays(-1);
        var turnos = (await _unitOfWork.Turnos.GetActivosAsync(cancellationToken)).ToList();
        var lineas = (await _unitOfWork.Lineas.GetActivasAsync(cancellationToken)).ToList();
        if (turnos.Count == 0 || lineas.Count == 0)
            return (0, 0);

        var existentes = (await _unitOfWork.TurnosProduccion.GetPorFechasAsync(desde, hoy, null, cancellationToken))
            .Select(t => (t.LineaId, t.TurnoId, t.FechaProduccion))
            .ToHashSet();
        var programacion = (await _unitOfWork.CalendariosLinea.GetActivosAsync(null, cancellationToken)).ToList();
        var excepciones = (await _unitOfWork.TiemposProgramadosDia.GetPorRangoAsync(desde, hoy, null, cancellationToken)).ToList();

        var abiertos = 0;
        var terminados = 0;
        foreach (var linea in lineas)
        {
            var progLinea = programacion.Where(p => p.LineaId == linea.LineaId).ToList();
            var excLinea = excepciones.Where(e => e.LineaId == linea.LineaId).ToList();
            // En orden cronológico: cada turno hereda el producto del anterior (también los que se crean ya terminados).
            var candidatos = new List<(DateOnly Fecha, Turno Turno, DateTimeOffset Inicio, DateTimeOffset Fin)>();
            for (var fecha = desde; fecha <= hoy; fecha = fecha.AddDays(1))
            {
                foreach (var turno in turnos)
                {
                    var (inicio, fin) = TurnoProduccionReglas.Ventana(fecha, turno, _zonaPlanta);
                    var minutos = TurnoProduccionReglas.MinutosProgramados(fecha, turno, turnos, progLinea, excLinea);
                    if (TurnoProduccionReglas.CorrespondeAperturaAutomatica(minutos, inicio, ahora, existentes.Contains((linea.LineaId, turno.TurnoId, fecha))))
                        candidatos.Add((fecha, turno, inicio, fin));
                }
            }

            foreach (var (fecha, turno, inicio, fin) in candidatos.OrderBy(c => c.Inicio))
            {
                var terminado = ahora >= fin;
                var creado = await EnTransaccionAsync(async () =>
                {
                    // Por si otro proceso lo creó mientras tanto (login, ingesta).
                    if (await _unitOfWork.TurnosProduccion.GetPorClaveAsync(linea.LineaId, turno.TurnoId, fecha, cancellationToken) != null)
                        return (TurnoProduccion?)null;
                    TurnoProduccion? anterior = null;
                    if (!terminado)
                        anterior = await CerrarAbiertoDeLaLineaAsync(linea.LineaId, null, ahora, turno, fecha, cancellationToken);

                    var t = await NuevoTurnoAsync(linea.LineaId, turno, fecha, turnos,
                        terminado ? EstadoTurnoProduccion.CERRADO_AUTOMATICO : EstadoTurnoProduccion.ABIERTO,
                        OrigenAperturaTurno.SISTEMA, null, inicio, cancellationToken);
                    if (terminado)
                    {
                        t.FinReal = t.FinProgramado;
                        t.ObservacionCierre = "Turno programado creado por el sistema ya terminado (el job no estaba corriendo).";
                    }
                    await HeredarProductoAsync(t, ahora, cancellationToken);
                    await _unitOfWork.TurnosProduccion.AddAsync(t, cancellationToken);
                    await RegistrarProduccionTramosCerradosAsync(t, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    if (anterior != null)
                        await ContinuarDetencionesDelTurnoAsync(anterior, ahora, cancellationToken);
                    return t;
                }, cancellationToken);
                if (creado == null)
                    continue;
                existentes.Add((linea.LineaId, turno.TurnoId, fecha));
                if (terminado) terminados++; else abiertos++;
                _logger.LogInformation(
                    "Turno de producción {TurnoProduccionId} ({Turno} {Fecha}) de la línea {Linea} abierto por el sistema ({Estado}), producto heredado: {Producto}",
                    creado.TurnoProduccionId, turno.Codigo, fecha, linea.Nombre, creado.Estado,
                    creado.Productos.FirstOrDefault()?.ProductoMaestroId.ToString() ?? "ninguno");
            }
        }
        return (abiertos, terminados);
    }

    public async Task<int> VencerSesionesAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        var vencidas = 0;
        foreach (var t in await _unitOfWork.TurnosProduccion.GetCerradosConOperadorVigenteAsync(cancellationToken))
        {
            if (TurnoProduccionReglas.SesionEnTolerancia(t, ToleranciaDe(t), ahora))
                continue;
            var fin = t.FinReal ?? t.FinProgramado;
            foreach (var o in t.Operadores.Where(o => !o.Anulado && o.Hasta == null))
            {
                o.Hasta = fin > o.Desde ? fin : o.Desde;
                vencidas++;
            }
        }
        if (vencidas > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Sesiones de operario vencidas (fin de la tolerancia): {Cantidad}", vencidas);
        }
        return vencidas;
    }

    // ===================== Detenciones que cruzan de turno (D.12) =====================

    /// <summary>Corta las paradas de cámara que seguían abiertas al cerrar el turno de máquina <paramref name="cerrado"/>.</summary>
    private async Task<int> ContinuarDetencionesDelTurnoAsync(TurnoProduccion cerrado, DateTimeOffset ahora, CancellationToken cancellationToken)
    {
        var abiertas = await _unitOfWork.Detenciones.GetPendientesAbiertasDeCamaraAsync([cerrado.TurnoProduccionId], cancellationToken);
        if (abiertas.Count == 0)
            return 0;
        // Las paradas que ya llegaron cerradas (CAM_Paradas) no siguen: terminaron (la del fin incierto no trae arranque).
        var terminadas = await _unitOfWork.ParadasCamara.GetIdentificadoresExistentesAsync(
            abiertas.Select(d => RaizParada(d.IdentificadorParadaCamara)).OfType<string>().ToList(), cancellationToken);
        var partes = 0;
        foreach (var d in abiertas.Where(d => RaizParada(d.IdentificadorParadaCamara) is not { } raiz || !terminadas.Contains(raiz)))
        {
            // Sólo si la cámara siguió mandando datos hasta cerca del fin del turno: una parada "abierta" de una cámara que
            // dejó de mandar no se arrastra de turno en turno (la alerta SIN_DATOS ya avisa).
            var camara = d.CamaraId is { } cid ? await _unitOfWork.Camaras.GetByIdAsync(cid, cancellationToken) : null;
            if (camara?.UltimoDatoRecibido is { } ultimo && ultimo < cerrado.FinProgramado.AddMinutes(-MinutosCamaraVivaParaContinuar))
            {
                _logger.LogWarning("La detención {Codigo} sigue sin arranque pero la cámara {Camara} no manda datos desde {Ultimo}: no se continúa en el turno siguiente",
                    d.Codigo, camara.Codigo, ultimo);
                continue;
            }
            partes += await ContinuarDetencionAsync(d, null, null, ahora, cancellationToken);
        }
        return partes;
    }

    public async Task<int> ContinuarDetencionAsync(
        Detencion detencion, DateTimeOffset? finParada, DateTimeOffset? arranque, DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        // La última parte de la cadena (si ya se cortó antes).
        var parte = detencion;
        while (await _unitOfWork.Detenciones.GetContinuacionAsync(parte.DetencionId, cancellationToken) is { } siguiente)
            parte = siguiente;

        var creadas = 0;
        await EnTransaccionAsync(async () =>
        {
            while (parte.EsPendienteDeclaracion && parte.OrigenRegistro == OrigenRegistroDetencion.CAMARA
                   && parte.FechaHoraArranqueDetectada == null && parte.FechaHoraParada is { } desde)
            {
                var turno = await _unitOfWork.TurnosProduccion.GetByIdAsync(parte.TurnoProduccionId, cancellationToken);
                if (turno == null)
                    break;
                var corte = turno.FinProgramado;
                if (finParada is { } fin && fin <= corte)
                {
                    // Terminó dentro de este turno: el arranque (sugerencia) va a esta parte.
                    parte.FechaHoraArranqueDetectada = arranque;
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    break;
                }
                if (finParada == null && (ahora < corte || turno.EstaAbierto))
                    break;   // sigue parada dentro del turno (todavía no cerró)

                // Cruza el fin del turno: esta parte termina en el corte y sigue otra en el turno siguiente.
                parte.FechaHoraArranqueDetectada = corte;
                parte.ModificadoPorUsuarioId = DetencionCamaraReglas.UsuarioSistemaCamara;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                if (desde >= corte)
                    break;
                var siguienteTurno = await AsegurarTurnoAsync(turno.LineaId, corte, soloSiProgramado: true, cancellationToken);
                if (siguienteTurno == null || siguienteTurno.TurnoProduccionId == turno.TurnoProduccionId)
                {
                    _logger.LogInformation("La parada de la detención {Codigo} sigue después del fin del turno pero el turno siguiente no está programado: no se continúa",
                        parte.Codigo);
                    break;
                }
                parte = await NuevaParteAsync(parte, siguienteTurno, corte, cancellationToken);
                creadas++;
            }
            return true;
        }, cancellationToken);
        return creadas;
    }

    private async Task<Detencion> NuevaParteAsync(Detencion anterior, TurnoProduccion turno, DateTimeOffset desde, CancellationToken cancellationToken)
    {
        var raiz = RaizParada(anterior.IdentificadorParadaCamara);
        var numero = NumeroParte(anterior.IdentificadorParadaCamara) + 1;
        var operador = TurnoProduccionReglas.OperadorALaHora(turno.Operadores, desde);
        var sistema = DetencionCamaraReglas.UsuarioSistemaCamara;
        var desdePlanta = HoraPlanta.En(desde, _zonaPlanta)!.Value;
        var parte = new Detencion
        {
            Codigo = await _unitOfWork.Detenciones.GenerarCodigoDetencionAsync(cancellationToken),
            LineaId = anterior.LineaId,
            TurnoProduccionId = turno.TurnoProduccionId,
            OperadorUsuarioId = operador?.OperadorUsuarioId,
            FechaHoraParada = desde,
            OrigenTiempo = OrigenTiempo.CALCULADO,
            MinutosEfectivos = 0,
            Descripcion = string.Empty,
            EstadoActual = EstadoDetencion.PENDIENTE_JUSTIFICACION,
            OrigenRegistro = OrigenRegistroDetencion.CAMARA,
            IdentificadorParadaCamara = raiz == null ? null : $"{raiz}{SeparadorParte}{numero}",
            CamaraId = anterior.CamaraId,
            DetencionAnteriorId = anterior.DetencionId,
            CreadoPorUsuarioId = sistema,
            ModificadoPorUsuarioId = sistema
        };
        parte.HistorialEstados.Add(new DetencionEstadoHistorial
        {
            EstadoAnterior = null,
            EstadoNuevo = EstadoDetencion.PENDIENTE_JUSTIFICACION,
            FechaHora = _ahora(),
            UsuarioId = sistema,
            Observacion = $"Viene del turno anterior: la parada de {anterior.Codigo} siguió después del cambio de turno. Esta parte empieza a las {desdePlanta:HH:mm} y se declara aparte.",
            OrigenAccion = "CAMARA"
        });
        await _unitOfWork.Detenciones.AddAsync(parte, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Detención {Codigo}: continúa en el turno {TurnoProduccionId} la parada de {Anterior} (desde {Desde:HH:mm})",
            parte.Codigo, turno.TurnoProduccionId, anterior.Codigo, desdePlanta);
        return parte;
    }

    /// <summary>Para cortar una parada abierta al cierre, la cámara tiene que haber mandado datos en estos últimos minutos del turno.</summary>
    public const int MinutosCamaraVivaParaContinuar = 15;

    /// <summary>Las partes de una parada que cruzó de turno llevan el identificador de la parada + "#2", "#3"... (único).</summary>
    public const char SeparadorParte = '#';

    /// <summary>Identificador de la parada de cámara (sin el número de parte).</summary>
    public static string? RaizParada(string? identificador) =>
        identificador == null ? null : identificador.Split(SeparadorParte)[0];

    private static int NumeroParte(string? identificador) =>
        identificador != null && identificador.Split(SeparadorParte) is { Length: 2 } p && int.TryParse(p[1], out var n) ? n : 1;

    /// <summary>
    /// Corre <paramref name="accion"/> en una transacción, o dentro de la que ya está abierta (abrir → cerrar el anterior →
    /// asegurar el siguiente → cortar las paradas): la de afuera confirma o deshace todo.
    /// </summary>
    private async Task<T> EnTransaccionAsync<T>(Func<Task<T>> accion, CancellationToken cancellationToken)
    {
        if (_nivelTransaccion > 0)
            return await accion();
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        _nivelTransaccion++;
        try
        {
            var resultado = await accion();
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return resultado;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            _nivelTransaccion--;
        }
    }

    // ===================== Consultas =====================

    public async Task<TurnoProduccionDto?> ObtenerActualAsync(int lineaId, CancellationToken cancellationToken = default)
    {
        var tp = await _unitOfWork.TurnosProduccion.GetAbiertoAsync(lineaId, cancellationToken);
        if (tp == null)
        {
            var turnos = (await _unitOfWork.Turnos.GetActivosAsync(cancellationToken)).ToList();
            if (TurnoProduccionReglas.TurnoQueContiene(turnos, HoraPlanta.Ahora(_zonaPlanta, _ahora()).DateTime) is var (turno, fecha))
                tp = await _unitOfWork.TurnosProduccion.GetPorClaveAsync(lineaId, turno.TurnoId, fecha, cancellationToken);
        }
        return tp == null ? null : Mapear(tp);
    }

    public async Task<TurnosProduccionDiaDto> ObtenerDiaAsync(
        DateOnly fecha, int? lineaId, IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas,
        CancellationToken cancellationToken = default)
    {
        var ahora = _ahora();
        // Líneas del listado: quien ve "Turnos de producción" (T-802), con el mismo alcance por línea que la corrección.
        var permisoVer = await _permisos.PermiteAsync(Opciones.ProduccionTurnos, AccionPermiso.Ver,
            () => TurnoProduccionReglas.TienePermisoCorregirPorRol(roles), cancellationToken);
        var lineas = (await _unitOfWork.Lineas.GetActivasAsync(cancellationToken))
            .Where(l => (lineaId == null || l.LineaId == lineaId) && TurnoProduccionReglas.PuedeCorregir(permisoVer, roles, lineasAsignadas, l.LineaId))
            .OrderBy(l => l.Orden).ThenBy(l => l.Nombre)
            .ToList();
        var turnos = (await _unitOfWork.Turnos.GetActivosAsync(cancellationToken)).OrderBy(t => t.Orden).ThenBy(t => t.HoraInicio).ToList();
        var ids = lineas.Select(l => l.LineaId).ToList();
        var registros = await _unitOfWork.TurnosProduccion.GetPorFechasAsync(fecha, fecha, ids, cancellationToken);
        var programacion = (await _unitOfWork.CalendariosLinea.GetActivosAsync(null, cancellationToken)).ToList();
        var excepciones = (await _unitOfWork.TiemposProgramadosDia.GetPorRangoAsync(fecha, fecha, null, cancellationToken)).ToList();
        var habilitaciones = (await _unitOfWork.OperadoresLinea.GetAllWithRelationsAsync(cancellationToken)).ToList();

        var dia = new TurnosProduccionDiaDto { Fecha = fecha };
        foreach (var linea in lineas)
        {
            var productos = await ProductosHabilitadosAsync(linea.LineaId, fecha, cancellationToken);
            var operadores = habilitaciones.Where(h => h.LineaId == linea.LineaId)
                .Select(h => h.UsuarioId).Distinct()
                .Select(u => new OperadorHabilitadoDto { UsuarioId = u })
                .ToList();
            var progLinea = programacion.Where(p => p.LineaId == linea.LineaId).ToList();
            var excLinea = excepciones.Where(e => e.LineaId == linea.LineaId).ToList();
            foreach (var turno in turnos)
            {
                var registro = registros.FirstOrDefault(r => r.LineaId == linea.LineaId && r.TurnoId == turno.TurnoId);
                var (inicio, fin) = registro != null
                    ? (registro.InicioProgramado, registro.FinProgramado)
                    : TurnoProduccionReglas.Ventana(fecha, turno, _zonaPlanta);
                dia.Filas.Add(new FilaTurnoProduccionDto
                {
                    LineaId = linea.LineaId,
                    LineaNombre = linea.Nombre,
                    TurnoId = turno.TurnoId,
                    TurnoCodigo = turno.Codigo,
                    TurnoNombre = turno.Nombre,
                    InicioProgramado = HoraPlanta.En(inicio, _zonaPlanta)!.Value,
                    FinProgramado = HoraPlanta.En(fin, _zonaPlanta)!.Value,
                    MinutosProgramados = registro?.MinutosProgramados
                                         ?? TurnoProduccionReglas.MinutosProgramados(fecha, turno, turnos, progLinea, excLinea),
                    EnCurso = ahora >= inicio && ahora < fin,
                    Turno = registro == null ? null : Mapear(registro, productos.Count == 0),
                    OperadoresHabilitados = operadores,
                    ProductosHabilitados = productos
                });
            }
        }
        return dia;
    }

    // ===================== Auxiliares =====================

    /// <summary>
    /// Cierra como CERRADO_AUTOMATICO el turno ABIERTO de la línea (si hay otro que no sea <paramref name="exceptoId"/>),
    /// a la hora de fin (o ahora, si todavía no llegó). Guarda (dentro de la transacción de quien abre): así el índice de
    /// un solo ABIERTO por línea no choca con el turno nuevo. Devuelve el cerrado: quien llama, después de crear el turno
    /// nuevo, corta las paradas que seguían abiertas (<see cref="ContinuarDetencionesDelTurnoAsync"/>).
    /// </summary>
    private async Task<TurnoProduccion?> CerrarAbiertoDeLaLineaAsync(int lineaId, long? exceptoId, DateTimeOffset ahora, Turno turnoNuevo, DateOnly fechaNueva, CancellationToken cancellationToken)
    {
        var abierto = await _unitOfWork.TurnosProduccion.GetAbiertoAsync(lineaId, cancellationToken);
        if (abierto == null || abierto.TurnoProduccionId == exceptoId)
            return null;
        Cerrar(abierto, EstadoTurnoProduccion.CERRADO_AUTOMATICO, Min(ahora, abierto.FinProgramado), null,
            $"Cerrado automáticamente al abrir el {turnoNuevo.Nombre} del {fechaNueva:dd/MM/yyyy}.");
        await RegistrarProduccionTramosCerradosAsync(abierto, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return abierto;
    }

    /// <summary>
    /// Cierra el turno de máquina. Los tramos de PRODUCTO terminan en FinReal; el tramo del OPERADOR no: es su sesión, que
    /// sigue durante la tolerancia (la termina él, un relevo o el job al vencer la tolerancia).
    /// </summary>
    private static void Cerrar(TurnoProduccion tp, EstadoTurnoProduccion estado, DateTimeOffset finReal, string? usuarioId, string? observacion)
    {
        tp.Estado = estado;
        tp.FinReal = finReal;
        tp.CerradoPorUsuarioId = usuarioId;
        tp.ObservacionCierre = observacion;
        tp.ModificadoPorUsuarioId = usuarioId ?? UsuarioSistema;
        foreach (var p in tp.Productos.Where(p => !p.Anulado && p.Hasta == null))
            p.Hasta = finReal > p.Desde ? finReal : p.Desde;
    }

    private async Task<TurnoProduccion> NuevoTurnoAsync(
        int lineaId, Turno turno, DateOnly fecha, IReadOnlyCollection<Turno> turnos, EstadoTurnoProduccion estado,
        OrigenAperturaTurno origen, string? usuarioId, DateTimeOffset inicioReal, CancellationToken cancellationToken)
    {
        var (inicio, fin) = TurnoProduccionReglas.Ventana(fecha, turno, _zonaPlanta);
        var programacion = await _unitOfWork.CalendariosLinea.GetActivosAsync(lineaId, cancellationToken);
        var excepciones = await _unitOfWork.TiemposProgramadosDia.GetPorRangoAsync(fecha, fecha, lineaId, cancellationToken);
        return new TurnoProduccion
        {
            LineaId = lineaId,
            TurnoId = turno.TurnoId,
            FechaProduccion = fecha,
            InicioProgramado = inicio,
            FinProgramado = fin,
            MinutosProgramados = TurnoProduccionReglas.MinutosProgramados(fecha, turno, turnos, programacion, excepciones),
            InicioReal = inicioReal,
            Estado = estado,
            OrigenApertura = origen,
            AbiertoPorUsuarioId = usuarioId,
            CreadoPorUsuarioId = usuarioId ?? UsuarioSistema,
            ModificadoPorUsuarioId = usuarioId ?? UsuarioSistema
        };
    }

    private static TurnoProduccionOperador NuevoTramoOperador(
        string operadorUsuarioId, DateTimeOffset desde, OrigenTramoOperador origen, string registradoPor, DateTimeOffset ahora, string? motivo) => new()
    {
        OperadorUsuarioId = operadorUsuarioId,
        Desde = desde,
        Origen = origen,
        RegistradoPorUsuarioId = registradoPor,
        FechaHoraRegistro = ahora,
        Motivo = motivo
    };

    /// <summary>Tramo con SNAPSHOT del parámetro vigente (objetivos, minutos de referencia) y de los metros por manga del maestro.</summary>
    private static TurnoProduccionProducto NuevoTramoProducto(
        ParametroLineaProducto parametro, DateTimeOffset desde, DateTimeOffset? hasta, OrigenTramoProducto origen,
        string usuarioId, DateTimeOffset ahora, string? motivo) => new()
    {
        ProductoMaestroId = parametro.ProductoMaestroId,
        ParametroLineaProductoId = parametro.ParametroLineaProductoId,
        MangasObjetivoTurno = parametro.MangasObjetivoTurno,
        BolsonesObjetivoTurno = parametro.BolsonesObjetivoTurno,
        MinutosTurnoReferencia = parametro.MinutosTurnoReferencia,
        MetrosPorManga = parametro.ProductoMaestro?.MetrosPorManga,
        Desde = desde,
        Hasta = hasta,
        Origen = origen,
        DeclaradoPorUsuarioId = usuarioId,
        FechaHoraRegistro = ahora,
        Motivo = motivo
    };

    /// <summary>Parámetro vigente en la fecha de producción para la línea y el producto, si está habilitado y el maestro activo.</summary>
    private async Task<ParametroLineaProducto?> ParametroHabilitadoAsync(int lineaId, int productoMaestroId, DateOnly fecha, CancellationToken cancellationToken)
    {
        var parametros = await _unitOfWork.ProductosMaestro.GetParametrosLineaAsync(lineaId, cancellationToken);
        var vigente = ParametroLineaProductoVigencia.Vigente(parametros, lineaId, productoMaestroId, fecha);
        return vigente is { Habilitado: true } && vigente.ProductoMaestro?.Activo != false ? vigente : null;
    }

    private async Task<List<ProductoHabilitadoDto>> ProductosHabilitadosAsync(int lineaId, DateOnly fecha, CancellationToken cancellationToken)
    {
        var parametros = await _unitOfWork.ProductosMaestro.GetParametrosLineaAsync(lineaId, cancellationToken);
        return ParametroLineaProductoVigencia.Vigentes(parametros, fecha)
            .Where(p => p.Habilitado && p.ProductoMaestro is { Activo: true })
            .OrderBy(p => p.ProductoMaestro.Orden).ThenBy(p => p.ProductoMaestro.Nombre)
            .Select(p => new ProductoHabilitadoDto
            {
                ProductoMaestroId = p.ProductoMaestroId,
                Codigo = p.ProductoMaestro.Codigo,
                Nombre = p.ProductoMaestro.Nombre,
                MangasObjetivoTurno = p.MangasObjetivoTurno,
                BolsonesObjetivoTurno = p.BolsonesObjetivoTurno,
                MinutosTurnoReferencia = p.MinutosTurnoReferencia
            })
            .ToList();
    }

    private static string MensajeProductoNoHabilitado(string linea) =>
        $"El producto elegido no está habilitado en {linea} (Maestros → parametrización línea-producto).";

    private static Result<TurnoProduccionDto> NoEncontrado() =>
        Result<TurnoProduccionDto>.Failure("NOT_FOUND", "El turno de producción no existe.");

    private async Task<Result<TurnoProduccionDto>> RecargarAsync(long turnoProduccionId, CancellationToken cancellationToken) =>
        Result<TurnoProduccionDto>.Success(Mapear((await _unitOfWork.TurnosProduccion.GetConDetalleAsync(turnoProduccionId, cancellationToken))!));

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private TurnoProduccionDto Mapear(TurnoProduccion tp, bool sinObjetivos = false)
    {
        var operadores = tp.Operadores.OrderBy(o => o.Desde).ThenBy(o => o.TurnoProduccionOperadorId).Select(o => new TramoOperadorDto
        {
            TurnoProduccionOperadorId = o.TurnoProduccionOperadorId,
            OperadorUsuarioId = o.OperadorUsuarioId,
            Desde = HoraPlanta.En(o.Desde, _zonaPlanta)!.Value,
            Hasta = HoraPlanta.En(o.Hasta, _zonaPlanta),
            Origen = o.Origen.ToString(),
            RegistradoPorUsuarioId = o.RegistradoPorUsuarioId,
            FechaHoraRegistro = HoraPlanta.En(o.FechaHoraRegistro, _zonaPlanta)!.Value,
            Motivo = o.Motivo,
            AnuladoPorId = o.AnuladoPorId
        }).ToList();
        var productos = tp.Productos.OrderBy(p => p.Desde).ThenBy(p => p.TurnoProduccionProductoId).Select(p => new TramoProductoDto
        {
            TurnoProduccionProductoId = p.TurnoProduccionProductoId,
            ProductoMaestroId = p.ProductoMaestroId,
            ProductoCodigo = p.ProductoMaestro?.Codigo ?? string.Empty,
            ProductoNombre = p.ProductoMaestro?.Nombre ?? string.Empty,
            Desde = HoraPlanta.En(p.Desde, _zonaPlanta)!.Value,
            Hasta = HoraPlanta.En(p.Hasta, _zonaPlanta),
            Origen = p.Origen.ToString(),
            ParametroLineaProductoId = p.ParametroLineaProductoId,
            MangasObjetivoTurno = p.MangasObjetivoTurno,
            BolsonesObjetivoTurno = p.BolsonesObjetivoTurno,
            MinutosTurnoReferencia = p.MinutosTurnoReferencia,
            MetrosPorManga = p.MetrosPorManga,
            DeclaradoPorUsuarioId = p.DeclaradoPorUsuarioId,
            FechaHoraRegistro = HoraPlanta.En(p.FechaHoraRegistro, _zonaPlanta)!.Value,
            Motivo = p.Motivo,
            AnuladoPorId = p.AnuladoPorId,
            ConfirmadoPorUsuarioId = p.ConfirmadoPorUsuarioId,
            FechaHoraConfirmacion = HoraPlanta.En(p.FechaHoraConfirmacion, _zonaPlanta),
            SinConfirmar = p.SinConfirmar
        }).ToList();

        var operadorVigente = TurnoProduccionReglas.OperadorVigente(tp.Operadores);
        var productoVigente = TurnoProduccionReglas.ProductoVigente(tp.Productos);
        var vivos = tp.Productos.Where(p => !p.Anulado).ToList();
        var sinConfirmar = vivos.Where(p => p.SinConfirmar).ToList();
        var alertas = new List<string>();
        if (vivos.Count == 0 && tp.Estado != EstadoTurnoProduccion.SIN_APERTURA)
            alertas.Add("Turno sin producto: corregilo (Corregir producto).");
        foreach (var p in sinConfirmar)
            alertas.Add($"Producto supuesto sin confirmar: {p.ProductoMaestro?.Nombre ?? "producto"} (heredado del turno anterior).");
        if (sinObjetivos)
            alertas.Add(TurnoProduccionReglas.MensajeSinObjetivos(tp.Linea?.Nombre ?? "La línea"));
        var tolerancia = ToleranciaDe(tp);
        return new TurnoProduccionDto
        {
            TurnoProduccionId = tp.TurnoProduccionId,
            LineaId = tp.LineaId,
            LineaNombre = tp.Linea?.Nombre ?? string.Empty,
            TurnoId = tp.TurnoId,
            TurnoCodigo = tp.Turno?.Codigo ?? string.Empty,
            TurnoNombre = tp.Turno?.Nombre ?? string.Empty,
            FechaProduccion = tp.FechaProduccion,
            InicioProgramado = HoraPlanta.En(tp.InicioProgramado, _zonaPlanta)!.Value,
            FinProgramado = HoraPlanta.En(tp.FinProgramado, _zonaPlanta)!.Value,
            MinutosProgramados = tp.MinutosProgramados,
            InicioReal = HoraPlanta.En(tp.InicioReal, _zonaPlanta),
            FinReal = HoraPlanta.En(tp.FinReal, _zonaPlanta),
            Estado = tp.Estado.ToString(),
            OrigenApertura = tp.OrigenApertura.ToString(),
            AbiertoPorUsuarioId = tp.AbiertoPorUsuarioId,
            CerradoPorUsuarioId = tp.CerradoPorUsuarioId,
            ObservacionCierre = tp.ObservacionCierre,
            ACompletar = vivos.Count == 0,
            SinOperador = tp.EstaAbierto && operadorVigente == null,
            ProductoSinConfirmar = sinConfirmar.Count > 0,
            SinObjetivos = sinObjetivos,
            Alertas = alertas,
            ToleranciaOperadorMinutos = tolerancia,
            FinToleranciaOperador = tp.EstaCerrado ? HoraPlanta.En(TurnoProduccionReglas.FinTolerancia(tp, tolerancia), _zonaPlanta) : null,
            Operadores = operadores,
            Productos = productos,
            OperadorActual = operadorVigente == null ? null : operadores.First(o => o.TurnoProduccionOperadorId == operadorVigente.TurnoProduccionOperadorId),
            ProductoActual = productoVigente == null ? null : productos.First(p => p.TurnoProduccionProductoId == productoVigente.TurnoProduccionProductoId)
        };
    }
}
