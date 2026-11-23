using System.Reflection;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Web.Layout;
using PSAG.Mantenimiento.Web.Models;
using PSAG.Mantenimiento.Web.Services;
using PSAG.Mantenimiento.Web.Shared;
using AccionDominio = PSAG.Mantenimiento.Domain.Permisos.AccionPermiso;
using Catalogo = PSAG.Mantenimiento.Domain.Permisos.CatalogoPermisos;

namespace PSAG.Mantenimiento.Web.Tests;

/// <summary>
/// T-803 · Menú por rol en la Web: con los permisos de la matriz (como los devuelve api/permisos/mios) la sección
/// DASHBOARD muestra exactamente los tableros de cada rol, con sus textos; "Dashboard de Control" no aparece (ni en el
/// NavMenu dibujado), y la raíz "/" lleva a la pantalla que corresponde.
/// </summary>
public class MenuDashboardsWebTests
{
    /// <summary>Lo que devuelve api/permisos/mios para un usuario con ese rol y la matriz del catálogo.</summary>
    public static PermisosUsuario PermisosDe(string rol, bool obligado = false)
    {
        var visibles = Catalogo.Todas.Where(o => !o.EsSeccion && o.De(rol) != AccionDominio.Ninguna).ToList();
        var secciones = Catalogo.Todas.Where(s => s.EsSeccion && visibles.Any(v => v.PadreCodigo == s.Codigo));
        var dto = new PermisosMiosDto { ObligadoATurno = obligado, Roles = [rol] };
        foreach (var s in secciones)
            dto.Opciones.Add(new OpcionPermitidaDto { Codigo = s.Codigo, Nombre = s.Nombre, Orden = s.Orden, Ver = true });
        foreach (var o in visibles)
        {
            var a = o.De(rol);
            dto.Opciones.Add(new OpcionPermitidaDto
            {
                Codigo = o.Codigo, Nombre = o.Nombre, PadreCodigo = o.PadreCodigo, Ruta = o.Ruta, Orden = o.Orden,
                Ver = a.HasFlag(AccionDominio.Ver), Alta = a.HasFlag(AccionDominio.Alta),
                Modificar = a.HasFlag(AccionDominio.Modificar), Baja = a.HasFlag(AccionDominio.Baja)
            });
        }
        return new PermisosUsuario(dto);
    }

    [Theory]
    [InlineData("ADMINISTRADOR", "Dashboard Admin|Presupuesto vs. real|Tablero de producción (gerencial)|Horas programadas")]
    [InlineData("SUPERVISOR", "Dashboard Admin|Presupuesto vs. real|Tablero del turno|Tablero de producción (gerencial)|Horas programadas")]
    [InlineData("GERENCIA", "Dashboard Admin|Presupuesto vs. real|Tablero de producción (gerencial)")]
    [InlineData("MANTENIMIENTO", "Dashboard Admin|Presupuesto vs. real")]
    [InlineData("OPERARIO", "Presupuesto vs. real|Tablero del turno")]
    public void SeccionDashboard_PorRol(string rol, string esperado)
    {
        var p = PermisosDe(rol);
        var items = MenuUi.ItemsDe(p, Opciones.Dashboard).ToList();

        Assert.Equal(esperado.Split('|'), items.Select(i => i.Texto));
        Assert.Equal(Opciones.Dashboard, p.Secciones.First().Codigo);   // primera sección del menú
        // Nadie ve "Inicio" / "Dashboard de Control" en ningún lado del menú
        var todos = p.Secciones.SelectMany(s => MenuUi.ItemsDe(p, s.Codigo)).ToList();
        Assert.DoesNotContain(todos, i => i.Ruta is "" or "dashboard" || i.Codigo == Opciones.DashboardInicio);
        // Lo movido a DASHBOARD no se duplica en Producción
        Assert.DoesNotContain(MenuUi.ItemsDe(p, Opciones.Produccion), i =>
            i.Codigo is Opciones.ProduccionPresupuestoReal or Opciones.ProduccionTablero or Opciones.ProduccionTableroGerencial);
    }

