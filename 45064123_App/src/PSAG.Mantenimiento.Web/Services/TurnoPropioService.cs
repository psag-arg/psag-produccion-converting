using System.Net;
using System.Net.Http.Json;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>
/// T-802 · Turno propio del usuario logueado: si está obligado a turno (algún rol con ObligadoATurno, de
/// <c>api/permisos/mios</c>) y el turno abierto en el que es el operador vigente (<c>GET api/turnos-produccion/mio</c>).
/// Obligado y sin turno = bloqueado: la Web sólo muestra "Inicio de turno" (la API igual responde 409 a todo lo demás).
/// Se cachea; se invalida al tomar / cerrar el turno, al cambiar los permisos y cuando la API responde 409 TURNO_REQUERIDO.
/// </summary>
public interface ITurnoPropioService
{
    Task<EstadoTurnoPropio> ObtenerAsync();
    Task<EstadoTurnoPropio> RefrescarAsync();
    void Invalidar();
    event Action? Cambio;
}

public sealed record EstadoTurnoPropio(bool Obligado, TurnoProduccionDto? Turno)
{
    public static readonly EstadoTurnoPropio Libre = new(false, null);

    /// <summary>Obligado a turno sin turno abierto: no puede hacer nada hasta abrir o tomar uno.</summary>
    public bool Bloqueado => Obligado && Turno == null;
}

public sealed class TurnoPropioService : ITurnoPropioService, IDisposable
{
    public const string RutaInicioTurno = "produccion/inicio-turno";

    private readonly HttpClient _httpClient;
    private readonly IPermisosService _permisos;
    private Task<EstadoTurnoPropio>? _carga;

    public event Action? Cambio;

    public TurnoPropioService(HttpClient httpClient, IPermisosService permisos)
    {
        _httpClient = httpClient;
        _permisos = permisos;
        _permisos.Cambiaron += Invalidar;
    }

    public Task<EstadoTurnoPropio> ObtenerAsync() => _carga ??= CargarAsync();

    public async Task<EstadoTurnoPropio> RefrescarAsync()
    {
        _carga = CargarAsync();
        var estado = await _carga;
        Cambio?.Invoke();
        return estado;
    }

    public void Invalidar()
    {
        _carga = null;
        Cambio?.Invoke();
    }

    private async Task<EstadoTurnoPropio> CargarAsync()
    {
        var permisos = await _permisos.ObtenerAsync();
        if (!permisos.ObligadoATurno)
            return EstadoTurnoPropio.Libre;
        try
        {
            var response = await _httpClient.GetAsync("api/turnos-produccion/mio");
            if (response.StatusCode == HttpStatusCode.NoContent)
                return new EstadoTurnoPropio(true, null);
            await ApiErrores.AsegurarExitoAsync(response);
            return new EstadoTurnoPropio(true, await response.Content.ReadFromJsonAsync<TurnoProduccionDto>());
        }
        catch
        {
            _carga = null;   // se reintenta en la próxima consulta; mientras tanto, bloqueado (la API decide)
            return new EstadoTurnoPropio(true, null);
        }
    }

    /// <summary>¿La ruta (relativa) es la de inicio de turno? Es la única que ve el obligado sin turno.</summary>
    public static bool EsInicioTurno(string rutaRelativa) =>
        rutaRelativa.Split('?', '#')[0].Trim('/').Equals(RutaInicioTurno, StringComparison.OrdinalIgnoreCase);

    public void Dispose() => _permisos.Cambiaron -= Invalidar;
}
