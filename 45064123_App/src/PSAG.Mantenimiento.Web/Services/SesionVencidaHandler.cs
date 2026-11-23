using System.Net;
using Microsoft.AspNetCore.Components;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>
/// Si la API responde 401 (token vencido o inválido), cierra la sesión y lleva al login
/// avisando que venció, con la pantalla actual para volver después de entrar.
/// T-802: un 403 vuelve a pedir los permisos (si se los cambiaron, el menú se alinea sin volver a entrar) y un 409
/// TURNO_REQUERIDO (obligado a turno sin turno: lo cerró, lo relevaron o venció) lleva a "Inicio de turno".
/// Los servicios resuelven el resto de la respuesta como siempre.
/// </summary>
public class SesionVencidaHandler : DelegatingHandler
{
    private readonly IServiceProvider _servicios;

    private static readonly TimeSpan EsperaEntreRefrescos = TimeSpan.FromSeconds(15);
    private DateTime _ultimoRefrescoPermisos = DateTime.MinValue;

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

        else if (response.StatusCode == HttpStatusCode.Forbidden && !esLogin
                 && request.RequestUri?.AbsolutePath.Contains("/api/permisos/", StringComparison.OrdinalIgnoreCase) != true
                 && DateTime.UtcNow - _ultimoRefrescoPermisos > EsperaEntreRefrescos)
        {
            _ultimoRefrescoPermisos = DateTime.UtcNow;
            _ = _servicios.GetRequiredService<IPermisosService>().RefrescarAsync();
        }
        else if (response.StatusCode == HttpStatusCode.Conflict && !esLogin)
        {
            await response.Content.LoadIntoBufferAsync();
            var texto = await response.Content.ReadAsStringAsync(cancellationToken);
            if (texto.Contains("TURNO_REQUERIDO", StringComparison.Ordinal))
            {
                _servicios.GetRequiredService<ITurnoPropioService>().Invalidar();
                var navegacion = _servicios.GetRequiredService<NavigationManager>();
                var actual = "/" + navegacion.ToBaseRelativePath(navegacion.Uri);
                if (!TurnoPropioService.EsInicioTurno(actual))
                    navegacion.NavigateTo($"/{TurnoPropioService.RutaInicioTurno}?volver={Uri.EscapeDataString(actual)}");
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

    /// <summary>T-770: pantalla central del operador obligado a turno.</summary>
    public const string Tablero = "/produccion/tablero";

    /// <summary>
    /// T-770: a dónde va después de tomar (o ya tener) el turno. El obligado a turno cae en el tablero, salvo que venga de
    /// una pantalla concreta (sesión vencida, enlace); "/" y el destino por defecto ("/detenciones") cuentan como "ninguna".
    /// El resto sigue como antes.
    /// </summary>
    public static string DestinoTrasTurno(string? volver, bool obligadoATurno)
    {
        var destino = DestinoSeguro(volver);
        if (!obligadoATurno)
            return destino;
        var ruta = destino.Split('?', '#')[0].TrimEnd('/');
        return ruta is "" or "/detenciones" ? Tablero : destino;
    }
}
