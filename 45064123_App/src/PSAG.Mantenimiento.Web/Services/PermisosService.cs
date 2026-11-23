using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>
/// Permisos del usuario logueado (T-802), cacheados en la sesión de la Web: se piden una vez a GET api/permisos/mios y se
/// vuelven a pedir al loguear / desloguear (cambio de estado de autenticación) o con <see cref="RefrescarAsync"/>.
/// </summary>
public interface IPermisosService
{
    Task<PermisosUsuario> ObtenerAsync();
    Task<PermisosUsuario> RefrescarAsync();
    event Action? Cambiaron;

    Task<ConfiguracionPermisosDto> GetConfiguracionAsync();
    Task<(RolPermisosDto? Resultado, string? Error)> GuardarRolAsync(string roleId, GuardarPermisosRolRequest request);
}

public sealed class PermisosService : IPermisosService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _auth;
    private Task<PermisosUsuario>? _carga;

    public event Action? Cambiaron;

    public PermisosService(HttpClient httpClient, AuthenticationStateProvider auth)
    {
        _httpClient = httpClient;
        _auth = auth;
        _auth.AuthenticationStateChanged += AlCambiarSesion;
    }

    private void AlCambiarSesion(Task<AuthenticationState> _)
    {
        _carga = null;
        Cambiaron?.Invoke();
    }

    public Task<PermisosUsuario> ObtenerAsync() => _carga ??= CargarAsync();

    public async Task<PermisosUsuario> RefrescarAsync()
    {
        _carga = CargarAsync();
        var permisos = await _carga;
        Cambiaron?.Invoke();
        return permisos;
    }

    private async Task<PermisosUsuario> CargarAsync()
    {
        try
        {
            var estado = await _auth.GetAuthenticationStateAsync();
            if (estado.User.Identity?.IsAuthenticated != true)
                return PermisosUsuario.Ninguno;
            var dto = await _httpClient.GetFromJsonAsync<PermisosMiosDto>("api/permisos/mios");
            return dto == null ? PermisosUsuario.Ninguno : new PermisosUsuario(dto);
        }
        catch
        {
            _carga = null;   // se reintenta en la próxima consulta
            return PermisosUsuario.Ninguno;
        }
    }

    public async Task<ConfiguracionPermisosDto> GetConfiguracionAsync()
    {
        var response = await _httpClient.GetAsync("api/permisos/roles");
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<ConfiguracionPermisosDto>() ?? new ConfiguracionPermisosDto();
    }

    public async Task<(RolPermisosDto? Resultado, string? Error)> GuardarRolAsync(string roleId, GuardarPermisosRolRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/permisos/roles/{Uri.EscapeDataString(roleId)}", request);
        var (ok, error) = await ApiErrores.ResultadoAsync(response, "No se pudieron guardar los permisos.");
        if (!ok)
            return (null, error);
        var dto = await response.Content.ReadFromJsonAsync<RolPermisosDto>();
        await RefrescarAsync();   // por si el admin cambió su propio rol
        return (dto, null);
    }

    public void Dispose() => _auth.AuthenticationStateChanged -= AlCambiarSesion;
}
