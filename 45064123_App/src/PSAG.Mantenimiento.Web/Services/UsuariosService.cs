using PSAG.Mantenimiento.Web.Models;
using System.Net.Http.Json;

namespace PSAG.Mantenimiento.Web.Services;

public interface IUsuariosService
{
    Task<List<UsuarioDto>> GetUsuariosAsync();
    Task<UsuarioDto> GetUsuarioAsync(string id);
    Task CreateUsuarioAsync(CrearUsuarioRequest request);
    Task UpdateUsuarioAsync(string id, ActualizarUsuarioRequest request);
    Task<List<string>> GetRolesAsync();
    Task ResetPasswordAsync(string userId, string nuevaPassword);
    Task<EliminarUsuarioResponse> DeleteUsuarioAsync(string id);
}

public class UsuariosService : IUsuariosService
{
    private readonly HttpClient _httpClient;

    public UsuariosService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<UsuarioDto>> GetUsuariosAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<UsuarioDto>>("api/usuarios");
        return result ?? new List<UsuarioDto>();
    }

    public async Task<UsuarioDto> GetUsuarioAsync(string id)
    {
        var result = await _httpClient.GetFromJsonAsync<UsuarioDto>($"api/usuarios/{id}");
        return result ?? new UsuarioDto();
    }

    public async Task CreateUsuarioAsync(CrearUsuarioRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/usuarios", request);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task UpdateUsuarioAsync(string id, ActualizarUsuarioRequest request)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/usuarios/{id}", request);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task<List<string>> GetRolesAsync()
    {
        var result = await _httpClient.GetFromJsonAsync<List<string>>("api/usuarios/roles");
        return result ?? new List<string>();
    }

    public async Task ResetPasswordAsync(string userId, string nuevaPassword)
    {
        var request = new { NuevaPassword = nuevaPassword };
        var response = await _httpClient.PostAsJsonAsync($"api/usuarios/{userId}/reset-password", request);
        await ApiErrores.AsegurarExitoAsync(response);
    }

    public async Task<EliminarUsuarioResponse> DeleteUsuarioAsync(string id)
    {
        var response = await _httpClient.DeleteAsync($"api/usuarios/{id}");
        await ApiErrores.AsegurarExitoAsync(response);
        var result = await response.Content.ReadFromJsonAsync<EliminarUsuarioResponse>();
        return result ?? new EliminarUsuarioResponse { Mensaje = "Operacion completada" };
    }
}
