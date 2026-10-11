using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Application.DTOs.Auth;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "ADMINISTRADOR")]
public class UsuariosController : ApiControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UsuariosController> _logger;

    public UsuariosController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IUnitOfWork unitOfWork,
        ILogger<UsuariosController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UsuarioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsuarios()
    {
        var users = await _userManager.Users.ToListAsync();
        var result = new List<UsuarioDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var lineas = await _unitOfWork.UsuariosLineas.FindAsync(ul => ul.UsuarioId == user.Id);
            var lineasInfo = new List<LineaAsignadaDto>();

            foreach (var ul in lineas)
            {
                var linea = await _unitOfWork.Lineas.GetByIdAsync(ul.LineaId);
                if (linea != null)
                {
                    lineasInfo.Add(new LineaAsignadaDto
                    {
                        LineaId = linea.LineaId,
                        Nombre = linea.Nombre
                    });
                }
            }

            result.Add(new UsuarioDto
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                NombreCompleto = user.NombreCompleto ?? string.Empty,
                Roles = roles.ToList(),
                LineasAsignadas = lineasInfo,
                Activo = user.Activo,
                FechaCreacion = user.FechaCreacion,
                UltimoAcceso = user.UltimoAcceso
            });
        }

        return Ok(result.OrderBy(u => u.NombreCompleto));
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUsuario(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var lineas = await _unitOfWork.UsuariosLineas.FindAsync(ul => ul.UsuarioId == user.Id);
        var lineasInfo = new List<LineaAsignadaDto>();

        foreach (var ul in lineas)
        {
            var linea = await _unitOfWork.Lineas.GetByIdAsync(ul.LineaId);
            if (linea != null)
            {
                lineasInfo.Add(new LineaAsignadaDto
                {
                    LineaId = linea.LineaId,
                    Nombre = linea.Nombre
                });
            }
        }

        return Ok(new UsuarioDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            NombreCompleto = user.NombreCompleto ?? string.Empty,
            Roles = roles.ToList(),
            LineasAsignadas = lineasInfo,
            Activo = user.Activo,
            FechaCreacion = user.FechaCreacion,
            UltimoAcceso = user.UltimoAcceso
        });
    }

    [HttpPost]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateUsuario([FromBody] CrearUsuarioRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        // Validar que no exista el username
        var existente = await _userManager.FindByNameAsync(request.UserName);
        if (existente != null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Usuario ya existe",
                Detail = "Ya existe un usuario con ese nombre de usuario.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        if (DetencionCargaReglas.OperarioSinLineas(request.Roles, request.LineasAsignadas.Count))
        {
            return OperarioSinLinea();
        }

        var user = new ApplicationUser
        {
            UserName = request.UserName,
            Email = request.Email,
            NombreCompleto = request.NombreCompleto,
            Activo = true,
            RequiereCambioPassword = true,
            EmailConfirmed = true,
            FechaCreacion = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Error al crear usuario",
                Detail = string.Join(" ", result.Errors.Select(e => e.Description)),
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Asignar roles
        if (request.Roles.Any())
        {
            foreach (var role in request.Roles)
            {
                if (await _roleManager.RoleExistsAsync(role))
                {
                    await _userManager.AddToRoleAsync(user, role);
                }
            }
        }

        // Asignar lineas
        foreach (var lineaId in request.LineasAsignadas)
        {
            var usuarioLinea = new Domain.Entities.UsuarioLinea
            {
                UsuarioId = user.Id,
                LineaId = lineaId,
                FechaAsignacion = DateTimeOffset.UtcNow,
                AsignadoPorUsuarioId = CurrentUserId
            };
            await _unitOfWork.UsuariosLineas.AddAsync(usuarioLinea);
        }

        if (request.LineasAsignadas.Any())
        {
            await _unitOfWork.SaveChangesAsync();
        }

        _logger.LogInformation("Usuario {UserName} creado por {UserId}", user.UserName, CurrentUserId);

        var roles = await _userManager.GetRolesAsync(user);
        var lineas = await _unitOfWork.UsuariosLineas.FindAsync(ul => ul.UsuarioId == user.Id);
        var lineasInfo = new List<LineaAsignadaDto>();

        foreach (var ul in lineas)
        {
            var linea = await _unitOfWork.Lineas.GetByIdAsync(ul.LineaId);
            if (linea != null)
            {
                lineasInfo.Add(new LineaAsignadaDto
                {
                    LineaId = linea.LineaId,
                    Nombre = linea.Nombre
                });
            }
        }

        return CreatedAtAction(nameof(GetUsuario), new { id = user.Id }, new UsuarioDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            NombreCompleto = user.NombreCompleto ?? string.Empty,
            Roles = roles.ToList(),
            LineasAsignadas = lineasInfo,
            Activo = user.Activo,
            FechaCreacion = user.FechaCreacion,
            UltimoAcceso = user.UltimoAcceso
        });
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(UsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateUsuario(string id, [FromBody] ActualizarUsuarioRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        // Un usuario inactivo se puede guardar igual (por ejemplo, para darlo de baja).
        if (request.Activo && DetencionCargaReglas.OperarioSinLineas(request.Roles, request.LineasAsignadas.Count))
        {
            return OperarioSinLinea();
        }

        user.Email = request.Email;
        user.NombreCompleto = request.NombreCompleto;
        user.Activo = request.Activo;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Error al actualizar usuario",
                Detail = string.Join(" ", updateResult.Errors.Select(e => e.Description)),
                Status = StatusCodes.Status400BadRequest
            });
        }

        // Actualizar roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(request.Roles);
        var rolesToAdd = request.Roles.Except(currentRoles);

        await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        foreach (var role in rolesToAdd)
        {
            if (await _roleManager.RoleExistsAsync(role))
            {
                await _userManager.AddToRoleAsync(user, role);
            }
        }

        // Actualizar lineas asignadas
        var lineasActuales = await _unitOfWork.UsuariosLineas.FindAsync(ul => ul.UsuarioId == id);
        var lineasActualesIds = lineasActuales.Select(ul => ul.LineaId).ToList();

        // Remover lineas que ya no estan
        foreach (var ul in lineasActuales.Where(ul => !request.LineasAsignadas.Contains(ul.LineaId)))
        {
            _unitOfWork.UsuariosLineas.DeleteAsync(ul);
        }

        // Agregar nuevas lineas
        foreach (var lineaId in request.LineasAsignadas.Where(id => !lineasActualesIds.Contains(id)))
        {
            await _unitOfWork.UsuariosLineas.AddAsync(new Domain.Entities.UsuarioLinea
            {
                UsuarioId = user.Id,
                LineaId = lineaId,
                FechaAsignacion = DateTimeOffset.UtcNow,
                AsignadoPorUsuarioId = CurrentUserId
            });
        }

        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Usuario {UserName} actualizado por {UserId}", user.UserName, CurrentUserId);

        // Recargar datos
        var roles = await _userManager.GetRolesAsync(user);
        var lineas = await _unitOfWork.UsuariosLineas.FindAsync(ul => ul.UsuarioId == user.Id);
        var lineasInfo = new List<LineaAsignadaDto>();

        foreach (var ul in lineas)
        {
            var linea = await _unitOfWork.Lineas.GetByIdAsync(ul.LineaId);
            if (linea != null)
            {
                lineasInfo.Add(new LineaAsignadaDto
                {
                    LineaId = linea.LineaId,
                    Nombre = linea.Nombre
                });
            }
        }

        return Ok(new UsuarioDto
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            NombreCompleto = user.NombreCompleto ?? string.Empty,
            Roles = roles.ToList(),
            LineasAsignadas = lineasInfo,
            Activo = user.Activo,
            FechaCreacion = user.FechaCreacion,
            UltimoAcceso = user.UltimoAcceso
        });
    }

    [HttpGet("roles")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
        return Ok(roles.Where(r => r != null).OrderBy(r => r));
    }

    /// <summary>
    /// Elimina o desactiva un usuario segun tenga movimientos registrados.
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(EliminarUsuarioResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteUsuario(string id)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        // No permitir eliminarse a si mismo
        if (id == CurrentUserId)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Operacion no permitida",
                Detail = "No puede eliminar su propio usuario.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        // Verificar si tiene movimientos registrados (trazabilidad)
        // Verificamos detenciones que es lo mas importante para trazabilidad
        var tieneDetenciones = await _unitOfWork.Detenciones.ExistsAsync(d => d.CreadoPorUsuarioId == id);

        bool tieneMovimientos = tieneDetenciones;

        if (tieneMovimientos)
        {
            // Solo desactivar - no eliminar por trazabilidad
            if (!user.Activo)
            {
                return Ok(new EliminarUsuarioResponse
                {
                    Eliminado = false,
                    Desactivado = false,
                    Mensaje = "El usuario ya estaba desactivado."
                });
            }

            user.Activo = false;
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("Usuario {UserName} desactivado por {UserId} (tiene movimientos)", user.UserName, CurrentUserId);

            return Ok(new EliminarUsuarioResponse
            {
                Eliminado = false,
                Desactivado = true,
                Mensaje = "El usuario tiene movimientos registrados y fue desactivado en lugar de eliminado para mantener la trazabilidad."
            });
        }
        else
        {
            // Eliminar fisicamente - no tiene movimientos
            // Primero eliminar asignaciones de lineas
            var asignaciones = await _unitOfWork.UsuariosLineas.FindAsync(ul => ul.UsuarioId == id);
            foreach (var asignacion in asignaciones)
            {
                _unitOfWork.UsuariosLineas.DeleteAsync(asignacion);
            }
            await _unitOfWork.SaveChangesAsync();

            // Eliminar usuario
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Error al eliminar usuario",
                    Detail = string.Join(" ", result.Errors.Select(e => e.Description)),
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Usuario {UserName} eliminado por {UserId}", user.UserName, CurrentUserId);

            return Ok(new EliminarUsuarioResponse
            {
                Eliminado = true,
                Desactivado = false,
                Mensaje = "El usuario fue eliminado correctamente."
            });
        }
    }

    [HttpPost("{id}/reset-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordRequest request)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return Unauthorized();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NuevaPassword);

        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Error al restablecer password",
                Detail = string.Join(" ", result.Errors.Select(e => e.Description)),
                Status = StatusCodes.Status400BadRequest
            });
        }

        user.RequiereCambioPassword = true;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Password de usuario {UserName} restablecido por {UserId}", user.UserName, CurrentUserId);

        return NoContent();
    }

    private BadRequestObjectResult OperarioSinLinea() => BadRequest(new ProblemDetails
    {
        Title = "Operario sin línea",
        Detail = DetencionCargaReglas.MensajeOperarioSinLinea,
        Status = StatusCodes.Status400BadRequest
    });
}

public class ResetPasswordRequest
{
    public string NuevaPassword { get; set; } = string.Empty;
}

public class EliminarUsuarioResponse
{
    public bool Eliminado { get; set; }
    public bool Desactivado { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}
