using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Analisis;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

public class AnalisisService : IAnalisisService
{
    private const int LimiteCerradasPorUsuario = 50;
    private const int LimiteFallasParecidas = 3;
    private const int LimiteSoluciones = 50;
    private const int LongitudMaximaSolucion = 4000;
    private const int LongitudMaximaCausaYPlan = 2000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AnalisisService> _logger;

    public AnalisisService(IUnitOfWork unitOfWork, ILogger<AnalisisService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<AnalisisDetencionDto>> ObtenerAsync(
        long detencionId,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result<AnalisisDetencionDto>.Failure("NOT_FOUND", "Detención no encontrada.");

        return Result<AnalisisDetencionDto>.Success(await ArmarDtoAsync(detencion, usuarioId, roles, cancellationToken));
    }

    public async Task<Result<AnalisisDetencionDto>> GuardarBorradorAsync(
        long detencionId,
        GuardarAnalisisRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var cargada = await CargarEditableAsync(detencionId, usuarioId, roles, request.RowVersion, cancellationToken);
        if (cargada.IsFailure)
            return Propagar<AnalisisDetencionDto>(cargada);

        var detencion = cargada.Value!;
        var largo = ValidarLargos(request);
        if (largo != null)
            return Result<AnalisisDetencionDto>.Failure("VALIDATION", largo);

        var ahora = DateTimeOffset.UtcNow;
        if (detencion.EstadoActual == EstadoDetencion.REGISTRADA)
            CambiarEstado(detencion, EstadoDetencion.EN_SEGUIMIENTO, usuarioId, "Análisis iniciado", ahora);

        AplicarCampos(detencion, request, usuarioId, ahora);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Análisis de la detención {DetencionId} guardado como borrador por usuario {UsuarioId}",
            detencionId, usuarioId);

        return Result<AnalisisDetencionDto>.Success(await ArmarDtoAsync(detencion, usuarioId, roles, cancellationToken));
    }

    public async Task<Result<AnalisisDetencionDto>> CerrarAsync(
        long detencionId,
        GuardarAnalisisRequest request,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var cargada = await CargarEditableAsync(detencionId, usuarioId, roles, request.RowVersion, cancellationToken);
        if (cargada.IsFailure)
            return Propagar<AnalisisDetencionDto>(cargada);

        var detencion = cargada.Value!;
        var motivo = AnalisisFallaReglas.MotivoNoCerrable(request.AccionesRealizadas, request.CausaFalla)
            ?? ValidarLargos(request);
        if (motivo != null)
            return Result<AnalisisDetencionDto>.Failure("VALIDATION", motivo);

        var ahora = DateTimeOffset.UtcNow;
        CambiarEstado(detencion, EstadoDetencion.CERRADA, usuarioId, "Análisis cerrado", ahora);

        AplicarCampos(detencion, request, usuarioId, ahora);
        detencion.AnalisisPorUsuarioId = usuarioId;
        detencion.FechaHoraAnalisis = ahora;
        detencion.FechaHoraCierre = ahora;
        detencion.CerradoPorUsuarioId = usuarioId;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Análisis de la detención {DetencionId} cerrado por usuario {UsuarioId}",
            detencionId, usuarioId);

