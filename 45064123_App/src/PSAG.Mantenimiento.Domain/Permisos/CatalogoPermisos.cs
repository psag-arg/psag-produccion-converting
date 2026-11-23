using PSAG.Mantenimiento.Domain.Common;

namespace PSAG.Mantenimiento.Domain.Permisos;

/// <summary>
/// Códigos de las opciones del sistema (T-802). Los usan el atributo <c>[Permiso]</c> de la API, la semilla y los tests;
/// la Web tiene su copia en <c>Shared/Opciones.cs</c> (un test verifica que coincidan).
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
    public const string ProduccionTableroGerencial = "PRODUCCION.TABLERO_GERENCIAL";

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

/// <summary>Una opción del catálogo con su matriz inicial (lo que el código permitía antes de T-802).</summary>
public sealed record OpcionDefinicion(
    string Codigo,
    string Nombre,
    string? PadreCodigo,
    string? Ruta,
    int Orden,
    AccionPermiso Aplicables,
    IReadOnlyDictionary<string, AccionPermiso> PorRol)
{
    public bool EsSeccion => PadreCodigo == null;

    public AccionPermiso De(string rol) => PorRol.TryGetValue(rol, out var a) ? a : AccionPermiso.Ninguna;
}

/// <summary>
/// Catálogo de opciones y matriz migrada (T-802 parte 1). Es la única fuente: la semilla de la API (DEV/tests) la carga
/// tal cual y el script <c>39_t802_roles_permisos.sql</c> tiene los mismos datos (lo verifica un test contra SQL Server).
/// La matriz reproduce lo que hasta T-798 decía el código (políticas de Program.cs, <c>[Authorize(Roles=)]</c>, AuthorizeView
/// del menú y chequeos por rol de los servicios); ver <c>docs/PERMISOS.md</c>.
/// </summary>
public static class CatalogoPermisos
{
    private const string A = RolesSistema.Administrador;
    private const string M = RolesSistema.Mantenimiento;
    private const string S = RolesSistema.Supervisor;
    private const string O = RolesSistema.Operario;
    private const string G = RolesSistema.Gerencia;

    private static readonly string[] Todos = [A, M, S, O, G];
    private static readonly string[] Carga = [A, M, S, O];       // ex política CanCreateDetenciones / RequireOperario
    private static readonly string[] Supervision = [A, M, S];    // ex RequireSupervisor
    private static readonly string[] Cierre = [A, M];            // ex CanCloseDetenciones
    private static readonly string[] Admin = [A];                // ex CanManageCatalogs / RequireAdministrador

    private const AccionPermiso V = AccionPermiso.Ver;
    private const AccionPermiso Al = AccionPermiso.Alta;
    private const AccionPermiso Mo = AccionPermiso.Modificar;
    private const AccionPermiso Ba = AccionPermiso.Baja;

    /// <summary>Roles con ObligadoATurno = 1 en la migración: sólo el operario (operador de máquina).</summary>
    public static readonly IReadOnlyList<string> RolesObligadosATurno = [O];

    public static readonly IReadOnlyList<OpcionDefinicion> Todas = Armar();

