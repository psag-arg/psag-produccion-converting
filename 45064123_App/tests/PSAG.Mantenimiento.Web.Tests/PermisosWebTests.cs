using System.Reflection;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Shared;
using Catalogo = PSAG.Mantenimiento.Domain.Permisos.CatalogoPermisos;
using OpcionesDominio = PSAG.Mantenimiento.Domain.Permisos.Opciones;
using OpcionesWeb = PSAG.Mantenimiento.Web.Shared.Opciones;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>T-802 en la Web: códigos de opción, ruta → opción, menú y botones según permisos.</summary>
public class PermisosWebTests
{
    private static Dictionary<string, string> Constantes(Type t) =>
        t.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToDictionary(f => f.Name, f => (string)f.GetRawConstantValue()!);

    [Fact]
    public void CodigosDeLaWeb_SonLosDelCatalogo()
    {
        Assert.Equal(Constantes(typeof(OpcionesDominio)).OrderBy(x => x.Key), Constantes(typeof(OpcionesWeb)).OrderBy(x => x.Key));
    }

    [Fact]
    public void CadaRutaDelCatalogo_ExigeSuOpcion()
    {
        foreach (var o in Catalogo.Todas.Where(o => o.Ruta != null))
            Assert.Equal(o.Codigo, RutasPermiso.OpcionDe(o.Ruta!));
    }

    [Theory]
    [InlineData("detenciones/15", OpcionesWeb.DetencionesListado)]
    [InlineData("detenciones/15/declarar?volver=/detenciones", OpcionesWeb.DetencionesListado)]
    [InlineData("detenciones/nueva", OpcionesWeb.DetencionesListado)]
    [InlineData("detenciones/pendientes", OpcionesWeb.DetencionesPendientes)]
    [InlineData("tareas-mantenimiento?nueva=1", OpcionesWeb.TareasPendientes)]
    [InlineData("tareas-mantenimiento/imprimir", OpcionesWeb.TareasPendientes)]
    [InlineData("control-proceso/nuevo", OpcionesWeb.ControlProcesoRegistros)]
    [InlineData("inspecciones/hallazgos", OpcionesWeb.InspeccionesNoOk)]
    [InlineData("inspecciones/7", OpcionesWeb.InspeccionesEjecuciones)]
    [InlineData("", null)]                       // T-803: la raíz sólo redirige (RutasInicio)
    [InlineData("dashboard", null)]
    [InlineData("dashboard/produccion", OpcionesWeb.ProduccionPresupuestoReal)]
    [InlineData("produccion/tablero", OpcionesWeb.ProduccionTablero)]
    [InlineData("produccion/tablero/gerencial", OpcionesWeb.ProduccionTableroGerencial)]
    [InlineData("login", null)]
    public void RutaSinMenu_UsaLaOpcionDeSuPantalla(string ruta, string? opcion) =>
        Assert.Equal(opcion, RutasPermiso.OpcionDe(ruta));

    private static PermisosUsuario Permisos(params OpcionPermitidaDto[] opciones) => new(new PermisosMiosDto { Opciones = [.. opciones] });

    [Fact]
    public void Menu_SoloSeccionesConAlgoVisible()
    {
        var p = Permisos(
            new OpcionPermitidaDto { Codigo = OpcionesWeb.Detenciones, Ver = true, Orden = 30 },
            new OpcionPermitidaDto { Codigo = OpcionesWeb.DetencionesListado, PadreCodigo = OpcionesWeb.Detenciones, Ruta = "detenciones", Ver = true, Alta = true },
            new OpcionPermitidaDto { Codigo = OpcionesWeb.DetencionesSeguimiento, PadreCodigo = OpcionesWeb.Detenciones, Ruta = null, Modificar = true },
            new OpcionPermitidaDto { Codigo = OpcionesWeb.General, Ver = true, Orden = 5 },
            new OpcionPermitidaDto { Codigo = OpcionesWeb.GeneralCatalogos, PadreCodigo = OpcionesWeb.General, Ver = true });

        Assert.Equal([OpcionesWeb.Detenciones], p.Secciones.Select(s => s.Codigo));
        Assert.Equal([OpcionesWeb.DetencionesListado], p.MenuDe(OpcionesWeb.Detenciones).Select(o => o.Codigo));
        Assert.True(p.Tiene(OpcionesWeb.DetencionesListado, AccionPermiso.Alta));
        Assert.False(p.Tiene(OpcionesWeb.DetencionesListado, AccionPermiso.Baja));
        Assert.True(p.Tiene(OpcionesWeb.DetencionesSeguimiento, AccionPermiso.Modificar));
        Assert.False(PermisosUsuario.Ninguno.Ve(OpcionesWeb.DashboardInicio));
    }

    [Theory]
    [InlineData("REGISTRADA", true, false, 1, true)]
    [InlineData("REGISTRADA", false, false, 1, false)]   // sin MODIFICACION
    [InlineData("REGISTRADA", true, true, 2, false)]     // sólo operario, línea ajena
    [InlineData("REGISTRADA", true, true, 1, true)]
    [InlineData("CERRADA", true, false, 1, false)]
    public void LapizDeEditar_PorPermisoYLineaDelOperario(string estado, bool permiso, bool soloOperario, int linea, bool esperado) =>
        Assert.Equal(esperado, RolesCarga.PuedeEditar(new DetencionDto { LineaId = linea, EstadoActual = estado }, permiso, soloOperario, [1]));

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Declarar_PorPermisoDeAlta(bool permiso, bool esperado) =>
        Assert.Equal(esperado, RolesCarga.PuedeDeclarar(new DetencionDto { LineaId = 1, EstadoActual = "PENDIENTE_JUSTIFICACION" }, permiso, false, []));
}
