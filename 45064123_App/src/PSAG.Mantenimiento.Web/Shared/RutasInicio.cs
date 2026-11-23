using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// T-803 · A dónde lleva la raíz "/" (y "/dashboard", el viejo "Dashboard de Control", que salió del menú) según los
/// permisos del usuario:
/// <list type="number">
/// <item>obligado a turno (operario): su tablero del turno (sin turno abierto, el layout lo lleva antes a Inicio de turno);</item>
/// <item>con "Dashboard Admin" (administrador, mantenimiento, supervisor, gerencia): Dashboard Admin;</item>
/// <item>con el tablero del turno: el tablero; con presupuesto vs. real: esa pantalla; con el gerencial: el gerencial;</item>
/// <item>si no, la primera pantalla de su menú y, sin nada, Ver detenciones (lo de antes).</item>
/// </list>
/// </summary>
public static class RutasInicio
{
    public const string DashboardAdmin = "/dashboard/reportes";
    public const string PresupuestoReal = "/dashboard/produccion";
    public const string Tablero = "/produccion/tablero";
    public const string TableroGerencial = "/produccion/tablero/gerencial";
    public const string PorDefecto = "/detenciones";

    public static string Destino(PermisosUsuario permisos)
    {
        if (permisos.ObligadoATurno)
            return Tablero;
        if (permisos.Ve(Opciones.DashboardReportes))
            return DashboardAdmin;
        if (permisos.Ve(Opciones.ProduccionTablero))
            return Tablero;
        if (permisos.Ve(Opciones.ProduccionPresupuestoReal))
            return PresupuestoReal;
        if (permisos.Ve(Opciones.ProduccionTableroGerencial))
            return TableroGerencial;
        var primera = permisos.Secciones.SelectMany(s => permisos.MenuDe(s.Codigo)).FirstOrDefault();
        return primera?.Ruta is { Length: > 0 } ruta ? "/" + ruta.TrimStart('/') : PorDefecto;
    }
}
