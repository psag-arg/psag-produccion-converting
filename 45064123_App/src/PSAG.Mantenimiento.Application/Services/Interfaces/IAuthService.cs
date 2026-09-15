using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Auth;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<Result> LogoutAsync(string usuarioId, CancellationToken cancellationToken = default);
    Task<Result> CambiarPasswordAsync(string usuarioId, CambiarPasswordRequest request, CancellationToken cancellationToken = default);
    Task<Result<UsuarioInfoDto>> ObtenerUsuarioActualAsync(string usuarioId, CancellationToken cancellationToken = default);
}

public interface IUsuarioService
{
    Task<Result<IReadOnlyList<UsuarioDto>>> ObtenerTodosAsync(CancellationToken cancellationToken = default);
    Task<Result<UsuarioDto>> ObtenerPorIdAsync(string usuarioId, CancellationToken cancellationToken = default);
    Task<Result<UsuarioDto>> CrearAsync(CrearUsuarioRequest request, string creadoPorUsuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActualizarAsync(string usuarioId, ActualizarUsuarioRequest request, string modificadoPorUsuarioId, CancellationToken cancellationToken = default);
    Task<Result> ActivarDesactivarAsync(string usuarioId, bool activo, string modificadoPorUsuarioId, CancellationToken cancellationToken = default);
    Task<Result> ResetearPasswordAsync(string usuarioId, string nuevaPassword, string modificadoPorUsuarioId, CancellationToken cancellationToken = default);
    Task<Result> AsignarLineasAsync(string usuarioId, IEnumerable<int> lineasIds, string modificadoPorUsuarioId, CancellationToken cancellationToken = default);
}
