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

public sealed record EstadoTurnoPropio(bool Obligado, TurnoProduccionDto? Turno, SesionToleranciaDto? Tolerancia = null)
{
    public static readonly EstadoTurnoPropio Libre = new(false, null);

    /// <summary>Obligado a turno sin turno abierto: no puede hacer nada hasta abrir o tomar uno.</summary>
    public bool Bloqueado => Obligado && Turno == null;

    /// <summary>
    /// Turno automático (C.10): el obligado está en un turno sin producto (o con el heredado sin confirmar) y la línea tiene
    /// productos con objetivo: tiene que confirmarlo o elegirlo en "Inicio de turno" antes de seguir.
    /// </summary>
    public bool FaltaProducto => Obligado && Turno != null && !Turno.SinObjetivos
                                 && (Turno.ProductoActual == null || Turno.ProductoActual.SinConfirmar);

    /// <summary>
    /// ¿Puede estar en esta pantalla? Sin obligación, o con turno y producto, en todas. Si no, en "Inicio de turno"; y en la
    /// tolerancia de su turno anterior, también en sus pendientes y en declarar / ver sus detenciones (la API valida que
    /// sean de ese turno).
    /// </summary>
    public bool PuedeEstarEn(string rutaRelativa)
    {
        if (!Obligado || (Turno != null && !FaltaProducto))
            return true;
        if (TurnoPropioService.EsInicioTurno(rutaRelativa))
            return true;
        return Tolerancia != null && TurnoPropioService.EsRutaDeTolerancia(rutaRelativa);
    }
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
            TurnoProduccionDto? turno = null;
            if (response.StatusCode != HttpStatusCode.NoContent)
            {
                await ApiErrores.AsegurarExitoAsync(response);
                turno = await response.Content.ReadFromJsonAsync<TurnoProduccionDto>();
            }
            // Turno automático: la tolerancia de su turno anterior (declarar lo suyo después del cierre).
            SesionToleranciaDto? tolerancia = null;
            var sesion = await _httpClient.GetAsync("api/turnos-produccion/mio/tolerancia");
            if (sesion.StatusCode == HttpStatusCode.OK)
                tolerancia = await sesion.Content.ReadFromJsonAsync<SesionToleranciaDto>();
            return new EstadoTurnoPropio(true, turno, tolerancia);
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

    /// <summary>
    /// Turno automático: pantallas que el obligado usa en la tolerancia de su turno anterior: Mis pendientes, declarar
    /// (detenciones/{id}/declarar) y el detalle de una detención (detenciones/{id}).
    /// </summary>
    public static bool EsRutaDeTolerancia(string rutaRelativa)
    {
        var partes = rutaRelativa.Split('?', '#')[0].Trim('/').Split('/');
        if (partes.Length < 2 || !partes[0].Equals("detenciones", StringComparison.OrdinalIgnoreCase))
            return false;
        if (partes.Length == 2)
            return partes[1].Equals("pendientes", StringComparison.OrdinalIgnoreCase) || long.TryParse(partes[1], out _);
        return partes.Length == 3 && long.TryParse(partes[1], out _) && partes[2].Equals("declarar", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _permisos.Cambiaron -= Invalidar;
}
