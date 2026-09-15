using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;

namespace PSAG.Mantenimiento.Application.Services.Implementations;

public class DetencionService : IDetencionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DetencionService> _logger;

    public DetencionService(IUnitOfWork unitOfWork, ILogger<DetencionService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result<CrearDetencionResponse>> CrearDetencionAsync(
        CrearDetencionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        // Validar línea existe
        var linea = await _unitOfWork.Lineas.GetByIdAsync(request.LineaId, cancellationToken);
        if (linea == null)
            return Result<CrearDetencionResponse>.Failure("NOT_FOUND", "Línea no encontrada.");

        // Obtener turno actual
        var turno = await _unitOfWork.Turnos.GetTurnoActualAsync(cancellationToken);
        if (turno == null)
            return Result<CrearDetencionResponse>.Failure("NO_TURNO", "No hay turno configurado para la hora actual.");

        // Calcular MinutosEfectivos según OrigenTiempo
        int minutosEfectivos;
        OrigenTiempo origenTiempo;

        if (!Enum.TryParse<OrigenTiempo>(request.OrigenTiempo, true, out origenTiempo))
            origenTiempo = OrigenTiempo.DECLARADO;

        if (origenTiempo == OrigenTiempo.CALCULADO)
        {
            if (!request.FechaHoraParada.HasValue || !request.FechaHoraArranque.HasValue)
                return Result<CrearDetencionResponse>.Failure("VALIDATION", "FechaHoraParada y FechaHoraArranque son requeridos para origen CALCULADO.");

            if (request.FechaHoraArranque <= request.FechaHoraParada)
                return Result<CrearDetencionResponse>.Failure("VALIDATION", "FechaHoraArranque debe ser posterior a FechaHoraParada.");

            minutosEfectivos = (int)(request.FechaHoraArranque.Value - request.FechaHoraParada.Value).TotalMinutes;
        }
        else
        {
            if (!request.MinutosDeclarados.HasValue || request.MinutosDeclarados <= 0)
                return Result<CrearDetencionResponse>.Failure("VALIDATION", "MinutosDeclarados es requerido y debe ser mayor a 0 para origen DECLARADO.");

            minutosEfectivos = request.MinutosDeclarados.Value;
        }

        // Generar código
        var codigo = await _unitOfWork.Detenciones.GenerarCodigoDetencionAsync(cancellationToken);

        var detencion = new Detencion
        {
            Codigo = codigo,
            LineaId = request.LineaId,
            EquipoId = request.EquipoId,
            TipoFallaId = request.TipoFallaId,
            DetalleFallaId = request.DetalleFallaId,
            TurnoId = turno.TurnoId,
            Fecha = request.Fecha,
            MinutosDeclarados = request.MinutosDeclarados,
            FechaHoraParada = request.FechaHoraParada,
            FechaHoraArranque = request.FechaHoraArranque,
            OrigenTiempo = origenTiempo,
            MinutosEfectivos = minutosEfectivos,
            Descripcion = request.Descripcion,
            EstadoActual = EstadoDetencion.REGISTRADA,
            CreadoPorUsuarioId = usuarioId,
            ModificadoPorUsuarioId = usuarioId
        };

        // Agregar historial inicial
        detencion.HistorialEstados.Add(new DetencionEstadoHistorial
        {
            EstadoAnterior = null,
            EstadoNuevo = EstadoDetencion.REGISTRADA,
            FechaHora = DateTimeOffset.UtcNow,
            UsuarioId = usuarioId,
            Observacion = "Detención registrada",
            OrigenAccion = "API"
        });

        await _unitOfWork.Detenciones.AddAsync(detencion, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Detención {Codigo} creada por usuario {UsuarioId}", codigo, usuarioId);

        return Result<CrearDetencionResponse>.Success(new CrearDetencionResponse
        {
            DetencionId = detencion.DetencionId,
            Codigo = detencion.Codigo,
            Estado = detencion.EstadoActual.ToString(),
            MinutosEfectivos = detencion.MinutosEfectivos,
            TurnoNombre = turno.Nombre,
            FechaHoraRegistro = detencion.FechaHoraCreacion
        });
    }

    public async Task<Result<DetencionDto>> ObtenerPorIdAsync(
        long detencionId,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result<DetencionDto>.Failure("NOT_FOUND", "Detención no encontrada.");

        return Result<DetencionDto>.Success(MapToDto(detencion));
    }

    public async Task<Result<DetencionDto>> ObtenerPorCodigoAsync(
        string codigo,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByCodigoAsync(codigo, cancellationToken);
        if (detencion == null)
            return Result<DetencionDto>.Failure("NOT_FOUND", "Detención no encontrada.");

        return Result<DetencionDto>.Success(MapToDto(detencion));
    }

    public async Task<Result<DetencionDetalleDto>> ObtenerDetalleAsync(
        long detencionId,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result<DetencionDetalleDto>.Failure("NOT_FOUND", "Detención no encontrada.");

        var dto = new DetencionDetalleDto
        {
            DetencionId = detencion.DetencionId,
            Codigo = detencion.Codigo,
            LineaId = detencion.LineaId,
            LineaNombre = detencion.Linea.Nombre,
            EquipoId = detencion.EquipoId,
            EquipoNombre = detencion.Equipo?.Nombre,
            TipoFallaId = detencion.TipoFallaId,
            TipoFallaNombre = detencion.TipoFalla?.Nombre,
            DetalleFallaId = detencion.DetalleFallaId,
            DetalleFallaNombre = detencion.DetalleFalla?.Nombre,
            TurnoId = detencion.TurnoId,
            TurnoNombre = detencion.Turno.Nombre,
            Fecha = detencion.Fecha,
            MinutosDeclarados = detencion.MinutosDeclarados,
            FechaHoraParada = detencion.FechaHoraParada,
            FechaHoraArranque = detencion.FechaHoraArranque,
            OrigenTiempo = detencion.OrigenTiempo.ToString(),
            MinutosEfectivos = detencion.MinutosEfectivos,
            Descripcion = detencion.Descripcion,
            EstadoActual = detencion.EstadoActual.ToString(),
            EstadoActualDescripcion = detencion.EstadoActual.ObtenerDescripcion(),
            AccionesRealizadas = detencion.AccionesRealizadas,
            ResolucionDescripcion = detencion.ResolucionDescripcion,
            FechaHoraCierre = detencion.FechaHoraCierre,
            CerradoPorUsuarioId = detencion.CerradoPorUsuarioId,
            FechaHoraCreacion = detencion.FechaHoraCreacion,
            CreadoPorUsuarioId = detencion.CreadoPorUsuarioId,
            RowVersion = detencion.RowVersion,
            HistorialEstados = detencion.HistorialEstados.Select(h => new DetencionHistorialDto
            {
                DetencionEstadoHistorialId = h.DetencionEstadoHistorialId,
                EstadoAnterior = h.EstadoAnterior?.ToString(),
                EstadoAnteriorDescripcion = h.EstadoAnterior?.ObtenerDescripcion(),
                EstadoNuevo = h.EstadoNuevo.ToString(),
                EstadoNuevoDescripcion = h.EstadoNuevo.ObtenerDescripcion(),
                FechaHora = h.FechaHora,
                UsuarioId = h.UsuarioId,
                UsuarioNombre = h.UsuarioId,
                Observacion = h.Observacion
            }).ToList(),
            Notas = detencion.Notas.Select(n => new DetencionNotaDto
            {
                DetencionNotaId = n.DetencionNotaId,
                Texto = n.Texto,
                UsuarioId = n.UsuarioId,
                UsuarioNombre = n.UsuarioId,
                FechaHora = n.FechaHora,
                TipoNota = n.TipoNota.ToString(),
                EsNotaDeCierre = n.EsNotaDeCierre
            }).ToList()
        };

        return Result<DetencionDetalleDto>.Success(dto);
    }

    public async Task<Result<PagedResult<DetencionListaDto>>> ObtenerPaginadoAsync(
        DetencionFiltroRequest filtro,
        string usuarioId,
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default)
    {
        EstadoDetencion? estadoEnum = null;
        if (!string.IsNullOrEmpty(filtro.Estado) && Enum.TryParse<EstadoDetencion>(filtro.Estado, true, out var e))
            estadoEnum = e;

        var (items, totalCount) = await _unitOfWork.Detenciones.GetPaginadoAsync(
            filtro.Page,
            filtro.PageSize,
            lineasPermitidas?.FirstOrDefault(),
            filtro.EquipoId,
            estadoEnum,
            filtro.TipoFallaId,
            filtro.DetalleFallaId,
            filtro.TurnoId,
            filtro.FechaDesde,
            filtro.FechaHasta,
            filtro.Busqueda,
            filtro.OrdenarPor,
            filtro.OrdenDescendente,
            cancellationToken);

        var dtos = items.Select(d => new DetencionListaDto
        {
            DetencionId = d.DetencionId,
            Codigo = d.Codigo,
            LineaNombre = d.Linea.Nombre,
            EquipoNombre = d.Equipo?.Nombre,
            TipoFallaNombre = d.TipoFalla?.Nombre,
            TurnoNombre = d.Turno.Nombre,
            Fecha = d.Fecha,
            MinutosEfectivos = d.MinutosEfectivos,
            EstadoActual = d.EstadoActual.ToString(),
            EstadoActualDescripcion = d.EstadoActual.ObtenerDescripcion(),
            CreadoPorUsuarioNombre = d.CreadoPorUsuarioId
        }).ToList();

        return Result<PagedResult<DetencionListaDto>>.Success(new PagedResult<DetencionListaDto>
        {
            Items = dtos,
            Page = filtro.Page,
            PageSize = filtro.PageSize,
            TotalCount = totalCount
        });
    }

    public async Task<Result> ActualizarDetencionAsync(
        long detencionId,
        ActualizarDetencionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result.Failure("NOT_FOUND", "Detención no encontrada.");

        if (!detencion.PuedeSerEditada)
            return Result.Failure("CERRADA", "No se puede editar una detención cerrada.");

        // Verificar concurrencia
        if (!detencion.RowVersion.SequenceEqual(request.RowVersion))
            return Result.Failure("CONCURRENCY", "La detención fue modificada por otro usuario.");

        detencion.Descripcion = request.Descripcion;
        detencion.AccionesRealizadas = request.AccionesRealizadas;
        detencion.ResolucionDescripcion = request.ResolucionDescripcion;
        detencion.EquipoId = request.EquipoId;
        detencion.TipoFallaId = request.TipoFallaId;
        detencion.DetalleFallaId = request.DetalleFallaId;
        detencion.ModificadoPorUsuarioId = usuarioId;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Detención {DetencionId} actualizada por usuario {UsuarioId}", detencionId, usuarioId);

        return Result.Success();
    }

    public async Task<Result> CambiarEstadoAsync(
        long detencionId,
        CambiarEstadoDetencionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result.Failure("NOT_FOUND", "Detención no encontrada.");

        if (!Enum.TryParse<EstadoDetencion>(request.NuevoEstado, true, out var nuevoEstado))
            return Result.Failure("VALIDATION", "Estado no válido.");

        if (!detencion.PuedeTransicionarA(nuevoEstado))
            return Result.Failure("TRANSITION", $"No se puede cambiar de {detencion.EstadoActual} a {nuevoEstado}.");

        if (!detencion.RowVersion.SequenceEqual(request.RowVersion))
            return Result.Failure("CONCURRENCY", "La detención fue modificada por otro usuario.");

        var estadoAnterior = detencion.EstadoActual;
        detencion.EstadoActual = nuevoEstado;
        detencion.ModificadoPorUsuarioId = usuarioId;

        if (nuevoEstado == EstadoDetencion.CERRADA)
        {
            detencion.FechaHoraCierre = DateTimeOffset.UtcNow;
            detencion.CerradoPorUsuarioId = usuarioId;
        }

        detencion.HistorialEstados.Add(new DetencionEstadoHistorial
        {
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = nuevoEstado,
            FechaHora = DateTimeOffset.UtcNow,
            UsuarioId = usuarioId,
            Observacion = request.Observacion,
            OrigenAccion = "API"
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Detención {DetencionId} cambió de {EstadoAnterior} a {EstadoNuevo} por usuario {UsuarioId}",
            detencionId, estadoAnterior, nuevoEstado, usuarioId);

        return Result.Success();
    }

    public async Task<Result> IniciarSeguimientoAsync(
        long detencionId,
        IniciarSeguimientoRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        return await CambiarEstadoAsync(detencionId, new CambiarEstadoDetencionRequest
        {
            NuevoEstado = EstadoDetencion.EN_SEGUIMIENTO.ToString(),
            Observacion = request.Observacion,
            RowVersion = request.RowVersion
        }, usuarioId, cancellationToken);
    }

    public async Task<Result> CerrarDetencionAsync(
        long detencionId,
        CerrarDetencionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result.Failure("NOT_FOUND", "Detención no encontrada.");

        if (!detencion.PuedeTransicionarA(EstadoDetencion.CERRADA))
            return Result.Failure("TRANSITION", $"No se puede cerrar una detención en estado {detencion.EstadoActual}.");

        if (!detencion.RowVersion.SequenceEqual(request.RowVersion))
            return Result.Failure("CONCURRENCY", "La detención fue modificada por otro usuario.");

        var estadoAnterior = detencion.EstadoActual;
        detencion.EstadoActual = EstadoDetencion.CERRADA;
        detencion.AccionesRealizadas = request.AccionesRealizadas;
        detencion.ResolucionDescripcion = request.ResolucionDescripcion;
        detencion.FechaHoraCierre = DateTimeOffset.UtcNow;
        detencion.CerradoPorUsuarioId = usuarioId;
        detencion.ModificadoPorUsuarioId = usuarioId;

        detencion.HistorialEstados.Add(new DetencionEstadoHistorial
        {
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = EstadoDetencion.CERRADA,
            FechaHora = DateTimeOffset.UtcNow,
            UsuarioId = usuarioId,
            Observacion = request.ObservacionCierre ?? "Detención cerrada",
            OrigenAccion = "API"
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Detención {DetencionId} cerrada por usuario {UsuarioId}", detencionId, usuarioId);

        return Result.Success();
    }

    public async Task<Result> AgregarNotaAsync(
        long detencionId,
        AgregarNotaDetencionRequest request,
        string usuarioId,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result.Failure("NOT_FOUND", "Detención no encontrada.");

        if (!Enum.TryParse<TipoNota>(request.TipoNota, true, out var tipoNota))
            tipoNota = TipoNota.Operativa;

        detencion.Notas.Add(new DetencionNota
        {
            Texto = request.Texto,
            UsuarioId = usuarioId,
            FechaHora = DateTimeOffset.UtcNow,
            TipoNota = tipoNota,
            EsNotaDeCierre = false
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Nota agregada a detención {DetencionId} por usuario {UsuarioId}", detencionId, usuarioId);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<DetencionHistorialDto>>> ObtenerHistorialAsync(
        long detencionId,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result<IReadOnlyList<DetencionHistorialDto>>.Failure("NOT_FOUND", "Detención no encontrada.");

        var historial = detencion.HistorialEstados
            .OrderByDescending(h => h.FechaHora)
            .Select(h => new DetencionHistorialDto
            {
                DetencionEstadoHistorialId = h.DetencionEstadoHistorialId,
                EstadoAnterior = h.EstadoAnterior?.ToString(),
                EstadoAnteriorDescripcion = h.EstadoAnterior?.ObtenerDescripcion(),
                EstadoNuevo = h.EstadoNuevo.ToString(),
                EstadoNuevoDescripcion = h.EstadoNuevo.ObtenerDescripcion(),
                FechaHora = h.FechaHora,
                UsuarioId = h.UsuarioId,
                UsuarioNombre = h.UsuarioId,
                Observacion = h.Observacion
            }).ToList();

        return Result<IReadOnlyList<DetencionHistorialDto>>.Success(historial);
    }

    public async Task<Result<IReadOnlyList<DetencionNotaDto>>> ObtenerNotasAsync(
        long detencionId,
        CancellationToken cancellationToken = default)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(detencionId, cancellationToken);
        if (detencion == null)
            return Result<IReadOnlyList<DetencionNotaDto>>.Failure("NOT_FOUND", "Detención no encontrada.");

        var notas = detencion.Notas
            .OrderByDescending(n => n.FechaHora)
            .Select(n => new DetencionNotaDto
            {
                DetencionNotaId = n.DetencionNotaId,
                Texto = n.Texto,
                UsuarioId = n.UsuarioId,
                UsuarioNombre = n.UsuarioId,
                FechaHora = n.FechaHora,
                TipoNota = n.TipoNota.ToString(),
                EsNotaDeCierre = n.EsNotaDeCierre
            }).ToList();

        return Result<IReadOnlyList<DetencionNotaDto>>.Success(notas);
    }

    public async Task<Result<IReadOnlyList<DetencionListaDto>>> ObtenerDetencionesAbiertasAsync(
        IEnumerable<int>? lineasPermitidas,
        CancellationToken cancellationToken = default)
    {
        var detenciones = await _unitOfWork.Detenciones.GetDetencionesAbiertasAsync(cancellationToken);

        if (lineasPermitidas != null && lineasPermitidas.Any())
        {
            var lineasSet = lineasPermitidas.ToHashSet();
            detenciones = detenciones.Where(d => lineasSet.Contains(d.LineaId));
        }

        var dtos = detenciones.Select(d => new DetencionListaDto
        {
            DetencionId = d.DetencionId,
            Codigo = d.Codigo,
            LineaNombre = d.Linea.Nombre,
            EquipoNombre = d.Equipo?.Nombre,
            TipoFallaNombre = d.TipoFalla?.Nombre,
            TurnoNombre = d.Turno.Nombre,
            Fecha = d.Fecha,
            MinutosEfectivos = d.MinutosEfectivos,
            EstadoActual = d.EstadoActual.ToString(),
            EstadoActualDescripcion = d.EstadoActual.ObtenerDescripcion(),
            CreadoPorUsuarioNombre = d.CreadoPorUsuarioId
        }).ToList();

        return Result<IReadOnlyList<DetencionListaDto>>.Success(dtos);
    }

    private static DetencionDto MapToDto(Detencion d)
    {
        return new DetencionDto
        {
            DetencionId = d.DetencionId,
            Codigo = d.Codigo,
            LineaId = d.LineaId,
            LineaNombre = d.Linea?.Nombre ?? string.Empty,
            EquipoId = d.EquipoId,
            EquipoNombre = d.Equipo?.Nombre,
            TipoFallaId = d.TipoFallaId,
            TipoFallaNombre = d.TipoFalla?.Nombre,
            DetalleFallaId = d.DetalleFallaId,
            DetalleFallaNombre = d.DetalleFalla?.Nombre,
            TurnoId = d.TurnoId,
            TurnoNombre = d.Turno?.Nombre ?? string.Empty,
            Fecha = d.Fecha,
            MinutosDeclarados = d.MinutosDeclarados,
            FechaHoraParada = d.FechaHoraParada,
            FechaHoraArranque = d.FechaHoraArranque,
            OrigenTiempo = d.OrigenTiempo.ToString(),
            MinutosEfectivos = d.MinutosEfectivos,
            Descripcion = d.Descripcion,
            EstadoActual = d.EstadoActual.ToString(),
            EstadoActualDescripcion = d.EstadoActual.ObtenerDescripcion(),
            AccionesRealizadas = d.AccionesRealizadas,
            ResolucionDescripcion = d.ResolucionDescripcion,
            FechaHoraCierre = d.FechaHoraCierre,
            CerradoPorUsuarioId = d.CerradoPorUsuarioId,
            CreadoPorUsuarioId = d.CreadoPorUsuarioId,
            FechaHoraCreacion = d.FechaHoraCreacion,
            FechaHoraModificacion = d.FechaHoraModificacion,
            RowVersion = d.RowVersion
        };
    }
}
