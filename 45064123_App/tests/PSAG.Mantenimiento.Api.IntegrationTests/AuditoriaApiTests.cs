using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Api.Controllers;
using PSAG.Mantenimiento.Application.DTOs.Auth;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Identity;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Bugs de la auditoría de la API (fix/api-auditoria): editar una pendiente de declaración (I), quitar una línea
/// o eliminar un usuario que es maquinista de una detención (B), producción por cámara y por turno (D), aviso de
/// fin de turno (N), tipo/detalle de falla al declarar (P) y restricción por línea de la pantalla de declarar (Q).
/// </summary>
public class AuditoriaApiTests : CalidadTestBase
{
    private readonly UnitOfWork _unitOfWork;
    private readonly DetencionService _detenciones;
    private readonly DeclaracionDetencionService _declaracion;
    private readonly DateTimeOffset _parada;   // 10:00 de planta hace 2 días (turno mañana)
    private UsuarioLinea _operarioTango = null!;

    public AuditoriaApiTests()
    {
        _unitOfWork = new UnitOfWork(Context);
        _detenciones = new DetencionService(_unitOfWork, NullLogger<DetencionService>.Instance);
        _declaracion = new DeclaracionDetencionService(_unitOfWork, _detenciones, ZonaPlanta, NullLogger<DeclaracionDetencionService>.Instance);
        var hoy = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaPlanta).Date;
        _parada = new DateTimeOffset(hoy.AddDays(-2).AddHours(10), TimeSpan.FromHours(-3));

        _operarioTango = new UsuarioLinea { UsuarioId = Operario, LineaId = Tango2.LineaId, TurnoId = TurnoManana.TurnoId, FechaAsignacion = DateTimeOffset.UtcNow, AsignadoPorUsuarioId = Admin };
        Context.Add(_operarioTango);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    private int _secuencia;

    private async Task<Detencion> PendienteAsync(DateTimeOffset parada, int? usuarioLineaId = null, Turno? turno = null)
    {
        var d = new Detencion
        {
            Codigo = $"DET-20261110-{++_secuencia + 100:000000}",
            LineaId = Tango2.LineaId,
            TurnoId = (turno ?? TurnoManana).TurnoId,
            UsuarioLineaId = usuarioLineaId,
            Fecha = DateOnly.FromDateTime(parada.DateTime),
            FechaHoraParada = parada,
            OrigenTiempo = OrigenTiempo.DECLARADO,
            OrigenRegistro = OrigenRegistroDetencion.CAMARA,
            Descripcion = string.Empty,
            EstadoActual = EstadoDetencion.PENDIENTE_JUSTIFICACION,
            CreadoPorUsuarioId = DetencionCamaraReglas.UsuarioSistemaCamara,
            ModificadoPorUsuarioId = DetencionCamaraReglas.UsuarioSistemaCamara
        };
        Context.Add(d);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
        return d;
    }

