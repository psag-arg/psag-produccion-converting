using PSAG.Mantenimiento.Application.Common;
using PSAG.Mantenimiento.Application.DTOs.Calidad;

namespace PSAG.Mantenimiento.Application.Services.Interfaces;

/// <summary>
/// Maestros de calidad (T-611). Los permisos se validan acá: sólo el administrador modifica.
/// </summary>
public interface ICalidadMaestrosService
{
    Task<IReadOnlyList<PuntoControlCalidadDto>> ObtenerPuntosControlAsync(int? lineaId, bool incluirInactivos, CancellationToken cancellationToken = default);

    Task<Result<PuntoControlCalidadDto>> CrearPuntoControlAsync(
        GuardarPuntoControlCalidadRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    Task<Result<PuntoControlCalidadDto>> ActualizarPuntoControlAsync(
        int puntoControlCalidadId, GuardarPuntoControlCalidadRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TipoDefectoCalidadDto>> ObtenerTiposDefectoAsync(bool incluirInactivos, CancellationToken cancellationToken = default);

    Task<Result<TipoDefectoCalidadDto>> CrearTipoDefectoAsync(
        GuardarTipoDefectoCalidadRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);

    Task<Result<TipoDefectoCalidadDto>> ActualizarTipoDefectoAsync(
        int tipoDefectoCalidadId, GuardarTipoDefectoCalidadRequest request, string usuarioId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken = default);
}

/// <summary>
/// Registros de calidad (T-611): grilla, detalle, imagen, carga manual, ingesta del detector y revisión humana.
/// Los permisos se validan acá, no sólo en la pantalla.
/// </summary>
public interface IRegistroCalidadService
{
    Task<Result<PagedResult<RegistroCalidadDto>>> ObtenerPaginadoAsync(RegistrosCalidadFiltroRequest filtro, CancellationToken cancellationToken = default);

    /// <summary>
    /// Detalle con el historial de revisión. Cualquier usuario autenticado lo puede ver.
    /// </summary>
    Task<Result<RegistroCalidadDto>> ObtenerAsync(long registroCalidadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga manual (supervisión y administrador). Nace CONFIRMADO por quien lo carga. La imagen es opcional.
    /// </summary>
    Task<Result<RegistroCalidadDto>> CrearManualAsync(
        CrearRegistroCalidadManualRequest request, ImagenEntrante? imagen, string usuarioId, IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registro que manda el detector (ya autenticado por la clave de servicio). Nace PENDIENTE y con origen AUTOMATICO.
    /// La imagen es obligatoria. Un IdentificadorExterno repetido devuelve el registro existente (Duplicado = true).
    /// </summary>
    Task<Result<IngestaCalidadRespuesta>> IngestarAsync(
        IngestaCalidadRequest request, ImagenEntrante? imagen, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirmado o falso positivo (supervisión, mantenimiento y administrador). Queda en el historial.
    /// </summary>
    Task<Result<RegistroCalidadDto>> RevisarAsync(
        long registroCalidadId, RevisarRegistroCalidadRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Relaciona (o desrelaciona) el registro con una detención de la misma línea. Mismos roles que la revisión.
    /// </summary>
    Task<Result<RegistroCalidadDto>> VincularDetencionAsync(
        long registroCalidadId, VincularDetencionCalidadRequest request, string usuarioId, IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken = default);

    Task<Result<AdjuntoArchivo>> AbrirImagenAsync(long registroCalidadId, CancellationToken cancellationToken = default);
}
