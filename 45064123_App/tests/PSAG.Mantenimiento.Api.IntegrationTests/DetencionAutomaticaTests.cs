using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-610: de la parada de la cámara a la detención automática. Umbral (5 min) y microcortes, idempotencia,
/// maquinista del turno, alertas de cámara, declaración del operador y solapa "Pendiente de declaración".
/// La cámara de la Tango (TEST-CAM-T2) crea detenciones; la de la Futura (TEST-CAM-F2) sólo informa, como la 09.
/// </summary>
public class DetencionAutomaticaTests : CalidadTestBase
{
    private readonly ConteoCamaraService _conteo;
    private readonly DeclaracionDetencionService _declaracion;
    private readonly AnalisisService _analisis;
    private readonly DateTimeOffset _parada;   // 10:43 de planta hace 2 días (turno mañana)
    private UsuarioLinea _maquinistaManana = null!;
    private UsuarioLinea _operarioFutura = null!;

    public DetencionAutomaticaTests()
    {
        var unitOfWork = new UnitOfWork(Context);
        _conteo = new ConteoCamaraService(unitOfWork, ZonaPlanta, NullLogger<ConteoCamaraService>.Instance);
        var detenciones = new DetencionService(unitOfWork, NullLogger<DetencionService>.Instance);
        _declaracion = new DeclaracionDetencionService(unitOfWork, detenciones, ZonaPlanta, NullLogger<DeclaracionDetencionService>.Instance);
        _analisis = new AnalisisService(unitOfWork, NullLogger<AnalisisService>.Instance);
        var hoy = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaPlanta).Date;
        _parada = new DateTimeOffset(hoy.AddDays(-2).AddHours(10).AddMinutes(43), TimeSpan.FromHours(-3));

        var camara = Context.PuntosControlCalidad.Single(p => p.PuntoControlCalidadId == CamaraTango.PuntoControlCalidadId);
        camara.CreaDetenciones = true;
        camara.MinutosUmbralDetencion = 5;
        _maquinistaManana = new UsuarioLinea { UsuarioId = Operario, LineaId = Tango2.LineaId, TurnoId = TurnoManana.TurnoId, FechaAsignacion = DateTimeOffset.UtcNow.AddDays(-30), AsignadoPorUsuarioId = Admin };
        _operarioFutura = new UsuarioLinea { UsuarioId = "oper-futura", LineaId = Futura2.LineaId, TurnoId = TurnoManana.TurnoId, FechaAsignacion = DateTimeOffset.UtcNow.AddDays(-30), AsignadoPorUsuarioId = Admin };
        Context.AddRange(_maquinistaManana, _operarioFutura);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    private static string Id(string punto, DateTimeOffset inicio) => $"{punto}-{inicio.UtcDateTime:yyyyMMddTHHmmss}Z";

    private IngestaConteoCamaraRequest EnCurso(DateTimeOffset inicio, int segundosHastaAhora, string punto = "TEST-CAM-T2") => new()
    {
        PuntoControl = punto,
        ParadasEnCurso = [new ParadaEnCursoIngesta { IdentificadorExterno = Id(punto, inicio), Inicio = inicio, Hasta = inicio.AddSeconds(segundosHastaAhora), Tipo = "SIN_MANGAS" }]
    };

    private IngestaConteoCamaraRequest Cerrada(DateTimeOffset inicio, int segundos, string punto = "TEST-CAM-T2", bool finIncierto = false) => new()
    {
        PuntoControl = punto,
        Paradas = [new ParadaCamaraIngesta { IdentificadorExterno = Id(punto, inicio), Inicio = inicio, Fin = inicio.AddSeconds(segundos), Tipo = "PARADA", Carriles = [1], FinIncierto = finIncierto }]
    };

