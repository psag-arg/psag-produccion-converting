using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Application.DTOs.Catalogos;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Identity;
using System.Text.RegularExpressions;

namespace PSAG.Mantenimiento.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CatalogosController : ApiControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CatalogosController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public CatalogosController(
        IUnitOfWork unitOfWork,
        ILogger<CatalogosController> logger,
        UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _userManager = userManager;
    }

    private static readonly Regex ColorHexRegex = new(@"^#[0-9A-F]{6}$");

    private static readonly string[] RolesResponsablesAnalisis = { Roles.Operario, Roles.Supervisor, Roles.Mantenimiento };

    /// <summary>
    /// Normaliza el color a #RRGGBB en mayúsculas. Vacío significa null (color automático).
    /// </summary>
    private static bool TryNormalizarColor(string? color, out string? normalizado)
    {
        normalizado = string.IsNullOrWhiteSpace(color) ? null : color.Trim().ToUpperInvariant();
        return normalizado == null || ColorHexRegex.IsMatch(normalizado);
    }

    #region Líneas

    [HttpGet("lineas")]
    [ProducesResponseType(typeof(IEnumerable<LineaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLineas([FromQuery] bool? soloActivas = true)
    {
        var lineas = soloActivas == true
            ? await _unitOfWork.Lineas.GetActivasAsync()
            : await _unitOfWork.Lineas.GetAllAsync();

        var items = lineas.Select(l => new LineaDto
        {
            LineaId = l.LineaId,
            Nombre = l.Nombre,
            Descripcion = l.Descripcion,
            Orden = l.Orden,
            Activo = l.Activo
        }).OrderBy(l => l.Orden);

        return Ok(items);
    }

    [HttpGet("lineas/{id:int}")]
    [ProducesResponseType(typeof(LineaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLinea(int id)
    {
        var linea = await _unitOfWork.Lineas.GetByIdAsync(id);
        if (linea == null)
            return NotFound();

        return Ok(new LineaDto
        {
            LineaId = linea.LineaId,
            Nombre = linea.Nombre,
            Descripcion = linea.Descripcion,
            Orden = linea.Orden,
            Activo = linea.Activo
        });
    }

    [HttpPost("lineas")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(LineaDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateLinea([FromBody] CrearLineaRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var linea = new Domain.Entities.Linea
        {
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Orden = request.Orden,
            Activo = true,
            CreadoPorUsuarioId = CurrentUserId,
            ModificadoPorUsuarioId = CurrentUserId
        };

        await _unitOfWork.Lineas.AddAsync(linea);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Línea {Nombre} creada por usuario {UserId}", linea.Nombre, CurrentUserId);

        return CreatedAtAction(nameof(GetLinea), new { id = linea.LineaId }, new LineaDto
        {
            LineaId = linea.LineaId,
            Nombre = linea.Nombre,
            Descripcion = linea.Descripcion,
            Orden = linea.Orden,
            Activo = linea.Activo
        });
    }

    [HttpPut("lineas/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(LineaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateLinea(int id, [FromBody] ActualizarLineaRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var linea = await _unitOfWork.Lineas.GetByIdAsync(id);
        if (linea == null)
            return NotFound();

        // Validar dependencias al desactivar
        if (linea.Activo && !request.Activo)
        {
            var equiposActivos = await _unitOfWork.Equipos.CountAsync(e => e.LineaId == id && e.Activo);
            if (equiposActivos > 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "No se puede desactivar la linea",
                    Detail = $"La linea tiene {equiposActivos} equipo(s) activo(s). Desactive primero los equipos asociados.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        linea.Nombre = request.Nombre;
        linea.Descripcion = request.Descripcion;
        linea.Orden = request.Orden;
        linea.Activo = request.Activo;
        linea.ModificadoPorUsuarioId = CurrentUserId;
        linea.FechaHoraModificacion = DateTimeOffset.UtcNow;

        await _unitOfWork.Lineas.UpdateAsync(linea);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Línea {Nombre} actualizada por usuario {UserId}", linea.Nombre, CurrentUserId);

        return Ok(new LineaDto
        {
            LineaId = linea.LineaId,
            Nombre = linea.Nombre,
            Descripcion = linea.Descripcion,
            Orden = linea.Orden,
            Activo = linea.Activo
        });
    }

    #endregion

    #region Equipos

    [HttpGet("equipos")]
    [ProducesResponseType(typeof(IEnumerable<EquipoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEquipos([FromQuery] int? lineaId = null, [FromQuery] bool? soloActivos = true)
    {
        IEnumerable<Domain.Entities.Equipo> equipos;

        if (lineaId.HasValue)
        {
            equipos = await _unitOfWork.Equipos.GetByLineaAsync(lineaId.Value);
        }
        else
        {
            equipos = soloActivos == true
                ? await _unitOfWork.Equipos.GetActivosAsync()
                : await _unitOfWork.Equipos.GetAllAsync();
        }

        var items = equipos.Select(e => new EquipoDto
        {
            EquipoId = e.EquipoId,
            LineaId = e.LineaId,
            LineaNombre = e.Linea?.Nombre ?? string.Empty,
            Codigo = e.Codigo,
            Nombre = e.Nombre,
            Descripcion = e.Descripcion,
            Orden = e.Orden,
            Critico = e.Critico,
            Activo = e.Activo
        }).OrderBy(e => e.Orden);

        return Ok(items);
    }

    [HttpGet("equipos/{id:int}")]
    [ProducesResponseType(typeof(EquipoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEquipo(int id)
    {
        var equipo = await _unitOfWork.Equipos.GetByIdAsync(id);
        if (equipo == null)
            return NotFound();

        return Ok(new EquipoDto
        {
            EquipoId = equipo.EquipoId,
            LineaId = equipo.LineaId,
            Codigo = equipo.Codigo,
            Nombre = equipo.Nombre,
            Descripcion = equipo.Descripcion,
            Orden = equipo.Orden,
            Critico = equipo.Critico,
            Activo = equipo.Activo
        });
    }

    [HttpPost("equipos")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(EquipoDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateEquipo([FromBody] CrearEquipoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var equipo = new Domain.Entities.Equipo
        {
            LineaId = request.LineaId,
            Codigo = request.Codigo,
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Orden = request.Orden,
            Critico = request.Critico,
            Activo = true,
            CreadoPorUsuarioId = CurrentUserId,
            ModificadoPorUsuarioId = CurrentUserId
        };

        await _unitOfWork.Equipos.AddAsync(equipo);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Equipo {Nombre} creado por usuario {UserId}", equipo.Nombre, CurrentUserId);

        return CreatedAtAction(nameof(GetEquipo), new { id = equipo.EquipoId }, new EquipoDto
        {
            EquipoId = equipo.EquipoId,
            LineaId = equipo.LineaId,
            Codigo = equipo.Codigo,
            Nombre = equipo.Nombre,
            Descripcion = equipo.Descripcion,
            Orden = equipo.Orden,
            Critico = equipo.Critico,
            Activo = equipo.Activo
        });
    }

    [HttpPut("equipos/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(EquipoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateEquipo(int id, [FromBody] ActualizarEquipoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var equipo = await _unitOfWork.Equipos.GetByIdAsync(id);
        if (equipo == null)
            return NotFound();

        // Validar dependencias al desactivar
        if (equipo.Activo && !request.Activo)
        {
            var detallesActivos = await _unitOfWork.DetallesFalla.CountAsync(d => d.EquipoId == id && d.Activo);
            if (detallesActivos > 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "No se puede desactivar el equipo",
                    Detail = $"El equipo tiene {detallesActivos} detalle(s) de falla activo(s). Desactive primero los detalles asociados.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        equipo.LineaId = request.LineaId;
        equipo.Codigo = request.Codigo;
        equipo.Nombre = request.Nombre;
        equipo.Descripcion = request.Descripcion;
        equipo.Orden = request.Orden;
        equipo.Critico = request.Critico;
        equipo.Activo = request.Activo;
        equipo.ModificadoPorUsuarioId = CurrentUserId;
        equipo.FechaHoraModificacion = DateTimeOffset.UtcNow;

        await _unitOfWork.Equipos.UpdateAsync(equipo);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Equipo {Nombre} actualizado por usuario {UserId}", equipo.Nombre, CurrentUserId);

        return Ok(new EquipoDto
        {
            EquipoId = equipo.EquipoId,
            LineaId = equipo.LineaId,
            Codigo = equipo.Codigo,
            Nombre = equipo.Nombre,
            Descripcion = equipo.Descripcion,
            Orden = equipo.Orden,
            Critico = equipo.Critico,
            Activo = equipo.Activo
        });
    }

    #endregion

    #region Tipos de Falla

    [HttpGet("tipos-falla")]
    [ProducesResponseType(typeof(IEnumerable<TipoFallaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTiposFalla([FromQuery] bool? soloActivos = true)
    {
        var tipos = soloActivos == true
            ? await _unitOfWork.TiposFalla.GetActivosAsync()
            : await _unitOfWork.TiposFalla.GetAllAsync();

        var items = tipos.Select(t => new TipoFallaDto
        {
            TipoFallaId = t.TipoFallaId,
            Codigo = t.Codigo,
            Nombre = t.Nombre,
            Descripcion = t.Descripcion,
            Orden = t.Orden,
            Color = t.Color,
            Activo = t.Activo
        }).OrderBy(t => t.Orden);

        return Ok(items);
    }

    [HttpGet("tipos-falla/{id:int}")]
    [ProducesResponseType(typeof(TipoFallaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTipoFalla(int id)
    {
        var tipo = await _unitOfWork.TiposFalla.GetByIdAsync(id);
        if (tipo == null)
            return NotFound();

        return Ok(new TipoFallaDto
        {
            TipoFallaId = tipo.TipoFallaId,
            Codigo = tipo.Codigo,
            Nombre = tipo.Nombre,
            Descripcion = tipo.Descripcion,
            Orden = tipo.Orden,
            Color = tipo.Color,
            Activo = tipo.Activo
        });
    }

    [HttpPost("tipos-falla")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(TipoFallaDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTipoFalla([FromBody] CrearTipoFallaRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        if (!TryNormalizarColor(request.Color, out var color))
            return BadRequest(new ProblemDetails
            {
                Title = "Color inválido",
                Detail = "El color debe tener el formato #RRGGBB.",
                Status = StatusCodes.Status400BadRequest
            });

        var tipo = new Domain.Entities.TipoFalla
        {
            Codigo = request.Codigo,
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Orden = request.Orden,
            Color = color,
            Activo = true,
            CreadoPorUsuarioId = CurrentUserId,
            ModificadoPorUsuarioId = CurrentUserId
        };

        await _unitOfWork.TiposFalla.AddAsync(tipo);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("TipoFalla {Nombre} creado por usuario {UserId}", tipo.Nombre, CurrentUserId);

        return CreatedAtAction(nameof(GetTipoFalla), new { id = tipo.TipoFallaId }, new TipoFallaDto
        {
            TipoFallaId = tipo.TipoFallaId,
            Codigo = tipo.Codigo,
            Nombre = tipo.Nombre,
            Descripcion = tipo.Descripcion,
            Orden = tipo.Orden,
            Color = tipo.Color,
            Activo = tipo.Activo
        });
    }

    [HttpPut("tipos-falla/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(TipoFallaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateTipoFalla(int id, [FromBody] ActualizarTipoFallaRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        if (!TryNormalizarColor(request.Color, out var color))
            return BadRequest(new ProblemDetails
            {
                Title = "Color inválido",
                Detail = "El color debe tener el formato #RRGGBB.",
                Status = StatusCodes.Status400BadRequest
            });

        var tipo = await _unitOfWork.TiposFalla.GetByIdAsync(id);
        if (tipo == null)
            return NotFound();

        // Validar dependencias al desactivar
        if (tipo.Activo && !request.Activo)
        {
            var detallesActivos = await _unitOfWork.DetallesFalla.CountAsync(d => d.TipoFallaId == id && d.Activo);
            if (detallesActivos > 0)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "No se puede desactivar el tipo de falla",
                    Detail = $"El tipo de falla tiene {detallesActivos} detalle(s) de falla activo(s). Desactive primero los detalles asociados.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        tipo.Codigo = request.Codigo;
        tipo.Nombre = request.Nombre;
        tipo.Descripcion = request.Descripcion;
        tipo.Orden = request.Orden;
        tipo.Color = color;
        tipo.Activo = request.Activo;
        tipo.ModificadoPorUsuarioId = CurrentUserId;
        tipo.FechaHoraModificacion = DateTimeOffset.UtcNow;

        await _unitOfWork.TiposFalla.UpdateAsync(tipo);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("TipoFalla {Nombre} actualizado por usuario {UserId}", tipo.Nombre, CurrentUserId);

        return Ok(new TipoFallaDto
        {
            TipoFallaId = tipo.TipoFallaId,
            Codigo = tipo.Codigo,
            Nombre = tipo.Nombre,
            Descripcion = tipo.Descripcion,
            Orden = tipo.Orden,
            Color = tipo.Color,
            Activo = tipo.Activo
        });
    }

    #endregion

    #region Detalles de Falla

    [HttpGet("detalles-falla")]
    [ProducesResponseType(typeof(IEnumerable<DetalleFallaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDetallesFalla(
        [FromQuery] int? tipoFallaId = null,
        [FromQuery] int? equipoId = null,
        [FromQuery] bool? soloActivos = true)
    {
        IEnumerable<Domain.Entities.DetalleFalla> detalles;

        if (tipoFallaId.HasValue || equipoId.HasValue)
        {
            detalles = await _unitOfWork.DetallesFalla.GetByFiltroAsync(equipoId, tipoFallaId);
        }
        else
        {
            detalles = soloActivos == true
                ? await _unitOfWork.DetallesFalla.GetActivosAsync()
                : await _unitOfWork.DetallesFalla.GetAllAsync();
        }

        var items = detalles.Select(d => new DetalleFallaDto
        {
            DetalleFallaId = d.DetalleFallaId,
            TipoFallaId = d.TipoFallaId,
            TipoFallaNombre = d.TipoFalla?.Nombre,
            EquipoId = d.EquipoId,
            EquipoNombre = d.Equipo?.Nombre,
            Codigo = d.Codigo,
            Nombre = d.Nombre,
            Descripcion = d.Descripcion,
            Orden = d.Orden,
            RequiereDescripcion = d.RequiereDescripcion,
            Activo = d.Activo
        }).OrderBy(d => d.Orden);

        return Ok(items);
    }

    [HttpGet("detalles-falla/{id:int}")]
    [ProducesResponseType(typeof(DetalleFallaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetalleFalla(int id)
    {
        var detalle = await _unitOfWork.DetallesFalla.GetByIdAsync(id);
        if (detalle == null)
            return NotFound();

        return Ok(new DetalleFallaDto
        {
            DetalleFallaId = detalle.DetalleFallaId,
            TipoFallaId = detalle.TipoFallaId,
            EquipoId = detalle.EquipoId,
            Codigo = detalle.Codigo,
            Nombre = detalle.Nombre,
            Descripcion = detalle.Descripcion,
            Orden = detalle.Orden,
            RequiereDescripcion = detalle.RequiereDescripcion,
            Activo = detalle.Activo
        });
    }

    [HttpPost("detalles-falla")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(DetalleFallaDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDetalleFalla([FromBody] CrearDetalleFallaRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var detalle = new Domain.Entities.DetalleFalla
        {
            TipoFallaId = request.TipoFallaId,
            EquipoId = request.EquipoId,
            Codigo = request.Codigo,
            Nombre = request.Nombre,
            Descripcion = request.Descripcion,
            Orden = request.Orden,
            RequiereDescripcion = request.RequiereDescripcion,
            Activo = true,
            CreadoPorUsuarioId = CurrentUserId,
            ModificadoPorUsuarioId = CurrentUserId
        };

        await _unitOfWork.DetallesFalla.AddAsync(detalle);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("DetalleFalla {Nombre} creado por usuario {UserId}", detalle.Nombre, CurrentUserId);

        return CreatedAtAction(nameof(GetDetalleFalla), new { id = detalle.DetalleFallaId }, new DetalleFallaDto
        {
            DetalleFallaId = detalle.DetalleFallaId,
            TipoFallaId = detalle.TipoFallaId,
            EquipoId = detalle.EquipoId,
            Codigo = detalle.Codigo,
            Nombre = detalle.Nombre,
            Descripcion = detalle.Descripcion,
            Orden = detalle.Orden,
            RequiereDescripcion = detalle.RequiereDescripcion,
            Activo = detalle.Activo
        });
    }

    [HttpPut("detalles-falla/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(DetalleFallaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDetalleFalla(int id, [FromBody] ActualizarDetalleFallaRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var detalle = await _unitOfWork.DetallesFalla.GetByIdAsync(id);
        if (detalle == null)
            return NotFound();

        detalle.TipoFallaId = request.TipoFallaId;
        detalle.EquipoId = request.EquipoId;
        detalle.Codigo = request.Codigo;
        detalle.Nombre = request.Nombre;
        detalle.Descripcion = request.Descripcion;
        detalle.Orden = request.Orden;
        detalle.RequiereDescripcion = request.RequiereDescripcion;
        detalle.Activo = request.Activo;
        detalle.ModificadoPorUsuarioId = CurrentUserId;
        detalle.FechaHoraModificacion = DateTimeOffset.UtcNow;

        await _unitOfWork.DetallesFalla.UpdateAsync(detalle);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("DetalleFalla {Nombre} actualizado por usuario {UserId}", detalle.Nombre, CurrentUserId);

        return Ok(new DetalleFallaDto
        {
            DetalleFallaId = detalle.DetalleFallaId,
            TipoFallaId = detalle.TipoFallaId,
            EquipoId = detalle.EquipoId,
            Codigo = detalle.Codigo,
            Nombre = detalle.Nombre,
            Descripcion = detalle.Descripcion,
            Orden = detalle.Orden,
            RequiereDescripcion = detalle.RequiereDescripcion,
            Activo = detalle.Activo
        });
    }

    #endregion

    #region Turnos

    [HttpGet("turnos")]
    [ProducesResponseType(typeof(IEnumerable<TurnoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTurnos([FromQuery] bool? soloActivos = true)
    {
        var turnos = soloActivos == true
            ? await _unitOfWork.Turnos.GetActivosAsync()
            : await _unitOfWork.Turnos.GetAllAsync();

        var items = turnos.Select(t => new TurnoDto
        {
            TurnoId = t.TurnoId,
            Codigo = t.Codigo,
            Nombre = t.Nombre,
            HoraInicio = t.HoraInicio,
            HoraFin = t.HoraFin,
            Orden = t.Orden,
            Activo = t.Activo,
            DuracionMinutos = t.DuracionMinutos()
        }).OrderBy(t => t.Orden);

        return Ok(items);
    }

    [HttpGet("turnos/{id:int}")]
    [ProducesResponseType(typeof(TurnoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTurno(int id)
    {
        var turno = await _unitOfWork.Turnos.GetByIdAsync(id);
        if (turno == null)
            return NotFound();

        return Ok(new TurnoDto
        {
            TurnoId = turno.TurnoId,
            Codigo = turno.Codigo,
            Nombre = turno.Nombre,
            HoraInicio = turno.HoraInicio,
            HoraFin = turno.HoraFin,
            Orden = turno.Orden,
            Activo = turno.Activo,
            DuracionMinutos = turno.DuracionMinutos()
        });
    }

    [HttpGet("turnos/actual")]
    [ProducesResponseType(typeof(TurnoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTurnoActual()
    {
        var turno = await _unitOfWork.Turnos.GetTurnoActualAsync();
        if (turno == null)
            return NotFound(new ProblemDetails
            {
                Title = "Turno no encontrado",
                Detail = "No hay un turno configurado para la hora actual",
                Status = StatusCodes.Status404NotFound
            });

        return Ok(new TurnoDto
        {
            TurnoId = turno.TurnoId,
            Codigo = turno.Codigo,
            Nombre = turno.Nombre,
            HoraInicio = turno.HoraInicio,
            HoraFin = turno.HoraFin,
            Orden = turno.Orden,
            Activo = turno.Activo,
            DuracionMinutos = turno.DuracionMinutos()
        });
    }

    [HttpPost("turnos")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(TurnoDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTurno([FromBody] CrearTurnoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var turno = new Domain.Entities.Turno
        {
            Codigo = request.Codigo,
            Nombre = request.Nombre,
            HoraInicio = request.HoraInicio,
            HoraFin = request.HoraFin,
            Orden = request.Orden,
            Activo = true,
            CreadoPorUsuarioId = CurrentUserId,
            ModificadoPorUsuarioId = CurrentUserId
        };

        await _unitOfWork.Turnos.AddAsync(turno);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Turno {Nombre} creado por usuario {UserId}", turno.Nombre, CurrentUserId);

        return CreatedAtAction(nameof(GetTurno), new { id = turno.TurnoId }, new TurnoDto
        {
            TurnoId = turno.TurnoId,
            Codigo = turno.Codigo,
            Nombre = turno.Nombre,
            HoraInicio = turno.HoraInicio,
            HoraFin = turno.HoraFin,
            Orden = turno.Orden,
            Activo = turno.Activo,
            DuracionMinutos = turno.DuracionMinutos()
        });
    }

    [HttpPut("turnos/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(TurnoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTurno(int id, [FromBody] ActualizarTurnoRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var turno = await _unitOfWork.Turnos.GetByIdAsync(id);
        if (turno == null)
            return NotFound();

        turno.Codigo = request.Codigo;
        turno.Nombre = request.Nombre;
        turno.HoraInicio = request.HoraInicio;
        turno.HoraFin = request.HoraFin;
        turno.Orden = request.Orden;
        turno.Activo = request.Activo;
        turno.ModificadoPorUsuarioId = CurrentUserId;
        turno.FechaHoraModificacion = DateTimeOffset.UtcNow;

        await _unitOfWork.Turnos.UpdateAsync(turno);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Turno {Nombre} actualizado por usuario {UserId}", turno.Nombre, CurrentUserId);

        return Ok(new TurnoDto
        {
            TurnoId = turno.TurnoId,
            Codigo = turno.Codigo,
            Nombre = turno.Nombre,
            HoraInicio = turno.HoraInicio,
            HoraFin = turno.HoraFin,
            Orden = turno.Orden,
            Activo = turno.Activo,
            DuracionMinutos = turno.DuracionMinutos()
        });
    }

    #endregion

    #region Reglas de análisis

    [HttpGet("reglas-analisis")]
    [ProducesResponseType(typeof(IEnumerable<ReglaAnalisisDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReglasAnalisis()
    {
        var reglas = await _unitOfWork.ReglasAnalisis.GetAllAsync();
        return Ok(await MapearReglasAnalisisAsync(reglas.OrderBy(r => r.ReglaAnalisisId)));
    }

    [HttpGet("reglas-analisis/{id:int}")]
    [ProducesResponseType(typeof(ReglaAnalisisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReglaAnalisis(int id)
    {
        var regla = await _unitOfWork.ReglasAnalisis.GetByIdAsync(id);
        if (regla == null)
            return NotFound();

        return Ok(await MapearReglaAnalisisAsync(regla));
    }

    [HttpPost("reglas-analisis")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(ReglaAnalisisDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReglaAnalisis([FromBody] CrearReglaAnalisisRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var datos = NormalizarRegla(request);
        var error = await ValidarReglaAnalisisAsync(datos);
        if (error != null)
            return BadRequest(new ProblemDetails
            {
                Title = "Regla de análisis inválida",
                Detail = error,
                Status = StatusCodes.Status400BadRequest
            });

        var regla = new Domain.Entities.ReglaAnalisis
        {
            CreadoPorUsuarioId = CurrentUserId,
            ModificadoPorUsuarioId = CurrentUserId
        };
        AplicarRegla(regla, datos);

        await _unitOfWork.ReglasAnalisis.AddAsync(regla);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Regla de análisis {Id} creada por usuario {UserId}", regla.ReglaAnalisisId, CurrentUserId);

        return CreatedAtAction(nameof(GetReglaAnalisis), new { id = regla.ReglaAnalisisId },
            await MapearReglaAnalisisAsync(regla));
    }

    [HttpPut("reglas-analisis/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(ReglaAnalisisDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateReglaAnalisis(int id, [FromBody] ActualizarReglaAnalisisRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var regla = await _unitOfWork.ReglasAnalisis.GetByIdAsync(id);
        if (regla == null)
            return NotFound();

        var datos = NormalizarRegla(request);
        var error = await ValidarReglaAnalisisAsync(datos);
        if (error != null)
            return BadRequest(new ProblemDetails
            {
                Title = "Regla de análisis inválida",
                Detail = error,
                Status = StatusCodes.Status400BadRequest
            });

        AplicarRegla(regla, datos);
        regla.Activo = request.Activo;
        regla.ModificadoPorUsuarioId = CurrentUserId;

        await _unitOfWork.ReglasAnalisis.UpdateAsync(regla);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Regla de análisis {Id} actualizada por usuario {UserId}", regla.ReglaAnalisisId, CurrentUserId);

        return Ok(await MapearReglaAnalisisAsync(regla));
    }

    private static CrearReglaAnalisisRequest NormalizarRegla(CrearReglaAnalisisRequest request) => new()
    {
        LineaId = IdOpcional(request.LineaId),
        TipoFallaId = request.TipoFallaId,
        EquipoId = IdOpcional(request.EquipoId),
        MinutosMinimos = request.MinutosMinimos,
        ResponsableRol = TextoOpcional(request.ResponsableRol)?.ToUpperInvariant(),
        ResponsableUsuarioId = TextoOpcional(request.ResponsableUsuarioId)
    };

    private static void AplicarRegla(Domain.Entities.ReglaAnalisis regla, CrearReglaAnalisisRequest datos)
    {
        regla.LineaId = datos.LineaId;
        regla.TipoFallaId = datos.TipoFallaId;
        regla.EquipoId = datos.EquipoId;
        regla.MinutosMinimos = datos.MinutosMinimos;
        regla.ResponsableRol = datos.ResponsableRol;
        regla.ResponsableUsuarioId = datos.ResponsableUsuarioId;
    }

    /// <summary>
    /// Valida una regla contra la base. Devuelve el motivo del rechazo o null si es válida.
    /// Exactamente un responsable: un rol de análisis o un usuario activo.
    /// </summary>
    private async Task<string?> ValidarReglaAnalisisAsync(CrearReglaAnalisisRequest datos)
    {
        if (datos.MinutosMinimos <= 0)
            return "Los minutos deben ser mayores que cero.";

        if (await _unitOfWork.TiposFalla.GetByIdAsync(datos.TipoFallaId) == null)
            return "Debe seleccionar un tipo de falla válido.";

        if (datos.LineaId.HasValue && await _unitOfWork.Lineas.GetByIdAsync(datos.LineaId.Value) == null)
            return "La línea seleccionada no existe.";

        if (datos.EquipoId.HasValue)
        {
            var equipo = await _unitOfWork.Equipos.GetByIdAsync(datos.EquipoId.Value);
            if (equipo == null)
                return "El equipo seleccionado no existe.";
            if (datos.LineaId.HasValue && equipo.LineaId != datos.LineaId.Value)
                return "El equipo no pertenece a la línea elegida.";
        }

        if ((datos.ResponsableRol == null) == (datos.ResponsableUsuarioId == null))
            return "Elegí un rol o un usuario responsable, pero no los dos.";

        if (datos.ResponsableRol != null && !RolesResponsablesAnalisis.Contains(datos.ResponsableRol))
            return "El rol responsable no puede analizar fallas.";

        if (datos.ResponsableUsuarioId != null)
        {
            var usuario = await _userManager.FindByIdAsync(datos.ResponsableUsuarioId);
            if (usuario == null || !usuario.Activo)
                return "El usuario responsable no existe o está inactivo.";
        }

        return null;
    }

    /// <summary>
    /// Arma el DTO de una regla pidiendo solo los catálogos que usa (línea, tipo, equipo y usuario).
    /// </summary>
    private async Task<ReglaAnalisisDto> MapearReglaAnalisisAsync(Domain.Entities.ReglaAnalisis regla)
    {
        var linea = regla.LineaId is int lineaId ? await _unitOfWork.Lineas.GetByIdAsync(lineaId) : null;
        var tipo = await _unitOfWork.TiposFalla.GetByIdAsync(regla.TipoFallaId);
        var equipo = regla.EquipoId is int equipoId ? await _unitOfWork.Equipos.GetByIdAsync(equipoId) : null;
        var usuario = regla.ResponsableUsuarioId != null
            ? await _userManager.FindByIdAsync(regla.ResponsableUsuarioId)
            : null;

        return ArmarReglaDto(regla,
            linea?.Nombre,
            tipo?.Nombre,
            equipo?.Nombre,
            usuario == null ? null : NombreVisible(usuario));
    }

    /// <summary>
    /// Arma los DTO de una lista con una consulta por catálogo, en lugar de una por regla.
    /// </summary>
    private async Task<List<ReglaAnalisisDto>> MapearReglasAnalisisAsync(IEnumerable<Domain.Entities.ReglaAnalisis> reglas)
    {
        var lista = reglas.ToList();
        var lineas = (await _unitOfWork.Lineas.GetAllAsync()).ToDictionary(l => l.LineaId, l => l.Nombre);
        var tipos = (await _unitOfWork.TiposFalla.GetAllAsync()).ToDictionary(t => t.TipoFallaId, t => t.Nombre);
        var equipos = (await _unitOfWork.Equipos.GetAllAsync()).ToDictionary(e => e.EquipoId, e => e.Nombre);

        var usuarioIds = lista.Select(r => r.ResponsableUsuarioId).OfType<string>().Distinct().ToList();
        var usuarios = (await _userManager.Users.Where(u => usuarioIds.Contains(u.Id)).ToListAsync())
            .ToDictionary(u => u.Id, NombreVisible);

        return lista.Select(r => ArmarReglaDto(r,
            r.LineaId is int lineaId ? lineas.GetValueOrDefault(lineaId) : null,
            tipos.GetValueOrDefault(r.TipoFallaId),
            r.EquipoId is int equipoId ? equipos.GetValueOrDefault(equipoId) : null,
            r.ResponsableUsuarioId is string usuarioId ? usuarios.GetValueOrDefault(usuarioId) : null)).ToList();
    }

    private static ReglaAnalisisDto ArmarReglaDto(
        Domain.Entities.ReglaAnalisis regla,
        string? lineaNombre,
        string? tipoNombre,
        string? equipoNombre,
        string? usuarioNombre) => new()
    {
        ReglaAnalisisId = regla.ReglaAnalisisId,
        LineaId = regla.LineaId,
        LineaNombre = lineaNombre,
        TipoFallaId = regla.TipoFallaId,
        TipoFallaNombre = tipoNombre ?? string.Empty,
        EquipoId = regla.EquipoId,
        EquipoNombre = equipoNombre,
        MinutosMinimos = regla.MinutosMinimos,
        ResponsableRol = regla.ResponsableRol,
        ResponsableUsuarioId = regla.ResponsableUsuarioId,
        ResponsableUsuarioNombre = usuarioNombre,
        Activo = regla.Activo
    };

    private static string NombreVisible(ApplicationUser usuario) =>
        string.IsNullOrWhiteSpace(usuario.NombreCompleto) ? usuario.UserName ?? usuario.Id : usuario.NombreCompleto;

    private static int? IdOpcional(int? id) => id is > 0 ? id : null;

    private static string? TextoOpcional(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    #endregion

    #region Tiempo programado por día

    /// <summary>
    /// Horas programadas de la línea día por día (ambos extremos incluidos). Incluye el estándar por turnos
    /// para los días sin excepción. Máximo 366 días.
    /// </summary>
    [HttpGet("tiempos-programados")]
    [ProducesResponseType(typeof(IEnumerable<DiaProgramadoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTiemposProgramados([FromQuery] int lineaId, [FromQuery] DateOnly desde, [FromQuery] DateOnly hasta)
    {
        if (desde > hasta || hasta.DayNumber - desde.DayNumber > 366)
            return BadRequest(new ProblemDetails
            {
                Title = "Rango inválido",
                Detail = "El rango debe empezar antes o el mismo día que termina, y tener como máximo 366 días.",
                Status = StatusCodes.Status400BadRequest
            });

        var linea = await _unitOfWork.Lineas.GetByIdAsync(lineaId);
        if (linea == null)
            return NotFound();

        var turnos = await _unitOfWork.Turnos.GetActivosAsync();
        var excepciones = (await _unitOfWork.TiemposProgramadosDia.GetPorRangoAsync(desde, hasta, lineaId)).ToList();

        var dias = TiempoProgramadoReporte.DiasDeLinea(lineaId, linea.Nombre, desde, hasta, turnos, excepciones)
            .Select(d => new DiaProgramadoDto
            {
                Fecha = d.Fecha,
                Horas = (decimal)d.Horas,
                Origen = d.Origen,
                Motivo = d.Motivo,
                TiempoProgramadoDiaId = d.TiempoProgramadoDiaId
            });

        return Ok(dias);
    }

    /// <summary>
    /// Crea o reemplaza la excepción de una línea en una fecha (una fila por línea y fecha).
    /// Una fila histórica pasa a EXCEPCION al editarla.
    /// </summary>
    [HttpPost("tiempos-programados")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(DiaProgramadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarTiempoProgramado([FromBody] GuardarTiempoProgramadoDiaRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var motivo = TextoOpcional(request.Motivo);
        if (request.Horas < 0 || request.Horas > 24)
            return BadRequest(new ProblemDetails
            {
                Title = "Horas inválidas",
                Detail = "Las horas programadas deben estar entre 0 y 24.",
                Status = StatusCodes.Status400BadRequest
            });

        if (motivo?.Length > 200)
            return BadRequest(new ProblemDetails
            {
                Title = "Motivo inválido",
                Detail = "El motivo no puede exceder 200 caracteres.",
                Status = StatusCodes.Status400BadRequest
            });

        if (await _unitOfWork.Lineas.GetByIdAsync(request.LineaId) == null)
            return BadRequest(new ProblemDetails
            {
                Title = "Línea inválida",
                Detail = "Debe seleccionar una línea válida.",
                Status = StatusCodes.Status400BadRequest
            });

        var fila = (await _unitOfWork.TiemposProgramadosDia.FindAsync(
            t => t.LineaId == request.LineaId && t.Fecha == request.Fecha)).FirstOrDefault();
        var esNueva = fila == null;

        fila ??= new Domain.Entities.TiempoProgramadoDia
        {
            LineaId = request.LineaId,
            Fecha = request.Fecha,
            CreadoPorUsuarioId = CurrentUserId
        };

        fila.Horas = Math.Round(request.Horas, 2);
        fila.Motivo = motivo;
        fila.Origen = "EXCEPCION";
        fila.Activo = true;
        fila.ModificadoPorUsuarioId = CurrentUserId;

        if (esNueva)
            await _unitOfWork.TiemposProgramadosDia.AddAsync(fila);
        else
            await _unitOfWork.TiemposProgramadosDia.UpdateAsync(fila);

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Tiempo programado de línea {LineaId} el {Fecha} guardado por usuario {UserId}", fila.LineaId, fila.Fecha, CurrentUserId);

        return Ok(new DiaProgramadoDto
        {
            Fecha = fila.Fecha,
            Horas = fila.Horas,
            Origen = fila.Origen,
            Motivo = fila.Motivo,
            TiempoProgramadoDiaId = fila.TiempoProgramadoDiaId
        });
    }

    /// <summary>
    /// Quita una excepción sin borrarla: el día vuelve a regirse por los turnos activos.
    /// </summary>
    [HttpPost("tiempos-programados/{id:int}/desactivar")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesactivarTiempoProgramado(int id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var fila = await _unitOfWork.TiemposProgramadosDia.GetByIdAsync(id);
        if (fila == null)
            return NotFound();

        fila.Activo = false;
        fila.ModificadoPorUsuarioId = CurrentUserId;

        await _unitOfWork.TiemposProgramadosDia.UpdateAsync(fila);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }

    #endregion

    #region Maquinistas

    [HttpGet("maquinistas")]
    [ProducesResponseType(typeof(IEnumerable<MaquinistaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMaquinistas([FromQuery] int? lineaId = null)
    {
        IEnumerable<Domain.Entities.UsuarioLinea> maquinistas;

        if (lineaId.HasValue)
        {
            maquinistas = await _unitOfWork.UsuariosLineas.GetByLineaIdAsync(lineaId.Value);
        }
        else
        {
            maquinistas = await _unitOfWork.UsuariosLineas.GetAllWithRelationsAsync();
        }

        var items = new List<MaquinistaDto>();
        foreach (var m in maquinistas)
        {
            var user = await _userManager.FindByIdAsync(m.UsuarioId);
            items.Add(new MaquinistaDto
            {
                UsuarioLineaId = m.UsuarioLineaId,
                UsuarioId = m.UsuarioId,
                NombreUsuario = user?.NombreCompleto ?? user?.UserName ?? "Usuario desconocido",
                EmailUsuario = user?.Email ?? string.Empty,
                LineaId = m.LineaId,
                LineaNombre = m.Linea?.Nombre ?? string.Empty,
                TurnoId = m.TurnoId,
                TurnoNombre = m.Turno?.Nombre,
                FechaAsignacion = m.FechaAsignacion
            });
        }

        return Ok(items.OrderBy(m => m.LineaNombre).ThenBy(m => m.NombreUsuario));
    }

    [HttpGet("maquinistas/{id:int}")]
    [ProducesResponseType(typeof(MaquinistaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMaquinista(int id)
    {
        var maquinista = await _unitOfWork.UsuariosLineas.GetByIdAsync(id);
        if (maquinista == null)
            return NotFound();

        var user = await _userManager.FindByIdAsync(maquinista.UsuarioId);

        return Ok(new MaquinistaDto
        {
            UsuarioLineaId = maquinista.UsuarioLineaId,
            UsuarioId = maquinista.UsuarioId,
            NombreUsuario = user?.NombreCompleto ?? user?.UserName ?? "Usuario desconocido",
            EmailUsuario = user?.Email ?? string.Empty,
            LineaId = maquinista.LineaId,
            LineaNombre = maquinista.Linea?.Nombre ?? string.Empty,
            TurnoId = maquinista.TurnoId,
            TurnoNombre = maquinista.Turno?.Nombre,
            FechaAsignacion = maquinista.FechaAsignacion
        });
    }

    [HttpGet("maquinistas/por-usuario/{usuarioId}")]
    [ProducesResponseType(typeof(MaquinistaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMaquinistaPorUsuario(string usuarioId)
    {
        var maquinista = await _unitOfWork.UsuariosLineas.GetByUsuarioIdAsync(usuarioId);
        if (maquinista == null)
            return NotFound();

        var user = await _userManager.FindByIdAsync(maquinista.UsuarioId);

        return Ok(new MaquinistaDto
        {
            UsuarioLineaId = maquinista.UsuarioLineaId,
            UsuarioId = maquinista.UsuarioId,
            NombreUsuario = user?.NombreCompleto ?? user?.UserName ?? "Usuario desconocido",
            EmailUsuario = user?.Email ?? string.Empty,
            LineaId = maquinista.LineaId,
            LineaNombre = maquinista.Linea?.Nombre ?? string.Empty,
            TurnoId = maquinista.TurnoId,
            TurnoNombre = maquinista.Turno?.Nombre,
            FechaAsignacion = maquinista.FechaAsignacion
        });
    }

    /// <summary>
    /// Obtiene todas las asignaciones del usuario actual (mis lineas y turnos).
    /// </summary>
    [HttpGet("maquinistas/mis-asignaciones")]
    [ProducesResponseType(typeof(IEnumerable<MaquinistaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMisAsignaciones()
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var asignaciones = await _unitOfWork.UsuariosLineas.GetAllWithRelationsAsync();
        var misAsignaciones = asignaciones.Where(a => a.UsuarioId == CurrentUserId);

        var user = await _userManager.FindByIdAsync(CurrentUserId);

        var items = misAsignaciones.Select(m => new MaquinistaDto
        {
            UsuarioLineaId = m.UsuarioLineaId,
            UsuarioId = m.UsuarioId,
            NombreUsuario = user?.NombreCompleto ?? user?.UserName ?? "Usuario desconocido",
            EmailUsuario = user?.Email ?? string.Empty,
            LineaId = m.LineaId,
            LineaNombre = m.Linea?.Nombre ?? string.Empty,
            TurnoId = m.TurnoId,
            TurnoNombre = m.Turno?.Nombre,
            FechaAsignacion = m.FechaAsignacion
        }).OrderBy(m => m.LineaNombre).ToList();

        return Ok(items);
    }

    [HttpPost("maquinistas")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(MaquinistaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateMaquinista([FromBody] CrearMaquinistaRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        // Validar que el usuario existe
        var user = await _userManager.FindByIdAsync(request.UsuarioId);
        if (user == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Usuario no encontrado",
                Detail = "El usuario especificado no existe en el sistema.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Validar que no exista ya una asignacion para ese usuario y linea
        var existente = await _unitOfWork.UsuariosLineas.GetByUsuarioYLineaAsync(request.UsuarioId, request.LineaId);
        if (existente != null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Asignacion duplicada",
                Detail = "El usuario ya esta asignado a esta linea.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var maquinista = new Domain.Entities.UsuarioLinea
        {
            UsuarioId = request.UsuarioId,
            LineaId = request.LineaId,
            TurnoId = request.TurnoId,
            FechaAsignacion = DateTimeOffset.UtcNow,
            AsignadoPorUsuarioId = CurrentUserId
        };

        await _unitOfWork.UsuariosLineas.AddAsync(maquinista);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Maquinista {UsuarioId} asignado a linea {LineaId} por usuario {UserId}",
            maquinista.UsuarioId, maquinista.LineaId, CurrentUserId);

        // Recargar con relaciones
        var creado = await _unitOfWork.UsuariosLineas.GetByUsuarioYLineaAsync(maquinista.UsuarioId, maquinista.LineaId);

        return CreatedAtAction(nameof(GetMaquinista), new { id = maquinista.UsuarioLineaId }, new MaquinistaDto
        {
            UsuarioLineaId = maquinista.UsuarioLineaId,
            UsuarioId = maquinista.UsuarioId,
            NombreUsuario = user.NombreCompleto ?? user.UserName ?? "Usuario desconocido",
            EmailUsuario = user.Email ?? string.Empty,
            LineaId = maquinista.LineaId,
            LineaNombre = creado?.Linea?.Nombre ?? string.Empty,
            TurnoId = maquinista.TurnoId,
            TurnoNombre = creado?.Turno?.Nombre,
            FechaAsignacion = maquinista.FechaAsignacion
        });
    }

    [HttpPut("maquinistas/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(typeof(MaquinistaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMaquinista(int id, [FromBody] ActualizarMaquinistaRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var maquinista = await _unitOfWork.UsuariosLineas.GetByIdAsync(id);
        if (maquinista == null)
            return NotFound();

        // Si cambia la linea, validar que no exista otra asignacion
        if (maquinista.LineaId != request.LineaId)
        {
            var existente = await _unitOfWork.UsuariosLineas.GetByUsuarioYLineaAsync(maquinista.UsuarioId, request.LineaId);
            if (existente != null && existente.UsuarioLineaId != id)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Asignacion duplicada",
                    Detail = "El usuario ya esta asignado a esta linea.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        maquinista.LineaId = request.LineaId;
        maquinista.TurnoId = request.TurnoId;

        await _unitOfWork.UsuariosLineas.UpdateAsync(maquinista);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Maquinista {UsuarioLineaId} actualizado por usuario {UserId}", id, CurrentUserId);

        // Recargar con relaciones
        var actualizado = await _unitOfWork.UsuariosLineas.GetByUsuarioYLineaAsync(maquinista.UsuarioId, maquinista.LineaId);
        var user = await _userManager.FindByIdAsync(maquinista.UsuarioId);

        return Ok(new MaquinistaDto
        {
            UsuarioLineaId = maquinista.UsuarioLineaId,
            UsuarioId = maquinista.UsuarioId,
            NombreUsuario = user?.NombreCompleto ?? user?.UserName ?? "Usuario desconocido",
            EmailUsuario = user?.Email ?? string.Empty,
            LineaId = maquinista.LineaId,
            LineaNombre = actualizado?.Linea?.Nombre ?? string.Empty,
            TurnoId = maquinista.TurnoId,
            TurnoNombre = actualizado?.Turno?.Nombre,
            FechaAsignacion = maquinista.FechaAsignacion
        });
    }

    [HttpDelete("maquinistas/{id:int}")]
    [Authorize(Policy = "CanManageCatalogs")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteMaquinista(int id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var maquinista = await _unitOfWork.UsuariosLineas.GetByIdAsync(id);
        if (maquinista == null)
            return NotFound();

        // Validar que no tenga detenciones asociadas
        var tieneDetenciones = await _unitOfWork.Detenciones.ExistsAsync(d => d.UsuarioLineaId == id);
        if (tieneDetenciones)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "No se puede eliminar",
                Detail = "El maquinista tiene detenciones asociadas. Puede reasignarlo a otra linea en lugar de eliminarlo.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Para UsuarioLinea usamos delete real ya que no tiene Activo
        // Esto es una excepcion al patron de soft delete porque es una tabla de relacion
        _unitOfWork.UsuariosLineas.DeleteAsync(maquinista);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Maquinista {UsuarioLineaId} eliminado por usuario {UserId}", id, CurrentUserId);

        return NoContent();
    }

    #endregion

    #region Productos (Eje 2)

    [HttpGet("productos")]
    [ProducesResponseType(typeof(IEnumerable<Application.DTOs.Proceso.ProductoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProductos([FromQuery] int? lineaId = null, [FromQuery] bool? soloActivos = true)
    {
        IEnumerable<Domain.Entities.Producto> productos;

        if (lineaId.HasValue)
        {
            productos = await _unitOfWork.Productos.GetByLineaIdAsync(lineaId.Value);
        }
        else
        {
            productos = soloActivos == true
                ? await _unitOfWork.Productos.GetActivosAsync()
                : await _unitOfWork.Productos.GetAllAsync();
        }

        var items = productos.Select(p => new Application.DTOs.Proceso.ProductoDto
        {
            ProductoId = p.ProductoId,
            Codigo = p.Codigo,
            Nombre = p.Nombre,
            Descripcion = p.Descripcion,
            Activo = p.Activo
        }).OrderBy(p => p.Nombre);

        return Ok(items);
    }

    [HttpGet("productos/{id:int}")]
    [ProducesResponseType(typeof(Application.DTOs.Proceso.ProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProducto(int id)
    {
        var producto = await _unitOfWork.Productos.GetByIdAsync(id);
        if (producto == null)
            return NotFound();

        return Ok(new Application.DTOs.Proceso.ProductoDto
        {
            ProductoId = producto.ProductoId,
            Codigo = producto.Codigo,
            Nombre = producto.Nombre,
            Descripcion = producto.Descripcion,
            Activo = producto.Activo
        });
    }

    #endregion
}
