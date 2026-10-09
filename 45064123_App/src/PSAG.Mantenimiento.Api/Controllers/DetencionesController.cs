using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DetencionesController : ApiControllerBase
{
    private const int MaxRegistrosExportacion = 10000;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IDetencionService _detencionService;
    private readonly IExportacionService _exportacionService;
    private readonly IAuditoriaService _auditoriaService;
    private readonly ILogger<DetencionesController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeZoneInfo _zonaPlanta;

    public DetencionesController(
        IUnitOfWork unitOfWork,
        IDetencionService detencionService,
        IExportacionService exportacionService,
        IAuditoriaService auditoriaService,
        ILogger<DetencionesController> logger,
        UserManager<ApplicationUser> userManager,
        TimeZoneInfo zonaPlanta)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
        _detencionService = detencionService;
        _exportacionService = exportacionService;
        _auditoriaService = auditoriaService;
        _logger = logger;
        _zonaPlanta = zonaPlanta;
    }

    /// <summary>
    /// Obtener lista paginada de detenciones con filtros.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DetencionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDetenciones([FromQuery] DetencionFiltroRequest filtro)
    {
        var (detenciones, total) = await BuscarDetencionesAsync(filtro, filtro.Page, filtro.PageSize);

        var items = detenciones.Select(d => DatosAnalisisMapper.ConDatosAnalisis(new DetencionDto
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
            RowVersion = d.RowVersion
        }, d)).ToList();

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

        var dto = DatosAnalisisMapper.ConDatosAnalisis(new DetencionDetalleDto
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
                EstadoNuevoDescripcion = h.EstadoNuevo.ObtenerDescripcion(),
                FechaHora = h.FechaHora,
                UsuarioId = h.UsuarioId,
                Observacion = h.Observacion
            }).OrderByDescending(h => h.FechaHora).ToList()
        }, detencion);

        dto.CreadoPorUsuarioNombre = await NombreMaquinistaAsync(detencion);

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

        // Con hora de inicio y fin, el turno sale de la hora de inicio.
        var tiempoManual = CalcularTiempoManual(request.Fecha, request.HoraInicio, request.HoraFin);
        var errorHoras = ValidarHorasManuales(request.HoraInicio, request.HoraFin, tiempoManual);
        if (errorHoras != null)
        {
            return BadRequest(errorHoras);
        }

        if (tiempoManual.HasValue)
        {
            var turnoPorHora = await _unitOfWork.Turnos.GetTurnoPorHoraAsync(request.HoraInicio!.Value);
            if (turnoPorHora == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Turno no encontrado",
                    Detail = "No hay un turno activo para la hora de inicio ingresada.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
            turnoId = turnoPorHora.TurnoId;
            turnoNombre = turnoPorHora.Nombre;
        }

        // Mismo cálculo que la previsualización de análisis.
        var tiempo = TiempoDetencionCalculador.Calcular(TiempoDeSolicitud(request), _zonaPlanta);

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
            MinutosDeclarados = tiempo.MinutosDeclarados,
            FechaHoraParada = tiempo.FechaHoraParada,
            FechaHoraArranque = tiempo.FechaHoraArranque,
            OrigenTiempo = tiempo.Origen,
            MinutosEfectivos = tiempo.MinutosEfectivos,
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

        // El cierre de una detención con análisis se hace desde el formulario de análisis (Solución y Causa).
        if (detencion.RequiereAnalisis)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Requiere análisis",
                Detail = "Esta detención requiere análisis: cerrala desde el formulario de Identificación de fallas",
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

        var items = detenciones.Select(d => DatosAnalisisMapper.ConDatosAnalisis(new DetencionListaDto
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
        }, d));

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

        var fecha = request.Fecha ?? detencion.Fecha;
        var tiempoManual = CalcularTiempoManual(fecha, request.HoraInicio, request.HoraFin);
        var errorHoras = ValidarHorasManuales(request.HoraInicio, request.HoraFin, tiempoManual);
        if (errorHoras != null)
        {
            return BadRequest(errorHoras);
        }

        // Lo que no cargó el usuario no se toca: tiempo, origen y turno siguen igual.
        var guardado = new TiempoDetencion(
            detencion.OrigenTiempo, detencion.MinutosEfectivos, detencion.MinutosDeclarados,
            detencion.FechaHoraParada, detencion.FechaHoraArranque);
        var edicion = TiempoDetencionCalculador.Editar(guardado, TiempoDeSolicitud(request, fecha), _zonaPlanta);

        int? turnoIdPorHora = null;
        if (edicion.RederivarTurno)
        {
            var turnoPorHora = await _unitOfWork.Turnos.GetTurnoPorHoraAsync(request.HoraInicio!.Value);
            if (turnoPorHora == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Turno no encontrado",
                    Detail = "No hay un turno activo para la hora de inicio ingresada.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
            turnoIdPorHora = turnoPorHora.TurnoId;
        }

        // Actualizar campos
        if (request.Fecha.HasValue)
        {
            detencion.Fecha = request.Fecha.Value;
        }
        detencion.EquipoId = request.EquipoId;
        detencion.TipoFallaId = request.TipoFallaId;
        detencion.DetalleFallaId = request.DetalleFallaId;
        detencion.Descripcion = request.Descripcion;
        // La solución es parte del análisis (/api/analisis): editar la detención no la borra.
        if (request.AccionesRealizadas != null)
            detencion.AccionesRealizadas = request.AccionesRealizadas;
        detencion.ResolucionDescripcion = request.ResolucionDescripcion;
        detencion.ModificadoPorUsuarioId = CurrentUserId;
        detencion.FechaHoraModificacion = DateTimeOffset.UtcNow;

        var tiempo = edicion.Tiempo;
        detencion.OrigenTiempo = tiempo.Origen;
        detencion.MinutosEfectivos = tiempo.MinutosEfectivos;
        detencion.MinutosDeclarados = tiempo.MinutosDeclarados;
        detencion.FechaHoraParada = tiempo.FechaHoraParada;
        detencion.FechaHoraArranque = tiempo.FechaHoraArranque;
        if (turnoIdPorHora.HasValue)
        {
            detencion.TurnoId = turnoIdPorHora.Value;
        }

        await _detencionService.AplicarReglaAnalisisAsync(detencion);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Detención {DetencionId} actualizada por usuario {UserId}", id, CurrentUserId);

        // Recargar con relaciones
        detencion = await _unitOfWork.Detenciones.GetByIdWithDetailsAsync(id);

        return Ok(DatosAnalisisMapper.ConDatosAnalisis(new DetencionDto
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
            RowVersion = detencion.RowVersion
        }, detencion!));
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
    /// Exportar detenciones a Excel (.xlsx) con los mismos filtros de la lista.
    /// </summary>
    [HttpGet("export/xlsx")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportarXlsx([FromQuery] DetencionFiltroRequest filtro)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        var (detenciones, _) = await BuscarDetencionesAsync(filtro, page: 1, pageSize: MaxRegistrosExportacion);
        var xlsx = _exportacionService.GenerarDetencionesXlsx(detenciones);

        _logger.LogInformation("Usuario {UserId} exportó {Count} detenciones a Excel", CurrentUserId, detenciones.Count());

        return File(
            xlsx,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"detenciones_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
    }

    /// <summary>
    /// Indica si una detención con estos datos va a requerir análisis. No guarda nada.
    /// </summary>
    [HttpPost("previsualizar-analisis")]
    [Authorize(Policy = "CanCreateDetenciones")]
    [ProducesResponseType(typeof(PrevisualizacionAnalisisResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PrevisualizarAnalisis([FromBody] CrearDetencionRequest request)
    {
        var tiempo = TiempoDetencionCalculador.Calcular(TiempoDeSolicitud(request), _zonaPlanta);

        var detencion = new Detencion
        {
            LineaId = request.LineaId,
            EquipoId = request.EquipoId,
            TipoFallaId = request.TipoFallaId,
            MinutosEfectivos = tiempo.MinutosEfectivos
        };
        await _detencionService.AplicarReglaAnalisisAsync(detencion);

        return Ok(new PrevisualizacionAnalisisResponse { RequiereAnalisis = detencion.RequiereAnalisis });
    }

    /// <summary>
    /// Eliminar una detención (borrado lógico). Sólo administrador. Requiere motivo.
    /// </summary>
    [HttpPost("{id:long}/eliminar")]
    [Authorize(Policy = "RequireAdministrador")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarDetencion(long id, [FromBody] EliminarDetencionRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        var motivo = request.Motivo?.Trim() ?? string.Empty;
        if (motivo.Length == 0 || motivo.Length > 500)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Motivo no válido",
                Detail = "El motivo es obligatorio y puede tener hasta 500 caracteres.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // El filtro global de EF oculta las eliminadas: eliminar una ya eliminada da 404.
        var detencion = await _unitOfWork.Detenciones.GetByIdAsync(id);
        if (detencion == null)
        {
            return NotFound();
        }

        detencion.Eliminada = true;
        detencion.FechaHoraEliminacion = DateTimeOffset.UtcNow;
        detencion.EliminadaPorUsuarioId = CurrentUserId;
        detencion.MotivoEliminacion = motivo;
        detencion.ModificadoPorUsuarioId = CurrentUserId;

        await _unitOfWork.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            CurrentUserId,
            TipoAccionAuditoria.CorreccionAdministrativa,
            "Detencion",
            id.ToString(),
            new { Eliminada = false },
            new { Eliminada = true, Motivo = motivo },
            $"Detención {detencion.Codigo} eliminada");

        _logger.LogInformation("Detención {DetencionId} eliminada por usuario {UserId}", id, CurrentUserId);

        return NoContent();
    }

    /// <summary>
    /// Restaurar una detención eliminada. Sólo administrador.
    /// </summary>
    [HttpPost("{id:long}/restaurar")]
    [Authorize(Policy = "RequireAdministrador")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestaurarDetencion(long id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        var detencion = await _unitOfWork.Detenciones.GetByIdIncluyendoEliminadasAsync(id);
        if (detencion == null)
        {
            return NotFound();
        }

        if (!detencion.Eliminada)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Detención no eliminada",
                Detail = "Sólo se pueden restaurar detenciones eliminadas.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var motivoAnterior = detencion.MotivoEliminacion;
        detencion.Eliminada = false;
        detencion.FechaHoraEliminacion = null;
        detencion.EliminadaPorUsuarioId = null;
        detencion.MotivoEliminacion = null;
        detencion.ModificadoPorUsuarioId = CurrentUserId;

        await _unitOfWork.SaveChangesAsync();

        await _auditoriaService.RegistrarAsync(
            CurrentUserId,
            TipoAccionAuditoria.CorreccionAdministrativa,
            "Detencion",
            id.ToString(),
            new { Eliminada = true, Motivo = motivoAnterior },
            new { Eliminada = false },
            $"Detención {detencion.Codigo} restaurada");

        _logger.LogInformation("Detención {DetencionId} restaurada por usuario {UserId}", id, CurrentUserId);

        return NoContent();
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

    private Task<(IEnumerable<Detencion> Items, int TotalCount)> BuscarDetencionesAsync(
        DetencionFiltroRequest filtro,
        int page,
        int pageSize)
    {
        EstadoDetencion? estadoFiltro = null;
        if (!string.IsNullOrEmpty(filtro.Estado) && Enum.TryParse<EstadoDetencion>(filtro.Estado, true, out var estado))
        {
            estadoFiltro = estado;
        }

        // Sólo el administrador ve las eliminadas; para el resto el filtro global de EF las oculta.
        var verEliminadas = filtro.VerEliminadas && User.IsInRole("ADMINISTRADOR");

        return _unitOfWork.Detenciones.GetPaginadoAsync(
            page,
            pageSize,
            lineaId: filtro.LineaId,
            equipoId: filtro.EquipoId,
            estado: estadoFiltro,
            tipoFallaId: filtro.TipoFallaId,
            detalleFallaId: filtro.DetalleFallaId,
            turnoId: filtro.TurnoId,
            fechaDesde: filtro.FechaDesde,
            fechaHasta: filtro.FechaHasta,
            busqueda: filtro.Busqueda,
            analisisPendiente: filtro.AnalisisPendiente,
            incluirEliminadas: verEliminadas);
    }

    /// <summary>
    /// Horas de inicio y fin van juntas. Devuelve el error de validación o null.
    /// </summary>
    private static ProblemDetails? ValidarHorasManuales(TimeOnly? horaInicio, TimeOnly? horaFin, TiempoManual? tiempo)
    {
        if (horaInicio.HasValue != horaFin.HasValue)
        {
            return new ProblemDetails
            {
                Title = "Horas incompletas",
                Detail = "Ingresá la hora de inicio y la de fin, o ninguna de las dos.",
                Status = StatusCodes.Status400BadRequest
            };
        }

        if (tiempo is { Minutos: <= 0 })
        {
            return new ProblemDetails
            {
                Title = "Horas no válidas",
                Detail = "La hora de inicio y la de fin no pueden ser iguales.",
                Status = StatusCodes.Status400BadRequest
            };
        }

        return null;
    }

    private TiempoManual? CalcularTiempoManual(DateOnly fecha, TimeOnly? horaInicio, TimeOnly? horaFin) =>
        horaInicio is { } inicio && horaFin is { } fin
            ? TiempoManualCalculador.Calcular(fecha, inicio, fin, _zonaPlanta)
            : null;

    private static TiempoCargado TiempoDeSolicitud(CrearDetencionRequest request) => new(
        request.Fecha,
        request.HoraInicio,
        request.HoraFin,
        ParsearOrigen(request.OrigenTiempo),
        request.MinutosDeclarados,
        request.FechaHoraParada,
        request.FechaHoraArranque);

    private static TiempoCargado TiempoDeSolicitud(ActualizarDetencionRequest request, DateOnly fecha) => new(
        fecha,
        request.HoraInicio,
        request.HoraFin,
        ParsearOrigen(request.OrigenTiempo),
        request.MinutosDeclarados,
        request.FechaHoraParada,
        request.FechaHoraArranque);

    private static OrigenTiempo? ParsearOrigen(string? texto) =>
        Enum.TryParse<OrigenTiempo>(texto, true, out var origen) ? origen : null;

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

    /// <summary>
    /// Maquinista: el usuario asignado a la línea en la carga; si no hay, quien la registró.
    /// </summary>
    private async Task<string> NombreMaquinistaAsync(Detencion detencion)
    {
        var usuarioId = detencion.CreadoPorUsuarioId;
        if (detencion.UsuarioLineaId.HasValue)
        {
            var asignacion = await _unitOfWork.UsuariosLineas.GetByIdAsync(detencion.UsuarioLineaId.Value);
            usuarioId = asignacion?.UsuarioId ?? usuarioId;
        }

        var usuario = string.IsNullOrEmpty(usuarioId) ? null : await _userManager.FindByIdAsync(usuarioId);
        return usuario == null ? string.Empty : (string.IsNullOrWhiteSpace(usuario.NombreCompleto) ? usuario.UserName ?? string.Empty : usuario.NombreCompleto);
    }
}