    private async Task<List<Detencion>> Automaticas()
    {
        Context.ChangeTracker.Clear();
        return await Context.Detenciones.IgnoreQueryFilters().Include(d => d.HistorialEstados)
            .Where(d => d.OrigenRegistro == OrigenRegistroDetencion.CAMARA).ToListAsync();
    }

    [Fact]
    public async Task ParadaEnCursoDe5Minutos_CreaUnaDetencionPendienteConLosCamposDelSistema()
    {
        var r = await _conteo.IngestarAsync(EnCurso(_parada, 5 * 60 + 10));

        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(1, r.Value!.DetencionesCreadas);
        var d = Assert.Single(await Automaticas());
        Assert.Equal(EstadoDetencion.PENDIENTE_JUSTIFICACION, d.EstadoActual);
        Assert.Equal(Tango2.LineaId, d.LineaId);
        Assert.Equal(TurnoManana.TurnoId, d.TurnoId);
        Assert.Equal(DateOnly.FromDateTime(_parada.DateTime), d.Fecha);
        Assert.Equal(_parada, d.FechaHoraParada);
        Assert.Equal(_maquinistaManana.UsuarioLineaId, d.UsuarioLineaId);
        Assert.Equal(CamaraTango.PuntoControlCalidadId, d.PuntoControlCalidadId);
        Assert.Equal(Id("TEST-CAM-T2", _parada), d.IdentificadorParadaCamara);
        Assert.StartsWith("DET-", d.Codigo);
        // Lo que declara el operador queda vacío.
        Assert.Null(d.FechaHoraArranque);
        Assert.Null(d.FechaHoraArranqueDetectada);
        Assert.Null(d.MinutosDeclarados);
        Assert.Equal(0, d.MinutosEfectivos);
        Assert.Null(d.EquipoId);
        Assert.Null(d.TipoFallaId);
        Assert.Null(d.DetalleFallaId);
        Assert.Equal(string.Empty, d.Descripcion);
        Assert.Equal(DetencionCamaraReglas.UsuarioSistemaCamara, d.CreadoPorUsuarioId);
        var h = Assert.Single(d.HistorialEstados);
        Assert.Equal("CAMARA", h.OrigenAccion);
        Assert.Contains("TEST-CAM-T2", h.Observacion);
    }

    [Fact]
    public async Task Reenvios_ParadaQueSigueYCierre_NoDuplicanYGuardanElArranqueComoSugerencia()
    {
        await _conteo.IngestarAsync(EnCurso(_parada, 5 * 60));
        await _conteo.IngestarAsync(EnCurso(_parada, 6 * 60));             // sigue parada
        await _conteo.IngestarAsync(EnCurso(_parada, 6 * 60));             // reenvío del mismo lote
        var cierre = await _conteo.IngestarAsync(Cerrada(_parada, 9 * 60)); // arrancó a las 10:52
        await _conteo.IngestarAsync(Cerrada(_parada, 9 * 60));

        Assert.Equal(0, cierre.Value!.DetencionesCreadas);
        Assert.Equal(1, cierre.Value.DetencionesExistentes);
        var d = Assert.Single(await Automaticas());
        Assert.Equal(_parada.AddMinutes(9), d.FechaHoraArranqueDetectada);
        Assert.Null(d.FechaHoraArranque);                                   // la hora de fin la declara el operador
        Assert.Equal(EstadoDetencion.PENDIENTE_JUSTIFICACION, d.EstadoActual);
        Assert.Single(await Context.ParadasCamara.ToListAsync());           // y la parada queda en el log
    }

    [Fact]
    public async Task ParadaQueLlegaCerrada_SinHaberPasadoEnCurso_IgualCreaLaDetencion()
    {
        var r = await _conteo.IngestarAsync(Cerrada(_parada, 7 * 60));
        Assert.Equal(1, r.Value!.DetencionesCreadas);
        var d = Assert.Single(await Automaticas());
        Assert.Equal(_parada.AddMinutes(7), d.FechaHoraArranqueDetectada);
    }

