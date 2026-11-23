using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Application.DTOs.Permisos;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-802 parte 2 · Obligado a turno, con la API entera (WebApplicationFactory) contra SQLite o SQL Server: sin turno el
/// operario (rol obligado) no puede usar nada de negocio (409), con turno todo se ajusta a su línea / turno / fecha, al
/// cerrar o ser relevado vuelve a quedar bloqueado, un operario no puede tener dos turnos abiertos, y el alta de usuario
/// pide líneas habilitadas para los roles obligados. Reloj real: la semilla de la API carga T1 05–13, T2 13–21 y T3 21–05,
/// así que siempre hay un turno en curso.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class TurnoObligadoApiTests : IDisposable
{
    private const string OperA = "oper-a";
    private const string OperB = "oper-b";

    private readonly BaseDePrueba _base;
    private readonly ApiDePrueba _api;
    private readonly int _linea1;
    private readonly int _linea2;

    public TurnoObligadoApiTests()
    {
        _base = BaseDePrueba.Crear();
        _api = new ApiDePrueba(_base);
        _ = _api.Server;   // semilla: roles, permisos, líneas L01–L06 y turnos

        using var ctx = _api.NuevoContexto();
        var lineas = ctx.Lineas.OrderBy(l => l.Orden).Select(l => l.LineaId).Take(2).ToList();
        _linea1 = lineas[0];
        _linea2 = lineas[1];
        var asignada = DateTimeOffset.UtcNow.AddDays(-1);
        ctx.OperadoresLinea.AddRange(
            new OperadorLinea { UsuarioId = OperA, LineaId = _linea1, FechaAsignacion = asignada, AsignadoPorUsuarioId = "admin" },
            new OperadorLinea { UsuarioId = OperA, LineaId = _linea2, FechaAsignacion = asignada, AsignadoPorUsuarioId = "admin" },
            new OperadorLinea { UsuarioId = OperB, LineaId = _linea1, FechaAsignacion = asignada, AsignadoPorUsuarioId = "admin" });
        ctx.SaveChanges();
    }

    public void Dispose()
    {
        _api.Dispose();
        _base.Dispose();
    }

    private HttpClient Operario(string id) => _api.Cliente(id, RolesSistema.Operario);

    private static DateOnly HoyPlanta => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(-3));

    private static async Task TurnoRequerido(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        var texto = await r.Content.ReadAsStringAsync();
        Assert.Contains("TURNO_REQUERIDO", texto);
        Assert.Contains("Abrí o tomá un turno para empezar", texto);
    }

    private static async Task FueraDelTurno(HttpResponseMessage r)
    {
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        Assert.Contains("Fuera de tu turno", await r.Content.ReadAsStringAsync());
    }

    private async Task<AperturaTurnoProduccionDto> AbrirAsync(string operario, int lineaId, bool relevar = false)
    {
        var r = await Operario(operario).PostAsJsonAsync("api/turnos-produccion/abrir", new { lineaId, relevar });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var dto = (await r.Content.ReadFromJsonAsync<AperturaTurnoProduccionDto>())!;
        Assert.False(dto.RequiereConfirmarRelevo);
        return dto;
    }

    private static object Detencion(int lineaId) => new
    {
        lineaId,
        fecha = HoyPlanta.ToString("yyyy-MM-dd"),
        origenTiempo = "DECLARADO",
        minutosDeclarados = 5,
        descripcion = "Prueba T-802"
    };

    [Fact]
    public async Task SinTurno_ElObligadoNoPuedeUsarNadaDeNegocio_SalvoInicioDeTurno()
    {
        var op = Operario(OperA);
        await TurnoRequerido(await op.GetAsync("api/detenciones"));
        await TurnoRequerido(await op.PostAsJsonAsync("api/detenciones", Detencion(_linea1)));
        await TurnoRequerido(await op.GetAsync($"api/indicadores/produccion?desde={HoyPlanta:yyyy-MM-dd}&hasta={HoyPlanta:yyyy-MM-dd}"));
        await TurnoRequerido(await op.GetAsync("api/analisis/pendientes"));
        await TurnoRequerido(await op.GetAsync("api/dashboard/resumen"));

        // Permitidos: menú, inicio de turno y las listas que usa esa pantalla
        var mios = await op.GetFromJsonAsync<PermisosMiosDto>("api/permisos/mios");
        Assert.True(mios!.ObligadoATurno);
        var propuesta = await op.GetFromJsonAsync<PropuestaTurnoProduccionDto>("api/turnos-produccion/propuesta");
        Assert.True(propuesta!.ObligadoATurno);
        Assert.True(propuesta.Preguntar);
        Assert.Equal(2, propuesta.Lineas.Count);
        Assert.Equal(HttpStatusCode.NoContent, (await op.GetAsync("api/turnos-produccion/mio")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("api/catalogos/lineas")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await op.GetAsync("api/catalogos/maquinistas/mis-asignaciones")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await op.GetAsync($"api/turnos-produccion/actual?lineaId={_linea1}")).StatusCode);
    }

    [Fact]
    public async Task RolNoObligado_NoQuedaBloqueado()
    {
        var sup = _api.Cliente("sup-1", RolesSistema.Supervisor);
        Assert.Equal(HttpStatusCode.OK, (await sup.GetAsync("api/detenciones")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await sup.GetAsync("api/analisis/pendientes")).StatusCode);
        var mios = await sup.GetFromJsonAsync<PermisosMiosDto>("api/permisos/mios");
        Assert.False(mios!.ObligadoATurno);
    }

    [Fact]
    public async Task ConTurno_TodoSeAjustaAlTurno_YAlCerrarVuelveABloquearse()
    {
        var apertura = await AbrirAsync(OperA, _linea1);
        var turno = apertura.Turno!;
        var op = Operario(OperA);

        // Detenciones de otros turnos (otra línea y otro día) que no tiene que ver
        long otraLinea, otroDia;
        using (var ctx = _api.NuevoContexto())
        {
            var turnoId = ctx.TurnosProduccion.Single(t => t.TurnoProduccionId == turno.TurnoProduccionId).TurnoId;
            otraLinea = AgregarDetencion(ctx, _linea2, ctx.TurnoProduccionId(_linea2, turnoId, turno.FechaProduccion), "DET-OTRA-LINEA");
            otroDia = AgregarDetencion(ctx, _linea1, ctx.TurnoProduccionId(_linea1, turnoId, turno.FechaProduccion.AddDays(-1)), "DET-OTRO-DIA");
        }

        // Alta en otra línea: rechazada; en la suya (sin horas = ahora): ok
        await FueraDelTurno(await op.PostAsJsonAsync("api/detenciones", Detencion(_linea2)));
        var alta = await op.PostAsJsonAsync("api/detenciones", Detencion(_linea1));
        Assert.True(alta.StatusCode == HttpStatusCode.Created, await alta.Content.ReadAsStringAsync());

        // Listado: aunque pida otra línea o no filtre, ve sólo lo de su turno
        foreach (var url in new[] { "api/detenciones", $"api/detenciones?lineaId={_linea2}", "api/detenciones?fechaDesde=2020-01-01" })
        {
            using var json = JsonDocument.Parse(await op.GetStringAsync(url));
            var items = json.RootElement.GetProperty("items").EnumerateArray().ToList();
            Assert.Single(items);
            Assert.Equal(turno.TurnoProduccionId, items[0].GetProperty("turnoProduccionId").GetInt64());
        }

        // Por id: lo de otro turno, no
        await FueraDelTurno(await op.GetAsync($"api/detenciones/{otraLinea}"));
        await FueraDelTurno(await op.GetAsync($"api/detenciones/{otroDia}"));
        await FueraDelTurno(await op.GetAsync($"api/analisis/{otroDia}"));

        // Combos: sólo la línea del turno
        using (var json = JsonDocument.Parse(await op.GetStringAsync("api/catalogos/lineas")))
            Assert.Equal([_linea1], json.RootElement.EnumerateArray().Select(l => l.GetProperty("lineaId").GetInt32()).ToArray());

        // El turno propio
        var mio = await op.GetFromJsonAsync<TurnoProduccionDto>("api/turnos-produccion/mio");
        Assert.Equal(turno.TurnoProduccionId, mio!.TurnoProduccionId);

        // Cerrar: vuelve a quedar bloqueado
        var cierre = await op.PostAsJsonAsync($"api/turnos-produccion/{turno.TurnoProduccionId}/cerrar", new { observacion = "fin" });
        Assert.True(cierre.StatusCode == HttpStatusCode.OK, await cierre.Content.ReadAsStringAsync());
        await TurnoRequerido(await op.GetAsync("api/detenciones"));
        Assert.Equal(HttpStatusCode.NoContent, (await op.GetAsync("api/turnos-produccion/mio")).StatusCode);
    }

    [Fact]
    public async Task UnOperarioNoTieneDosTurnosAbiertos_YElRelevoNoSuperpone()
    {
        await AbrirAsync(OperA, _linea1);

        // Con el turno de la línea 1 abierto, no puede abrir el de la línea 2
        var otro = await Operario(OperA).PostAsJsonAsync("api/turnos-produccion/abrir", new { lineaId = _linea2 });
        Assert.Equal(HttpStatusCode.Conflict, otro.StatusCode);
        Assert.Contains("Ya tenés abierto", await otro.Content.ReadAsStringAsync());
        var propuesta = await Operario(OperA).GetFromJsonAsync<PropuestaTurnoProduccionDto>("api/turnos-produccion/propuesta");
        Assert.False(propuesta!.Lineas.Single(l => l.LineaId == _linea2).PuedeTomar);

        // B lo releva: A queda sin turno (bloqueado) y un solo operador vigente, sin superposición
        var relevo = await AbrirAsync(OperB, _linea1, relevar: true);
        Assert.Equal(OperA, relevo.ReemplazadoUsuarioId);
        await TurnoRequerido(await Operario(OperA).GetAsync("api/detenciones"));
        Assert.Equal(HttpStatusCode.OK, (await Operario(OperB).GetAsync("api/detenciones")).StatusCode);
        using (var ctx = _api.NuevoContexto())
        {
            var tramos = await ctx.Set<TurnoProduccionOperador>()
                .Where(o => o.TurnoProduccionId == relevo.Turno!.TurnoProduccionId && o.AnuladoPorId == null)
                .OrderBy(o => o.TurnoProduccionOperadorId).ToListAsync();
            Assert.Equal(2, tramos.Count);
            Assert.Single(tramos, t => t.Hasta == null);
            Assert.Equal(tramos[1].Desde, tramos[0].Hasta);
        }

        // Relevado en la línea 1, ya puede abrir la línea 2
        await AbrirAsync(OperA, _linea2);

        // B no está habilitado en la línea 2: no la puede abrir
        var b2 = await Operario(OperB).PostAsJsonAsync("api/turnos-produccion/abrir", new { lineaId = _linea2 });
        Assert.Equal(HttpStatusCode.Forbidden, b2.StatusCode);
    }

    [Fact]
    public async Task AltaDeUsuario_RolObligadoPideLineas_LasCreaComoHabilitacion_YElNoObligadoNo()
    {
        var admin = _api.Cliente("usr-admin", RolesSistema.Administrador);

        var sinLineas = await admin.PostAsJsonAsync("api/usuarios", Usuario("op.nuevo", RolesSistema.Operario, []));
        Assert.Equal(HttpStatusCode.BadRequest, sinLineas.StatusCode);
        Assert.Contains("obligado a turno", await sinLineas.Content.ReadAsStringAsync());

        var inexistente = await admin.PostAsJsonAsync("api/usuarios", Usuario("op.nuevo", RolesSistema.Operario, [999_999]));
        Assert.Equal(HttpStatusCode.BadRequest, inexistente.StatusCode);

        var conLineas = await admin.PostAsJsonAsync("api/usuarios", Usuario("op.nuevo", RolesSistema.Operario, [_linea1, _linea2, _linea1]));
        Assert.True(conLineas.StatusCode == HttpStatusCode.Created, await conLineas.Content.ReadAsStringAsync());
        using (var ctx = _api.NuevoContexto())
        {
            var id = ctx.Users.Single(u => u.UserName == "op.nuevo").Id;
            Assert.Equal(new[] { _linea1, _linea2 }.OrderBy(x => x), ctx.OperadoresLinea.Where(o => o.UsuarioId == id).Select(o => o.LineaId).OrderBy(x => x).ToList());
        }

        var supervisor = await admin.PostAsJsonAsync("api/usuarios", Usuario("sup.nuevo", RolesSistema.Supervisor, []));
        Assert.True(supervisor.StatusCode == HttpStatusCode.Created, await supervisor.Content.ReadAsStringAsync());

        var obligados = await admin.GetFromJsonAsync<List<string>>("api/usuarios/roles-obligados-turno");
        Assert.Equal([RolesSistema.Operario], obligados);
    }

    private static object Usuario(string nombre, string rol, int[] lineas) => new
    {
        userName = nombre,
        email = nombre + "@psag.test",
        nombreCompleto = nombre,
        password = "Clave123!",
        roles = new[] { rol },
        lineasAsignadas = lineas
    };

    private static long AgregarDetencion(Infrastructure.Data.MantenimientoDbContext ctx, int lineaId, long turnoProduccionId, string codigo)
    {
        var d = new Detencion
        {
            Codigo = codigo,
            LineaId = lineaId,
            TurnoProduccionId = turnoProduccionId,
            MinutosDeclarados = 5,
            MinutosEfectivos = 5,
            OrigenTiempo = OrigenTiempo.DECLARADO,
            Descripcion = "Otro turno",
            EstadoActual = EstadoDetencion.REGISTRADA,
            CreadoPorUsuarioId = "admin",
            ModificadoPorUsuarioId = "admin"
        };
        ctx.Detenciones.Add(d);
        ctx.SaveChanges();
        return d.DetencionId;
    }
}
