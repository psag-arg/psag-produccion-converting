namespace PSAG.Mantenimiento.Web.Shared;

/// <summary>
/// Códigos de las opciones del sistema (T-802). Copia de PSAG.Mantenimiento.Domain.Permisos.Opciones (la Web no referencia
/// Domain); un test de Web.Tests verifica que coincidan.
/// </summary>
public static class Opciones
{
    public const string General = "GENERAL";
    public const string GeneralCatalogos = "GENERAL.CATALOGOS";
    public const string Dashboard = "DASHBOARD";
    public const string DashboardInicio = "DASHBOARD.INICIO";
    public const string DashboardReportes = "DASHBOARD.REPORTES";
    public const string DashboardHorasProgramadas = "DASHBOARD.HORAS_PROGRAMADAS";
    public const string Maestros = "MAESTROS";
    public const string MaestrosLineas = "MAESTROS.LINEAS";
    public const string MaestrosEquipos = "MAESTROS.EQUIPOS";
    public const string MaestrosTiposFalla = "MAESTROS.TIPOS_FALLA";
    public const string MaestrosFallas = "MAESTROS.FALLAS";
    public const string MaestrosReglasAnalisis = "MAESTROS.REGLAS_ANALISIS";
    public const string MaestrosControlProceso = "MAESTROS.CONTROL_PROCESO";
    public const string MaestrosInspecciones = "MAESTROS.INSPECCIONES";
    public const string MaestrosProductos = "MAESTROS.PRODUCTOS";
    public const string MaestrosParametros = "MAESTROS.PARAMETROS";
    public const string MaestrosCamaras = "MAESTROS.CAMARAS";
    public const string MaestrosCalidad = "MAESTROS.CALIDAD";
    public const string Detenciones = "DETENCIONES";
    public const string DetencionesListado = "DETENCIONES.LISTADO";
    public const string DetencionesPendientes = "DETENCIONES.PENDIENTES";
    public const string DetencionesSoluciones = "DETENCIONES.SOLUCIONES";
    public const string DetencionesDeclararManual = "DETENCIONES.DECLARAR_MANUAL";
    public const string DetencionesSeguimiento = "DETENCIONES.SEGUIMIENTO";
    public const string DetencionesNotas = "DETENCIONES.NOTAS";
    public const string DetencionesAnalisisCerrado = "DETENCIONES.ANALISIS_CERRADO";
    public const string Produccion = "PRODUCCION";
    public const string ProduccionInicioTurno = "PRODUCCION.INICIO_TURNO";
    public const string ProduccionOperacionTurno = "PRODUCCION.OPERACION_TURNO";
    public const string ProduccionTurnos = "PRODUCCION.TURNOS";
    public const string ProduccionPresupuestoReal = "PRODUCCION.PRESUPUESTO_REAL";
    public const string ProduccionTablero = "PRODUCCION.TABLERO";
    public const string Tareas = "TAREAS";
    public const string TareasPendientes = "TAREAS.PENDIENTES";
    public const string TareasHistorial = "TAREAS.HISTORIAL";
    public const string ControlProceso = "CONTROL_PROCESO";
    public const string ControlProcesoRegistros = "CONTROL_PROCESO.REGISTROS";
    public const string Inspecciones = "INSPECCIONES";
    public const string InspeccionesEjecuciones = "INSPECCIONES.EJECUCIONES";
    public const string InspeccionesNoOk = "INSPECCIONES.NO_OK";
    public const string Calidad = "CALIDAD";
    public const string CalidadRegistro = "CALIDAD.REGISTRO";
    public const string CalidadParadasCamara = "CALIDAD.PARADAS_CAMARA";
    public const string Configuracion = "CONFIG";
    public const string ConfigTurnos = "CONFIG.TURNOS";
    public const string ConfigMaquinistas = "CONFIG.MAQUINISTAS";
    public const string ConfigUsuarios = "CONFIG.USUARIOS";
    public const string ConfigRoles = "CONFIG.ROLES";
}

/// <summary>
/// Qué opción hay que VER para entrar a cada página (T-802). Las del menú tienen su ruta en el catálogo; acá se suman las
/// páginas sin entrada de menú (alta, detalle, impresión). Gana el prefijo más largo; las que no están (login, inicio de
/// sesión) no se controlan.
/// </summary>
public static class RutasPermiso
{
    private static readonly (string Prefijo, string Opcion)[] Mapa =
    [
        ("", Opciones.DashboardInicio),
        ("dashboard", Opciones.DashboardInicio),
        ("dashboard/reportes", Opciones.DashboardReportes),
        ("dashboard/produccion", Opciones.ProduccionPresupuestoReal),
        ("configuracion/horas-programadas", Opciones.DashboardHorasProgramadas),
        ("maestros/lineas", Opciones.MaestrosLineas),
        ("maestros/equipos", Opciones.MaestrosEquipos),
        ("maestros/tipos-falla", Opciones.MaestrosTiposFalla),
        ("maestros/fallas", Opciones.MaestrosFallas),
        ("maestros/reglas-analisis", Opciones.MaestrosReglasAnalisis),
        ("maestros/control-proceso", Opciones.MaestrosControlProceso),
        ("maestros/inspecciones", Opciones.MaestrosInspecciones),
        ("maestros/productos", Opciones.MaestrosProductos),
        ("maestros/parametros-linea-producto", Opciones.MaestrosParametros),
        ("maestros/camaras", Opciones.MaestrosCamaras),
        ("maestros/calidad", Opciones.MaestrosCalidad),
        ("detenciones", Opciones.DetencionesListado),
        ("detenciones/pendientes", Opciones.DetencionesPendientes),
        ("detenciones/soluciones", Opciones.DetencionesSoluciones),
        ("detenciones/declarar-manual", Opciones.DetencionesDeclararManual),
        ("produccion/inicio-turno", Opciones.ProduccionInicioTurno),
        ("produccion/turnos", Opciones.ProduccionTurnos),
        ("produccion/tablero", Opciones.ProduccionTablero),
        ("tareas-mantenimiento", Opciones.TareasPendientes),
        ("tareas-mantenimiento/historial", Opciones.TareasHistorial),
        ("control-proceso", Opciones.ControlProcesoRegistros),
        ("registros-proceso", Opciones.ControlProcesoRegistros),
        ("inspecciones", Opciones.InspeccionesEjecuciones),
        ("inspecciones/hallazgos", Opciones.InspeccionesNoOk),
        ("calidad", Opciones.CalidadRegistro),
        ("paradas-camara", Opciones.CalidadParadasCamara),
        ("configuracion/turnos", Opciones.ConfigTurnos),
        ("configuracion/maquinistas", Opciones.ConfigMaquinistas),
        ("configuracion/usuarios", Opciones.ConfigUsuarios),
        ("configuracion/roles", Opciones.ConfigRoles),
    ];

    /// <summary>Opción de una ruta relativa ("detenciones/15", "control-proceso?x=1"); null si no se controla.</summary>
    public static string? OpcionDe(string rutaRelativa)
    {
        var ruta = rutaRelativa.Split('?', '#')[0].Trim('/').ToLowerInvariant();
        string? mejor = null;
        var largo = -1;
        foreach (var (prefijo, opcion) in Mapa)
        {
            var coincide = prefijo.Length == 0 ? ruta.Length == 0 : ruta == prefijo || ruta.StartsWith(prefijo + "/");
            if (coincide && prefijo.Length > largo)
            {
                mejor = opcion;
                largo = prefijo.Length;
            }
        }
        return mejor;
    }
}