    [Fact]
    public async Task Microcorte_QuedaSoloEnElLogDeParadas()
    {
        await _conteo.IngestarAsync(EnCurso(_parada, 4 * 60 + 59));
        var r = await _conteo.IngestarAsync(Cerrada(_parada, 4 * 60 + 59));

        Assert.Equal(1, r.Value!.ParadasNuevas);
        Assert.Equal(0, r.Value.DetencionesCreadas);
        Assert.Empty(await Automaticas());
        var log = await _conteo.ObtenerParadasAsync(new ParadasCamaraFiltroRequest { FechaDesde = DiaPlanta(3), FechaHasta = DiaPlanta(0) });
        var p = Assert.Single(log.Value!.Items);
        Assert.False(p.SuperaUmbral);
        Assert.Equal("SIN_DETENCION", p.Cruce);
    }

    [Fact]
    public async Task UmbralConfigurableEnMaestros_SinRedeploy()
    {
        var resultado = await Maestros.ActualizarPuntoControlAsync(CamaraTango.PuntoControlCalidadId, new Application.DTOs.Calidad.GuardarPuntoControlCalidadRequest
        {
            LineaId = Tango2.LineaId, Codigo = "TEST-CAM-T2", Nombre = "Cámara 10", Activo = true,
            NumeroCamara = 10, CreaDetenciones = true, MinutosUmbralDetencion = 2
        }, Admin, [RolesSistema.Administrador]);
        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);

        var r = await _conteo.IngestarAsync(Cerrada(_parada, 150));
        Assert.Equal(1, r.Value!.DetencionesCreadas);