    public static OpcionDefinicion? Buscar(string codigo) =>
        Todas.FirstOrDefault(o => string.Equals(o.Codigo, codigo, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<OpcionDefinicion> Armar()
    {
        var lista = new List<OpcionDefinicion>();

        void Seccion(string codigo, string nombre, int orden) =>
            lista.Add(new OpcionDefinicion(codigo, nombre, null, null, orden, AccionPermiso.Ninguna, new Dictionary<string, AccionPermiso>()));

        // acciones: (acción, roles) en el orden VER, ALTA, MODIFICACION, BAJA
        void Opcion(string codigo, string nombre, string padre, string? ruta, int orden, params (AccionPermiso Accion, string[] Roles)[] acciones)
        {
            var porRol = new Dictionary<string, AccionPermiso>();
            foreach (var (accion, roles) in acciones)
                foreach (var rol in roles)
                    porRol[rol] = (porRol.TryGetValue(rol, out var previo) ? previo : AccionPermiso.Ninguna) | accion;
            var aplicables = acciones.Aggregate(AccionPermiso.Ninguna, (t, a) => t | a.Accion);
            lista.Add(new OpcionDefinicion(codigo, nombre, padre, ruta, orden, aplicables, porRol));
        }

        Seccion(Opciones.General, "General (sin menú)", 5);
        Opcion(Opciones.GeneralCatalogos, "Consultas comunes (listas de líneas, equipos, fallas, turnos, productos y operadores)", Opciones.General, null, 1,
            (V, Todos));

        Seccion(Opciones.Dashboard, "Dashboard", 10);
        Opcion(Opciones.DashboardInicio, "Inicio", Opciones.Dashboard, "", 1, (V, Todos));
        Opcion(Opciones.DashboardReportes, "Reportes", Opciones.Dashboard, "dashboard/reportes", 2, (V, Todos));
        Opcion(Opciones.DashboardHorasProgramadas, "Horas programadas", Opciones.Dashboard, "configuracion/horas-programadas", 3,
            (V, [A, S]), (Al, [A, S]), (Ba, [A, S]));

        Seccion(Opciones.Maestros, "Maestros", 20);
        Opcion(Opciones.MaestrosLineas, "Líneas", Opciones.Maestros, "maestros/lineas", 1, (V, Admin), (Al, Admin), (Mo, Admin));
        Opcion(Opciones.MaestrosEquipos, "Equipos", Opciones.Maestros, "maestros/equipos", 2, (V, Admin), (Al, Admin), (Mo, Admin));
        Opcion(Opciones.MaestrosTiposFalla, "Tipos de falla", Opciones.Maestros, "maestros/tipos-falla", 3, (V, Admin), (Al, Admin), (Mo, Admin));
        Opcion(Opciones.MaestrosFallas, "Fallas", Opciones.Maestros, "maestros/fallas", 4, (V, Admin), (Al, Admin), (Mo, Admin));
        Opcion(Opciones.MaestrosReglasAnalisis, "Reglas de análisis", Opciones.Maestros, "maestros/reglas-analisis", 5, (V, Admin), (Al, Admin), (Mo, Admin));
        Opcion(Opciones.MaestrosControlProceso, "Puntos de control de proceso", Opciones.Maestros, "maestros/control-proceso", 6, (V, Admin), (Al, Admin), (Mo, Admin));
        Opcion(Opciones.MaestrosInspecciones, "Planes de inspección", Opciones.Maestros, "maestros/inspecciones", 7, (V, Admin), (Al, Admin), (Mo, Admin));
        Opcion(Opciones.MaestrosProductos, "Productos", Opciones.Maestros, "maestros/productos", 8, (V, Admin), (Al, Admin), (Mo, Admin));
        Opcion(Opciones.MaestrosParametros, "Parámetros línea + producto", Opciones.Maestros, "maestros/parametros-linea-producto", 9, (V, Admin), (Al, Admin), (Mo, Admin));
        Opcion(Opciones.MaestrosCamaras, "Cámaras", Opciones.Maestros, "maestros/camaras", 10, (V, Admin), (Al, Admin), (Mo, Admin), (Ba, Admin));
        Opcion(Opciones.MaestrosCalidad, "Calidad (puntos y defectos)", Opciones.Maestros, "maestros/calidad", 11, (V, Admin), (Al, Admin), (Mo, Admin));

        Seccion(Opciones.Detenciones, "Detenciones", 30);
        Opcion(Opciones.DetencionesListado, "Ver detenciones", Opciones.Detenciones, "detenciones", 1,
            (V, Todos), (Al, Carga), (Mo, Carga), (Ba, Admin));
        Opcion(Opciones.DetencionesPendientes, "Mis pendientes (análisis de falla)", Opciones.Detenciones, "detenciones/pendientes", 2,
            (V, Todos), (Mo, Todos));
        Opcion(Opciones.DetencionesSoluciones, "Base de soluciones", Opciones.Detenciones, "detenciones/soluciones", 3, (V, Todos));
        Opcion(Opciones.DetencionesDeclararManual, "Declarar detención manual", Opciones.Detenciones, "detenciones/declarar-manual", 4,
            (V, Supervision));
        Opcion(Opciones.DetencionesSeguimiento, "Seguimiento y cierre de detenciones", Opciones.Detenciones, null, 5, (Mo, Cierre));
        Opcion(Opciones.DetencionesNotas, "Notas en detenciones", Opciones.Detenciones, null, 6, (Al, Todos));
        Opcion(Opciones.DetencionesAnalisisCerrado, "Corregir análisis cerrado", Opciones.Detenciones, null, 7, (Mo, [A, S]));

        Seccion(Opciones.Produccion, "Producción", 40);
        Opcion(Opciones.ProduccionInicioTurno, "Inicio de turno", Opciones.Produccion, "produccion/inicio-turno", 1, (V, [O]));
        Opcion(Opciones.ProduccionOperacionTurno, "Operar el turno (tomar, cambiar producto, relevar, cerrar)", Opciones.Produccion, null, 2,
            (V, Todos), (Al, Carga), (Mo, Carga));
        Opcion(Opciones.ProduccionTurnos, "Turnos de producción (corregir turno cerrado y producción)", Opciones.Produccion, "produccion/turnos", 3,
            (V, Supervision), (Mo, Supervision));
        Opcion(Opciones.ProduccionPresupuestoReal, "Presupuesto vs. real", Opciones.Produccion, "dashboard/produccion", 4, (V, Todos));
        // T-770 (script 41): tablero del operador en la tablet. El obligado a turno ve el suyo; el supervisor, una línea en sólo lectura.
        Opcion(Opciones.ProduccionTablero, "Tablero del turno (operador)", Opciones.Produccion, "produccion/tablero", 5, (V, [S, O]));
        // T-770 iteración 2 (script 42): el mismo tablero para cualquier línea y turno (en curso o cerrado), sólo lectura.
        // Sin ruta de menú a propósito (el menú lo reordena T-803): la pantalla es /produccion/tablero/gerencial.
        Opcion(Opciones.ProduccionTableroGerencial, "Tablero gerencial (cualquier línea y turno)", Opciones.Produccion, null, 6, (V, [A, S, G]));

        Seccion(Opciones.Tareas, "Tareas de mantenimiento", 50);
        Opcion(Opciones.TareasPendientes, "Pendientes y programadas", Opciones.Tareas, "tareas-mantenimiento", 1,
            (V, Todos), (Al, [O, S, A]), (Mo, [M, S, A]));
        Opcion(Opciones.TareasHistorial, "Historial", Opciones.Tareas, "tareas-mantenimiento/historial", 2, (V, Todos));

        Seccion(Opciones.ControlProceso, "Control de Proceso", 60);
        Opcion(Opciones.ControlProcesoRegistros, "Controles de proceso", Opciones.ControlProceso, "control-proceso", 1,
            (V, Todos), (Al, Carga), (Mo, Cierre));

        Seccion(Opciones.Inspecciones, "Inspecciones", 70);
        Opcion(Opciones.InspeccionesEjecuciones, "Inspecciones", Opciones.Inspecciones, "inspecciones", 1,
            (V, Todos), (Al, Carga), (Mo, Carga));
        Opcion(Opciones.InspeccionesNoOk, "Panel NO OK", Opciones.Inspecciones, "inspecciones/hallazgos", 2, (V, Todos), (Mo, Cierre));

        Seccion(Opciones.Calidad, "Calidad", 80);
        Opcion(Opciones.CalidadRegistro, "Registro de defectos", Opciones.Calidad, "calidad", 1,
            (V, Todos), (Al, [S, A]), (Mo, [S, M, A]));
        Opcion(Opciones.CalidadParadasCamara, "Paradas y producción (cámara)", Opciones.Calidad, "paradas-camara", 2, (V, Todos));

        Seccion(Opciones.Configuracion, "Configuración", 90);
        Opcion(Opciones.ConfigTurnos, "Turnos", Opciones.Configuracion, "configuracion/turnos", 1, (V, Admin), (Al, Admin), (Mo, Admin));
        // T-802: sólo consulta (las líneas habilitadas se cargan desde Usuarios).
        Opcion(Opciones.ConfigMaquinistas, "Maquinistas (consulta)", Opciones.Configuracion, "configuracion/maquinistas", 2, (V, Admin));
        Opcion(Opciones.ConfigUsuarios, "Usuarios", Opciones.Configuracion, "configuracion/usuarios", 3,
            (V, Admin), (Al, Admin), (Mo, Admin), (Ba, Admin));
        Opcion(Opciones.ConfigRoles, "Roles y permisos", Opciones.Configuracion, "configuracion/roles", 4, (V, Admin), (Mo, Admin));

        return lista;
    }
}

/// <summary>Reglas puras de la matriz de permisos (T-802).</summary>
public static class ReglasPermisos
{
    public const string MensajeAdminSinRoles =
        "El rol ADMINISTRADOR no puede quedarse sin Ver y Modificar en \"Roles y permisos\": se quedaría sin poder volver a configurarlos.";

    /// <summary>
    /// Deja sólo las acciones aplicables y, si la opción tiene VER y no está tildado, saca las demás
    /// (lo que no se ve no se puede cargar, editar ni dar de baja).
    /// </summary>
    public static AccionPermiso Normalizar(AccionPermiso pedidas, AccionPermiso aplicables)
    {
        var resultado = pedidas & aplicables;
        if (aplicables.HasFlag(AccionPermiso.Ver) && !resultado.HasFlag(AccionPermiso.Ver))
            return AccionPermiso.Ninguna;
        return resultado;
    }

    /// <summary>El administrador no puede perder <see cref="Opciones.ConfigRoles"/> (Ver + Modificar).</summary>
    public static bool AdminConservaRoles(string rol, AccionPermiso accionesConfigRoles) =>
        !string.Equals(rol, RolesSistema.Administrador, StringComparison.OrdinalIgnoreCase)
        || accionesConfigRoles.HasFlag(AccionPermiso.Ver | AccionPermiso.Modificar);
}