    [Theory]
    [InlineData("OPERARIO", true, "/produccion/tablero")]
    [InlineData("SUPERVISOR", false, "/dashboard/reportes")]
    [InlineData("GERENCIA", false, "/dashboard/reportes")]
    [InlineData("ADMINISTRADOR", false, "/dashboard/reportes")]
    [InlineData("MANTENIMIENTO", false, "/dashboard/reportes")]
    [InlineData("OPERARIO", false, "/produccion/tablero")]   // operario no obligado a turno (si se edita el rol)
    public void Raiz_LlevaALaPantallaDelRol(string rol, bool obligado, string esperado) =>
        Assert.Equal(esperado, RutasInicio.Destino(PermisosDe(rol, obligado)));

    [Fact]
    public void Raiz_SinTablerosNiNada_VaALaPrimeraPantallaOADetenciones()
    {
        Assert.Equal(RutasInicio.PorDefecto, RutasInicio.Destino(PermisosUsuario.Ninguno));
        var soloCalidad = new PermisosUsuario(new PermisosMiosDto
        {
            Opciones =
            [
                new OpcionPermitidaDto { Codigo = Opciones.Calidad, Ver = true, Orden = 80 },
                new OpcionPermitidaDto { Codigo = Opciones.CalidadRegistro, PadreCodigo = Opciones.Calidad, Ruta = "calidad", Ver = true, Orden = 1 }
            ]
        });
        Assert.Equal("/calidad", RutasInicio.Destino(soloCalidad));
    }

    // ===================== NavMenu dibujado (HtmlRenderer, sin navegador) =====================

    [Theory]
    [InlineData("OPERARIO")]
    [InlineData("SUPERVISOR")]
    [InlineData("GERENCIA")]
    [InlineData("ADMINISTRADOR")]
    public async Task NavMenu_SinDashboardDeControl_YConLosTablerosDelRol(string rol)
    {
        var html = System.Net.WebUtility.HtmlDecode(await RenderNavMenuAsync(PermisosDe(rol)));

        Assert.DoesNotContain("Dashboard de Control", html);
        Assert.DoesNotContain("href=\"\"", html.Replace("class=\"navbar-brand\" href=\"\"", ""));   // sólo la marca va a "/"
        foreach (var item in MenuUi.ItemsDe(PermisosDe(rol), Opciones.Dashboard))
            Assert.Contains($"href=\"{item.Ruta}\"", html);
        Assert.Equal(rol != "OPERARIO", html.Contains("Dashboard Admin"));
    }

    private static async Task<string> RenderNavMenuAsync(PermisosUsuario permisos)
    {
        var services = new ServiceCollection();
        services.AddSingleton<NavigationManager>(new NavegacionDePrueba());
        services.AddSingleton<IPermisosService>(new PermisosFijos(permisos));
        services.AddSingleton(Nulo<IAnalisisService>.Crear());
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<NavMenu>(ParameterView.Empty)).ToHtmlString());
    }

    private sealed class NavegacionDePrueba : NavigationManager
    {
        public NavegacionDePrueba() => Initialize("http://localhost/", "http://localhost/");
        protected override void NavigateToCore(string uri, bool forceLoad) { }
    }

    private sealed class PermisosFijos(PermisosUsuario permisos) : IPermisosService
    {
        public event Action? Cambiaron { add { } remove { } }
        public Task<PermisosUsuario> ObtenerAsync() => Task.FromResult(permisos);
        public Task<PermisosUsuario> RefrescarAsync() => Task.FromResult(permisos);
        public Task<ConfiguracionPermisosDto> GetConfiguracionAsync() => throw new NotSupportedException();
        public Task<(RolPermisosDto? Resultado, string? Error)> GuardarRolAsync(string roleId, GuardarPermisosRolRequest request) => throw new NotSupportedException();
    }

    /// <summary>Implementación vacía de una interfaz: métodos Task&lt;T&gt; devuelven default, eventos no hacen nada.</summary>
    public class Nulo<T> : DispatchProxy where T : class
    {
        public static T Crear() => Create<T, Nulo<T>>();

        protected override object? Invoke(MethodInfo? metodo, object?[]? args)
        {
            var tipo = metodo!.ReturnType;
            if (tipo == typeof(Task))
                return Task.CompletedTask;
            if (tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(Task<>))
            {
                var valor = tipo.GenericTypeArguments[0].IsValueType ? Activator.CreateInstance(tipo.GenericTypeArguments[0]) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(tipo.GenericTypeArguments[0]).Invoke(null, [valor]);
            }
            return tipo.IsValueType && tipo != typeof(void) ? Activator.CreateInstance(tipo) : null;
        }
    }
}
