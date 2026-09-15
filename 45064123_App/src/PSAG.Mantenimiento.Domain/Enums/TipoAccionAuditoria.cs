namespace PSAG.Mantenimiento.Domain.Enums;

/// <summary>
/// Tipos de acciones registradas en auditoría.
/// </summary>
public enum TipoAccionAuditoria
{
    Crear = 1,
    Modificar = 2,
    CambioEstado = 3,
    Cerrar = 4,
    CorreccionAdministrativa = 5,
    AltaCatalogo = 6,
    ModificacionCatalogo = 7,
    AltaUsuario = 8,
    ModificacionUsuario = 9,
    BajaLogicaUsuario = 10,
    CambioRol = 11,
    CambioAsignacionLineas = 12,
    Exportacion = 13,
    AccesoNoAutorizado = 14,
    InicioSesion = 15,
    CierreSesion = 16
}
