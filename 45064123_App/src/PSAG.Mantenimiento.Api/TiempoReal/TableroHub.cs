using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Domain.Permisos;
using PSAG.Mantenimiento.Domain.Services;

namespace PSAG.Mantenimiento.Api.TiempoReal;

/// <summary>
/// T-770 · Hub de SignalR del tablero del operador (<c>/hubs/tablero</c>). Con sesión (JWT; en el navegador viaja en
/// <c>?access_token=</c>, ver Program.cs). El cliente pide seguir un turno de producción (<see cref="Seguir"/>) y recibe
/// <see cref="Evento"/> con el id del turno cada vez que la ingesta de cámara o las detenciones lo cambian; entonces
/// vuelve a pedir <c>GET api/tablero/turno</c> (el hub no manda datos: los permisos y el filtro de turno siguen en la API).
/// </summary>
[Authorize]
public sealed class TableroHub : Hub
{
    /// <summary>Mensaje que reciben los clientes: (turnoProduccionId, motivo).</summary>
    public const string Evento = "TableroCambio";

    public const string Ruta = "/hubs/tablero";

    private readonly IPermisosService _permisos;
    private readonly IUnitOfWork _unitOfWork;

    public TableroHub(IPermisosService permisos, IUnitOfWork unitOfWork)
    {
        _permisos = permisos;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Suma la conexión al grupo del turno. Hace falta VER de "Tablero del turno"; el obligado a turno sólo puede seguir
    /// el suyo (el turno abierto en el que es el operador vigente). Si no puede, HubException con el motivo.
    /// </summary>
    public async Task Seguir(long turnoProduccionId)
    {
        var usuario = Context.User;
        var usuarioId = usuario?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (usuario == null || string.IsNullOrEmpty(usuarioId))
            throw new HubException("Sin sesión.");
        var permisos = await _permisos.ObtenerDeRolesAsync(usuario.FindAll(ClaimTypes.Role).Select(c => c.Value), Context.ConnectionAborted);
        long? propio = null;
        if (permisos.ObligadoATurno)
            propio = (await _unitOfWork.TurnosProduccion.GetAbiertosDeOperadorAsync(usuarioId, Context.ConnectionAborted))
                .FirstOrDefault()?.TurnoProduccionId;
        if (!TableroReglas.PuedeSeguir(permisos.Tiene(Opciones.ProduccionTablero, AccionPermiso.Ver), permisos.ObligadoATurno, propio, turnoProduccionId))
            throw new HubException("No podés seguir ese turno.");
        await Groups.AddToGroupAsync(Context.ConnectionId, TableroReglas.GrupoTurno(turnoProduccionId), Context.ConnectionAborted);
    }

    /// <summary>Deja de seguir el turno (cambió de línea o de turno).</summary>
    public Task Dejar(long turnoProduccionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, TableroReglas.GrupoTurno(turnoProduccionId), Context.ConnectionAborted);
}

/// <summary>
/// T-770 · <see cref="INotificadorTablero"/> con SignalR: manda <see cref="TableroHub.Evento"/> al grupo de cada turno que
/// cambió. Nunca lanza (lo llama el UnitOfWork después de guardar): si falla, lo registra y el tablero se entera por el
/// refresco de respaldo (10 s).
/// </summary>
public sealed class NotificadorTableroSignalR : INotificadorTablero
{
    private readonly IHubContext<TableroHub> _hub;
    private readonly ILogger<NotificadorTableroSignalR> _logger;

    public NotificadorTableroSignalR(IHubContext<TableroHub> hub, ILogger<NotificadorTableroSignalR> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task NotificarAsync(IReadOnlyCollection<CambioTablero> cambios, CancellationToken cancellationToken = default)
    {
        foreach (var turno in cambios.GroupBy(c => c.TurnoProduccionId))
        {
            try
            {
                var motivo = string.Join(",", turno.Select(c => c.Motivo).Distinct().OrderBy(m => m));
                await _hub.Clients.Group(TableroReglas.GrupoTurno(turno.Key)).SendAsync(TableroHub.Evento, turno.Key, motivo, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo avisar al tablero del turno {TurnoProduccionId}", turno.Key);
            }
        }
    }
}
