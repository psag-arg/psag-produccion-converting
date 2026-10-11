using System.Net;
using Microsoft.AspNetCore.Components;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>
/// Si la API responde 401 (token vencido o inválido), cierra la sesión y lleva al login
/// avisando que venció, con la pantalla actual para volver después de entrar.
/// Los servicios resuelven el resto de la respuesta como siempre.
/// </summary>
public class SesionVencidaHandler : DelegatingHandler
{
    private readonly IServiceProvider _servicios;

    // Se resuelven al usarse: el proveedor de autenticación depende del mismo HttpClient.
    public SesionVencidaHandler(IServiceProvider servicios)
    {
        _servicios = servicios;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);

        var esLogin = request.RequestUri?.AbsolutePath.Contains("/api/auth/", StringComparison.OrdinalIgnoreCase) == true;
        if (response.StatusCode == HttpStatusCode.Unauthorized && !esLogin)
        {
            await _servicios.GetRequiredService<CustomAuthStateProvider>().CerrarSesionVencidaAsync();
            var navegacion = _servicios.GetRequiredService<NavigationManager>();
            var actual = "/" + navegacion.ToBaseRelativePath(navegacion.Uri);
            if (!actual.StartsWith("/login", StringComparison.OrdinalIgnoreCase))
            {
                navegacion.NavigateTo(RutasLogin.SesionVencida(actual));
            }
        }

        return response;
    }
}

/// <summary>
/// Arma y lee la ruta al login con el aviso de sesión vencida y la pantalla a la que volver.
/// </summary>
public static class RutasLogin
{
    public static string SesionVencida(string volver) => $"/login?vencida=1&volver={Uri.EscapeDataString(volver)}";

    public static string ConVolver(string volver) => $"/login?volver={Uri.EscapeDataString(volver)}";

    /// <summary>Sólo rutas internas: el parámetro viene en la URL y no se confía en él.</summary>
    public static string DestinoSeguro(string? volver) =>
        volver is { Length: > 1 } v && v.StartsWith('/') && !v.StartsWith("//") && !v.StartsWith("/\\")
            && !v.StartsWith("/login", StringComparison.OrdinalIgnoreCase)
            ? v
            : "/detenciones";
}
