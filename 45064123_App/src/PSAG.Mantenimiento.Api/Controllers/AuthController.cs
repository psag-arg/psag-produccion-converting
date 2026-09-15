using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PSAG.Mantenimiento.Application.DTOs.Auth;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _env;

    public AuthController(
        IAuthService authService,
        ILogger<AuthController> logger,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment env)
    {
        _authService = authService;
        _logger = logger;
        _userManager = userManager;
        _env = env;
    }

    /// <summary>
    /// Iniciar sesión con usuario y contraseña.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Error de autenticación",
                Detail = result.Error,
                Status = StatusCodes.Status401Unauthorized,
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Renovar token de acceso usando el refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(request, cancellationToken);

        if (!result.IsSuccess)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "Error de autenticación",
                Detail = result.Error,
                Status = StatusCodes.Status401Unauthorized,
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Cerrar sesión actual.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        await _authService.LogoutAsync(CurrentUserId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Cambiar contraseña del usuario actual.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] CambiarPasswordRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        var result = await _authService.CambiarPasswordAsync(CurrentUserId, request, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// Obtener información del usuario actual.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UsuarioInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
        {
            return Unauthorized();
        }

        var result = await _authService.ObtenerUsuarioActualAsync(CurrentUserId, cancellationToken);
        return HandleResult(result);
    }

    /// <summary>
    /// [SOLO DESARROLLO] Establece contraseñas para usuarios de prueba.
    /// </summary>
    [HttpPost("seed-passwords")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SeedPasswords()
    {
        if (!_env.IsDevelopment())
        {
            return Forbid();
        }

        var results = new List<string>();

        // Primero crear admin si no existe
        var adminUser = await _userManager.FindByNameAsync("admin");
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@psag.local",
                NombreCompleto = "Administrador del Sistema",
                Activo = true,
                RequiereCambioPassword = false,
                EmailConfirmed = true
            };
            var createResult = await _userManager.CreateAsync(adminUser, "Admin123!");
            if (createResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(adminUser, "ADMINISTRADOR");
                results.Add("admin: CREADO");
            }
            else
            {
                results.Add($"admin: ERROR AL CREAR - {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(adminUser);
            var resetResult = await _userManager.ResetPasswordAsync(adminUser, token, "Admin123!");
            results.Add(resetResult.Succeeded ? "admin: PASSWORD ACTUALIZADO" : $"admin: ERROR - {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
        }

        // Actualizar passwords de otros usuarios existentes
        var usersToSeed = new[]
        {
            ("gerente", "Gerente123!"),
            ("mantenimiento1", "Mant123!"),
            ("supervisor1", "Super123!"),
            ("operario1", "Oper123!"),
            ("operario2", "Oper123!")
        };

        foreach (var (username, password) in usersToSeed)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, password);
                results.Add(result.Succeeded ? $"{username}: OK" : $"{username}: ERROR - {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
            else
            {
                results.Add($"{username}: NO ENCONTRADO");
            }
        }

        _logger.LogInformation("Seed de contraseñas ejecutado: {Results}", string.Join("; ", results));

        return Ok(new { message = "Seed completado", results });
    }
}
