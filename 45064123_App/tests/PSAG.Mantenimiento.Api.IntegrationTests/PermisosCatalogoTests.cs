using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using PSAG.Mantenimiento.Api.Autorizacion;
using PSAG.Mantenimiento.Api.Controllers;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-802 sin base de datos: todas las acciones de la API tienen [Permiso] (salvo las que quedan sin opción a propósito),
/// el catálogo es consistente y la matriz migrada reproduce lo que el código permitía antes (T-798).
/// </summary>
public class PermisosCatalogoTests
{
    /// <summary>Acciones que quedan sin opción a propósito (además de las [AllowAnonymous]): la sesión propia y el menú.</summary>
    public static readonly string[] SinOpcion =
    [
        "AuthController.Logout", "AuthController.ChangePassword", "AuthController.GetCurrentUser", "PermisosController.GetMios"
    ];

    private static IEnumerable<MethodInfo> Acciones() =>
        typeof(ApiControllerBase).Assembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetCustomAttributes<HttpMethodAttribute>().Any());

    private static bool EsAnonima(MethodInfo m) =>
        m.GetCustomAttribute<AllowAnonymousAttribute>() != null || m.DeclaringType!.GetCustomAttribute<AllowAnonymousAttribute>() != null;

    [Fact]
    public void TodasLasAcciones_TienenPermiso_SalvoLasAnonimasYLaSesion()
    {
        var sinPermiso = Acciones()
            .Where(m => !EsAnonima(m) && m.GetCustomAttribute<PermisoAttribute>() == null)
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}")
            .Except(SinOpcion)
            .ToList();
        Assert.Empty(sinPermiso);

        // Las anónimas son exactamente login, refresh, seed de claves e ingestas/configuración de cámaras (con clave)
        var anonimas = Acciones().Where(EsAnonima).Select(m => $"{m.DeclaringType!.Name}.{m.Name}").OrderBy(x => x).ToList();
        Assert.Equal(new[]
        {
            "AuthController.Login", "AuthController.RefreshToken", "AuthController.SeedPasswords",
            "CalidadIngestaController.Ingestar", "CamarasConfiguracionController.Get", "ConteoCamaraIngestaController.Ingestar"
        }, anonimas);

        // Nadie usa más los roles fijos en los atributos
        Assert.DoesNotContain(Acciones().SelectMany(m => m.GetCustomAttributes<AuthorizeAttribute>().Concat(m.DeclaringType!.GetCustomAttributes<AuthorizeAttribute>())),
            a => a.Roles != null || a.Policy != null);
    }

    [Fact]
    public void LosPermisosDeLasAcciones_UsanOpcionesYAccionesDelCatalogo()
    {
        foreach (var m in Acciones())
        {
            var p = m.GetCustomAttribute<PermisoAttribute>();
            if (p == null)
                continue;
            foreach (var codigo in p.Codigos)
            {
                var def = CatalogoPermisos.Buscar(codigo);
                Assert.True(def != null, $"{m.DeclaringType!.Name}.{m.Name}: opción {codigo} inexistente.");
                Assert.True(def!.Aplicables.HasFlag(p.Accion), $"{m.DeclaringType!.Name}.{m.Name}: {p.Accion} no aplica a {codigo}.");
            }
            // GET = VER (salvo superpuestas, que es parte de cargar); POST/PUT/DELETE nunca VER.
            var esGet = m.GetCustomAttributes<HttpGetAttribute>().Any();
            if (!esGet)
                Assert.True(p.Accion != AccionPermiso.Ver, $"{m.DeclaringType!.Name}.{m.Name}: una escritura no puede pedir sólo VER.");
        }
    }

    [Fact]
    public void Catalogo_EsConsistente()
    {
        var todas = CatalogoPermisos.Todas;
        Assert.Equal(todas.Count, todas.Select(o => o.Codigo).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        foreach (var o in todas.Where(o => !o.EsSeccion))
        {
            var padre = CatalogoPermisos.Buscar(o.PadreCodigo!);
            Assert.True(padre is { EsSeccion: true }, $"{o.Codigo}: el padre tiene que ser una sección.");
            Assert.StartsWith(o.PadreCodigo + ".", o.Codigo);
            Assert.NotEqual(AccionPermiso.Ninguna, o.Aplicables);
            foreach (var (rol, acciones) in o.PorRol)
            {
                Assert.Contains(rol, MatrizMigrada.Roles);
                // La matriz migrada ya está normalizada (sin VER no hay otras; sólo acciones aplicables)
                Assert.Equal(acciones, ReglasPermisos.Normalizar(acciones, o.Aplicables));
            }
        }
        Assert.Equal(1, todas.Count(o => o.Ruta == ""));
        Assert.Equal(todas.Where(o => o.Ruta != null).Count(), todas.Where(o => o.Ruta != null).Select(o => o.Ruta).Distinct().Count());
        Assert.True(ReglasPermisos.AdminConservaRoles(RolesSistema.Administrador, CatalogoPermisos.Buscar(Opciones.ConfigRoles)!.De(RolesSistema.Administrador)));
        Assert.Equal([RolesSistema.Operario], CatalogoPermisos.RolesObligadosATurno);
    }

    [Theory]
    [InlineData(AccionPermiso.Alta | AccionPermiso.Modificar, AccionPermiso.Todas, AccionPermiso.Ninguna)]
    [InlineData(AccionPermiso.Todas, AccionPermiso.Ver, AccionPermiso.Ver)]
    [InlineData(AccionPermiso.Ver | AccionPermiso.Baja, AccionPermiso.Ver | AccionPermiso.Alta, AccionPermiso.Ver)]
    [InlineData(AccionPermiso.Modificar, AccionPermiso.Modificar, AccionPermiso.Modificar)]   // función sin VER
    public void Normalizar_SinVerSacaLasDemas_YSoloAplicables(AccionPermiso pedidas, AccionPermiso aplicables, AccionPermiso esperado) =>
        Assert.Equal(esperado, ReglasPermisos.Normalizar(pedidas, aplicables));

    [Fact]
    public void AdminNoPuedePerderRolesYPermisos()
    {
        Assert.False(ReglasPermisos.AdminConservaRoles(RolesSistema.Administrador, AccionPermiso.Ver));
        Assert.False(ReglasPermisos.AdminConservaRoles(RolesSistema.Administrador, AccionPermiso.Ninguna));
        Assert.True(ReglasPermisos.AdminConservaRoles(RolesSistema.Supervisor, AccionPermiso.Ninguna));
    }

    /// <summary>
    /// Lecturas que antes eran de cualquier usuario logueado y ahora piden el VER de la única pantalla que las usa
    /// (documentado en docs/PERMISOS.md). Ninguna otra pantalla las llama: para el usuario no cambia nada.
    /// También "corregir análisis cerrado": el endpoint dejaba pasar a todos pero el servicio sólo aceptaba A y S.
    /// </summary>
    public static readonly Dictionary<string, string> CambiosDocumentados = new()
    {
        ["CalendarioLineasController.GetLineas"] = "AS",
        ["CalendarioLineasController.GetCalendario"] = "AS",
        ["CatalogosController.GetTiemposProgramados"] = "AS",
        ["CatalogosController.GetReglasAnalisis"] = "A",
        ["CatalogosController.GetReglaAnalisis"] = "A",
        ["ParametrosLineaProductoController.Get"] = "A",
        ["AnalisisController.EditarCerrado"] = "AS",
    };

    /// <summary>Roles que tenía cada acción antes de T-802 (sacado de los atributos y políticas de la rama T-798).</summary>
    public static TheoryData<string, string, string> RolesAntes => new()
    {
        { "AnalisisController", "AgregarAdjunto", "AMSOG" },
        { "AnalisisController", "BuscarSoluciones", "AMSOG" },
        { "AnalisisController", "CerrarAnalisis", "AMSOG" },
        { "AnalisisController", "EditarCerrado", "AMSOG" },
        { "AnalisisController", "GetAdjunto", "AMSOG" },
        { "AnalisisController", "GetAnalisis", "AMSOG" },
        { "AnalisisController", "GetConteoPendientes", "AMSOG" },
        { "AnalisisController", "GetMisPendientes", "AMSOG" },
        { "AnalisisController", "GuardarBorrador", "AMSOG" },
        { "CalendarioLineasController", "DesactivarVersion", "AS" },
        { "CalendarioLineasController", "GetCalendario", "AMSOG" },
        { "CalendarioLineasController", "GetLineas", "AMSOG" },
        { "CalendarioLineasController", "GuardarCalendario", "AS" },
        { "CalidadController", "ActualizarPuntoControl", "A" },
        { "CalidadController", "ActualizarTipoDefecto", "A" },
        { "CalidadController", "CrearManual", "AS" },
        { "CalidadController", "CrearPuntoControl", "A" },
        { "CalidadController", "CrearTipoDefecto", "A" },
        { "CalidadController", "GetImagen", "AMSOG" },
        { "CalidadController", "GetPuntosControl", "AMSOG" },
        { "CalidadController", "GetRegistro", "AMSOG" },
        { "CalidadController", "GetRegistros", "AMSOG" },
        { "CalidadController", "GetTiposDefecto", "AMSOG" },
        { "CalidadController", "Revisar", "AMS" },
        { "CalidadController", "VincularDetencion", "AMS" },
        { "CamarasController", "Actualizar", "A" },
        { "CamarasController", "Crear", "A" },
        { "CamarasController", "Desactivar", "A" },
        { "CamarasController", "Get", "AMSOG" },
        { "CamarasController", "GetPorId", "AMSOG" },
        { "CatalogosController", "CreateDetalleFalla", "A" },
        { "CatalogosController", "CreateEquipo", "A" },
        { "CatalogosController", "CreateLinea", "A" },
        { "CatalogosController", "CreateMaquinista", "A" },
        { "CatalogosController", "CreateReglaAnalisis", "A" },
        { "CatalogosController", "CreateTipoFalla", "A" },
        { "CatalogosController", "CreateTurno", "A" },
        { "CatalogosController", "DeleteMaquinista", "A" },
        { "CatalogosController", "DesactivarTiempoProgramado", "AS" },
        { "CatalogosController", "GetDetalleFalla", "AMSOG" },
        { "CatalogosController", "GetDetallesFalla", "AMSOG" },
        { "CatalogosController", "GetEquipo", "AMSOG" },
        { "CatalogosController", "GetEquipos", "AMSOG" },
        { "CatalogosController", "GetLinea", "AMSOG" },
        { "CatalogosController", "GetLineas", "AMSOG" },
        { "CatalogosController", "GetMaquinista", "AMSOG" },
        { "CatalogosController", "GetMaquinistaPorUsuario", "AMSOG" },
        { "CatalogosController", "GetMaquinistas", "AMSOG" },
        { "CatalogosController", "GetMisAsignaciones", "AMSOG" },
        { "CatalogosController", "GetProducto", "AMSOG" },
        { "CatalogosController", "GetProductos", "AMSOG" },
        { "CatalogosController", "GetReglaAnalisis", "AMSOG" },
        { "CatalogosController", "GetReglasAnalisis", "AMSOG" },
        { "CatalogosController", "GetTiemposProgramados", "AMSOG" },
        { "CatalogosController", "GetTipoFalla", "AMSOG" },
        { "CatalogosController", "GetTiposFalla", "AMSOG" },
        { "CatalogosController", "GetTurno", "AMSOG" },
        { "CatalogosController", "GetTurnoActual", "AMSOG" },
        { "CatalogosController", "GetTurnos", "AMSOG" },
        { "CatalogosController", "GuardarTiempoProgramado", "AS" },
        { "CatalogosController", "UpdateDetalleFalla", "A" },
        { "CatalogosController", "UpdateEquipo", "A" },
        { "CatalogosController", "UpdateLinea", "A" },
        { "CatalogosController", "UpdateMaquinista", "A" },
        { "CatalogosController", "UpdateReglaAnalisis", "A" },
        { "CatalogosController", "UpdateTipoFalla", "A" },
        { "CatalogosController", "UpdateTurno", "A" },
        { "ConteoCamaraController", "GetAlertas", "AMSOG" },
        { "ConteoCamaraController", "GetParadas", "AMSOG" },
        { "ConteoCamaraController", "GetProduccion", "AMSOG" },
        { "ControlProcesoController", "ActualizarGrupo", "A" },
        { "ControlProcesoController", "ActualizarPunto", "A" },
        { "ControlProcesoController", "CrearGrupo", "A" },
        { "ControlProcesoController", "CrearPunto", "A" },
        { "ControlProcesoController", "GetPlantilla", "A" },
        { "DashboardController", "GetDashboard", "AMSOG" },
        { "DashboardController", "GetDetencionesPorLinea", "AMSOG" },
        { "DashboardController", "GetDetencionesPorTipoFalla", "AMSOG" },
        { "DashboardController", "GetFallosMayores", "AMSOG" },
        { "DashboardController", "GetIndisponibilidad", "AMSOG" },
        { "DashboardController", "GetPareto", "AMSOG" },
        { "DashboardController", "GetResumen", "AMSOG" },
        { "DashboardController", "GetTendencia", "AMSOG" },
        { "DashboardController", "GetTiempoProgramado", "AMSOG" },
        { "DashboardController", "GetTopDetallesFalla", "AMSOG" },
        { "DeclaracionDetencionesController", "Declarar", "AMSO" },
        { "DeclaracionDetencionesController", "GetAvisoFinTurno", "AMSOG" },
        { "DeclaracionDetencionesController", "GetDeclaracion", "AMSOG" },
        { "DetencionesController", "AgregarNota", "AMSOG" },
        { "DetencionesController", "CerrarDetencion", "AM" },
        { "DetencionesController", "CreateDetencion", "AMSO" },
        { "DetencionesController", "EliminarDetencion", "A" },
        { "DetencionesController", "ExportarCsv", "AMSOG" },
        { "DetencionesController", "ExportarXlsx", "AMSOG" },
        { "DetencionesController", "GetDetencion", "AMSOG" },
        { "DetencionesController", "GetDetenciones", "AMSOG" },
        { "DetencionesController", "GetDetencionesAbiertas", "AMSOG" },
        { "DetencionesController", "GetResumenTurno", "AMSOG" },
        { "DetencionesController", "GetSuperpuestas", "AMSO" },
        { "DetencionesController", "IniciarSeguimiento", "AM" },
        { "DetencionesController", "PrevisualizarAnalisis", "AMSO" },
        { "DetencionesController", "RestaurarDetencion", "A" },
        { "DetencionesController", "UpdateDetencion", "AMSO" },
        { "IndicadoresController", "GetProduccion", "AMSOG" },
        { "IndicadoresController", "GetTurno", "AMSOG" },
        { "InspeccionPlantillaController", "ActualizarItem", "A" },
        { "InspeccionPlantillaController", "ActualizarPlan", "A" },
        { "InspeccionPlantillaController", "CrearItem", "A" },
        { "InspeccionPlantillaController", "CrearPlan", "A" },
        { "InspeccionPlantillaController", "GetPlantilla", "A" },
        { "InspeccionesController", "CerrarEjecucion", "AMSO" },
        { "InspeccionesController", "GetEjecucion", "AMSOG" },
        { "InspeccionesController", "GetEjecuciones", "AMSOG" },
        { "InspeccionesController", "GetHallazgosPaginado", "AMSOG" },
        { "InspeccionesController", "GetHallazgosPendientes", "AMSOG" },
        { "InspeccionesController", "GetPendientes", "AMSOG" },
        { "InspeccionesController", "GetPlan", "AMSOG" },
        { "InspeccionesController", "GetPlanes", "AMSOG" },
        { "InspeccionesController", "GetPlanesLinea", "AMSOG" },
        { "InspeccionesController", "GuardarResultados", "AMSO" },
        { "InspeccionesController", "IniciarEjecucion", "AMSO" },
        { "InspeccionesController", "ResolverHallazgo", "AM" },
        { "ParametrosLineaProductoController", "Actualizar", "A" },
        { "ParametrosLineaProductoController", "Crear", "A" },
        { "ParametrosLineaProductoController", "Get", "AMSOG" },
        { "ProductosMaestroController", "Actualizar", "A" },
        { "ProductosMaestroController", "ActualizarSku", "A" },
        { "ProductosMaestroController", "Crear", "A" },
        { "ProductosMaestroController", "CrearSku", "A" },
        { "ProductosMaestroController", "Get", "AMSOG" },
        { "ProductosMaestroController", "GetPorId", "AMSOG" },
        { "RegistrosProcesoController", "CerrarRegistro", "AM" },
        { "RegistrosProcesoController", "GetGruposConEstandares", "AMSOG" },
        { "RegistrosProcesoController", "GetRegistro", "AMSOG" },
        { "RegistrosProcesoController", "GetRegistros", "AMSOG" },
        { "RegistrosProcesoController", "GuardarValores", "AMSO" },
        { "RegistrosProcesoController", "IniciarRegistro", "AMSO" },
        { "TareasMantenimientoController", "Cerrar", "AMS" },
        { "TareasMantenimientoController", "Crear", "ASO" },
        { "TareasMantenimientoController", "GetActivas", "AMSOG" },
        { "TareasMantenimientoController", "GetHistorial", "AMSOG" },
        { "TareasMantenimientoController", "Programar", "AMS" },
        { "TurnosProduccionController", "Abrir", "AMSO" },
        { "TurnosProduccionController", "CambiarProducto", "AMSO" },
        { "TurnosProduccionController", "Cerrar", "AMSO" },
        { "TurnosProduccionController", "CorregirOperador", "AMS" },
        { "TurnosProduccionController", "CorregirProducto", "AMS" },
        { "TurnosProduccionController", "GetActual", "AMSOG" },
        { "TurnosProduccionController", "GetDia", "AMS" },
        { "TurnosProduccionController", "GetPropuesta", "AMSOG" },
        { "TurnosProduccionController", "RegistrarProduccion", "AMS" },
        { "TurnosProduccionController", "Relevar", "AMSO" },
        { "UsuariosController", "CreateUsuario", "A" },
        { "UsuariosController", "DeleteUsuario", "A" },
        { "UsuariosController", "GetRoles", "A" },
        { "UsuariosController", "GetUsuario", "A" },
        { "UsuariosController", "GetUsuarios", "A" },
        { "UsuariosController", "ResetPassword", "A" },
        { "UsuariosController", "UpdateUsuario", "A" },
    };

    [Theory]
    [MemberData(nameof(RolesAntes))]
    public void MatrizMigrada_ReproduceLosRolesDeAntes(string controller, string accion, string rolesAntes)
    {
        var tipo = typeof(ApiControllerBase).Assembly.GetTypes().Single(t => t.Name == controller);
        var metodo = tipo.GetMethods().Single(m => m.Name == accion && m.GetCustomAttributes<HttpMethodAttribute>().Any());
        var ahora = string.Concat(MatrizMigrada.RolesConPermiso(metodo).Select(r => r[0]));   // A M S O G
        var esperado = CambiosDocumentados.TryGetValue($"{controller}.{accion}", out var cambio) ? cambio : rolesAntes;
        Assert.Equal(esperado, ahora);
    }
}