    private static T ConUsuario<T>(T controller, string usuarioId, params string[] roles) where T : ControllerBase
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, usuarioId) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
        return controller;
    }

    private DetencionesController Detenciones(string usuarioId, params string[] roles) =>
        ConUsuario(new DetencionesController(_unitOfWork, _detenciones, null!, null!, NullLogger<DetencionesController>.Instance, null!, ZonaPlanta),
            usuarioId, roles);

    private DeclaracionDetencionesController Declaracion(string usuarioId, params string[] roles) =>
        ConUsuario(new DeclaracionDetencionesController(_declaracion, _unitOfWork, null!), usuarioId, roles);

    // ===================== I: editar una pendiente de declaración =====================

    [Fact]
    public async Task Editar_PendienteDeDeclaracion_SeRechazaYPideDeclararla()
    {
        var d = await PendienteAsync(_parada);

        var r = await Detenciones(Admin, RolesSistema.Administrador).UpdateDetencion(d.DetencionId, new ActualizarDetencionRequest
        {
            Descripcion = "Editada sin declarar", MinutosDeclarados = 5, OrigenTiempo = "DECLARADO", RowVersion = d.RowVersion
        });

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Contains("Declarala desde Mis pendientes", Assert.IsType<ProblemDetails>(bad.Value).Detail);
        var sinCambios = await Context.Detenciones.SingleAsync(x => x.DetencionId == d.DetencionId);
        Assert.Equal(EstadoDetencion.PENDIENTE_JUSTIFICACION, sinCambios.EstadoActual);
        Assert.Equal(string.Empty, sinCambios.Descripcion);
    }

    [Fact]
    public async Task Editar_DetencionRegistrada_SigueFuncionando()
    {
        var d = await Context.Detenciones.AsNoTracking().SingleAsync(x => x.DetencionId == DetencionTango.DetencionId);

        var r = await Detenciones(Admin, RolesSistema.Administrador).UpdateDetencion(d.DetencionId, new ActualizarDetencionRequest
        {
            Descripcion = "Corregida", RowVersion = d.RowVersion
        });

        Assert.IsType<OkObjectResult>(r);
        Context.ChangeTracker.Clear();
        Assert.Equal("Corregida", (await Context.Detenciones.SingleAsync(x => x.DetencionId == d.DetencionId)).Descripcion);
    }

    // ===================== Q: pantalla de declarar restringida por línea =====================

    [Fact]
    public async Task PantallaDeDeclarar_OperarioDeOtraLinea_NoLaVe()
    {
        var d = await PendienteAsync(_parada);

        var ajeno = await Declaracion("oper-futura", RolesSistema.Operario).GetDeclaracion(d.DetencionId);
        var propio = await Declaracion(Operario, RolesSistema.Operario).GetDeclaracion(d.DetencionId);
        var supervisor = await Declaracion(Supervisor, RolesSistema.Supervisor).GetDeclaracion(d.DetencionId);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(ajeno).StatusCode);
        Assert.IsType<OkObjectResult>(propio);
        Assert.IsType<OkObjectResult>(supervisor);   // misma regla que declarar: sólo el operario está atado a sus líneas
    }

    // ===================== P: tipo y detalle de falla al declarar =====================

    [Fact]
    public async Task Declarar_ConDetalleQueNoEsDelEquipoOTipo_SeRechaza()
    {
        var rebobinadora = new Equipo { LineaId = Tango2.LineaId, Codigo = "REB", Nombre = "Rebobinadora", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        var empaquetadora = new Equipo { LineaId = Tango2.LineaId, Codigo = "EMP", Nombre = "Empaquetadora", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        var mecanica = new TipoFalla { Codigo = "MEC", Nombre = "Mecánica", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        var electrica = new TipoFalla { Codigo = "ELE", Nombre = "Eléctrica", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        Context.AddRange(rebobinadora, empaquetadora, mecanica, electrica);
        await Context.SaveChangesAsync();
        var rodillo = new DetalleFalla { Codigo = "ROD", Nombre = "Rodillo", EquipoId = rebobinadora.EquipoId, TipoFallaId = mecanica.TipoFallaId, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        Context.Add(rodillo);
        await Context.SaveChangesAsync();
        var d = await PendienteAsync(_parada);

        Task<Application.Common.Result<DeclararDetencionResponse>> Declarar(int? equipo, int? tipo, int? detalle) =>
            _declaracion.DeclararAsync(d.DetencionId, new DeclararDetencionRequest
            {
                EquipoId = equipo, TipoFallaId = tipo, DetalleFallaId = detalle,
                Descripcion = "Se trabó", MinutosDeclarados = 8, RowVersion = d.RowVersion
            }, Operario, _operarioTango);

        var otroEquipo = await Declarar(empaquetadora.EquipoId, mecanica.TipoFallaId, rodillo.DetalleFallaId);
        var otroTipo = await Declarar(rebobinadora.EquipoId, electrica.TipoFallaId, rodillo.DetalleFallaId);
        var sinTipo = await Declarar(rebobinadora.EquipoId, null, rodillo.DetalleFallaId);
        var tipoInexistente = await Declarar(rebobinadora.EquipoId, 9999, null);

        Assert.All([otroEquipo, otroTipo, sinTipo, tipoInexistente], r => Assert.Equal("VALIDATION", r.ErrorCode));
        Assert.Contains("detalle de falla no corresponde", otroEquipo.ErrorMessage);
        Context.ChangeTracker.Clear();
        Assert.Equal(EstadoDetencion.PENDIENTE_JUSTIFICACION, (await Context.Detenciones.SingleAsync(x => x.DetencionId == d.DetencionId)).EstadoActual);

        var ok = await Declarar(rebobinadora.EquipoId, mecanica.TipoFallaId, rodillo.DetalleFallaId);
        Assert.True(ok.IsSuccess, ok.ErrorMessage);
    }

    // ===================== N: aviso de fin de turno =====================

    [Fact]
    public async Task AvisoFinDeTurno_SeparaLasDeEsteTurnoDeLasAnteriores_YDaElTurnoConSuFecha()
    {
        var ayer = _parada.AddDays(1);                                  // hace 1 día, 10:00
        await PendienteAsync(_parada);                                  // turno mañana de hace 2 días
        await PendienteAsync(ayer.AddHours(-1));                        // 09:00 del turno mañana en curso
        DateTimeOffset ahora = new DateTimeOffset(ayer.Date.AddHours(13).AddMinutes(40), ayer.Offset);  // 20 min para las 14
        var servicio = new DeclaracionDetencionService(_unitOfWork, _detenciones, ZonaPlanta, NullLogger<DeclaracionDetencionService>.Instance, () => ahora);

        var aviso = await servicio.ObtenerAvisoFinTurnoAsync([Tango2.LineaId], 30);

        Assert.True(aviso.Mostrar);
        Assert.Equal(2, aviso.Pendientes);
        Assert.Equal(1, aviso.PendientesTurnoActual);
        Assert.Equal(TurnoManana.TurnoId, aviso.TurnoId);
        Assert.Equal(DateOnly.FromDateTime(ayer.DateTime), aviso.FechaInicioTurno);
    }

    [Fact]
    public async Task AvisoFinDeTurno_GerenciaNoLoRecibe()
    {
        await PendienteAsync(_parada);

        var gerencia = await Declaracion("ger-1", RolesSistema.Gerencia).GetAvisoFinTurno(120);
        var admin = await Declaracion(Admin, RolesSistema.Administrador).GetAvisoFinTurno(120);

        var avisoGerencia = Assert.IsType<AvisoFinTurnoDto>(Assert.IsType<OkObjectResult>(gerencia).Value);
        Assert.False(avisoGerencia.Mostrar);
        Assert.Equal(0, avisoGerencia.Pendientes);
        Assert.Equal(1, Assert.IsType<AvisoFinTurnoDto>(Assert.IsType<OkObjectResult>(admin).Value).Pendientes);
    }

    // ===================== D: producción por cámara y con las ventanas de turno de la eficiencia =====================

    [Fact]
    public async Task Produccion_TodasLasCamaras_NoSumaRollosConMangas_YLaNocheEsDelDiaEnQueEmpieza()
    {
        var noche = new Turno
        {
            Codigo = "T3", Nombre = "Turno 3 (Noche)", HoraInicio = new TimeOnly(22, 0), HoraFin = new TimeOnly(6, 0), Orden = 3,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        Context.Add(noche);
        await Context.SaveChangesAsync();
        var conteo = new ConteoCamaraService(_unitOfWork, ZonaPlanta, NullLogger<ConteoCamaraService>.Instance);
        var dia = _parada.AddDays(-1);   // 10:00 de hace 3 días
        MinutoCamaraIngesta M(DateTimeOffset m, decimal rollos) => new() { Minuto = m, Carril = 1, Rollos = rollos, SegundosMarcha = 60, Estado = "MARCHA" };

        await conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-T2",
            Minutos =
            [
                M(new DateTimeOffset(dia.Date.AddHours(2), dia.Offset), 5),                           // 02:00: noche del día anterior
                M(dia, 22),                                                                          // 10:00: mañana
                M(new DateTimeOffset(dia.Date.AddHours(23), dia.Offset), 7),                          // 23:00: noche del día
                M(new DateTimeOffset(dia.Date.AddDays(1).AddHours(3), dia.Offset), 11)                // 03:00 del siguiente: noche del día
            ]
        });
        await conteo.IngestarAsync(new IngestaConteoCamaraRequest { PuntoControl = "TEST-CAM-F2", Minutos = [M(dia, 166)] });

        var r = await conteo.ObtenerProduccionAsync(DateOnly.FromDateTime(dia.DateTime), null);

        Assert.True(r.IsSuccess);
        Assert.Equal(new DateTimeOffset(dia.Date.AddHours(6), dia.Offset), r.Value!.Inicio);
        Assert.Equal(new DateTimeOffset(dia.Date.AddDays(1).AddHours(6), dia.Offset), r.Value.Fin);
        Assert.Equal(2, r.Value.Camaras.Count);
        var tango = r.Value.Camaras.Single(c => c.Codigo == "TEST-CAM-T2");
        var futura = r.Value.Camaras.Single(c => c.Codigo == "TEST-CAM-F2");
        Assert.Equal(40, tango.Total);                     // 22 + 7 + 11: el 02:00 es de la noche anterior
        Assert.Equal(166, futura.Total);                   // no se suma con la otra cámara
        Assert.Equal(22, tango.Turnos.Single(t => t.TurnoId == TurnoManana.TurnoId).Total);
        Assert.Equal(18, tango.Turnos.Single(t => t.TurnoId == noche.TurnoId).Total);
        Assert.DoesNotContain(tango.Turnos, t => t.TurnoId == null);
        Assert.Equal(22, tango.Horas.Single(h => h.Hora == dia).RollosPorCarril[1]);   // el carril 1 de cada cámara, separado
        Assert.Equal(166, futura.Horas.Single(h => h.Hora == dia).RollosPorCarril[1]);
    }

    // ===================== B: quitar línea / eliminar usuario que es maquinista de una detención =====================

    private (UsuariosController Controller, UserManager<ApplicationUser> Usuarios) Usuarios()
    {
        var userManager = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser>(Context), null!, null!, null!, null!, null!, null!, null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);
        var roleManager = new RoleManager<IdentityRole>(
            new RoleStore<IdentityRole>(Context), null!, null!, null!, NullLogger<RoleManager<IdentityRole>>.Instance);
        var controller = ConUsuario(new UsuariosController(userManager, roleManager, _unitOfWork, NullLogger<UsuariosController>.Instance),
            Admin, RolesSistema.Administrador);
        return (controller, userManager);
    }

    private async Task<(UsuarioLinea Tango, UsuarioLinea Futura)> MaquinistaConDetencionAsync(string usuarioId)
    {
        Context.Users.Add(new ApplicationUser { Id = usuarioId, UserName = usuarioId, Email = $"{usuarioId}@psag.test", NombreCompleto = "Maquinista", Activo = true });
        var tango = new UsuarioLinea { UsuarioId = usuarioId, LineaId = Tango2.LineaId, TurnoId = TurnoManana.TurnoId, FechaAsignacion = DateTimeOffset.UtcNow, AsignadoPorUsuarioId = Admin };
        var futura = new UsuarioLinea { UsuarioId = usuarioId, LineaId = Futura2.LineaId, FechaAsignacion = DateTimeOffset.UtcNow, AsignadoPorUsuarioId = Admin };
        Context.AddRange(tango, futura);
        await Context.SaveChangesAsync();
        // La creó la cámara (CreadoPor = sistema) con este maquinista: es la que hacía saltar la FK.
        await PendienteAsync(_parada, tango.UsuarioLineaId);
        return (tango, futura);
    }

    [Fact]
    public async Task QuitarLaLineaDeUnaDetencion_DevuelveMensajeClaroYNoBorraNada()
    {
        var (tango, futura) = await MaquinistaConDetencionAsync("maq-1");
        var (controller, userManager) = Usuarios();

        var r = await controller.UpdateUsuario("maq-1", new ActualizarUsuarioRequest
        {
            Email = "maq-1@psag.test", NombreCompleto = "Otro nombre", Activo = true, LineasAsignadas = [Futura2.LineaId]
        });

        var bad = Assert.IsType<BadRequestObjectResult>(r);
        Assert.Contains("Tango 2", Assert.IsType<ProblemDetails>(bad.Value).Detail);
        Context.ChangeTracker.Clear();
        Assert.Equal(2, await Context.UsuariosLineas.CountAsync(ul => ul.UsuarioId == "maq-1"));
        Assert.Equal("Maquinista", (await Context.Users.SingleAsync(u => u.Id == "maq-1")).NombreCompleto);   // todo o nada
        userManager.Dispose();
    }

    [Fact]
    public async Task QuitarUnaLineaSinDetenciones_SeQuita()
    {
        await MaquinistaConDetencionAsync("maq-2");
        var (controller, userManager) = Usuarios();

        var r = await controller.UpdateUsuario("maq-2", new ActualizarUsuarioRequest
        {
            Email = "maq-2@psag.test", NombreCompleto = "Maquinista", Activo = true, LineasAsignadas = [Tango2.LineaId]
        });

        Assert.IsType<OkObjectResult>(r);
        Context.ChangeTracker.Clear();
        Assert.Equal(Tango2.LineaId, (await Context.UsuariosLineas.SingleAsync(ul => ul.UsuarioId == "maq-2")).LineaId);
        userManager.Dispose();
    }

    [Fact]
    public async Task EliminarUsuarioMaquinistaDeUnaDetencion_SeDesactivaEnVezDeDar500()
    {
        await MaquinistaConDetencionAsync("maq-3");
        var (controller, userManager) = Usuarios();

        var r = await controller.DeleteUsuario("maq-3");

        var respuesta = Assert.IsType<EliminarUsuarioResponse>(Assert.IsType<OkObjectResult>(r).Value);
        Assert.True(respuesta.Desactivado);
        Assert.False(respuesta.Eliminado);
        Context.ChangeTracker.Clear();
        Assert.False((await Context.Users.SingleAsync(u => u.Id == "maq-3")).Activo);
        Assert.Equal(2, await Context.UsuariosLineas.CountAsync(ul => ul.UsuarioId == "maq-3"));
        userManager.Dispose();
    }

    [Fact]
    public async Task AsignacionesEnUso_CuentaTambienLasDetencionesEliminadas()
    {
        var (tango, futura) = await MaquinistaConDetencionAsync("maq-4");
        var d = await Context.Detenciones.SingleAsync(x => x.UsuarioLineaId == tango.UsuarioLineaId);
        d.Eliminada = true;
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var enUso = await _unitOfWork.Detenciones.GetUsuarioLineaIdsEnUsoAsync([tango.UsuarioLineaId, futura.UsuarioLineaId]);

        Assert.Equal([tango.UsuarioLineaId], enUso);
    }
}
