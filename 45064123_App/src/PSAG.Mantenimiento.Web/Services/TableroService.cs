using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.SignalR.Client;
using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Services;

/// <summary>T-770 · Tablero del operador: GET api/tablero/turno (una sola llamada con todo el turno).</summary>
public interface ITableroService
{
    /// <summary>El tablero del turno propio (obligado a turno) o de la línea (supervisor). Null si la línea no tiene turno. Lanza si la API falla.</summary>
    Task<TableroTurnoDto?> GetTurnoAsync(int? lineaId);

    /// <summary>Modo gerencial: el tablero de un turno de producción cualquiera (sólo lectura). Lanza si la API falla.</summary>
    Task<TableroTurnoDto?> GetTurnoGerencialAsync(long turnoProduccionId);

    /// <summary>Modo gerencial: líneas a tu alcance y sus turnos de producción de la fecha.</summary>
    Task<TurnosTableroDto> GetTurnosAsync(DateOnly fecha);
}

public class TableroService : ITableroService
{
    private readonly HttpClient _httpClient;

    public TableroService(HttpClient httpClient) => _httpClient = httpClient;

    public async Task<TableroTurnoDto?> GetTurnoAsync(int? lineaId)
    {
        var response = await _httpClient.GetAsync("api/tablero/turno" + (lineaId is { } l ? $"?lineaId={l}" : ""));
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<TableroTurnoDto>();
    }

    public async Task<TableroTurnoDto?> GetTurnoGerencialAsync(long turnoProduccionId)
    {
        var response = await _httpClient.GetAsync($"api/tablero/turno?turnoProduccionId={turnoProduccionId}");
        if (response.StatusCode == HttpStatusCode.NoContent)
            return null;
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<TableroTurnoDto>();
    }

    public async Task<TurnosTableroDto> GetTurnosAsync(DateOnly fecha)
    {
        var response = await _httpClient.GetAsync($"api/tablero/turnos?fecha={fecha:yyyy-MM-dd}");
        await ApiErrores.AsegurarExitoAsync(response);
        return await response.Content.ReadFromJsonAsync<TurnosTableroDto>() ?? new TurnosTableroDto { Fecha = fecha };
    }
}

/// <summary>
/// T-770 · Avisos en línea del tablero por SignalR (<c>/hubs/tablero</c> de la API, con el JWT en <c>access_token</c>).
/// Cuando la cámara manda conteo o cambian las detenciones del turno, llega <see cref="Cambio"/> y la pantalla vuelve a
/// pedir el tablero. Si no conecta (red, proxy, token vencido) no rompe nada: la pantalla sigue con su refresco de
/// respaldo cada 10 s. Una conexión por pantalla (se descarta al salir).
/// </summary>
public sealed class TableroTiempoReal : IAsyncDisposable
{
    public const string Evento = "TableroCambio";

    private readonly HubConnection _conexion;
    private long? _turno;

    /// <summary>Cambió el turno (id del turno de producción).</summary>
    public event Func<long, Task>? Cambio;

    /// <summary>Cambió el estado de la conexión (para mostrar "en línea" / "sin conexión en línea").</summary>
    public event Action? EstadoCambio;

    public TableroTiempoReal(Uri apiBase, Func<Task<string?>> token)
    {
        _conexion = new HubConnectionBuilder()
            .WithUrl(new Uri(apiBase, "hubs/tablero"), o => o.AccessTokenProvider = token)
            .WithAutomaticReconnect()
            .Build();
        _conexion.On<long, string>(Evento, async (turno, _) =>
        {
            if (Cambio != null)
                await Cambio.Invoke(turno);
        });
        _conexion.Reconnecting += _ => { EstadoCambio?.Invoke(); return Task.CompletedTask; };
        _conexion.Reconnected += async _ =>
        {
            // Al reconectar hay que volver a entrar al grupo del turno.
            if (_turno is { } t)
                await SeguirInternoAsync(t);
            EstadoCambio?.Invoke();
        };
        _conexion.Closed += _ => { EstadoCambio?.Invoke(); return Task.CompletedTask; };
    }

    public bool Conectado => _conexion.State == HubConnectionState.Connected;

    /// <summary>Conecta (si hace falta) y sigue el turno. Devuelve false si no se pudo (queda el refresco de respaldo).</summary>
    public async Task<bool> SeguirAsync(long turnoProduccionId)
    {
        try
        {
            if (_conexion.State == HubConnectionState.Disconnected)
                await _conexion.StartAsync();
            if (_turno == turnoProduccionId && Conectado)
                return true;
            if (_turno is { } anterior && anterior != turnoProduccionId && Conectado)
                await _conexion.InvokeAsync("Dejar", anterior);
            return await SeguirInternoAsync(turnoProduccionId);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tablero en línea: {ex.Message}");
            return false;
        }
        finally
        {
            EstadoCambio?.Invoke();
        }
    }

    private async Task<bool> SeguirInternoAsync(long turnoProduccionId)
    {
        try
        {
            await _conexion.InvokeAsync("Seguir", turnoProduccionId);
            _turno = turnoProduccionId;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tablero en línea (seguir turno): {ex.Message}");
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await _conexion.DisposeAsync(); } catch { /* al salir de la pantalla */ }
    }
}
