using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Camaras;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Maestro de cámaras (CAM_Camaras, T-798 etapa 4): ABM con borrado lógico (sólo el administrador) y la
/// configuración que pide el servicio PSAG_ConteoCamara.
/// </summary>
public interface ICamaraService
{
    Task<IReadOnlyList<CamaraDto>> ObtenerAsync(int? lineaId, bool incluirInactivas, CancellationToken cancellationToken = default);

    Task<CamaraDto?> ObtenerPorIdAsync(int camaraId, CancellationToken cancellationToken = default);

    Task<Result<CamaraDto>> CrearAsync(GuardarCamaraRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    Task<Result<CamaraDto>> ActualizarAsync(int camaraId, GuardarCamaraRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    /// <summary>Borrado lógico: Activo = 0, deja de estar habilitada y de contar la producción oficial.</summary>
    Task<Result<CamaraDto>> DesactivarAsync(int camaraId, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    /// <summary>Cámaras activas y habilitadas, para el servicio de conteo (GET api/camaras/configuracion).</summary>
    Task<IReadOnlyList<CamaraConfiguracionDto>> ObtenerConfiguracionAsync(CancellationToken cancellationToken = default);
}
