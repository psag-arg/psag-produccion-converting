using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Application.DTOs.Permisos;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Permisos;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-803 · Menú de dashboards por rol, de punta a punta: la API arranca con su semilla (SQLite o SQL Server) y
/// <c>GET api/permisos/mios</c> (lo que arma el menú de la Web) trae en la sección DASHBOARD exactamente los tableros que
/// corresponden a cada rol; "Dashboard de Control" (ruta "/") no aparece en ningún lado; Dashboard Admin no es del operario.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class MenuDashboardsTests : IDisposable
{
    private readonly BaseDePrueba _base;
    private readonly ApiDePrueba _api;

    public MenuDashboardsTests()
    {
        _base = BaseDePrueba.Crear();
        _api = new ApiDePrueba(_base);
        _ = _api.Server;   // arranca la API: corre la semilla
    }

    public void Dispose()
    {
        _api.Dispose();
        _base.Dispose();
    }

    private async Task<PermisosMiosDto> MiosAsync(string rol) =>
        (await _api.Cliente("usr-menu-" + rol.ToLowerInvariant(), rol).GetFromJsonAsync<PermisosMiosDto>("api/permisos/mios"))!;

    /// <summary>Lo que muestra el menú de una sección: opciones con ruta y VER, en orden (PermisosUsuario.MenuDe de la Web).</summary>
    private static List<string> Menu(PermisosMiosDto p, string seccion) =>
        p.Opciones.Where(o => o.PadreCodigo == seccion && o.Ruta != null && o.Ver).OrderBy(o => o.Orden).Select(o => o.Ruta!).ToList();

    [Theory]
    [InlineData(RolesSistema.Administrador, "dashboard/reportes|dashboard/produccion|produccion/tablero/gerencial|configuracion/horas-programadas")]
    [InlineData(RolesSistema.Supervisor, "dashboard/reportes|dashboard/produccion|produccion/tablero|produccion/tablero/gerencial|configuracion/horas-programadas")]
    [InlineData(RolesSistema.Gerencia, "dashboard/reportes|dashboard/produccion|produccion/tablero/gerencial")]
    [InlineData(RolesSistema.Mantenimiento, "dashboard/reportes|dashboard/produccion")]
    [InlineData(RolesSistema.Operario, "dashboard/produccion|produccion/tablero")]
    public async Task SeccionDashboard_PorRol_SoloLosTableros(string rol, string esperado)
    {
        var p = await MiosAsync(rol);

        Assert.Equal(esperado.Split('|'), Menu(p, Opciones.Dashboard));
        // "Dashboard de Control" (/ y /dashboard) no está en ningún menú; Inicio queda sin ruta
        Assert.DoesNotContain(p.Opciones, o => o.Ruta is "" or "dashboard");
        Assert.DoesNotContain(p.Opciones, o => o.Nombre.Contains("Dashboard de Control", StringComparison.OrdinalIgnoreCase));
        // Lo que se movió a DASHBOARD no queda duplicado en Producción
        Assert.DoesNotContain(Menu(p, Opciones.Produccion), r => r.StartsWith("dashboard/") || r.StartsWith("produccion/tablero"));
        // Los nombres del menú
        var reportes = p.Opciones.SingleOrDefault(o => o.Codigo == Opciones.DashboardReportes);
        if (reportes != null)
            Assert.Equal("Dashboard Admin", reportes.Nombre);
        Assert.Equal(rol != RolesSistema.Operario, reportes?.Ver == true);
    }

    [Fact]
    public async Task Operario_SinDashboardAdmin_PeroSiguenLasLecturasDelDashboard()
    {
        var operario = await MiosAsync(RolesSistema.Operario);
        Assert.DoesNotContain(operario.Opciones, o => o.Codigo == Opciones.DashboardReportes && o.Ver);
        // DASHBOARD.INICIO (sin menú) conserva VER: las lecturas de api/dashboard no cambian
        Assert.Contains(operario.Opciones, o => o.Codigo == Opciones.DashboardInicio && o.Ver && o.Ruta == null);
    }

    /// <summary>
    /// La semilla (como el script 43) le saca Dashboard Admin al operario en una base migrada antes de T-803 (fila que dejó
    /// el 39), y NO toca una fila editada desde el configurador.
    /// </summary>
    [Fact]
    public async Task Semilla_QuitaDashboardAdminAlOperario_SoloSiNoSeEdito()
    {
        await using var ctx = _api.NuevoContexto();
        var opcion = await ctx.OpcionesSistema.SingleAsync(o => o.Codigo == Opciones.DashboardReportes);
        var operario = await ctx.Roles.SingleAsync(r => r.Name == RolesSistema.Operario);

        async Task<PermisoRol?> FilaAsync()
        {
            ctx.ChangeTracker.Clear();
            return await ctx.PermisosRol.AsNoTracking().SingleOrDefaultAsync(p => p.RoleId == operario.Id && p.OpcionId == opcion.OpcionId);
        }

        async Task PonerAsync(string usuario)
        {
            ctx.ChangeTracker.Clear();
            var fila = await ctx.PermisosRol.SingleOrDefaultAsync(p => p.RoleId == operario.Id && p.OpcionId == opcion.OpcionId);
            if (fila == null)
            {
                fila = new PermisoRol { RoleId = operario.Id, OpcionId = opcion.OpcionId };
                ctx.PermisosRol.Add(fila);
            }
            fila.Asignar(AccionPermiso.Ver);
            fila.FechaHoraModificacion = DateTimeOffset.UtcNow;
            fila.ModificadoPorUsuarioId = usuario;
            await ctx.SaveChangesAsync();
            ctx.ChangeTracker.Clear();
        }

        // Base migrada con la matriz vieja: la fila la dejó el script 39
        await PonerAsync("SCRIPT-39-T802");
        await SemillaPermisos.SembrarAsync(ctx);
        var fila = await FilaAsync();
        Assert.Equal(AccionPermiso.Ninguna, fila!.Acciones);
        Assert.Equal("SCRIPT-43-T803", fila.ModificadoPorUsuarioId);

        // Otra vez: no cambia nada
        await SemillaPermisos.SembrarAsync(ctx);
        Assert.Equal(AccionPermiso.Ninguna, (await FilaAsync())!.Acciones);

        // Un administrador se lo devuelve desde el configurador: la semilla no lo pisa
        await PonerAsync("admin-que-edito");
        await SemillaPermisos.SembrarAsync(ctx);
        Assert.Equal(AccionPermiso.Ver, (await FilaAsync())!.Acciones);
    }
}
