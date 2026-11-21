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
/// <item>Cierre automático (vencido o al abrir el siguiente): FinReal = el menor entre ahora y FinProgramado (el
/// turno terminó a su hora aunque nadie lo cerrara). Cierre manual: FinReal = ahora. Al cerrar, los tramos
/// abiertos se cierran con Hasta = FinReal.</item>
/// <item>Un turno CERRADO / CERRADO_AUTOMATICO no se reabre desde el login (lo corrige el supervisor).</item>
/// <item>Si lo abre un SUPERVISOR, no queda él como operador: el operador se carga con "Corregir operador".</item>
/// <item>AsegurarTurnoAsync: InicioReal = instante del dato que lo pidió; si el turno ya terminó, se crea
/// CERRADO_AUTOMATICO con FinReal = FinProgramado. Si existe (en cualquier estado) se devuelve tal cual.</item>
/// <item>SIN_APERTURA: OrigenApertura = SISTEMA e InicioReal null; al abrirlo pasa a ABIERTO con el origen y el
/// usuario de quien lo abre.</item>
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

    public TurnoProduccionService(IUnitOfWork unitOfWork, TimeZoneInfo zonaPlanta, TurnosProduccionOpciones opciones,
        ILogger<TurnoProduccionService> logger)
        : this(unitOfWork, zonaPlanta, opciones, logger, () => DateTimeOffset.UtcNow)
    {
    }

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
        string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default)
    {
        var propuesta = new PropuestaTurnoProduccionDto();
        if (!roles.Contains(RolesSistema.Operario))
            return propuesta;

        var turnos = (await _unitOfWork.Turnos.GetActivosAsync(cancellationToken)).ToList();
        var actual = TurnoProduccionReglas.TurnoQueContiene(turnos, HoraPlanta.Ahora(_zonaPlanta, _ahora()).DateTime);
        if (actual is not var (turno, fecha))
            return propuesta;   // sin turno de planta a esta hora no hay nada que tomar
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
            propuesta.Lineas.Add(new OpcionTurnoProduccionDto
            {
                LineaId = linea.LineaId,
                LineaNombre = linea.Nombre,
                TurnoProduccionId = tp?.TurnoProduccionId,
                Estado = tp?.Estado.ToString(),
                PuedeTomar = tp == null || !tp.EstaCerrado,
                SoyOperadorActual = operador?.OperadorUsuarioId == usuarioId,
                OperadorActualUsuarioId = operador?.OperadorUsuarioId,
                OperadorActualDesde = HoraPlanta.En(operador?.Desde, _zonaPlanta),
                ProductoActualId = producto?.ProductoMaestroId,
                ProductoActualNombre = producto?.ProductoMaestro?.Nombre,
                Productos = await ProductosHabilitadosAsync(linea.LineaId, fecha, cancellationToken)
            });
        }

        // Se pregunta si hay una línea para tomar, o si ya es el operador pero falta elegir el producto.
        propuesta.Preguntar = propuesta.Lineas.Any(o =>
            o.PuedeTomar && (!o.SoyOperadorActual || (o.ProductoActualId == null && o.Productos.Count > 0)));
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
        else if (!TurnoProduccionReglas.PuedeCorregir(roles, lineasAsignadas, lineaId))
        {
            return Result<AperturaTurnoProduccionDto>.Failure("FORBIDDEN", $"No podés abrir el turno de {linea.Nombre}.");
        }

        var ahora = _ahora();
        var turnos = (await _unitOfWork.Turnos.GetActivosAsync(cancellationToken)).ToList();
        if (TurnoProduccionReglas.TurnoQueContiene(turnos, HoraPlanta.Ahora(_zonaPlanta, ahora).DateTime) is not var (turno, fecha))
            return Result<AperturaTurnoProduccionDto>.Failure("VALIDATION", TurnoProduccionReglas.MensajeSinTurno);

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

        // Relevo sin confirmar: no se cambia nada, el cliente tiene que preguntarle al usuario.
        if (origen == OrigenAperturaTurno.LOGIN && tp is { EstaAbierto: true }
            && TurnoProduccionReglas.OperadorVigente(tp.Operadores) is { } otro && otro.OperadorUsuarioId != usuarioId && !relevar)
        {
            resultado.RequiereConfirmarRelevo = true;
            resultado.ReemplazadoUsuarioId = otro.OperadorUsuarioId;
            resultado.Turno = Mapear(tp);
            return Result<AperturaTurnoProduccionDto>.Success(resultado);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (tp == null || tp.Estado == EstadoTurnoProduccion.SIN_APERTURA)
            {
                resultado.TurnoAnteriorCerradoId = await CerrarAbiertoDeLaLineaAsync(lineaId, tp?.TurnoProduccionId, ahora, turno, fecha, cancellationToken);
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

            var habiaOperador = false;
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
                    if (vigente != null)
                    {
                        vigente.Hasta = ahora;
                        resultado.ReemplazadoUsuarioId = vigente.OperadorUsuarioId;
                    }
                    tp.Operadores.Add(NuevoTramoOperador(usuarioId, ahora, OrigenTramoOperador.LOGIN, usuarioId, ahora, null));
                }
            }

            if (parametro != null)
            {
                var vigente = TurnoProduccionReglas.ProductoVigente(tp.Productos);
                if (vigente?.ProductoMaestroId != parametro.ProductoMaestroId)
                {
                    if (vigente != null)
                        vigente.Hasta = ahora;
                    tp.Productos.Add(NuevoTramoProducto(parametro, ahora, null,
                        vigente == null ? OrigenTramoProducto.LOGIN : OrigenTramoProducto.CAMBIO, usuarioId, ahora, null));
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Turno de producción {TurnoProduccionId} ({Turno} {Fecha}) de la línea {LineaId} tomado por {UsuarioId} ({Origen}); había operador: {HabiaOperador}, releva a: {Reemplazado}, anterior cerrado: {Anterior}",
                tp.TurnoProduccionId, turno.Codigo, fecha, lineaId, usuarioId, origen, habiaOperador, resultado.ReemplazadoUsuarioId, resultado.TurnoAnteriorCerradoId);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        resultado.Turno = Mapear((await _unitOfWork.TurnosProduccion.GetConDetalleAsync(tp.TurnoProduccionId, cancellationToken))!);
        return Result<AperturaTurnoProduccionDto>.Success(resultado);
    }

    // ===================== Turno pedido por un dato (ingesta, detenciones) =====================

    public async Task<TurnoProduccion?> AsegurarTurnoAsync(int lineaId, DateTimeOffset instante, CancellationToken cancellationToken = default)
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

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (estado == EstadoTurnoProduccion.ABIERTO)
                await CerrarAbiertoDeLaLineaAsync(lineaId, null, ahora, turno, fecha, cancellationToken);

            var nuevo = await NuevoTurnoAsync(lineaId, turno, fecha, turnos, estado, OrigenAperturaTurno.SISTEMA, null, instante, cancellationToken);
            if (estado == EstadoTurnoProduccion.CERRADO_AUTOMATICO)
            {
                nuevo.FinReal = nuevo.FinProgramado;
                nuevo.ObservacionCierre = "Creado por el sistema para un dato de un turno ya terminado.";
            }
            await _unitOfWork.TurnosProduccion.AddAsync(nuevo, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Turno de producción {TurnoProduccionId} ({Turno} {Fecha}) de la línea {LineaId} creado por el sistema en {Estado} para un dato de {Instante}",
                nuevo.TurnoProduccionId, turno.Codigo, fecha, lineaId, estado, instante);
            return nuevo;
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            // Puede ser que otro proceso (otra ingesta, el login) lo haya creado al mismo tiempo (índice único):
            // quien llama reintenta y lo encuentra.
            _logger.LogWarning(ex, "No se pudo crear el turno de producción de la línea {LineaId} para {Instante}", lineaId, instante);
            throw;
        }
    }

    // ===================== Operación del turno =====================

    public async Task<Result<TurnoProduccionDto>> CambiarProductoAsync(
        long turnoProduccionId, CambiarProductoTurnoRequest request, string usuarioId,
        IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas, CancellationToken cancellationToken = default)
    {
        var tp = await _unitOfWork.TurnosProduccion.GetConDetalleAsync(turnoProduccionId, cancellationToken);
        if (tp == null)
            return NoEncontrado();
        if (!TurnoProduccionReglas.PuedeOperar(roles, lineasAsignadas, tp.LineaId))
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
        if (!TurnoProduccionReglas.PuedeOperar(roles, lineasAsignadas, tp.LineaId))
            return Result<TurnoProduccionDto>.Failure("FORBIDDEN", $"No podés cerrar el turno de {tp.Linea.Nombre}.");
        if (!tp.EstaAbierto)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", "El turno no está abierto.");
        if (observacion?.Trim().Length > TurnoProduccion.ObservacionMaxima)
            return Result<TurnoProduccionDto>.Failure("VALIDATION", $"La observación no puede pasar de {TurnoProduccion.ObservacionMaxima} caracteres.");

        var ahora = _ahora();
        Cerrar(tp, EstadoTurnoProduccion.CERRADO, ahora, usuarioId, string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
        if (!TurnoProduccionReglas.PuedeCorregir(roles, lineasAsignadas, tp.LineaId))
            return Result<TurnoProduccionDto>.Failure("FORBIDDEN", $"No podés corregir el turno de {tp.Linea.Nombre}.");
        if (string.IsNullOrWhiteSpace(request.OperadorUsuarioId)
            || await _unitOfWork.OperadoresLinea.GetByUsuarioYLineaAsync(request.OperadorUsuarioId, tp.LineaId, cancellationToken) == null)
            return Result<TurnoProduccionDto>.Failure("VALIDATION",
                $"El operador elegido no está habilitado en {tp.Linea.Nombre} (Configuración → Maquinistas).");

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
        if (!TurnoProduccionReglas.PuedeCorregir(roles, lineasAsignadas, tp.LineaId))
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
                vigente!.Hasta = request.Desde;
            var nuevo = NuevoTramoProducto(parametro, request.Desde, request.Hasta, OrigenTramoProducto.SUPERVISOR, usuarioId, ahora, request.Motivo!.Trim());
            tp.Productos.Add(nuevo);
            tp.ModificadoPorUsuarioId = usuarioId;
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            if (anulado != null)
            {
                anulado.AnuladoPorId = nuevo.TurnoProduccionProductoId;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
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

    // ===================== Job: cierre automático y SIN_APERTURA =====================

    public async Task<int> CerrarVencidosAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        var tolerancia = _opciones.ToleranciaCierreAutomaticoMinutos;
        var vencidos = (await _unitOfWork.TurnosProduccion.GetAbiertosAsync(cancellationToken))
            .Where(t => TurnoProduccionReglas.Vencido(t, ahora, tolerancia))
            .ToList();
        foreach (var t in vencidos)
        {
            Cerrar(t, EstadoTurnoProduccion.CERRADO_AUTOMATICO, Min(ahora, t.FinProgramado), null,
                $"Cerrado automáticamente: pasó el fin del turno ({HoraPlanta.En(t.FinProgramado, _zonaPlanta):HH:mm}) más {tolerancia} min de tolerancia.");
        }
        if (vencidos.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Turnos de producción cerrados por vencimiento: {Ids}", string.Join(", ", vencidos.Select(v => v.TurnoProduccionId)));
        }
        return vencidos.Count;
    }

    public async Task<int> MarcarSinAperturaAsync(DateTimeOffset ahora, CancellationToken cancellationToken = default)
    {
        var hoy = HoraPlanta.Hoy(_zonaPlanta, ahora);
        var desde = hoy.AddDays(-1);
        var turnos = (await _unitOfWork.Turnos.GetActivosAsync(cancellationToken)).ToList();
        var lineas = (await _unitOfWork.Lineas.GetActivasAsync(cancellationToken)).ToList();
        if (turnos.Count == 0 || lineas.Count == 0)
            return 0;

        var existentes = (await _unitOfWork.TurnosProduccion.GetPorFechasAsync(desde, hoy, null, cancellationToken))
            .Select(t => (t.LineaId, t.TurnoId, t.FechaProduccion))
            .ToHashSet();
        var programacion = (await _unitOfWork.CalendariosLinea.GetActivosAsync(null, cancellationToken)).ToList();
        var excepciones = (await _unitOfWork.TiemposProgramadosDia.GetPorRangoAsync(desde, hoy, null, cancellationToken)).ToList();

        var creados = new List<TurnoProduccion>();
        foreach (var linea in lineas)
        {
            var progLinea = programacion.Where(p => p.LineaId == linea.LineaId).ToList();
            var excLinea = excepciones.Where(e => e.LineaId == linea.LineaId).ToList();
            for (var fecha = desde; fecha <= hoy; fecha = fecha.AddDays(1))
            {
                foreach (var turno in turnos)
                {
                    var (inicio, fin) = TurnoProduccionReglas.Ventana(fecha, turno, _zonaPlanta);
                    var minutos = TurnoProduccionReglas.MinutosProgramados(fecha, turno, turnos, progLinea, excLinea);
                    var existe = existentes.Contains((linea.LineaId, turno.TurnoId, fecha));
                    if (!TurnoProduccionReglas.CorrespondeSinApertura(minutos, inicio, ahora, _opciones.ToleranciaSinAperturaMinutos, existe))
                        continue;

                    creados.Add(new TurnoProduccion
                    {
                        LineaId = linea.LineaId,
                        TurnoId = turno.TurnoId,
                        FechaProduccion = fecha,
                        InicioProgramado = inicio,
                        FinProgramado = fin,
                        MinutosProgramados = minutos,
                        Estado = EstadoTurnoProduccion.SIN_APERTURA,
                        OrigenApertura = OrigenAperturaTurno.SISTEMA,
                        CreadoPorUsuarioId = UsuarioSistema,
                        ModificadoPorUsuarioId = UsuarioSistema
                    });
                }
            }
        }

        foreach (var t in creados)
            await _unitOfWork.TurnosProduccion.AddAsync(t, cancellationToken);
        if (creados.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Turnos de producción marcados SIN_APERTURA: {Cantidad}", creados.Count);
        }
        return creados.Count;
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
        var lineas = (await _unitOfWork.Lineas.GetActivasAsync(cancellationToken))
            .Where(l => (lineaId == null || l.LineaId == lineaId) && TurnoProduccionReglas.PuedeCorregir(roles, lineasAsignadas, l.LineaId))
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
                    Turno = registro == null ? null : Mapear(registro),
                    OperadoresHabilitados = operadores,
                    ProductosHabilitados = productos
                });
            }
        }
        return dia;
    }

    // ===================== Auxiliares =====================

    /// <summary>
    /// Cierra como CERRADO_AUTOMATICO el turno ABIERTO de la línea (si hay otro que no sea <paramref name="exceptoId"/>).
    /// Guarda (la llamada está dentro de la transacción de quien abre): así el índice de un solo ABIERTO por línea
    /// no choca con el turno nuevo. Devuelve el id del cerrado.
    /// </summary>
    private async Task<long?> CerrarAbiertoDeLaLineaAsync(int lineaId, long? exceptoId, DateTimeOffset ahora, Turno turnoNuevo, DateOnly fechaNueva, CancellationToken cancellationToken)
    {
        var abierto = await _unitOfWork.TurnosProduccion.GetAbiertoAsync(lineaId, cancellationToken);
        if (abierto == null || abierto.TurnoProduccionId == exceptoId)
            return null;
        Cerrar(abierto, EstadoTurnoProduccion.CERRADO_AUTOMATICO, Min(ahora, abierto.FinProgramado), null,
            $"Cerrado automáticamente al abrir el {turnoNuevo.Nombre} del {fechaNueva:dd/MM/yyyy}.");
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return abierto.TurnoProduccionId;
    }

    private static void Cerrar(TurnoProduccion tp, EstadoTurnoProduccion estado, DateTimeOffset finReal, string? usuarioId, string? observacion)
    {
        tp.Estado = estado;
        tp.FinReal = finReal;
        tp.CerradoPorUsuarioId = usuarioId;
        tp.ObservacionCierre = observacion;
        tp.ModificadoPorUsuarioId = usuarioId ?? UsuarioSistema;
        foreach (var o in tp.Operadores.Where(o => !o.Anulado && o.Hasta == null))
            o.Hasta = finReal > o.Desde ? finReal : o.Desde;
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

    private TurnoProduccionDto Mapear(TurnoProduccion tp)
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
            AnuladoPorId = p.AnuladoPorId
        }).ToList();

        var operadorVigente = TurnoProduccionReglas.OperadorVigente(tp.Operadores);
        var productoVigente = TurnoProduccionReglas.ProductoVigente(tp.Productos);
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
            ACompletar = !tp.Productos.Any(p => !p.Anulado),
            Operadores = operadores,
            Productos = productos,
            OperadorActual = operadorVigente == null ? null : operadores.First(o => o.TurnoProduccionOperadorId == operadorVigente.TurnoProduccionOperadorId),
            ProductoActual = productoVigente == null ? null : productos.First(p => p.TurnoProduccionProductoId == productoVigente.TurnoProduccionProductoId)
        };
    }
}
