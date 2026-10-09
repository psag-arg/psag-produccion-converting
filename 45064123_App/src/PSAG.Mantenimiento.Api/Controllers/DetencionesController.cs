using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;

namespace PSAG.Mantenimiento.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DetencionesController : ApiControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDetencionService _detencionService;
    private readonly ILogger<DetencionesController> _logger;

    public DetencionesController(
        IUnitOfWork unitOfWork,
        IDetencionService detencionService,
        ILogger<DetencionesController> logger)
    {
        _unitOfWork = unitOfWork;
        _detencionService = detencionService;
        _logger = logger;
    }

    /// <summary>
    /// Obtener lista paginada de detenciones con filtros.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DetencionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDetenciones([FromQuery] DetencionFiltroRequest filtro)
    {
        EstadoDetencion? estadoFiltro = null;
        if (!string.IsNullOrEmpty(filtro.Estado) && Enum.TryParse<EstadoDetencion>(filtro.Estado, true, out var estado))
        {
            estadoFiltro = estado;
        }

        var (detenciones, total) = await _unitOfWork.Detenciones.GetPaginadoAsync(
            filtro.Page,
            filtro.PageSize,
            filtro.LineaId,
            filtro.EquipoId,
            estadoFiltro,
            filtro.TipoFallaId,
            filtro.DetalleFallaId,
            filtro.TurnoId,
            filtro.FechaDesde,
            filtro.FechaHasta,
            filtro.Busqueda);

        var items = detenciones.Select(d => new DetencionDto
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
            CreadoPorUsuarioId = d.CreadoPorUsuarioId,
            FechaHoraCreacion = d.FechaHoraCreacion,
            FechaHoraModificacion = d.FechaHoraModificacion,
            Eliminada = d.Eliminada,
            RequiereAnalisis = d.RequiereAnalisis,
            ResponsableRol = d.ResponsableRol,
            ResponsableUsuarioId = d.ResponsableUsuarioId,
            CausaFalla = d.CausaFalla,
            PlanAccion = d.PlanAccion,
            AnalisisPorUsuarioId = d.AnalisisPorUsuarioId,
            FechaHoraAnalisis = d.FechaHoraAnalisis,
            EquipoCritico = d.Equipo?.Critico ?? false,
            RowVersion = d.RowVersion
        }).ToList();

        return Ok(new PagedResult<DetencionDto>
        {
            Items = items,
            TotalCount = total,
            Page = filtro.Page,
            PageSize = filtro.PageSize
        });
    }

    /// <summary>
    /// Obtener una detención por su ID.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(DetencionDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetencion(long id)
    {
        var detencion = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(id);

        if (detencion == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Detención no encontrada",
                Detail = $"No se encontró la detención con ID {id}",
                Status = StatusCodes.Status404NotFound
            });
        }

        var dto = new DetencionDetalleDto
        {
            DetencionId = detencion.DetencionId,
            Codigo = detencion.Codigo,
            LineaId = detencion.LineaId,
            LineaNombre = detencion.Linea?.Nombre ?? string.Empty,
            EquipoId = detencion.EquipoId,
            EquipoNombre = detencion.Equipo?.Nombre,
            TipoFallaId = detencion.TipoFallaId,
            TipoFallaNombre = detencion.TipoFalla?.Nombre,
            DetalleFallaId = detencion.DetalleFallaId,
            DetalleFallaNombre = detencion.DetalleFalla?.Nombre,
            TurnoId = detencion.TurnoId,
            TurnoNombre = detencion.Turno?.Nombre ?? string.Empty,
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
            Eliminada = detencion.Eliminada,
            RequiereAnalisis = detencion.RequiereAnalisis,
            ResponsableRol = detencion.ResponsableRol,
            ResponsableUsuarioId = detencion.ResponsableUsuarioId,
            CausaFalla = detencion.CausaFalla,
            PlanAccion = detencion.PlanAccion,
            AnalisisPorUsuarioId = detencion.AnalisisPorUsuarioId,
            FechaHoraAnalisis = detencion.FechaHoraAnalisis,
            EquipoCritico = detencion.Equipo?.Critico ?? false,
            RowVersion = detencion.RowVersion,
            Notas = detencion.Notas.Select(n => new DetencionNotaDto
            {
                DetencionNotaId = n.DetencionNotaId,
                Texto = n.Texto,
                UsuarioId = n.UsuarioId,
                FechaHora = n.FechaHora,
                TipoNota = n.TipoNota.ToString(),
                EsNotaDeCierre = n.EsNotaDeCierre
            }).OrderByDescending(n => n.FechaHora).ToList(),
            HistorialEstados = detencion.HistorialEstados.Select(h => new DetencionHistorialDto
            {
                DetencionEstadoHistorialId = h.DetencionEstadoHistorialId,
                EstadoAnterior = h.EstadoAnterior?.ToString(),
                EstadoNuevo = h.EstadoNuevo.ToString(),
                FechaHora = h.FechaHora,
                UsuarioId = h.UsuarioId,
                Observacion = h.Observacion
            }).OrderByDescending(h => h.FechaHora).ToList()
        };

        return Ok(dto);
    }

    /// <summary>
    /// Crear una nueva detención.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "CanCreateDetenciones")]
    [ProducesResponseType(typeof(CrearDetencionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDetencion([FromBody] CrearDetencionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        int turnoId;
        int? usuarioLineaId = null;
        string turnoNombre;

        // Si se proporciona UsuarioLineaId en el request (admin/supervisor asignando a otro maquinista)
        if (request.UsuarioLineaId.HasValue)
        {
            var usuarioLineaAsignado = await _unitOfWork.UsuariosLineas.GetByIdAsync(request.UsuarioLineaId.Value);
            if (usuarioLineaAsignado == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Maquinista no encontrado",
                    Detail = "El maquinista seleccionado no existe.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
            if (!usuarioLineaAsignado.TurnoId.HasValue)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Turno no asignado",
                    Detail = "El maquinista seleccionado no tiene turno asignado.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
            turnoId = usuarioLineaAsignado.TurnoId.Value;
            usuarioLineaId = usuarioLineaAsignado.UsuarioLineaId;
            var turno = await _unitOfWork.Turnos.GetByIdAsync(turnoId);
            turnoNombre = turno?.Nombre ?? "Turno desconocido";
        }
        else
        {
            // Buscar asignacion del usuario actual para la linea
            var usuarioLinea = await _unitOfWork.UsuariosLineas.GetByUsuarioYLineaAsync(CurrentUserId, request.LineaId);

            if (usuarioLinea != null && usuarioLinea.TurnoId.HasValue)
            {
                // Usuario tiene asignacion con turno - usar turno del maquinista
                turnoId = usuarioLinea.TurnoId.Value;
                usuarioLineaId = usuarioLinea.UsuarioLineaId;
                var turno = await _unitOfWork.Turnos.GetByIdAsync(turnoId);
                turnoNombre = turno?.Nombre ?? "Turno desconocido";
            }
            else
            {
                // Fallback: usar turno actual por hora (para supervisores/admins sin asignacion)
                var turnoActual = await _unitOfWork.Turnos.GetTurnoActualAsync();
                if (turnoActual == null)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "Turno no encontrado",
                        Detail = "No se encontró un turno activo para la hora actual y el usuario no tiene asignación de turno para esta línea.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
                turnoId = turnoActual.TurnoId;
                turnoNombre = turnoActual.Nombre;
            }
        }

        // Calcular MinutosEfectivos
        int minutosEfectivos;
        OrigenTiempo origenTiempo;

        if (Enum.TryParse<OrigenTiempo>(request.OrigenTiempo, true, out var parsedOrigen))
        {
            origenTiempo = parsedOrigen;
        }
        else
        {
            origenTiempo = OrigenTiempo.DECLARADO;
        }

        if (origenTiempo == OrigenTiempo.DECLARADO)
        {
            minutosEfectivos = request.MinutosDeclarados ?? 0;
        }
        else if (origenTiempo == OrigenTiempo.CALCULADO && request.FechaHoraParada.HasValue && request.FechaHoraArranque.HasValue)
        {
            minutosEfectivos = (int)(request.FechaHoraArranque.Value - request.FechaHoraParada.Value).TotalMinutes;
        }
        else
        {
            minutosEfectivos = 0;
        }

        // Generar código único
        var codigo = await _unitOfWork.Detenciones.GenerarCodigoDetencionAsync();

        var detencion = new Domain.Entities.Detencion
        {
            Codigo = codigo,
            LineaId = request.LineaId,
            EquipoId = request.EquipoId,
            TipoFallaId = request.TipoFallaId,
            DetalleFallaId = request.DetalleFallaId,
            TurnoId = turnoId,
            UsuarioLineaId = usuarioLineaId,
            Fecha = request.Fecha,
            MinutosDeclarados = request.MinutosDeclarados,
            FechaHoraParada = request.FechaHoraParada,
            FechaHoraArranque = request.FechaHoraArranque,
            OrigenTiempo = origenTiempo,
            MinutosEfectivos = minutosEfectivos,
            Descripcion = request.Descripcion,
            EstadoActual = EstadoDetencion.REGISTRADA,
            CreadoPorUsuarioId = CurrentUserId,
            ModificadoPorUsuarioId = CurrentUserId
        };

        // Agregar historial de estado inicial
        detencion.HistorialEstados.Add(new Domain.Entities.DetencionEstadoHistorial
        {
            EstadoAnterior = null,
            EstadoNuevo = EstadoDetencion.REGISTRADA,
            UsuarioId = CurrentUserId,
            Observacion = "Detención registrada",
            CorrelationId = CorrelationId
        });

        await _detencionService.AplicarReglaAnalisisAsync(detencion);
        await _unitOfWork.Detenciones.AddAsync(detencion);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Detención {Codigo} creada por usuario {UserId}, UsuarioLineaId: {UsuarioLineaId}",
            codigo, CurrentUserId, usuarioLineaId);

        var response = new CrearDetencionResponse
        {
            DetencionId = detencion.DetencionId,
            Codigo = detencion.Codigo,
            Estado = detencion.EstadoActual.ToString(),
            MinutosEfectivos = detencion.MinutosEfectivos,
            TurnoNombre = turnoNombre,
            FechaHoraRegistro = detencion.FechaHoraCreacion
        };

        return CreatedAtAction(nameof(GetDetencion), new { id = detencion.DetencionId }, response);
    }

    /// <summary>
    /// Cerrar una detención (opcional).
    /// </summary>
    [HttpPost("{id:long}/cerrar")]
    [Authorize(Policy = "CanCloseDetenciones")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CerrarDetencion(long id, [FromBody] CerrarDetencionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(id);
        if (detencion == null)
        {
            return NotFound();
        }

        if (detencion.EstadoActual == EstadoDetencion.CERRADA)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Detención ya cerrada",
                Detail = "Esta detención ya se encuentra cerrada",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var estadoAnterior = detencion.EstadoActual;
        detencion.EstadoActual = EstadoDetencion.CERRADA;
        detencion.AccionesRealizadas = request.AccionesRealizadas;
        detencion.ResolucionDescripcion = request.ResolucionDescripcion;
        detencion.FechaHoraCierre = DateTimeOffset.UtcNow;
        detencion.CerradoPorUsuarioId = CurrentUserId;
        detencion.ModificadoPorUsuarioId = CurrentUserId;
        detencion.FechaHoraModificacion = DateTimeOffset.UtcNow;

        detencion.HistorialEstados.Add(new Domain.Entities.DetencionEstadoHistorial
        {
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = EstadoDetencion.CERRADA,
            UsuarioId = CurrentUserId,
            Observacion = request.ObservacionCierre ?? "Detención cerrada",
            CorrelationId = CorrelationId
        });

        // Agregar nota de cierre si se proporciona
        if (!string.IsNullOrEmpty(request.ObservacionCierre))
        {
            detencion.Notas.Add(new Domain.Entities.DetencionNota
            {
                Texto = request.ObservacionCierre,
                UsuarioId = CurrentUserId,
                TipoNota = TipoNota.Cierre,
                EsNotaDeCierre = true
            });
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Detención {DetencionId} cerrada por usuario {UserId}", id, CurrentUserId);

        return NoContent();
    }

    /// <summary>
    /// Agregar una nota a una detención.
    /// </summary>
    [HttpPost("{id:long}/notas")]
    [ProducesResponseType(typeof(DetencionNotaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AgregarNota(long id, [FromBody] AgregarNotaDetencionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(id);
        if (detencion == null)
        {
            return NotFound();
        }

        var tipoNota = Enum.TryParse<TipoNota>(request.TipoNota, true, out var parsedTipo)
            ? parsedTipo
            : TipoNota.Operativa;

        var nota = new Domain.Entities.DetencionNota
        {
            DetencionId = id,
            Texto = request.Texto,
            UsuarioId = CurrentUserId,
            TipoNota = tipoNota,
            EsNotaDeCierre = false
        };

        detencion.Notas.Add(nota);
        detencion.ModificadoPorUsuarioId = CurrentUserId;
        detencion.FechaHoraModificacion = DateTimeOffset.UtcNow;

        await _unitOfWork.SaveChangesAsync();

        return CreatedAtAction(nameof(GetDetencion), new { id }, new DetencionNotaDto
        {
            DetencionNotaId = nota.DetencionNotaId,
            Texto = nota.Texto,
            UsuarioId = nota.UsuarioId,
            FechaHora = nota.FechaHora,
            TipoNota = nota.TipoNota.ToString(),
            EsNotaDeCierre = nota.EsNotaDeCierre
        });
    }

    /// <summary>
    /// Obtener detenciones abiertas (no cerradas).
    /// </summary>
    [HttpGet("abiertas")]
    [ProducesResponseType(typeof(IEnumerable<DetencionListaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDetencionesAbiertas([FromQuery] int? lineaId = null)
    {
        var detenciones = await _unitOfWork.Detenciones.GetDetencionesAbiertasAsync();

        if (lineaId.HasValue)
        {
            detenciones = detenciones.Where(d => d.LineaId == lineaId.Value);
        }

        var items = detenciones.Select(d => new DetencionListaDto
        {
            DetencionId = d.DetencionId,
            Codigo = d.Codigo,
            LineaNombre = d.Linea?.Nombre ?? string.Empty,
            EquipoNombre = d.Equipo?.Nombre,
            TipoFallaNombre = d.TipoFalla?.Nombre,
            TurnoNombre = d.Turno?.Nombre ?? string.Empty,
            Fecha = d.Fecha,
            MinutosEfectivos = d.MinutosEfectivos,
            EstadoActual = d.EstadoActual.ToString(),
            EstadoActualDescripcion = d.EstadoActual.ObtenerDescripcion(),
            Eliminada = d.Eliminada,
            RequiereAnalisis = d.RequiereAnalisis,
            ResponsableRol = d.ResponsableRol,
            ResponsableUsuarioId = d.ResponsableUsuarioId,
            CausaFalla = d.CausaFalla,
            PlanAccion = d.PlanAccion,
            AnalisisPorUsuarioId = d.AnalisisPorUsuarioId,
            FechaHoraAnalisis = d.FechaHoraAnalisis,
            EquipoCritico = d.Equipo?.Critico ?? false
        });

        return Ok(items);
    }

    /// <summary>
    /// Actualizar una detención existente.
    /// </summary>
    [HttpPut("{id:long}")]
    [Authorize(Policy = "CanCreateDetenciones")]
    [ProducesResponseType(typeof(DetencionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateDetencion(long id, [FromBody] ActualizarDetencionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(id);
        if (detencion == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Detención no encontrada",
                Detail = $"No se encontró la detención con ID {id}",
                Status = StatusCodes.Status404NotFound
            });
        }

        // Verificar concurrencia
        if (!detencion.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conflicto de concurrencia",
                Detail = "La detención fue modificada por otro usuario. Por favor, recargue los datos e intente nuevamente.",
                Status = StatusCodes.Status409Conflict
            });
        }

        // No permitir editar detenciones cerradas
        if (detencion.EstadoActual == EstadoDetencion.CERRADA)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Detención cerrada",
                Detail = "No se puede modificar una detención cerrada",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Actualizar campos
        detencion.EquipoId = request.EquipoId;
        detencion.TipoFallaId = request.TipoFallaId;
        detencion.DetalleFallaId = request.DetalleFallaId;
        detencion.MinutosDeclarados = request.MinutosDeclarados;
        detencion.FechaHoraParada = request.FechaHoraParada;
        detencion.FechaHoraArranque = request.FechaHoraArranque;
        detencion.Descripcion = request.Descripcion;
        detencion.AccionesRealizadas = request.AccionesRealizadas;
        detencion.ResolucionDescripcion = request.ResolucionDescripcion;
        detencion.ModificadoPorUsuarioId = CurrentUserId;
        detencion.FechaHoraModificacion = DateTimeOffset.UtcNow;

        // Recalcular MinutosEfectivos
        if (Enum.TryParse<OrigenTiempo>(request.OrigenTiempo, true, out var origenTiempo))
        {
            detencion.OrigenTiempo = origenTiempo;
        }

        if (detencion.OrigenTiempo == OrigenTiempo.DECLARADO)
        {
            detencion.MinutosEfectivos = request.MinutosDeclarados ?? 0;
        }
        else if (detencion.OrigenTiempo == OrigenTiempo.CALCULADO && request.FechaHoraParada.HasValue && request.FechaHoraArranque.HasValue)
        {
            detencion.MinutosEfectivos = (int)(request.FechaHoraArranque.Value - request.FechaHoraParada.Value).TotalMinutes;
        }

        await _detencionService.AplicarReglaAnalisisAsync(detencion);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Detención {DetencionId} actualizada por usuario {UserId}", id, CurrentUserId);

        // Recargar con relaciones
        detencion = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(id);

        return Ok(new DetencionDto
        {
            DetencionId = detencion!.DetencionId,
            Codigo = detencion.Codigo,
            LineaId = detencion.LineaId,
            LineaNombre = detencion.Linea?.Nombre ?? string.Empty,
            EquipoId = detencion.EquipoId,
            EquipoNombre = detencion.Equipo?.Nombre,
            TipoFallaId = detencion.TipoFallaId,
            TipoFallaNombre = detencion.TipoFalla?.Nombre,
            DetalleFallaId = detencion.DetalleFallaId,
            DetalleFallaNombre = detencion.DetalleFalla?.Nombre,
            TurnoId = detencion.TurnoId,
            TurnoNombre = detencion.Turno?.Nombre ?? string.Empty,
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
            CreadoPorUsuarioId = detencion.CreadoPorUsuarioId,
            FechaHoraCreacion = detencion.FechaHoraCreacion,
            FechaHoraModificacion = detencion.FechaHoraModificacion,
            Eliminada = detencion.Eliminada,
            RequiereAnalisis = detencion.RequiereAnalisis,
            ResponsableRol = detencion.ResponsableRol,
            ResponsableUsuarioId = detencion.ResponsableUsuarioId,
            CausaFalla = detencion.CausaFalla,
            PlanAccion = detencion.PlanAccion,
            AnalisisPorUsuarioId = detencion.AnalisisPorUsuarioId,
            FechaHoraAnalisis = detencion.FechaHoraAnalisis,
            EquipoCritico = detencion.Equipo?.Critico ?? false,
            RowVersion = detencion.RowVersion
        });
    }

    /// <summary>
    /// Iniciar seguimiento de una detención.
    /// Estado: REGISTRADA → EN_SEGUIMIENTO.
    /// </summary>
    [HttpPost("{id:long}/iniciar-seguimiento")]
    [Authorize(Policy = "CanCloseDetenciones")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IniciarSeguimiento(long id, [FromBody] IniciarSeguimientoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(id);
        if (detencion == null)
        {
            return NotFound();
        }

        // Verificar concurrencia
        if (!detencion.RowVersion.SequenceEqual(request.RowVersion))
        {
            return Conflict(new ProblemDetails
            {
                Title = "Conflicto de concurrencia",
                Detail = "La detención fue modificada por otro usuario.",
                Status = StatusCodes.Status409Conflict
            });
        }

        if (detencion.EstadoActual != EstadoDetencion.REGISTRADA)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Estado inválido",
                Detail = $"Solo se puede iniciar seguimiento de detenciones en estado REGISTRADA. Estado actual: {detencion.EstadoActual}",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var estadoAnterior = detencion.EstadoActual;
        detencion.EstadoActual = EstadoDetencion.EN_SEGUIMIENTO;
        detencion.ModificadoPorUsuarioId = CurrentUserId;
        detencion.FechaHoraModificacion = DateTimeOffset.UtcNow;

        detencion.HistorialEstados.Add(new Domain.Entities.DetencionEstadoHistorial
        {
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = EstadoDetencion.EN_SEGUIMIENTO,
            UsuarioId = CurrentUserId,
            Observacion = request.Observacion ?? "Seguimiento iniciado",
            CorrelationId = CorrelationId
        });

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Detención {DetencionId} en seguimiento por usuario {UserId}", id, CurrentUserId);

        return NoContent();
    }

    /// <summary>
    /// Obtener resumen de detenciones del turno actual agrupadas por tipo de falla.
    /// </summary>
    [HttpGet("resumen-turno")]
    [ProducesResponseType(typeof(ResumenTurnoResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetResumenTurno([FromQuery] int? lineaId = null)
    {
        // Obtener turno actual
        var turno = await _unitOfWork.Turnos.GetTurnoActualAsync();
        if (turno == null)
        {
            return Ok(new ResumenTurnoResponse
            {
                TurnoNombre = "Sin turno",
                Fecha = DateOnly.FromDateTime(DateTime.Today),
                Items = new List<ResumenTurnoItem>(),
                TotalMinutos = 0
            });
        }

        // Obtener detenciones del día y turno actual
        var fechaHoy = DateOnly.FromDateTime(DateTime.Today);
        var (detenciones, _) = await _unitOfWork.Detenciones.GetPaginadoAsync(
            page: 1,
            pageSize: 1000,
            lineaId: lineaId,
            equipoId: null,
            estado: null,
            tipoFallaId: null,
            detalleFallaId: null,
            turnoId: turno.TurnoId,
            fechaDesde: fechaHoy,
            fechaHasta: fechaHoy,
            busqueda: null);

        // Agrupar por tipo de falla
        var resumen = detenciones
            .GroupBy(d => new { d.TipoFallaId, TipoFallaNombre = d.TipoFalla?.Nombre ?? "Sin clasificar" })
            .Select(g => new ResumenTurnoItem
            {
                TipoFallaId = g.Key.TipoFallaId,
                TipoFallaNombre = g.Key.TipoFallaNombre,
                CantidadDetenciones = g.Count(),
                TotalMinutos = g.Sum(d => d.MinutosEfectivos)
            })
            .OrderByDescending(r => r.TotalMinutos)
            .ToList();

        return Ok(new ResumenTurnoResponse
        {
            TurnoNombre = turno.Nombre,
            Fecha = fechaHoy,
            Items = resumen,
            TotalMinutos = resumen.Sum(r => r.TotalMinutos)
        });
    }

    /// <summary>
    /// Exportar detenciones a CSV.
    /// </summary>
    [HttpGet("export/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportarCsv([FromQuery] DetencionFiltroRequest filtro)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        EstadoDetencion? estadoFiltro = null;
        if (!string.IsNullOrEmpty(filtro.Estado) && Enum.TryParse<EstadoDetencion>(filtro.Estado, true, out var estado))
        {
            estadoFiltro = estado;
        }

        // Obtener datos sin paginación (max 10000)
        var (detenciones, _) = await _unitOfWork.Detenciones.GetPaginadoAsync(
            page: 1,
            pageSize: 10000,
            lineaId: filtro.LineaId,
            equipoId: filtro.EquipoId,
            estado: estadoFiltro,
            tipoFallaId: filtro.TipoFallaId,
            detalleFallaId: filtro.DetalleFallaId,
            turnoId: filtro.TurnoId,
            fechaDesde: filtro.FechaDesde,
            fechaHasta: filtro.FechaHasta,
            busqueda: filtro.Busqueda);

        // Generar CSV
        var csv = GenerarCsv(detenciones);

        var fileName = $"detenciones_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        _logger.LogInformation("Usuario {UserId} exportó {Count} detenciones a CSV", CurrentUserId, detenciones.Count());

        return File(csv, "text/csv; charset=utf-8", fileName);
    }

    private static byte[] GenerarCsv(IEnumerable<Domain.Entities.Detencion> detenciones)
    {
        var sb = new System.Text.StringBuilder();

        // Cabecera
        sb.AppendLine("Codigo,Fecha,Linea,Equipo,TipoFalla,DetalleFalla,Turno,MinutosDeclarados,FechaHoraParada,FechaHoraArranque,OrigenTiempo,MinutosEfectivos,Descripcion,Estado,AccionesRealizadas,Resolucion,FechaHoraCierre,FechaHoraCreacion");

        foreach (var d in detenciones)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(d.Codigo),
                d.Fecha.ToString("yyyy-MM-dd"),
                EscapeCsv(d.Linea?.Nombre ?? ""),
                EscapeCsv(d.Equipo?.Nombre ?? ""),
                EscapeCsv(d.TipoFalla?.Nombre ?? ""),
                EscapeCsv(d.DetalleFalla?.Nombre ?? ""),
                EscapeCsv(d.Turno?.Nombre ?? ""),
                d.MinutosDeclarados?.ToString() ?? "",
                d.FechaHoraParada?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                d.FechaHoraArranque?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                d.OrigenTiempo.ToString(),
                d.MinutosEfectivos.ToString(),
                EscapeCsv(d.Descripcion),
                EscapeCsv(d.EstadoActual.ObtenerDescripcion()),
                EscapeCsv(d.AccionesRealizadas ?? ""),
                EscapeCsv(d.ResolucionDescripcion ?? ""),
                d.FechaHoraCierre?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
                d.FechaHoraCreacion.ToString("yyyy-MM-dd HH:mm:ss")
            ));
        }

        // BOM UTF-8 para Excel
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var content = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[bom.Length + content.Length];
        bom.CopyTo(result, 0);
        content.CopyTo(result, bom.Length);

        return result;
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
