using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;

namespace PSAG.Mantenimiento.Web.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;
    private const string TokenKey = "authToken";
    private const string UserKey = "authUser";

    public AuthService(
        HttpClient httpClient,
        ILocalStorageService localStorage,
        AuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }

    public async Task<LoginResult> LoginAsync(string username, string password)
    {
        try
        {
            var request = new LoginRequest { Usuario = username, Password = password };
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (loginResponse != null)
                {
                    await _localStorage.SetItemAsync(TokenKey, loginResponse.AccessToken);
                    await _localStorage.SetItemAsync(UserKey, loginResponse.Usuario.UserName);

                    ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(loginResponse.AccessToken);

                    return new LoginResult
                    {
                        Success = true,
                        Token = loginResponse.AccessToken,
                        UserName = loginResponse.Usuario.UserName,
                        NombreCompleto = loginResponse.Usuario.NombreCompleto,
                        Roles = loginResponse.Usuario.Roles
                    };
                }
            }

            var error = await response.Content.ReadAsStringAsync();
            return new LoginResult { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            return new LoginResult { Success = false, Error = ex.Message };
        }
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        await _localStorage.RemoveItemAsync(UserKey);
        ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
    }

    public async Task<string?> GetTokenAsync()
    {
        return await _localStorage.GetItemAsync<string>(TokenKey);
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }
}
