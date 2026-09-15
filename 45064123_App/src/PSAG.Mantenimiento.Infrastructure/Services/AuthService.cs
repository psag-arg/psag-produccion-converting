using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Auth;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Identity;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PSAG.Mantenimiento.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly IAuditoriaService _auditoriaService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration,
        ILogger<AuthService> logger,
        IAuditoriaService auditoriaService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _logger = logger;
        _auditoriaService = auditoriaService;
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByNameAsync(request.Usuario);

        if (user == null)
        {
            _logger.LogWarning("Intento de login fallido: usuario {Username} no encontrado", request.Usuario);
            return Result<LoginResponse>.Failure("Usuario o contraseña incorrectos");
        }

        if (!user.Activo)
        {
            _logger.LogWarning("Intento de login con usuario inactivo: {Username}", request.Usuario);
            return Result<LoginResponse>.Failure("Usuario inactivo. Contacte al administrador.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            _logger.LogWarning("Usuario {Username} bloqueado por intentos fallidos", request.Usuario);
            return Result<LoginResponse>.Failure("Usuario bloqueado temporalmente. Intente más tarde.");
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Intento de login fallido para usuario {Username}", request.Usuario);
            return Result<LoginResponse>.Failure("Usuario o contraseña incorrectos");
        }

        // Generar tokens
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = GenerateAccessToken(user, roles);
        var refreshToken = GenerateRefreshToken();

        // Guardar refresh token
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiration = DateTimeOffset.UtcNow.AddDays(
            int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7"));
        user.UltimoAcceso = DateTimeOffset.UtcNow;

        await _userManager.UpdateAsync(user);

        // Registrar auditoría
        await _auditoriaService.RegistrarAsync(
            user.Id,
            TipoAccionAuditoria.InicioSesion,
            "Usuario",
            user.Id,
            null,
            null,
            "Inicio de sesión exitoso",
            cancellationToken);

        _logger.LogInformation("Usuario {Username} inició sesión exitosamente", request.Usuario);

        return Result<LoginResponse>.Success(new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Expiration = DateTimeOffset.UtcNow.AddMinutes(
                int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"] ?? "60")),
            Usuario = MapToUsuarioInfoDto(user, roles)
        });
    }

    public async Task<Result<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        // Find user by refresh token
        var users = _userManager.Users.Where(u => u.RefreshToken == request.RefreshToken);
        var user = users.FirstOrDefault();

        if (user == null || user.RefreshTokenExpiration <= DateTimeOffset.UtcNow)
        {
            return Result<LoginResponse>.Failure("Token de actualización inválido o expirado");
        }

        if (!user.Activo)
        {
            return Result<LoginResponse>.Failure("Usuario inactivo");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var newAccessToken = GenerateAccessToken(user, roles);
        var newRefreshToken = GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiration = DateTimeOffset.UtcNow.AddDays(
            int.Parse(_configuration["JwtSettings:RefreshTokenExpirationDays"] ?? "7"));

        await _userManager.UpdateAsync(user);

        return Result<LoginResponse>.Success(new LoginResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshToken,
            Expiration = DateTimeOffset.UtcNow.AddMinutes(
                int.Parse(_configuration["JwtSettings:AccessTokenExpirationMinutes"] ?? "60")),
            Usuario = MapToUsuarioInfoDto(user, roles)
        });
    }

    public async Task<Result> LogoutAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(usuarioId);
        if (user == null)
        {
            return Result.Failure("Usuario no encontrado");
        }

        user.RefreshToken = null;
        user.RefreshTokenExpiration = null;
        await _userManager.UpdateAsync(user);

        await _auditoriaService.RegistrarAsync(
            usuarioId,
            TipoAccionAuditoria.CierreSesion,
            "Usuario",
            usuarioId,
            null,
            null,
            "Cierre de sesión",
            cancellationToken);

        return Result.Success();
    }

    public async Task<Result> CambiarPasswordAsync(string usuarioId, CambiarPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(usuarioId);
        if (user == null)
        {
            return Result.Failure("Usuario no encontrado");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.PasswordActual, request.PasswordNuevo);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Result.Failure($"Error al cambiar contraseña: {errors}");
        }

        user.RequiereCambioPassword = false;
        await _userManager.UpdateAsync(user);

        await _auditoriaService.RegistrarAsync(
            usuarioId,
            TipoAccionAuditoria.Modificar,
            "Usuario",
            usuarioId,
            null,
            null,
            "Contraseña actualizada",
            cancellationToken);

        _logger.LogInformation("Usuario {UserId} cambió su contraseña", usuarioId);

        return Result.Success();
    }

    public async Task<Result<UsuarioInfoDto>> ObtenerUsuarioActualAsync(string usuarioId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(usuarioId);
        if (user == null)
        {
            return Result<UsuarioInfoDto>.Failure("Usuario no encontrado");
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Result<UsuarioInfoDto>.Success(MapToUsuarioInfoDto(user, roles));
    }

    private string GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!),
            new(ClaimTypes.Email, user.Email ?? ""),
            new("NombreCompleto", user.NombreCompleto),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expiration = DateTimeOffset.UtcNow.AddMinutes(
            int.Parse(jwtSettings["AccessTokenExpirationMinutes"] ?? "60"));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiration.UtcDateTime,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string GenerateRefreshToken()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private static UsuarioInfoDto MapToUsuarioInfoDto(ApplicationUser user, IList<string> roles)
    {
        return new UsuarioInfoDto
        {
            Id = user.Id,
            UserName = user.UserName!,
            Email = user.Email ?? "",
            NombreCompleto = user.NombreCompleto,
            Roles = roles.ToList(),
            RequiereCambioPassword = user.RequiereCambioPassword
        };
    }
}
