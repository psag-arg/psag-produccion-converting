using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Maquinista de cada turno de cada línea (T-783): el operario lo confirma después del login y el supervisor lo
/// corrige. Los nombres de usuario los completa la API (Application sólo conoce ids).
/// </summary>
public interface IMaquinistaTurnoService
{
    /// <summary>Qué preguntarle al usuario que acaba de entrar (sólo a los OPERARIO).</summary>
    Task<PropuestaMaquinistaTurnoDto> ObtenerPropuestaAsync(
        string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    /// <summary>
    /// El usuario confirma que es el maquinista del turno en curso de la línea. Tiene que tener asignación en la
    /// línea para ese turno (o sin turno fijo). Las detenciones pendientes del turno pasan a tomarlo.
    /// </summary>
    Task<Result<ConfirmacionMaquinistaTurnoDto>> ConfirmarAsync(
        string usuarioId, string nombreUsuario, int lineaId, CancellationToken cancellationToken = default);

    /// <summary>Turnos que empezaron ese día, por línea, con el maquinista vigente, el historial y las asignaciones.</summary>
    Task<MaquinistasTurnoDiaDto> ObtenerDiaAsync(
        DateOnly fecha, IReadOnlyCollection<string> roles, IReadOnlyCollection<int> lineasAsignadas,
        CancellationToken cancellationToken = default);

    /// <summary>El supervisor carga o corrige el maquinista de un turno (registro nuevo; el anterior queda en el historial).</summary>
    Task<Result<CorreccionMaquinistaTurnoDto>> CorregirAsync(
        CorregirMaquinistaTurnoRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        IReadOnlyCollection<int> lineasAsignadas, string nombreMaquinista, CancellationToken cancellationToken = default);
}
