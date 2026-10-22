using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.DTOs.Catalogos;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

/// <summary>
/// Calendario base de horas programadas por línea (T-542): qué turnos trabaja cada día de la semana.
/// Lo carga el supervisor de la línea o un administrador. Las excepciones por día siguen en
/// api/catalogos/tiempos-programados.
/// </summary>
[Route("api/calendario-lineas")]
[ApiController]
[Authorize]
public class CalendarioLineasController : ApiControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CalendarioLineasController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public CalendarioLineasController(
        IUnitOfWork unitOfWork,
        ILogger<CalendarioLineasController> logger,
        UserManager<ApplicationUser> userManager)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _userManager = userManager;
    }

    /// <summary>
    /// Líneas activas con la marca de si el usuario puede cargar su calendario.
    /// </summary>
    [HttpGet("lineas")]
    [ProducesResponseType(typeof(IEnumerable<LineaCalendarioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLineas()
    {
        var asignadas = await LineasAsignadasAsync();
        var lineas = (await _unitOfWork.Lineas.GetAllAsync())
            .Where(l => l.Activo)
            .OrderBy(l => l.Orden)
            .ThenBy(l => l.Nombre)
            .Select(l => new LineaCalendarioDto
            {
                LineaId = l.LineaId,
                Nombre = l.Nombre,
                PuedeEditar = CalendarioLineaReglas.PuedeEditar(Roles, asignadas, l.LineaId)
            });

        return Ok(lineas);
    }

    [HttpGet("{lineaId:int}")]
    [ProducesResponseType(typeof(CalendarioLineaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalendario(int lineaId)
    {
        var linea = await _unitOfWork.Lineas.GetByIdAsync(lineaId);
        if (linea == null)
            return NotFound();

        var turnos = (await _unitOfWork.Turnos.GetActivosAsync()).ToList();
        var versiones = await _unitOfWork.CalendariosLinea.GetActivosAsync(lineaId);
        var asignadas = await LineasAsignadasAsync();

        var nombres = new Dictionary<string, string>();
        foreach (var id in versiones.Select(v => v.ModificadoPorUsuarioId).Distinct())
        {
            var usuario = await _userManager.FindByIdAsync(id);
            nombres[id] = usuario == null ? id : (string.IsNullOrWhiteSpace(usuario.NombreCompleto) ? usuario.UserName ?? id : usuario.NombreCompleto);
        }

        var dto = new CalendarioLineaDto
        {
            LineaId = linea.LineaId,
            LineaNombre = linea.Nombre,
            PuedeEditar = CalendarioLineaReglas.PuedeEditar(Roles, asignadas, lineaId),
            HorasEstandar = TurnoReglas.MinutosProgramados(turnos) / 60m,
            Turnos = turnos.Select(t => new TurnoDto
            {
                TurnoId = t.TurnoId,
                Codigo = t.Codigo,
                Nombre = t.Nombre,
                HoraInicio = t.HoraInicio,
                HoraFin = t.HoraFin,
                Orden = t.Orden,
                Activo = t.Activo,
                DuracionMinutos = t.DuracionMinutos()
            }).ToList(),
            Versiones = versiones
                .OrderByDescending(v => v.VigenteDesde)
                .Select(v => new CalendarioLineaVersionDto
                {
                    CalendarioLineaId = v.CalendarioLineaId,
                    VigenteDesde = v.VigenteDesde,
                    ModificadoPor = nombres.GetValueOrDefault(v.ModificadoPorUsuarioId, v.ModificadoPorUsuarioId),
                    FechaHoraModificacion = v.FechaHoraModificacion,
                    Dias = Enum.GetValues<DayOfWeek>()
                        .Select(dia =>
                        {
                            var ids = v.TurnosDelDia(dia).ToHashSet();
                            return new CalendarioLineaDiaDto
                            {
                                DiaSemana = (int)dia,
                                TurnoIds = ids.OrderBy(i => i).ToList(),
                                Horas = TurnoReglas.MinutosProgramados(turnos.Where(t => ids.Contains(t.TurnoId))) / 60m
                            };
                        })
                        .ToList()
                })
                .ToList()
        };

        return Ok(dto);
    }

    /// <summary>
    /// Guarda una versión del calendario que rige desde VigenteDesde. Si ya había una versión activa con
    /// esa fecha, queda inactiva (no se borra) y la reemplaza la nueva.
    /// </summary>
    [HttpPost("{lineaId:int}")]
    [Authorize(Roles = RolesSistema.Administrador + "," + RolesSistema.Supervisor)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GuardarCalendario(int lineaId, [FromBody] GuardarCalendarioLineaRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var linea = await _unitOfWork.Lineas.GetByIdAsync(lineaId);
        if (linea == null)
            return BadRequest(Problema("Línea inválida", "Elegí una línea válida."));

        if (!CalendarioLineaReglas.PuedeEditar(Roles, await LineasAsignadasAsync(), lineaId))
            return Forbid();

        if (request.VigenteDesde == default)
            return BadRequest(Problema("Fecha inválida", "Elegí desde qué fecha rige el calendario."));

        var grilla = (request.Dias ?? new())
            .SelectMany(d => (d.TurnoIds ?? new()).Select(t => ((DayOfWeek)d.DiaSemana, t)))
            .ToList();
        var turnos = await _unitOfWork.Turnos.GetActivosAsync();
        var motivo = CalendarioLineaReglas.MotivoInvalido(grilla, turnos);
        if (motivo != null)
            return BadRequest(Problema("Calendario inválido", motivo));

        var anteriores = await _unitOfWork.CalendariosLinea.FindAsync(
            c => c.LineaId == lineaId && c.VigenteDesde == request.VigenteDesde && c.Activo);

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Primero se desactiva la versión de la misma fecha: el índice único admite una sola activa.
            foreach (var anterior in anteriores)
            {
                anterior.Activo = false;
                anterior.ModificadoPorUsuarioId = CurrentUserId;
                await _unitOfWork.CalendariosLinea.UpdateAsync(anterior);
            }
            await _unitOfWork.SaveChangesAsync();

            var version = new CalendarioLinea
            {
                LineaId = lineaId,
                VigenteDesde = request.VigenteDesde,
                Activo = true,
                CreadoPorUsuarioId = CurrentUserId,
                ModificadoPorUsuarioId = CurrentUserId,
                Turnos = CalendarioLineaReglas.Filas(grilla)
            };
            await _unitOfWork.CalendariosLinea.AddAsync(version);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();

            _logger.LogInformation("Calendario de línea {LineaId} desde {VigenteDesde} guardado por usuario {UserId}",
                lineaId, request.VigenteDesde, CurrentUserId);

            return Ok(new { version.CalendarioLineaId });
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <summary>
    /// Quita una versión sin borrarla: desde su fecha rige la versión anterior (o el estándar de planta).
    /// </summary>
    [HttpPost("versiones/{id:int}/desactivar")]
    [Authorize(Roles = RolesSistema.Administrador + "," + RolesSistema.Supervisor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DesactivarVersion(int id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var version = await _unitOfWork.CalendariosLinea.GetByIdAsync(id);
        if (version == null)
            return NotFound();

        if (!CalendarioLineaReglas.PuedeEditar(Roles, await LineasAsignadasAsync(), version.LineaId))
            return Forbid();

        version.Activo = false;
        version.ModificadoPorUsuarioId = CurrentUserId;
        await _unitOfWork.CalendariosLinea.UpdateAsync(version);
        await _unitOfWork.SaveChangesAsync();

        return NoContent();
    }

    private async Task<int[]> LineasAsignadasAsync() =>
        string.IsNullOrEmpty(CurrentUserId)
            ? Array.Empty<int>()
            : (await _unitOfWork.UsuariosLineas.FindAsync(ul => ul.UsuarioId == CurrentUserId))
                .Select(ul => ul.LineaId)
                .Distinct()
                .ToArray();

    private static ProblemDetails Problema(string titulo, string detalle) =>
        new() { Title = titulo, Detail = detalle, Status = StatusCodes.Status400BadRequest };
}
