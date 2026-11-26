using PSAG.Mantenimiento.Web.Models;

namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Entradas del menú lateral (NavMenu) de una sección: qué se muestra lo deciden los permisos (T-802); acá, el texto
/// corto, el ícono y los accesos de alta. Separado del componente para poder probar el menú por rol (T-803).
/// </summary>
public static class MenuUi
{
    public sealed record ItemMenu(string Codigo, string Ruta, string Texto, string Icono, bool Exacta);

    // Presentación (íconos y textos cortos); qué se muestra lo deciden los permisos.
    private static readonly Dictionary<string, (string Texto, string Icono, bool Exacta)> Presentacion = new()
    {
        // T-803: sección DASHBOARD = sólo los tableros
        [Opciones.DashboardReportes] = ("Dashboard Admin", "bi-bar-chart", false),
        [Opciones.ProduccionPresupuestoReal] = ("Presupuesto vs. real", "bi-graph-up-arrow", false),
        [Opciones.ProduccionTablero] = ("Tablero del turno", "bi-speedometer2", true),
        [Opciones.ProduccionTableroGerencial] = ("Tablero de producción (gerencial)", "bi-display", false),
        [Opciones.DashboardHorasProgramadas] = ("Horas programadas", "bi-calendar-week", false),
        [Opciones.MaestrosLineas] = ("Líneas", "bi-building", false),
        [Opciones.MaestrosEquipos] = ("Equipos", "bi-cpu", false),
        [Opciones.MaestrosTiposFalla] = ("Tipos de falla", "bi-exclamation-triangle", false),
        [Opciones.MaestrosFallas] = ("Fallas", "bi-x-circle", false),
        [Opciones.MaestrosReglasAnalisis] = ("Reglas de análisis", "bi-sliders", false),
        [Opciones.MaestrosControlProceso] = ("Puntos de control de proceso", "bi-ui-checks", false),
        [Opciones.MaestrosInspecciones] = ("Planes de inspección", "bi-clipboard-check", false),
        [Opciones.MaestrosProductos] = ("Productos", "bi-box-seam", false),
        [Opciones.MaestrosParametros] = ("Parámetros línea + producto", "bi-bullseye", false),
        [Opciones.MaestrosCamaras] = ("Cámaras", "bi-camera-video", false),
        [Opciones.MaestrosCalidad] = ("Calidad (puntos y defectos)", "bi-camera", false),
        [Opciones.DetencionesListado] = ("Ver detenciones", "bi-list-ul", true),
        [Opciones.DetencionesPendientes] = ("Mis pendientes", "bi-inbox", false),
        [Opciones.DetencionesSoluciones] = ("Base de soluciones", "bi-book", false),
        [Opciones.DetencionesDeclararManual] = ("Declarar detención manual", "bi-lightning-charge", false),
        [Opciones.ProduccionInicioTurno] = ("Inicio de turno", "bi-play-circle", false),
        [Opciones.ProduccionTurnos] = ("Turnos de producción", "bi-calendar3", false),
        [Opciones.TareasPendientes] = ("Pendientes y programadas", "bi-list-ul", true),
        [Opciones.TareasHistorial] = ("Historial", "bi-clock-history", false),
        [Opciones.ControlProcesoRegistros] = ("Ver controles", "bi-list-ul", true),
        [Opciones.InspeccionesEjecuciones] = ("Ver inspecciones", "bi-list-check", true),
        [Opciones.InspeccionesNoOk] = ("Panel NO OK", "bi-exclamation-triangle", false),
        [Opciones.CalidadRegistro] = ("Registro de defectos", "bi-list-ul", true),
        [Opciones.CalidadParadasCamara] = ("Paradas y producción (cámara)", "bi-stopwatch", false),
        [Opciones.ConfigTurnos] = ("Turnos", "bi-clock", false),
        [Opciones.ConfigMaquinistas] = ("Maquinistas", "bi-people", false),
        [Opciones.ConfigUsuarios] = ("Usuarios", "bi-person-gear", false),
        [Opciones.ConfigRoles] = ("Roles y permisos", "bi-shield-lock", false),
    };

    /// <summary>Entradas de una sección para el usuario, en orden (incluye los accesos "Nueva …" si tiene ALTA).</summary>
    public static IEnumerable<ItemMenu> ItemsDe(PermisosUsuario permisos, string seccion)
    {
        foreach (var o in permisos.MenuDe(seccion))
        {
            var (texto, icono, exacta) = Presentacion.TryGetValue(o.Codigo, out var p) ? p : (o.Nombre, "bi-dot", false);
            if (o.Codigo == Opciones.TareasPendientes && o.Alta)
                yield return new ItemMenu(o.Codigo + ".NUEVA", "tareas-mantenimiento?nueva=1", "Nueva tarea", "bi-plus-circle", false);
            yield return new ItemMenu(o.Codigo, o.Ruta!, texto, icono, exacta);
            if (o.Codigo == Opciones.ControlProcesoRegistros && o.Alta)
                yield return new ItemMenu(o.Codigo + ".NUEVO", "control-proceso/nuevo", "Nuevo control", "bi-plus-circle", false);
            if (o.Codigo == Opciones.InspeccionesEjecuciones && o.Alta)
                yield return new ItemMenu(o.Codigo + ".NUEVA", "inspecciones/nueva", "Nueva inspección", "bi-plus-circle", false);
        }
    }
}
