using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Api.Controllers;
using PSAG.Mantenimiento.Application.DTOs.Calidad;
using PSAG.Mantenimiento.Application.DTOs.Camaras;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-798 etapa 4: maestro único de cámaras. Ingesta con formato viejo y nuevo, cámara no habilitada rechazada,
/// minuto colgado del turno de producción, configuración para el servicio (sólo habilitadas), producción oficial
/// única por línea y magnitud y alerta SIN_DATOS (sin datos no es producción cero).
/// </summary>
public class CamarasUnificadasTests : CalidadTestBase
{
    private readonly ConteoCamaraService _conteo;
    private readonly DateTimeOffset _dia;      // 10:00 de planta hace 2 días (turno mañana)
    private readonly DateTimeOffset _ayer2230; // ayer 22:30: ningún turno de planta de la base lo cubre (06-14, 14-22)

    public CamarasUnificadasTests()
    {
        var unitOfWork = new UnitOfWork(Context);
        _conteo = new ConteoCamaraService(unitOfWork, NuevoTurnosProduccion(unitOfWork), ZonaPlanta, NullLogger<ConteoCamaraService>.Instance);
        var hoy = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaPlanta).Date;
        _dia = new DateTimeOffset(hoy.AddDays(-2).AddHours(10), TimeSpan.FromHours(-3));
        _ayer2230 = new DateTimeOffset(hoy.AddDays(-1).AddHours(22).AddMinutes(30), TimeSpan.FromHours(-3));
    }

    private static MinutoCamaraIngesta Minuto(DateTimeOffset m, decimal? cantidad = 10m, decimal? rollos = null) => new()
    {
        Minuto = m, Carril = 1, Cantidad = cantidad, Rollos = rollos, SegundosMarcha = 60, Estado = "MARCHA"
    };

    private async Task<Camara> CamaraAsync(int id)
    {
        Context.ChangeTracker.Clear();
        return await Context.Camaras.AsNoTracking().SingleAsync(c => c.CamaraId == id);
    }

    private async Task ModificarAsync(int camaraId, Action<Camara> cambio)
    {
        var c = await Context.Camaras.SingleAsync(x => x.CamaraId == camaraId);
        cambio(c);
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();
    }

    // ===================== Ingesta =====================

    [Fact]
    public async Task Ingesta_FormatoNuevoYViejo_GuardanLaCantidad()
    {
        var nuevo = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "test-cam-t2", Minutos = [Minuto(_dia, cantidad: 12.34m)] });
        var viejo = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { PuntoControl = "TEST-CAM-T2", Minutos = [Minuto(_dia.AddMinutes(1), cantidad: null, rollos: 7m)] });

        Assert.True(nuevo.IsSuccess, nuevo.ErrorMessage);
        Assert.True(viejo.IsSuccess, viejo.ErrorMessage);
        var minutos = await Context.ConteosCamaraMinuto.AsNoTracking().OrderBy(m => m.ConteoCamaraMinutoId).ToListAsync();
        Assert.Equal([12.3m, 7m], minutos.Select(m => m.Cantidad));
        Assert.All(minutos, m => Assert.Equal(CamTango.CamaraId, m.CamaraId));
        Assert.NotNull((await CamaraAsync(CamTango.CamaraId)).UltimoDatoRecibido);
    }

    [Fact]
    public async Task Ingesta_SinCantidad_SeRechaza()
    {
        var r = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Minutos = [Minuto(_dia, cantidad: null)] });
        Assert.True(r.IsFailure);
        Assert.Contains("cantidad", r.ErrorMessage);
    }

    [Fact]
    public async Task Ingesta_CamaraNoHabilitadaInactivaODeCalidad_SeRechaza()
    {
        await ModificarAsync(CamTango.CamaraId, c => c.Habilitada = false);
        var deshabilitada = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Minutos = [Minuto(_dia)] });
        Assert.True(deshabilitada.IsFailure);
        Assert.Contains("no está habilitada", deshabilitada.ErrorMessage);
        Assert.Equal(0, await Context.ConteosCamaraMinuto.CountAsync());
        Assert.Null((await CamaraAsync(CamTango.CamaraId)).UltimoDatoRecibido);

        await ModificarAsync(CamFutura.CamaraId, c => c.Activo = false);
        var inactiva = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-F2", Minutos = [Minuto(_dia)] });
        Assert.True(inactiva.IsFailure);
        Assert.Contains("Maestros → Cámaras", inactiva.ErrorMessage);

        Context.Add(new Camara
        {
            LineaId = Tango2.LineaId, Codigo = "TEST-CAM-CAL", Nombre = "Calidad", Funcion = FuncionCamara.Calidad, Habilitada = true,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        });
        await Context.SaveChangesAsync();
        var calidad = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-CAL", Minutos = [Minuto(_dia)] });
        Assert.True(calidad.IsFailure);
    }

    [Fact]
    public async Task Ingesta_MinutoColgadoDelTurno_SinTurnoLoAbreElSistema()
    {
        var r = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Minutos = [Minuto(_dia), Minuto(_dia.AddMinutes(1))] });

        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(0, r.Value!.MinutosSinTurno);
        var fecha = DateOnly.FromDateTime(_dia.DateTime);
        var turno = await Context.TurnosProduccion.AsNoTracking().SingleAsync(t => t.FechaProduccion == fecha);
        Assert.Equal(OrigenAperturaTurno.SISTEMA, turno.OrigenApertura);
        Assert.Equal((Tango2.LineaId, TurnoManana.TurnoId), (turno.LineaId, turno.TurnoId));
        Assert.All(await Context.ConteosCamaraMinuto.AsNoTracking().ToListAsync(), m => Assert.Equal(turno.TurnoProduccionId, m.TurnoProduccionId));

        // Un minuto que ningún turno de planta cubre se guarda igual, sin turno.
        var fuera = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Minutos = [Minuto(_ayer2230)] });
        Assert.Equal(1, fuera.Value!.MinutosSinTurno);
        Assert.Null((await Context.ConteosCamaraMinuto.AsNoTracking().SingleAsync(m => m.Minuto == _ayer2230)).TurnoProduccionId);
    }

    [Fact]
    public async Task Ingesta_ConTurnoYaAbierto_UsaEseTurno()
    {
        var fecha = DateOnly.FromDateTime(_dia.DateTime);
        var existente = Context.TurnoProduccionId(Tango2.LineaId, TurnoManana.TurnoId, fecha);

        await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Minutos = [Minuto(_dia)] });

        Assert.Equal(existente, (await Context.ConteosCamaraMinuto.AsNoTracking().SingleAsync()).TurnoProduccionId);
        Assert.Equal(1, await Context.TurnosProduccion.CountAsync(t => t.FechaProduccion == fecha));
    }

    [Fact]
    public async Task AlertaSinConfiguracion_NoSeDuplica_YSeCierraAlLlegarMinutos()
    {
        AlertaCamaraIngesta Alerta(DateTimeOffset inicio) => new() { Tipo = "SIN_CONFIGURACION", Inicio = inicio, Detalle = "sin calibración" };

        var primera = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Alertas = [Alerta(_dia)] });
        var reinicio = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Alertas = [Alerta(_dia.AddMinutes(30))] });
        Assert.Equal(1, primera.Value!.AlertasNuevas);
        Assert.Equal(0, reinicio.Value!.AlertasNuevas);
        Assert.Null((await CamaraAsync(CamTango.CamaraId)).UltimoDatoRecibido);   // una alerta no es un dato

        var datos = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Minutos = [Minuto(_dia.AddHours(1))] });
        Assert.Equal(1, datos.Value!.AlertasCerradas);
        var alerta = await Context.AlertasCamara.AsNoTracking().SingleAsync();
        Assert.NotNull(alerta.FechaHoraFin);
        Assert.Equal(TipoAlertaCamara.SIN_CONFIGURACION, alerta.Tipo);
    }

    // ===================== Alerta SIN_DATOS (job) =====================

    private async Task AbrirTurnoAsync(DateTimeOffset inicioReal)
    {
        var fecha = DateOnly.FromDateTime(inicioReal.DateTime);
        Context.Add(new TurnoProduccion
        {
            LineaId = Tango2.LineaId, TurnoId = TurnoTarde.TurnoId, FechaProduccion = fecha,
            InicioProgramado = inicioReal, FinProgramado = inicioReal.AddHours(8), InicioReal = inicioReal,
            Estado = EstadoTurnoProduccion.ABIERTO, OrigenApertura = OrigenAperturaTurno.SUPERVISOR,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        });
        await ModificarAsync(CamTango.CamaraId, c => c.CuentaProduccionOficial = true);
    }

    [Fact]
    public async Task SinDatos_ConTurnoAbierto_AbreLaAlerta_UnMinutoLaCierra()
    {
        await AbrirTurnoAsync(_ayer2230);

        Assert.Equal(0, await _conteo.VigilarSinDatosAsync(_ayer2230.AddMinutes(5), 5));   // justo 5 min: todavía no
        Assert.Equal(1, await _conteo.VigilarSinDatosAsync(_ayer2230.AddMinutes(6), 5));
        Assert.Equal(0, await _conteo.VigilarSinDatosAsync(_ayer2230.AddMinutes(8), 5));   // ya está abierta: no duplica

        var alerta = await Context.AlertasCamara.AsNoTracking().SingleAsync();
        Assert.Equal(TipoAlertaCamara.SIN_DATOS, alerta.Tipo);
        Assert.Equal(CamTango.CamaraId, alerta.CamaraId);
        Assert.Null(alerta.FechaHoraFin);
        Assert.Equal(_ayer2230, alerta.FechaHoraInicio);

        // Llega un minuto (en 0: la máquina no produjo, pero la cámara manda datos): se cierra.
        var r = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Minutos = [Minuto(_ayer2230.AddMinutes(30), cantidad: 0)] });
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(1, r.Value!.AlertasCerradas);
        Assert.NotNull((await Context.AlertasCamara.AsNoTracking().SingleAsync()).FechaHoraFin);
        Assert.Equal(0, await _conteo.VigilarSinDatosAsync(_ayer2230.AddMinutes(33), 5));

        // Si vuelve a cortarse, se abre otra (no choca con la anterior).
        Assert.Equal(1, await _conteo.VigilarSinDatosAsync(_ayer2230.AddMinutes(40), 5));
        Assert.Equal(2, await Context.AlertasCamara.CountAsync());
    }

    [Fact]
    public async Task MinutoConCantidadCero_NoAbreLaAlerta()
    {
        await AbrirTurnoAsync(_ayer2230);
        await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Minutos = [Minuto(_ayer2230.AddMinutes(1), cantidad: 0)] });

        Assert.Equal(0, await _conteo.VigilarSinDatosAsync(_ayer2230.AddMinutes(6), 5));   // último minuto 22:31 (hasta 22:32)
        Assert.Empty(await Context.AlertasCamara.ToListAsync());
        Assert.Equal(1, await _conteo.VigilarSinDatosAsync(_ayer2230.AddMinutes(7).AddSeconds(1), 5));
    }

    [Fact]
    public async Task SinDatos_SoloLaCamaraOficialHabilitada_YConTurnoAbierto()
    {
        // Sin turno abierto: nada.
        await ModificarAsync(CamTango.CamaraId, c => c.CuentaProduccionOficial = true);
        Assert.Equal(0, await _conteo.VigilarSinDatosAsync(DateTimeOffset.UtcNow, 5));

        await AbrirTurnoAsync(_ayer2230);
        await ModificarAsync(CamTango.CamaraId, c => c.CuentaProduccionOficial = false);
        Assert.Equal(0, await _conteo.VigilarSinDatosAsync(_ayer2230.AddHours(1), 5));     // no es la oficial
        await ModificarAsync(CamTango.CamaraId, c => { c.CuentaProduccionOficial = true; c.Habilitada = false; });
        Assert.Equal(0, await _conteo.VigilarSinDatosAsync(_ayer2230.AddHours(1), 5));     // deshabilitada a propósito
    }

    // ===================== Maestro de cámaras y configuración =====================

    [Fact]
    public async Task Configuracion_SoloHabilitadasYActivas_ConElUmbralEfectivo()
    {
        await ModificarAsync(CamFutura.CamaraId, c => c.Habilitada = false);
        var nueva = NuevaCamara(Tango2.LineaId, "TEST-CAM-10", "Cámara 10", MagnitudProduccion.Mangas);
        nueva.NumeroCanalGrabador = 10;
        nueva.CreaDetenciones = true;
        nueva.UmbralDetencionSegundos = 45;
        var baja = NuevaCamara(Tango2.LineaId, "TEST-CAM-BAJA", "Dada de baja");
        baja.Activo = false;
        Context.AddRange(nueva, baja);
        await Context.SaveChangesAsync();

        var config = await Camaras.ObtenerConfiguracionAsync();

        Assert.Equal(["TEST-CAM-10", "TEST-CAM-T2"], config.Select(c => c.Codigo).Order());
        var c10 = config.Single(c => c.Codigo == "TEST-CAM-10");
        Assert.Equal(10, c10.Canal);
        Assert.Equal("MANGAS", c10.Magnitud);
        Assert.Equal("PASOS", c10.MetodoDeteccion);
        Assert.Equal(45, c10.UmbralDetencionSegundos);
        Assert.Equal(300, config.Single(c => c.Codigo == "TEST-CAM-T2").UmbralDetencionSegundos);   // vacío = 5 min
        Assert.Equal("Tango 2", c10.LineaNombre);
    }

    [Fact]
    public void Configuracion_UsaLaClaveDeIngesta_YElAbmNoTieneDelete()
    {
        var config = typeof(CamarasConfiguracionController);
        Assert.NotNull(config.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(config.GetCustomAttribute<ClaveIngestaCalidadAttribute>());
        Assert.Equal("api/camaras/configuracion", config.GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>(inherit: false)!.Template);
        Assert.NotNull(typeof(CamarasController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.DoesNotContain(typeof(CamarasController).GetMethods(), m => m.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>() != null);
    }

    [Fact]
    public async Task ProduccionOficial_UnaSolaPorLineaYMagnitud()
    {
        var oficialRollos = RequestDe(CamTango);
        oficialRollos.CuentaProduccionOficial = true;
        Assert.True((await Camaras.ActualizarAsync(CamTango.CamaraId, oficialRollos, Admin, [RolesSistema.Administrador])).IsSuccess);

        var otra = new GuardarCamaraRequest
        {
            Codigo = "TEST-CAM-09B", Nombre = "Otra de rollos", LineaId = Tango2.LineaId, Funcion = "CONTEO", Magnitud = "ROLLOS",
            MetodoDeteccion = "CARRILES", CantidadCarriles = 4, CuentaProduccionOficial = true, Habilitada = true
        };
        var repetida = await Camaras.CrearAsync(otra, Admin, [RolesSistema.Administrador]);
        Assert.True(repetida.IsFailure);
        Assert.Contains("TEST-CAM-T2", repetida.ErrorMessage);

        otra.Magnitud = "MANGAS";      // otra magnitud en la misma línea: sí
        otra.MetodoDeteccion = "PASOS";
        Assert.True((await Camaras.CrearAsync(otra, Admin, [RolesSistema.Administrador])).IsSuccess);

        var futura = RequestDe(CamFutura);   // misma magnitud en otra línea: sí
        futura.CuentaProduccionOficial = true;
        Assert.True((await Camaras.ActualizarAsync(CamFutura.CamaraId, futura, Admin, [RolesSistema.Administrador])).IsSuccess);

        // La base tampoco lo deja pasar (índice único filtrado).
        var directa = NuevaCamara(Tango2.LineaId, "TEST-CAM-DIRECTA", "Directa");
        directa.CuentaProduccionOficial = true;
        Context.Add(directa);
        await Assert.ThrowsAsync<DbUpdateException>(() => Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Abm_ValidaPorFuncion_YSoloAdministrador()
    {
        var sinMagnitud = new GuardarCamaraRequest { Codigo = "TEST-X", Nombre = "X", LineaId = Tango2.LineaId, Funcion = "CONTEO", MetodoDeteccion = "PASOS" };
        Assert.True((await Camaras.CrearAsync(sinMagnitud, Admin, [RolesSistema.Administrador])).IsFailure);
        Assert.Equal("FORBIDDEN", (await Camaras.CrearAsync(sinMagnitud, Supervisor, [RolesSistema.Supervisor])).ErrorCode);

        // Una de calidad: lo del conteo se limpia.
        var calidad = await Camaras.CrearAsync(new GuardarCamaraRequest
        {
            Codigo = "TEST-CAM-3", Nombre = "Cámara 3 embolsadora", LineaId = Tango2.LineaId, Funcion = "CALIDAD",
            Magnitud = "BOLSONES", CreaDetenciones = true, DetectaParadas = true, UmbralDetencionSegundos = 60, Habilitada = true
        }, Admin, [RolesSistema.Administrador]);
        Assert.True(calidad.IsSuccess, calidad.ErrorMessage);
        Assert.Null(calidad.Value!.Magnitud);
        Assert.False(calidad.Value.CreaDetenciones);

        var duplicada = await Camaras.CrearAsync(new GuardarCamaraRequest { Codigo = "test-cam-t2", Nombre = "Dup", LineaId = Tango2.LineaId, Funcion = "CALIDAD" },
            Admin, [RolesSistema.Administrador]);
        Assert.Contains("Ya hay una cámara", duplicada.ErrorMessage);
    }

    [Fact]
    public async Task Desactivar_EsBorradoLogico_YNoSiLaUsaUnPuntoActivo()
    {
        var bloqueada = await Camaras.DesactivarAsync(CamTango.CamaraId, Admin, [RolesSistema.Administrador]);
        Assert.True(bloqueada.IsFailure);   // CamaraTango (punto de calidad activo) la usa

        var punto = await Context.PuntosControlCalidad.SingleAsync(p => p.PuntoControlCalidadId == CamaraTango.PuntoControlCalidadId);
        punto.CamaraId = null;
        await Context.SaveChangesAsync();
        Context.ChangeTracker.Clear();

        var ok = await Camaras.DesactivarAsync(CamTango.CamaraId, Admin, [RolesSistema.Administrador]);
        Assert.True(ok.IsSuccess, ok.ErrorMessage);
        Assert.False(ok.Value!.Activo);
        Assert.False(ok.Value.Habilitada);
        Assert.NotNull(await Context.Camaras.AsNoTracking().SingleOrDefaultAsync(c => c.CamaraId == CamTango.CamaraId));   // no se borra
        Assert.DoesNotContain(await Camaras.ObtenerConfiguracionAsync(), c => c.Codigo == "TEST-CAM-T2");
    }

    [Fact]
    public async Task CambiarDeLinea_ConDatos_NoSePuede()
    {
        await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Minutos = [Minuto(_dia)] });
        var punto = await Context.PuntosControlCalidad.SingleAsync(p => p.PuntoControlCalidadId == CamaraTango.PuntoControlCalidadId);
        punto.CamaraId = null;
        await Context.SaveChangesAsync();

        var request = RequestDe(await CamaraAsync(CamTango.CamaraId));
        request.LineaId = Futura2.LineaId;
        var r = await Camaras.ActualizarAsync(CamTango.CamaraId, request, Admin, [RolesSistema.Administrador]);

        Assert.True(r.IsFailure);
        Assert.Contains("otra cámara", r.ErrorMessage);
    }

    [Fact]
    public async Task PuntoDeCalidad_EligeSuCamara_DeLaMismaLinea()
    {
        var request = new GuardarPuntoControlCalidadRequest { LineaId = Tango2.LineaId, Codigo = "TEST-PUNTO", Nombre = "Punto", CamaraId = CamFutura.CamaraId };
        var otraLinea = await Maestros.CrearPuntoControlAsync(request, Admin, [RolesSistema.Administrador]);
        Assert.True(otraLinea.IsFailure);

        request.CamaraId = CamTango.CamaraId;
        var ok = await Maestros.CrearPuntoControlAsync(request, Admin, [RolesSistema.Administrador]);
        Assert.True(ok.IsSuccess, ok.ErrorMessage);
        Assert.Equal("TEST-CAM-T2", ok.Value!.CamaraCodigo);

        request.CamaraId = null;   // opcional
        request.Codigo = "TEST-PUNTO-2";
        Assert.True((await Maestros.CrearPuntoControlAsync(request, Admin, [RolesSistema.Administrador])).IsSuccess);
    }

    [Fact]
    public async Task UltimoDato_EstadoEnElListado()
    {
        await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { Camara = "TEST-CAM-T2", Minutos = [Minuto(_dia)] });

        var lista = await Camaras.ObtenerAsync(null, incluirInactivas: false);

        Assert.Equal("VERDE", lista.Single(c => c.Codigo == "TEST-CAM-T2").EstadoUltimoDato);
        Assert.Equal("SIN_DATOS", lista.Single(c => c.Codigo == "TEST-CAM-F2").EstadoUltimoDato);
        Assert.Equal(TimeSpan.FromHours(-3), lista.Single(c => c.Codigo == "TEST-CAM-T2").UltimoDatoRecibido!.Value.Offset);
    }
}

/// <summary>El script 36 de SQL Server deja CAM_Camaras, los CAM_*, DET_Detenciones y CAL_PuntosControl como el modelo de EF.</summary>
public class CamarasScriptTests
{
    [Fact]
    public void Script36_TablaCheckFkEIndicesDelModelo_YRollback()
    {
        using var conexion = new SqliteConnection("DataSource=:memory:");
        conexion.Open();
        using var context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(conexion).Options);
        var script = ScriptSql.Texto("36_t798_camaras.sql");
        var rollback = ScriptSql.Texto("36_t798_camaras_rollback.sql");
        var modelo = context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;

        var camara = modelo.FindEntityType(typeof(Camara))!;
        Assert.Equal("CAM_Camaras", camara.GetTableName());
        foreach (var p in camara.GetProperties())
            Assert.Contains($"[{p.GetColumnName()}]", script);
        foreach (var check in camara.GetCheckConstraints())
            Assert.Contains(check.Name!, script);
        foreach (var indice in camara.GetIndexes())
            Assert.Contains(indice.GetDatabaseName()!, script);
        Assert.Contains("WHERE [CuentaProduccionOficial] = 1", script);

        foreach (var tipo in new[] { typeof(ConteoCamaraMinuto), typeof(ParadaCamara), typeof(AlertaCamara), typeof(Detencion), typeof(PuntoControlCalidad) })
        {
            var entidad = modelo.FindEntityType(tipo)!;
            var columnas = entidad.GetProperties().Select(p => p.GetColumnName()).ToList();
            Assert.DoesNotContain("PuntoControlCalidadId", tipo == typeof(PuntoControlCalidad) ? [] : columnas);
            foreach (var fk in entidad.GetForeignKeys().Where(f => f.Properties.Any(p => p.Name is "CamaraId" or "TurnoProduccionId")
                                                                    && (tipo != typeof(Detencion) || f.Properties.Any(p => p.Name == "CamaraId"))))
                Assert.Contains(fk.GetConstraintName()!, script);
            foreach (var indice in entidad.GetIndexes().Where(i => i.Properties.Any(p => p.Name is "CamaraId")))
                Assert.Contains(indice.GetDatabaseName()!, script);
        }

        var minuto = modelo.FindEntityType(typeof(ConteoCamaraMinuto))!;
        Assert.Contains("Cantidad", minuto.GetProperties().Select(p => p.GetColumnName()));
        Assert.DoesNotContain("LineaId", minuto.GetProperties().Select(p => p.GetColumnName()));
        Assert.Contains("CK_ConteosCamaraMinuto_Cantidad", script);
        Assert.Contains("'SIN_DATOS', 'SIN_CONFIGURACION'", script);
        Assert.Contains("N'mnt.CAM_ConteosMinuto.Rollos', N'Cantidad'", script);
        Assert.Contains("CAM_CamarasMigracionT798", script);
        // Las cámaras conservan el código y los puntos que eran sólo cámara se desactivan, no se borran.
        Assert.Contains("k.[Codigo] = c.[Codigo]", script);
        Assert.Contains("SET p.[Activo] = 0", script);
        Assert.DoesNotContain("DELETE", script.ToUpperInvariant());
        // Rollback: vuelve a los nombres de los scripts 25 y 26.
        Assert.Contains("UX_ConteosCamaraMinuto_Punto_Minuto_Carril", rollback);
        Assert.Contains("FK_Detenciones_PuntosControlCalidad", rollback);
        Assert.Contains("N'Rollos', N'COLUMN'", rollback);
        Assert.Contains("DROP TABLE [mnt].[CAM_Camaras]", rollback);
    }
}
