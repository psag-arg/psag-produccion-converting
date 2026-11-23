namespace PSAG.Mantenimiento.Domain.Services;

/// <summary>
/// T-802 · Obligado a turno: un usuario con algún rol con <c>ObligadoATurno</c> (CFG_RolesConfiguracion) no puede hacer nada
/// hasta abrir o tomar un turno de una de sus líneas habilitadas, y mientras lo tiene abierto todo se ajusta a ese turno.
/// </summary>
public static class TurnoObligatorioReglas
{
    public const string MensajeObligadoSinLineas =
        "Alguno de los roles elegidos está obligado a turno: elegí al menos una línea habilitada (el usuario sólo puede abrir turno en esas líneas).";
}