        return Result<AnalisisDetencionDto>.Success(await ArmarDtoAsync(detencion, usuarioId, roles, cancellationToken));
    }

    public async Task<Result<MisPendientesDto>> ObtenerMisPendientesAsync(
        string usuarioId,
        IReadOnlyCollection<string> roles,
        string? responsableRol = null,
        string? responsableUsuarioId = null,
        CancellationToken cancellationToken = default)
    {
        var esAdministrador = roles.Contains(RolesSistema.Administrador);
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var abiertos = (await _unitOfWork.Detenciones.GetAnalisisAbiertosAsync(cancellationToken)).ToList();
        var visibles = FiltrarVisibles(abiertos, usuarioId, roles, responsableRol, responsableUsuarioId);

        var cerradas = await _unitOfWork.Detenciones.GetAnalisisCerradosPorUsuarioAsync(
            usuarioId, LimiteCerradasPorUsuario, cancellationToken);

        var inicioMes = new DateTimeOffset(DateTime.Today.Year, DateTime.Today.Month, 1, 0, 0, 0, DateTimeOffset.Now.Offset);
        var cerradasEsteMes = await _unitOfWork.Detenciones.CountAsync(d =>
            d.RequiereAnalisis
            && d.EstadoActual == EstadoDetencion.CERRADA
            && d.AnalisisPorUsuarioId == usuarioId
            && d.FechaHoraAnalisis >= inicioMes,
            cancellationToken);

        var veDeclaracion = roles.Contains(RolesSistema.Operario);
        var dto = new MisPendientesDto
        {
            EsAdministrador = esAdministrador,
            VeDeclaracion = veDeclaracion,
            VeAnalisis = !veDeclaracion || roles.Any(r => r is RolesSistema.Administrador or RolesSistema.Mantenimiento or RolesSistema.Supervisor),
            ParaAnalizar = visibles
                .Where(d => d.EstadoActual == EstadoDetencion.REGISTRADA)
                .Select(d => MapearPendiente(d, hoy))
                .ToList(),
            EnAnalisis = visibles
                .Where(d => d.EstadoActual == EstadoDetencion.EN_SEGUIMIENTO)
                .Select(d => MapearPendiente(d, hoy))
                .ToList(),
            CerradasPorMi = cerradas.Select(d => MapearPendiente(d, hoy)).ToList(),
            CerradasEsteMes = cerradasEsteMes,
            DemoraPromedioDias = visibles.Count == 0
                ? null
                : visibles.Average(d => (double)(hoy.DayNumber - d.Fecha.DayNumber)),
            Responsables = esAdministrador ? ObtenerResponsables(abiertos) : new List<ResponsableOpcionDto>()
        };

        if (veDeclaracion)
            dto.ParaDeclarar = await ObtenerParaDeclararAsync(usuarioId, cancellationToken);

        return Result<MisPendientesDto>.Success(dto);
    }

    public async Task<int> ContarPendientesAsync(
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default)
    {
        var abiertos = await _unitOfWork.Detenciones.GetAnalisisAbiertosAsync(cancellationToken);
        var visibles = FiltrarVisibles(abiertos, usuarioId, roles, null, null);
        var paraDeclarar = roles.Contains(RolesSistema.Operario)
            ? (await ObtenerParaDeclararAsync(usuarioId, cancellationToken)).Count
            : 0;

        return visibles.Count + paraDeclarar;
    }

    public async Task<Result<IReadOnlyList<SolucionDto>>> BuscarSolucionesAsync(
        BuscarSolucionesRequest filtro,
        CancellationToken cancellationToken = default)
    {
        var detenciones = await _unitOfWork.Detenciones.GetSolucionesAsync(
            filtro.Busqueda,
            filtro.LineaId,
            filtro.EquipoId,
            filtro.TipoFallaId,
            limite: LimiteSoluciones,
            cancellationToken: cancellationToken);

        return Result<IReadOnlyList<SolucionDto>>.Success(detenciones.Select(MapearSolucion).ToList());
    }

    /// <summary>
    /// Carga la detención y valida que el análisis se pueda modificar por este usuario.
    /// </summary>
    private async Task<Result<Detencion>> CargarEditableAsync(
        long detencionId,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        byte[] rowVersion,
        CancellationToken cancellationToken)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result<Detencion>.Failure("NOT_FOUND", "Detención no encontrada.");

        if (!AnalisisFallaReglas.PuedeEditar(detencion, usuarioId, roles))
            return Result<Detencion>.Failure("FORBIDDEN", "Solo el responsable del análisis o un administrador puede completarlo.");

        if (!detencion.RequiereAnalisis)
            return Result<Detencion>.Failure("VALIDATION", "Esta detención no requiere análisis.");

        if (detencion.EstadoActual is not (EstadoDetencion.REGISTRADA or EstadoDetencion.EN_SEGUIMIENTO))
            return Result<Detencion>.Failure("VALIDATION", "El análisis solo se puede completar mientras la detención está registrada o en análisis.");

        if (!detencion.RowVersion.SequenceEqual(rowVersion))
            return Result<Detencion>.Failure("CONCURRENCY", "El análisis fue modificado por otro usuario. Recargue los datos e intente nuevamente.");

        return Result<Detencion>.Success(detencion);
    }

    /// <summary>
    /// El administrador ve todos los análisis abiertos (con filtro de responsable opcional).
    /// Los demás, solo los que son responsables por rol o por usuario.
    /// </summary>
    private static List<Detencion> FiltrarVisibles(
        IEnumerable<Detencion> abiertos,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        string? responsableRol,
        string? responsableUsuarioId)
    {
        if (roles.Contains(RolesSistema.Administrador))
        {
            return abiertos
                .Where(d => CoincideResponsable(d, responsableRol, responsableUsuarioId))
                .ToList();
        }

        return abiertos
            .Where(d => AnalisisFallaReglas.EsResponsable(d, usuarioId, roles))
            .ToList();
    }

    private async Task<List<PendienteDeclaracionDto>> ObtenerParaDeclararAsync(
        string usuarioId,
        CancellationToken cancellationToken)
    {
        var lineasIds = (await _unitOfWork.UsuariosLineas.FindAsync(ul => ul.UsuarioId == usuarioId, cancellationToken))
            .Select(ul => ul.LineaId)
            .ToHashSet();

        if (lineasIds.Count == 0)
            return new List<PendienteDeclaracionDto>();

        var nombresLineas = (await _unitOfWork.Lineas.GetAllAsync(cancellationToken))
            .ToDictionary(l => l.LineaId, l => l.Nombre);

        var pendientes = await _unitOfWork.Detenciones.FindAsync(
            d => d.EstadoActual == EstadoDetencion.PENDIENTE_JUSTIFICACION && lineasIds.Contains(d.LineaId),
            cancellationToken);

        return pendientes
            .OrderBy(d => d.Fecha)
            .ThenBy(d => d.DetencionId)
            .Select(d => new PendienteDeclaracionDto
            {
                DetencionId = d.DetencionId,
                Codigo = d.Codigo,
                Fecha = d.Fecha,
                LineaNombre = nombresLineas.GetValueOrDefault(d.LineaId, string.Empty),
                FechaHoraParada = d.FechaHoraParada,
                FechaHoraArranque = d.FechaHoraArranque,
                MinutosEfectivos = d.MinutosEfectivos,
                EstadoActualDescripcion = d.EstadoActual.ObtenerDescripcion()
            })
            .ToList();
    }

    private async Task<AnalisisDetencionDto> ArmarDtoAsync(
        Detencion detencion,
        string usuarioId,
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        var dto = new AnalisisDetencionDto
        {
            DetencionId = detencion.DetencionId,
            Codigo = detencion.Codigo,
            EstadoActual = detencion.EstadoActual.ToString(),
            EstadoActualDescripcion = detencion.EstadoActual.ObtenerDescripcion(),
            RequiereAnalisis = detencion.RequiereAnalisis,
            LineaNombre = detencion.Linea?.Nombre ?? string.Empty,
            Fecha = detencion.Fecha,
            FechaHoraParada = detencion.FechaHoraParada,
            TurnoNombre = detencion.Turno?.Nombre ?? string.Empty,
            MinutosEfectivos = detencion.MinutosEfectivos,
            EquipoNombre = detencion.Equipo?.Nombre,
            EquipoCritico = detencion.Equipo?.Critico ?? false,
            TipoFallaNombre = detencion.TipoFalla?.Nombre,
            DetalleFallaNombre = detencion.DetalleFalla?.Nombre,
            Descripcion = detencion.Descripcion,
            CreadoPorUsuarioId = detencion.CreadoPorUsuarioId,
            ResponsableRol = detencion.ResponsableRol,
            ResponsableUsuarioId = detencion.ResponsableUsuarioId,
            AccionesRealizadas = detencion.AccionesRealizadas,
            CausaFalla = detencion.CausaFalla,
            PlanAccion = detencion.PlanAccion,
            AnalisisPorUsuarioId = detencion.AnalisisPorUsuarioId,
            FechaHoraAnalisis = detencion.FechaHoraAnalisis,
            PuedeEditar = detencion.RequiereAnalisis
                && (detencion.EstadoActual == EstadoDetencion.REGISTRADA || detencion.EstadoActual == EstadoDetencion.EN_SEGUIMIENTO)
                && AnalisisFallaReglas.PuedeEditar(detencion, usuarioId, roles),
            RowVersion = detencion.RowVersion
        };

        if (detencion.ReglaAnalisisId.HasValue)
        {
            var regla = await _unitOfWork.ReglasAnalisis.GetByIdAsync(detencion.ReglaAnalisisId.Value, cancellationToken);
            dto.MinutosMinimosRegla = regla?.MinutosMinimos;
        }

        // Fallas parecidas: mismo equipo; si la falla no tiene equipo, mismo tipo de falla.
        if (detencion.EquipoId.HasValue || detencion.TipoFallaId.HasValue)
        {
            var parecidas = await _unitOfWork.Detenciones.GetSolucionesAsync(
                equipoId: detencion.EquipoId,
                tipoFallaId: detencion.EquipoId.HasValue ? null : detencion.TipoFallaId,
                excluirDetencionId: detencion.DetencionId,
                limite: LimiteFallasParecidas,
                cancellationToken: cancellationToken);

            dto.FallasParecidas = parecidas.Select(MapearSolucion).ToList();
        }

        return dto;
    }

    private static void CambiarEstado(
        Detencion detencion,
        EstadoDetencion nuevoEstado,
        string usuarioId,
        string observacion,
        DateTimeOffset ahora)
    {
        var estadoAnterior = detencion.EstadoActual;
        detencion.EstadoActual = nuevoEstado;
        detencion.HistorialEstados.Add(new DetencionEstadoHistorial
        {
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = nuevoEstado,
            FechaHora = ahora,
            UsuarioId = usuarioId,
            Observacion = observacion,
            OrigenAccion = "API"
        });
    }

    private static void AplicarCampos(
        Detencion detencion,
        GuardarAnalisisRequest request,
        string usuarioId,
        DateTimeOffset ahora)
    {
        detencion.AccionesRealizadas = Limpiar(request.AccionesRealizadas);
        detencion.CausaFalla = Limpiar(request.CausaFalla);
        detencion.PlanAccion = Limpiar(request.PlanAccion);
        detencion.ModificadoPorUsuarioId = usuarioId;
        detencion.FechaHoraModificacion = ahora;
    }

    private static string? ValidarLargos(GuardarAnalisisRequest request)
    {
        if ((request.AccionesRealizadas?.Length ?? 0) > LongitudMaximaSolucion)
            return $"La solución no puede superar {LongitudMaximaSolucion} caracteres.";

        if ((request.CausaFalla?.Length ?? 0) > LongitudMaximaCausaYPlan)
            return $"La causa no puede superar {LongitudMaximaCausaYPlan} caracteres.";

        if ((request.PlanAccion?.Length ?? 0) > LongitudMaximaCausaYPlan)
            return $"El plan de acción no puede superar {LongitudMaximaCausaYPlan} caracteres.";

        return null;
    }

    private static string? Limpiar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static bool CoincideResponsable(Detencion detencion, string? responsableRol, string? responsableUsuarioId)
    {
        if (responsableRol == null && responsableUsuarioId == null)
            return true;

        return (responsableRol != null && detencion.ResponsableRol == responsableRol)
            || (responsableUsuarioId != null && detencion.ResponsableUsuarioId == responsableUsuarioId);
    }

    private static List<ResponsableOpcionDto> ObtenerResponsables(IEnumerable<Detencion> abiertos)
    {
        var roles = abiertos
            .Where(d => d.ResponsableRol != null)
            .Select(d => d.ResponsableRol!)
            .Distinct()
            .OrderBy(r => r)
            .Select(r => new ResponsableOpcionDto { Rol = r, Nombre = $"Rol {Capitalizar(r)}" });

        // El nombre del usuario lo completa la API, que tiene acceso a los usuarios.
        var usuarios = abiertos
            .Where(d => d.ResponsableUsuarioId != null)
            .Select(d => d.ResponsableUsuarioId!)
            .Distinct()
            .Select(u => new ResponsableOpcionDto { UsuarioId = u });

        return roles.Concat(usuarios).ToList();
    }

    private static string Capitalizar(string texto) =>
        texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..].ToLowerInvariant();

    private static PendienteAnalisisDto MapearPendiente(Detencion detencion, DateOnly hoy) => new()
    {
        DetencionId = detencion.DetencionId,
        Codigo = detencion.Codigo,
        Fecha = detencion.Fecha,
        LineaNombre = detencion.Linea?.Nombre ?? string.Empty,
        EquipoNombre = detencion.Equipo?.Nombre,
        EquipoCritico = detencion.Equipo?.Critico ?? false,
        TipoFallaNombre = detencion.TipoFalla?.Nombre,
        MinutosEfectivos = detencion.MinutosEfectivos,
        EstadoActual = detencion.EstadoActual.ToString(),
        EstadoActualDescripcion = detencion.EstadoActual.ObtenerDescripcion(),
        DiasEsperando = AnalisisFallaReglas.EstaAbierto(detencion)
            ? hoy.DayNumber - detencion.Fecha.DayNumber
            : null,
        ResponsableRol = detencion.ResponsableRol,
        ResponsableUsuarioId = detencion.ResponsableUsuarioId,
        AnalisisPorUsuarioId = detencion.AnalisisPorUsuarioId,
        FechaHoraAnalisis = detencion.FechaHoraAnalisis
    };

    private static SolucionDto MapearSolucion(Detencion detencion) => new()
    {
        DetencionId = detencion.DetencionId,
        Codigo = detencion.Codigo,
        Fecha = detencion.Fecha,
        LineaNombre = detencion.Linea?.Nombre ?? string.Empty,
        EquipoNombre = detencion.Equipo?.Nombre,
        TipoFallaNombre = detencion.TipoFalla?.Nombre,
        MinutosEfectivos = detencion.MinutosEfectivos,
        Descripcion = detencion.Descripcion,
        AccionesRealizadas = detencion.AccionesRealizadas,
        CausaFalla = detencion.CausaFalla,
        AnalisisPorUsuarioId = detencion.AnalisisPorUsuarioId,
        FechaHoraAnalisis = detencion.FechaHoraAnalisis
    };

    private static Result<T> Propagar<T>(Result origen) =>
        Result<T>.Failure(origen.ErrorCode ?? "ERROR", origen.ErrorMessage ?? "Error desconocido.");
}
