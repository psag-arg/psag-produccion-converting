using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PSAG.Mantenimiento.Application.DTOs.Permisos;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Permisos;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-802: la API entera (WebApplicationFactory) contra la base de prueba (SQLite o SQL Server), con la semilla de permisos
/// que corre al arrancar. Autenticación de prueba por encabezados (usuario y roles); la autorización es la real.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class PermisosApiTests : IDisposable
{
    private readonly BaseDePrueba _base;
    private readonly ApiDePrueba _api;

    public PermisosApiTests()
    {
        _base = BaseDePrueba.Crear();
        _api = new ApiDePrueba(_base);
        _ = _api.Server;   // arranca la API: corre la semilla (roles, admin, permisos)
    }

    public void Dispose()
    {
        _api.Dispose();
        _base.Dispose();
    }

    private HttpClient Como(string rol) => _api.Cliente("usr-" + rol.ToLowerInvariant(), rol);

    private static async Task Prohibido(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        var problema = await r.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Sin permiso", problema!.Title);
        Assert.Contains("no tienen permiso", problema.Detail);
    }

    /// <summary>Pasó la autorización por permisos (puede fallar después por validación, 404 o una regla de negocio).</summary>
    private static async Task NoProhibido(HttpResponseMessage r)
    {
        Assert.NotEqual(HttpStatusCode.Unauthorized, r.StatusCode);
        if (r.StatusCode == HttpStatusCode.Forbidden)
        {
            var texto = await r.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Sin permiso", texto);
        }
    }

    private async Task GuardarAsync(string rol, Func<RolPermisosDto, GuardarPermisosRolRequest> cambiar, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        var admin = Como(RolesSistema.Administrador);
        var config = await admin.GetFromJsonAsync<ConfiguracionPermisosDto>("api/permisos/roles");
        var dto = config!.Roles.Single(r => r.Nombre == rol);
        var r = await admin.PutAsJsonAsync($"api/permisos/roles/{dto.RoleId}", cambiar(dto));
        Assert.Equal(esperado, r.StatusCode);
    }

    private static GuardarPermisosRolRequest Con(RolPermisosDto dto, string codigo, AccionPermiso acciones)
    {
        var permisos = dto.Permisos.Where(p => p.Codigo != codigo).ToList();
        permisos.Add(PermisoOpcionDto.De(codigo, acciones));
        return new GuardarPermisosRolRequest { ObligadoATurno = dto.ObligadoATurno, Descripcion = dto.Descripcion, Permisos = permisos };
    }

    // ===================== sin sesión =====================

    [Fact]
    public async Task SinSesion_401()
    {
        var r = await _api.CreateClient().GetAsync("api/detenciones");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    // ===================== Detenciones =====================

    [Fact]
    public async Task Detenciones_VerAltaModificarBaja()
    {
        // El operario tiene VER, pero está obligado a turno (T-802): sin turno abierto, 409 (ver TurnoObligadoApiTests).
        Assert.Equal(HttpStatusCode.Conflict, (await Como(RolesSistema.Operario).GetAsync("api/detenciones")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Como(RolesSistema.Gerencia).GetAsync("api/detenciones")).StatusCode);

        // Gerencia: VER sin ALTA ni MODIFICACION
        await Prohibido(await Como(RolesSistema.Gerencia).PostAsJsonAsync("api/detenciones", new { }));
        await Prohibido(await Como(RolesSistema.Gerencia).PutAsJsonAsync("api/detenciones/1", new { }));
        // El operario sí carga (pasa la autorización; el cuerpo vacío lo rechaza la validación)
        await NoProhibido(await Como(RolesSistema.Operario).PostAsJsonAsync("api/detenciones", new { }));

        // BAJA (eliminar) sólo administrador
        await Prohibido(await Como(RolesSistema.Supervisor).PostAsync("api/detenciones/1/eliminar", JsonContent.Create(new { motivo = "x" })));
        await NoProhibido(await Como(RolesSistema.Administrador).PostAsync("api/detenciones/1/eliminar", JsonContent.Create(new { motivo = "x" })));

        // Seguimiento y cierre: administrador y mantenimiento
        await Prohibido(await Como(RolesSistema.Supervisor).PostAsJsonAsync("api/detenciones/1/iniciar-seguimiento", new { }));
        await NoProhibido(await Como(RolesSistema.Mantenimiento).PostAsJsonAsync("api/detenciones/1/iniciar-seguimiento", new { }));
    }

    [Fact]
    public async Task CambiarUnPermiso_SeAplicaEnLaSiguienteRequest()
    {
        var gerencia = Como(RolesSistema.Gerencia);
        Assert.Equal(HttpStatusCode.OK, (await gerencia.GetAsync("api/detenciones")).StatusCode);   // queda en caché

        await GuardarAsync(RolesSistema.Gerencia, dto => Con(dto, Opciones.DetencionesListado, AccionPermiso.Ninguna));
        await Prohibido(await gerencia.GetAsync("api/detenciones"));

        var mios = await gerencia.GetFromJsonAsync<PermisosMiosDto>("api/permisos/mios");
        Assert.DoesNotContain(mios!.Opciones, o => o.Codigo == Opciones.DetencionesListado);

        await GuardarAsync(RolesSistema.Gerencia, dto => Con(dto, Opciones.DetencionesListado, AccionPermiso.Ver | AccionPermiso.Alta));
        Assert.Equal(HttpStatusCode.OK, (await gerencia.GetAsync("api/detenciones")).StatusCode);
        await NoProhibido(await gerencia.PostAsJsonAsync("api/detenciones", new { }));
    }

    // ===================== Turnos de producción (T-793: quién corrige) =====================

    [Fact]
    public async Task TurnosProduccion_VerYCorregirSonPermisos()
    {
        await Prohibido(await Como(RolesSistema.Operario).GetAsync("api/turnos-produccion"));
        Assert.Equal(HttpStatusCode.OK, (await Como(RolesSistema.Supervisor).GetAsync("api/turnos-produccion")).StatusCode);
        await Prohibido(await Como(RolesSistema.Operario).PostAsJsonAsync("api/turnos-produccion/999/corregir-operador", new { }));
        await Prohibido(await Como(RolesSistema.Gerencia).PostAsJsonAsync("api/turnos-produccion/999/produccion", new { }));

        // Se le da a GERENCIA "Turnos de producción" VER + MODIFICACION: pasa la API y también el chequeo del servicio
        // (el turno no existe: 404, no 403).
        await GuardarAsync(RolesSistema.Gerencia, dto => Con(dto, Opciones.ProduccionTurnos, AccionPermiso.Ver | AccionPermiso.Modificar));
        Assert.Equal(HttpStatusCode.OK, (await Como(RolesSistema.Gerencia).GetAsync("api/turnos-produccion")).StatusCode);
        var r = await Como(RolesSistema.Gerencia).PostAsJsonAsync("api/turnos-produccion/999/corregir-producto",
            new { productoMaestroId = 1, desde = DateTimeOffset.UtcNow, motivo = "prueba" });
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    // ===================== Maestros =====================

    [Fact]
    public async Task Maestros_SoloAdministrador()
    {
        await Prohibido(await Como(RolesSistema.Supervisor).GetAsync("api/control-proceso/plantilla/1"));
        Assert.Equal(HttpStatusCode.OK, (await Como(RolesSistema.Administrador).GetAsync("api/control-proceso/plantilla/1")).StatusCode);
        await Prohibido(await Como(RolesSistema.Supervisor).PostAsJsonAsync("api/catalogos/lineas", new { }));
        await Prohibido(await Como(RolesSistema.Mantenimiento).PutAsJsonAsync("api/catalogos/equipos/1", new { }));
        // Las listas comunes (combos) las leen todos
        Assert.Equal(HttpStatusCode.OK, (await Como(RolesSistema.Operario).GetAsync("api/catalogos/lineas")).StatusCode);
    }

    // ===================== Cámaras =====================

    [Fact]
    public async Task Camaras_VerSiAltaYBajaNo_HastaQueSeConfigura()
    {
        Assert.Equal(HttpStatusCode.OK, (await Como(RolesSistema.Gerencia).GetAsync("api/camaras")).StatusCode);   // por "Paradas y producción (cámara)"
        await Prohibido(await Como(RolesSistema.Supervisor).PostAsJsonAsync("api/camaras", new { }));
        await Prohibido(await Como(RolesSistema.Supervisor).PostAsync("api/camaras/999/desactivar", null));

        // Sin VER de ninguna de las pantallas que la usan: 403 en el GET
        await GuardarAsync(RolesSistema.Gerencia, dto => Con(dto, Opciones.CalidadParadasCamara, AccionPermiso.Ninguna));
        await Prohibido(await Como(RolesSistema.Gerencia).GetAsync("api/camaras"));

        // Con VER + BAJA de Maestros → Cámaras, el supervisor pasa la API y el servicio (no existe: 404)
        await GuardarAsync(RolesSistema.Supervisor, dto => Con(dto, Opciones.MaestrosCamaras, AccionPermiso.Ver | AccionPermiso.Baja));
        Assert.Equal(HttpStatusCode.NotFound, (await Como(RolesSistema.Supervisor).PostAsync("api/camaras/999/desactivar", null)).StatusCode);
        await Prohibido(await Como(RolesSistema.Supervisor).PostAsJsonAsync("api/camaras", new { }));
    }

    // ===================== Configuración =====================

    [Fact]
    public async Task Configuracion_RolesYUsuarios_SoloAdministrador()
    {
        await Prohibido(await Como(RolesSistema.Supervisor).GetAsync("api/permisos/roles"));
        await Prohibido(await Como(RolesSistema.Gerencia).GetAsync("api/usuarios"));
        await Prohibido(await Como(RolesSistema.Supervisor).DeleteAsync("api/usuarios/x"));
        Assert.Equal(HttpStatusCode.OK, (await Como(RolesSistema.Administrador).GetAsync("api/permisos/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Como(RolesSistema.Administrador).GetAsync("api/usuarios")).StatusCode);
    }

    [Fact]
    public async Task Administrador_NoPuedeQuitarseRolesYPermisos()
    {
        var admin = Como(RolesSistema.Administrador);
        var config = await admin.GetFromJsonAsync<ConfiguracionPermisosDto>("api/permisos/roles");
        var dto = config!.Roles.Single(r => r.Nombre == RolesSistema.Administrador);

        foreach (var acciones in new[] { AccionPermiso.Ninguna, AccionPermiso.Ver })
        {
            var r = await admin.PutAsJsonAsync($"api/permisos/roles/{dto.RoleId}", Con(dto, Opciones.ConfigRoles, acciones));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
            Assert.Contains("ADMINISTRADOR", (await r.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail);
        }
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("api/permisos/roles")).StatusCode);
    }

    [Fact]
    public async Task Guardar_NormalizaYAudita()
    {
        // Sin VER se sacan las demás; lo que no aplica no se guarda.
        await GuardarAsync(RolesSistema.Supervisor, dto => Con(dto, Opciones.CalidadRegistro, AccionPermiso.Alta | AccionPermiso.Modificar));
        await GuardarAsync(RolesSistema.Supervisor, dto => Con(dto, Opciones.DetencionesSoluciones, AccionPermiso.Todas));
        var config = await Como(RolesSistema.Administrador).GetFromJsonAsync<ConfiguracionPermisosDto>("api/permisos/roles");
        var sup = config!.Roles.Single(r => r.Nombre == RolesSistema.Supervisor);
        Assert.Equal(AccionPermiso.Ninguna, sup.Permisos.Single(p => p.Codigo == Opciones.CalidadRegistro).Acciones());
        Assert.Equal(AccionPermiso.Ver, sup.Permisos.Single(p => p.Codigo == Opciones.DetencionesSoluciones).Acciones());

        using var ctx = _api.NuevoContexto();
        Assert.True(await ctx.Auditoria.AnyAsync(a => a.Entidad == "CFG_PermisosRol" && a.EntidadId == RolesSistema.Supervisor));
    }

    [Fact]
    public async Task Mios_OperarioObligadoATurnoYSinMaestros()
    {
        var mios = await Como(RolesSistema.Operario).GetFromJsonAsync<PermisosMiosDto>("api/permisos/mios");
        Assert.True(mios!.ObligadoATurno);
        Assert.Contains(mios.Opciones, o => o.Codigo == Opciones.ProduccionInicioTurno && o.Ver);
        Assert.Contains(mios.Opciones, o => o.Codigo == Opciones.Detenciones && o.PadreCodigo == null);   // sección
        Assert.DoesNotContain(mios.Opciones, o => o.Codigo.StartsWith("MAESTROS"));
        Assert.DoesNotContain(mios.Opciones, o => o.Codigo == Opciones.ProduccionTurnos);

        var sup = await Como(RolesSistema.Supervisor).GetFromJsonAsync<PermisosMiosDto>("api/permisos/mios");
        Assert.False(sup!.ObligadoATurno);

        // Varios roles: unión
        var ambos = await _api.Cliente("usr-x", RolesSistema.Operario, RolesSistema.Supervisor).GetFromJsonAsync<PermisosMiosDto>("api/permisos/mios");
        Assert.True(ambos!.ObligadoATurno);
        Assert.Contains(ambos.Opciones, o => o.Codigo == Opciones.ProduccionTurnos && o.Modificar);
        Assert.Contains(ambos.Opciones, o => o.Codigo == Opciones.ProduccionInicioTurno);
    }

    [Fact]
    public async Task SemillaAlArrancar_EsLaMatrizMigrada_YNoPisaLoEditado()
    {
        using (var ctx = _api.NuevoContexto())
        {
            var esperados = CatalogoPermisos.Todas.Where(d => !d.EsSeccion)
                .SelectMany(d => MatrizMigrada.Roles.Select(r => (d.Codigo, Rol: r, Acciones: d.De(r))))
                .Where(x => x.Acciones != AccionPermiso.Ninguna)
                .Select(x => $"{x.Rol}|{x.Codigo}|{(int)x.Acciones}").OrderBy(x => x).ToList();
            var reales = (await ctx.PermisosRol.Include(p => p.Opcion).ToListAsync())
                .Join(ctx.Roles.ToList(), p => p.RoleId, r => r.Id, (p, r) => $"{r.Name}|{p.Opcion!.Codigo}|{(int)p.Acciones}")
                .OrderBy(x => x).ToList();
            Assert.Equal(esperados, reales);
        }

        await GuardarAsync(RolesSistema.Gerencia, dto => Con(dto, Opciones.DashboardReportes, AccionPermiso.Ninguna));
        using (var ctx = _api.NuevoContexto())
        {
            await SemillaPermisos.SembrarAsync(ctx);   // como al volver a arrancar la API
            var rolId = ctx.Roles.Single(r => r.Name == RolesSistema.Gerencia).Id;
            var fila = await ctx.PermisosRol.Include(p => p.Opcion).SingleAsync(p => p.RoleId == rolId && p.Opcion!.Codigo == Opciones.DashboardReportes);
            Assert.Equal(AccionPermiso.Ninguna, fila.Acciones);
        }
    }
}

/// <summary>La API con la base de prueba, sin jobs y con autenticación por encabezados.</summary>
public sealed class ApiDePrueba : WebApplicationFactory<Program>
{
    public const string Esquema = "Prueba";
    private readonly BaseDePrueba _base;

    public ApiDePrueba(BaseDePrueba baseDePrueba) => _base = baseDePrueba;

    public HttpClient Cliente(string usuarioId, params string[] roles)
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Add(AutenticacionDePrueba.EncabezadoUsuario, usuarioId);
        cliente.DefaultRequestHeaders.Add(AutenticacionDePrueba.EncabezadoRoles, string.Join(',', roles));
        return cliente;
    }

    public MantenimientoDbContext NuevoContexto()
    {
        var opciones = new DbContextOptionsBuilder<MantenimientoDbContext>();
        _base.ConfigurarOpciones(opciones);
        return new MantenimientoDbContext(opciones.Options);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(l => l.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<MantenimientoDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<MantenimientoDbContext>>();
            services.AddDbContext<MantenimientoDbContext>(o => _base.ConfigurarOpciones(o));
            services.RemoveAll<IHostedService>();
            services.AddAuthentication(o =>
                {
                    o.DefaultAuthenticateScheme = Esquema;
                    o.DefaultChallengeScheme = Esquema;
                    o.DefaultScheme = Esquema;
                })
                .AddScheme<AuthenticationSchemeOptions, AutenticacionDePrueba>(Esquema, _ => { });
        });
    }
}

public sealed class AutenticacionDePrueba(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string EncabezadoUsuario = "X-Prueba-Usuario";
    public const string EncabezadoRoles = "X-Prueba-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(EncabezadoUsuario, out var usuario) || string.IsNullOrEmpty(usuario))
            return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, usuario!), new(ClaimTypes.Name, usuario!) };
        claims.AddRange(Request.Headers[EncabezadoRoles].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => new Claim(ClaimTypes.Role, r)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, ApiDePrueba.Esquema));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, ApiDePrueba.Esquema)));
    }
}