        var invalido = await Maestros.ActualizarPuntoControlAsync(CamaraTango.PuntoControlCalidadId, new Application.DTOs.Calidad.GuardarPuntoControlCalidadRequest
        {
            LineaId = Tango2.LineaId, Codigo = "TEST-CAM-T2", Nombre = "Cámara 10", CreaDetenciones = true, MinutosUmbralDetencion = 0
        }, Admin, [RolesSistema.Administrador]);
        Assert.False(invalido.IsSuccess);
    }

    [Fact]
    public async Task CamaraQueSoloInforma_NoCreaDetenciones()
    {
        var r = await _conteo.IngestarAsync(Cerrada(_parada, 20 * 60, punto: "TEST-CAM-F2"));
        Assert.Equal(1, r.Value!.ParadasNuevas);
        Assert.Empty(await Automaticas());
    }

    [Fact]
    public async Task DetencionEliminada_NoSeVuelveACrear()
    {
        await _conteo.IngestarAsync(EnCurso(_parada, 6 * 60));
        var d = Assert.Single(await Automaticas());
        var tracked = await Context.Detenciones.SingleAsync(x => x.DetencionId == d.DetencionId);
        tracked.Eliminada = true;
        await Context.SaveChangesAsync();

        var r = await _conteo.IngestarAsync(Cerrada(_parada, 8 * 60));
        Assert.Equal(0, r.Value!.DetencionesCreadas);
        Assert.Single(await Automaticas());
    }

    [Fact]
    public async Task PantallaDeDeclarar_TraeElEquipoDelCuerpoDeLaCamaraParaPrecargarlo()
    {
        // Ferretti 04/11: el equipo que manda en la detención es la rebobinadora (el cuerpo de la cámara 10).
        var rebobinadora = new Equipo { LineaId = Tango2.LineaId, Codigo = "REB", Nombre = "REBOBINADORA", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        Context.Add(rebobinadora);
        await Context.SaveChangesAsync();
        var camara = await Context.PuntosControlCalidad.SingleAsync(p => p.PuntoControlCalidadId == CamaraTango.PuntoControlCalidadId);
        camara.EquipoId = rebobinadora.EquipoId;
        await Context.SaveChangesAsync();

        await _conteo.IngestarAsync(EnCurso(_parada, 6 * 60));
        var d = Assert.Single(await Automaticas());
        Assert.Null(d.EquipoId);   // la detención queda sin equipo hasta que el operador declara

        var pantalla = await _declaracion.ObtenerAsync(d.DetencionId);
        Assert.True(pantalla.IsSuccess, pantalla.ErrorMessage);
        Assert.Equal(rebobinadora.EquipoId, pantalla.Value!.CuerpoCamaraEquipoId);
        Assert.Equal("REBOBINADORA", pantalla.Value.CuerpoCamaraNombre);
    }

    [Fact]
    public async Task SinMaquinistaDelTurno_QuedaVacioYLoCompletaQuienDeclara()
    {
        var tarde = new DateTimeOffset(_parada.Date.AddHours(15), _parada.Offset);   // turno tarde: nadie asignado
        await _conteo.IngestarAsync(EnCurso(tarde, 6 * 60));
        var d = Assert.Single(await Automaticas());
        Assert.Null(d.UsuarioLineaId);
        Assert.Contains("lo completa quien declara", d.HistorialEstados.Single().Observacion);

        var r = await _declaracion.DeclararAsync(d.DetencionId, new DeclararDetencionRequest
        {
            Descripcion = "Se llenó la empaquetadora", HoraArranque = new TimeOnly(15, 20), RowVersion = d.RowVersion
        }, Operario, _maquinistaManana);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Context.ChangeTracker.Clear();
        Assert.Equal(_maquinistaManana.UsuarioLineaId, (await Context.Detenciones.SingleAsync(x => x.DetencionId == d.DetencionId)).UsuarioLineaId);
    }

    [Fact]
    public async Task Declarar_PasaARegistradaConMinutosCalculadosYSaleDeLaSolapa()
    {
        await _conteo.IngestarAsync(Cerrada(_parada, 9 * 60));
        var d = Assert.Single(await Automaticas());

        var pendientesAntes = await _analisis.ObtenerMisPendientesAsync(Operario, [RolesSistema.Operario]);
        var item = Assert.Single(pendientesAntes.Value!.ParaDeclarar);
        Assert.Equal(d.Codigo, item.Codigo);
        Assert.Equal("CAMARA", item.OrigenRegistro);
        Assert.Equal("TEST-CAM-T2", item.CamaraCodigo);
        Assert.Equal(TurnoManana.Nombre, item.TurnoNombre);
        Assert.NotNull(item.FechaHoraArranqueDetectada);
        Assert.Equal(1, await _analisis.ContarPendientesAsync(Operario, [RolesSistema.Operario]));

        // Validaciones: igual que una detención manual, la descripción es obligatoria.
        var sinDescripcion = await _declaracion.DeclararAsync(d.DetencionId, new DeclararDetencionRequest
        {
            HoraArranque = new TimeOnly(10, 52), RowVersion = d.RowVersion
        }, Operario, _maquinistaManana);
        Assert.False(sinDescripcion.IsSuccess);
        var antesDeLaParada = await _declaracion.DeclararAsync(d.DetencionId, new DeclararDetencionRequest
        {
            Descripcion = "x", HoraArranque = new TimeOnly(10, 30), RowVersion = d.RowVersion
        }, Operario, _maquinistaManana);
        Assert.False(antesDeLaParada.IsSuccess);   // 10:30 sería del día siguiente: casi 24 h, error de tipeo

        var ok = await _declaracion.DeclararAsync(d.DetencionId, new DeclararDetencionRequest
        {
            Descripcion = "Se cortó el papel", HoraArranque = new TimeOnly(10, 55), RowVersion = d.RowVersion
        }, Operario, _maquinistaManana);

        Assert.True(ok.IsSuccess, ok.ErrorMessage);
        Assert.Equal(12, ok.Value!.MinutosEfectivos);     // 10:43 a 10:55
        var declarada = (await Automaticas()).Single();
        Assert.Equal(EstadoDetencion.REGISTRADA, declarada.EstadoActual);
        Assert.Equal(OrigenTiempo.CALCULADO, declarada.OrigenTiempo);
        Assert.Equal(_parada.AddMinutes(12), declarada.FechaHoraArranque);
        Assert.Equal(_parada.AddMinutes(9), declarada.FechaHoraArranqueDetectada);   // la sugerencia no se pisa
        Assert.Equal(OrigenRegistroDetencion.CAMARA, declarada.OrigenRegistro);       // sigue marcada como de cámara
        Assert.Equal(2, declarada.HistorialEstados.Count);

        var despues = await _analisis.ObtenerMisPendientesAsync(Operario, [RolesSistema.Operario]);
        Assert.Empty(despues.Value!.ParaDeclarar);

        var otraVez = await _declaracion.DeclararAsync(d.DetencionId, new DeclararDetencionRequest
        {
            Descripcion = "x", HoraArranque = new TimeOnly(11, 0), RowVersion = declarada.RowVersion
        }, Operario, _maquinistaManana);
        Assert.False(otraVez.IsSuccess);
    }

    [Fact]
    public async Task Declarar_ConMinutosSinHora()
    {
        await _conteo.IngestarAsync(EnCurso(_parada, 6 * 60));
        var d = Assert.Single(await Automaticas());
        var ok = await _declaracion.DeclararAsync(d.DetencionId, new DeclararDetencionRequest
        {
            Descripcion = "Atasco", MinutosDeclarados = 8, RowVersion = d.RowVersion
        }, Operario, _maquinistaManana);
        Assert.True(ok.IsSuccess, ok.ErrorMessage);
        var x = (await Automaticas()).Single();
        Assert.Equal(OrigenTiempo.DECLARADO, x.OrigenTiempo);
        Assert.Equal(8, x.MinutosEfectivos);
        Assert.Null(x.FechaHoraArranque);
    }

    [Fact]
    public async Task SolapaPendienteDeDeclaracion_FiltraPorLaLineaDelUsuario()
    {
        await _conteo.IngestarAsync(EnCurso(_parada, 6 * 60));

        var deTango = await _analisis.ObtenerMisPendientesAsync(Operario, [RolesSistema.Operario]);
        var deFutura = await _analisis.ObtenerMisPendientesAsync("oper-futura", [RolesSistema.Operario]);
        var admin = await _analisis.ObtenerMisPendientesAsync(Admin, [RolesSistema.Administrador]);
        var gerencia = await _analisis.ObtenerMisPendientesAsync("ger-1", [RolesSistema.Gerencia]);

        Assert.Single(deTango.Value!.ParaDeclarar);
        Assert.Empty(deFutura.Value!.ParaDeclarar);
        Assert.Single(admin.Value!.ParaDeclarar);          // admin sin líneas asignadas: ve todas
        Assert.False(gerencia.Value!.VeDeclaracion);
    }

    [Fact]
    public async Task AvisoFinDeTurno_ConPendientesCercaDelFin()
    {
        await _conteo.IngestarAsync(EnCurso(_parada, 6 * 60));
        var unitOfWork = new UnitOfWork(Context);
        var detenciones = new DetencionService(unitOfWork, NullLogger<DetencionService>.Instance);
        DateTimeOffset ahora = new DateTimeOffset(_parada.Date.AddHours(13).AddMinutes(40), _parada.Offset);  // 20 min para las 14
        var servicio = new DeclaracionDetencionService(unitOfWork, detenciones, ZonaPlanta, NullLogger<DeclaracionDetencionService>.Instance, () => ahora);

        var cerca = await servicio.ObtenerAvisoFinTurnoAsync([Tango2.LineaId], 30);
        Assert.True(cerca.Mostrar);
        Assert.Equal(1, cerca.Pendientes);
        Assert.Equal(20, cerca.MinutosParaFin);

        ahora = ahora.AddHours(-2);
        Assert.False((await servicio.ObtenerAvisoFinTurnoAsync([Tango2.LineaId], 30)).Mostrar);
        ahora = ahora.AddHours(2);
        Assert.False((await servicio.ObtenerAvisoFinTurnoAsync([Futura2.LineaId], 30)).Mostrar);   // otra línea, sin pendientes
    }

    [Fact]
    public async Task AlertaDeCamara_SeAbreYSeCierra_SinCrearDetencion()
    {
        var inicio = _parada;
        var abrir = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-T2",
            Alertas = [new AlertaCamaraIngesta { Tipo = "CONGELADA", Inicio = inicio, Detalle = "Imagen congelada" }]
        });
        Assert.Equal(1, abrir.Value!.AlertasNuevas);
        var activas = await _conteo.ObtenerAlertasAsync(null, horas: 24 * 5);
        var a = Assert.Single(activas);
        Assert.True(a.Activa);
        Assert.Equal("Cámara congelada", a.TipoDescripcion);

        var cerrar = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-T2",
            Alertas = [new AlertaCamaraIngesta { Tipo = "CONGELADA", Inicio = inicio, Fin = inicio.AddMinutes(8) }]
        });
        Assert.Equal(1, cerrar.Value!.AlertasCerradas);
        await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-T2",
            Alertas = [new AlertaCamaraIngesta { Tipo = "CONGELADA", Inicio = inicio, Fin = inicio.AddMinutes(8) }]
        });
        Context.ChangeTracker.Clear();
        var guardada = await Context.AlertasCamara.SingleAsync();
        Assert.Equal(inicio.AddMinutes(8), guardada.FechaHoraFin);
        Assert.Empty(await Automaticas());
    }

    [Fact]
    public async Task ProduccionPorTurnoYPorCamara_NoMezclaLa09ConLa10()
    {
        var minuto = new DateTimeOffset(_parada.Year, _parada.Month, _parada.Day, 10, 0, 0, _parada.Offset);
        await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-T2",
            Minutos = [new MinutoCamaraIngesta { Minuto = minuto, Carril = 1, Rollos = 22, SegundosMarcha = 60, Estado = "MARCHA" },
                       new MinutoCamaraIngesta { Minuto = minuto.AddMinutes(1), Carril = 1, Rollos = 21, SegundosMarcha = 60, Estado = "MARCHA" }]
        });
        await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-F2",
            Minutos = [new MinutoCamaraIngesta { Minuto = minuto, Carril = 1, Rollos = 166, SegundosMarcha = 60, Estado = "MARCHA" }]
        });

        var p = await _conteo.ObtenerProduccionAsync(DateOnly.FromDateTime(minuto.DateTime), null, CamaraTango.PuntoControlCalidadId);
        Assert.Equal(43, p.Value!.TotalRollos);
        var t = Assert.Single(p.Value.Turnos);
        Assert.Equal(TurnoManana.Nombre, t.TurnoNombre);
        Assert.Equal(43, t.Total);
        Assert.Equal(2, t.MinutosMarcha);
    }

    [Fact]
    public async Task DetencionManual_SigueIgual()
    {
        Context.ChangeTracker.Clear();
        var manual = await Context.Detenciones.SingleAsync(d => d.DetencionId == DetencionTango.DetencionId);
        Assert.Equal(OrigenRegistroDetencion.MANUAL, manual.OrigenRegistro);
        Assert.Null(manual.IdentificadorParadaCamara);
        var validador = new Application.Validators.CrearDetencionValidator();
        var sinDescripcion = validador.Validate(new CrearDetencionRequest
        {
            LineaId = Tango2.LineaId, Fecha = DiaPlanta(0), OrigenTiempo = "DECLARADO", MinutosDeclarados = 5, Descripcion = ""
        });
        Assert.False(sinDescripcion.IsValid);
    }
}
