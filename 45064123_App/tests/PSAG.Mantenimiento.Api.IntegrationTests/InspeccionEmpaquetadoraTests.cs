using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using PSAG.Mantenimiento.Application.DTOs.Inspeccion;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Inspecciones con el formato de Control de Proceso (reunión 03/11 [78:17]) y la planilla de Ferretti de la
/// EMPAQUETADORA TMC de TANGO 2. Lee los VALUES de database/27_..., los carga por el maestro y prueba la carga.
/// </summary>
public class InspeccionEmpaquetadoraTests : IDisposable
{
    private const string Script = "27_inspecciones_empaquetadora_tmc_tango2.sql";
    private const string Admin = "SCRIPT-27";
    private const string Operador = "op-packaging";
    private const string Mecanico = "mecanico";

    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;
    private readonly InspeccionPlantillaService _maestro;
    private readonly InspeccionService _carga;
    private readonly Linea _tango2;
    private readonly Equipo _empaquetadora;
    private readonly Turno _turno;

    public InspeccionEmpaquetadoraTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(_conexion).Options);
        _context.Database.EnsureCreated();

        var uow = new UnitOfWork(_context);
        _maestro = new InspeccionPlantillaService(uow, NullLogger<InspeccionPlantillaService>.Instance);
        _carga = new InspeccionService(uow, NullLogger<InspeccionService>.Instance);

        _tango2 = new Linea { Codigo = "", Nombre = "TANGO 2", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _turno = new Turno
        {
            Codigo = "T1", Nombre = "Turno 1 (Mañana)", HoraInicio = new TimeOnly(0, 0), HoraFin = new TimeOnly(0, 0),
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        _context.AddRange(_tango2, _turno);
        _context.SaveChanges();
        _empaquetadora = new Equipo { LineaId = _tango2.LineaId, Codigo = "EMP", Nombre = "EMPAQUETADORA TMC", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _context.Add(_empaquetadora);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _conexion.Dispose();
    }

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    // ---------------------------------------------------------------------------------------------
    // Script 27
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Script_TraeLos13PuntosDeLaPlanillaCorregidos()
    {
        var items = LeerScript();

        Assert.Equal(13, items.Count);
        Assert.Equal(Enumerable.Range(1, 13), items.Select(i => i.Numero));
        Assert.Equal(13, items.Select(i => i.Codigo).Distinct().Count());
        Assert.Equal(11, items.Count(i => i.Quien == TipoPuesto.PACKAGING && i.Frecuencia == 1));
        Assert.Equal(new[] { 12, 13 }, items.Where(i => i.Quien == TipoPuesto.MECANICO && i.Frecuencia == 10).Select(i => i.Numero));

        var todo = string.Join("\n", items.SelectMany(i => new[] { i.Parte, i.Tareas, i.Estandar, i.Metodo }));
        Assert.DoesNotContain("INSFEC", todo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CEDENA", todo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("regitro", todo, StringComparison.OrdinalIgnoreCase);

        var primero = items[0];
        Assert.Equal("Difusores de rollos", primero.Parte);
        Assert.Equal("Inspeccionar el estado de:\n1) Correa tracción de rollo\n2) Correa de mando\n3) Correa dentada de apertura\n4) Poleas", primero.Tareas);
        Assert.Equal("Control visual / tacto", primero.Metodo);
        Assert.Equal("Revisión de \"registro\" en máquina", items[2].Metodo);
        Assert.Equal(("Cadena pente", "Control de lubricación y tensión"), (items[11].Parte, items[11].Tareas));
        Assert.Equal(("Carro pente", "Quitar carros e inspeccionar"), (items[12].Parte, items[12].Metodo));
        Assert.Equal("Rodillos de salida de soldador (banda teflón)", items[10].Parte);
        Assert.All(items, i => Assert.True(i.Tareas.Length <= 300 && i.Estandar.Length <= 300 && i.Parte.Length <= 150, i.Codigo));
    }

    [Fact]
    public void Script_EsTSqlValidoParaSqlServer2022()
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var lector = new StringReader(ScriptSql.Texto(Script));
        parser.Parse(lector, out var errores);

        Assert.True(errores.Count == 0, string.Join("\n", errores.Select(e => $"línea {e.Line}: {e.Message}")));
    }

    [Fact]
    public void Script_EsIdempotente_CadaCambioTieneSuGuarda()
    {
        var sql = ScriptSql.Texto(Script);
        var lotes = Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline);

        foreach (var lote in lotes)
        {
            foreach (Match m in Regex.Matches(lote, @"CREATE TABLE \[mnt\]\.\[(\w+)\]"))
                Assert.Contains($"OBJECT_ID(N'[mnt].[{m.Groups[1].Value}]', N'U') IS NULL", lote);

            foreach (Match m in Regex.Matches(lote, @"ALTER TABLE \[mnt\]\.\[(\w+)\] ADD \[(\w+)\]"))
                Assert.Contains($"COL_LENGTH(N'mnt.{m.Groups[1].Value}', N'{m.Groups[2].Value}') IS NULL", lote);

            foreach (Match m in Regex.Matches(lote, @"ADD CONSTRAINT \[(CK_\w+)\]"))
                Assert.Contains($"[name] = N'{m.Groups[1].Value}'", lote);
        }

        // Los datos: el plan por código y los puntos que falten (no pisa ni duplica).
        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM [mnt].[PlanesInspeccion] WHERE [Codigo] = @PlanCodigo)", sql);
        Assert.Contains("WHERE NOT EXISTS (SELECT 1 FROM [mnt].[ItemsPlanInspeccion] x WHERE x.[PlanInspeccionId] = @PlanId AND x.[Codigo] = i.[Codigo])", sql);
        Assert.DoesNotContain("DELETE ", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP ", sql, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------------------------
    // Carga igual que Control de Proceso
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task QuienRealiza_FiltraLosPuntos_YLaDelDiaSeRetoma()
    {
        var (planId, _) = await CargarPlanAsync();

        var op = await Iniciar(planId, "PACKAGING", Operador);
        Assert.True(op.EsNuevo);
        Assert.Equal(11, op.CantidadItems);

        var mec = await Iniciar(planId, "MECANICO", Mecanico);
        Assert.Equal(2, mec.CantidadItems);
        Assert.NotEqual(op.EjecucionInspeccionId, mec.EjecucionInspeccionId);

        var todos = await Iniciar(planId, null, Operador);
        Assert.Equal(13, todos.CantidadItems);

        // Misma línea + plan + día + quién realiza: se retoma.
        var otraVez = await Iniciar(planId, "OP. PACKAGING", Operador);
        Assert.False(otraVez.EsNuevo);
        Assert.Equal(op.EjecucionInspeccionId, otraVez.EjecucionInspeccionId);

        var ejecucion = (await _carga.ObtenerEjecucionAsync(op.EjecucionInspeccionId)).Value!;
        Assert.Equal("EMPAQUETADORA TMC", ejecucion.PlanNombre);
        Assert.Equal("EMPAQUETADORA TMC", ejecucion.EquipoNombre);
        Assert.Equal("Operador Packaging", ejecucion.PuestoDescripcion);
        Assert.Equal(Enumerable.Range(1, 11), ejecucion.Detalles.Select(d => d.NumeroItem));
        Assert.All(ejecucion.Detalles, d => Assert.Equal(("Diaria", true), (d.FrecuenciaDescripcion, d.VenceHoy)));
        Assert.Equal("EN_CURSO", ejecucion.Estado);
    }

    [Fact]
    public async Task NoOkSinObservacion_NoGuardaNada_YConObservacionAbreHallazgo()
    {
        var (planId, codigos) = await CargarPlanAsync();
        var inicio = await Iniciar(planId, "PACKAGING", Operador);
        var e = (await _carga.ObtenerEjecucionAsync(inicio.EjecucionInspeccionId)).Value!;

        var rechazo = await _carga.GuardarResultadosAsync(new GuardarResultadosInspeccionRequest
        {
            EjecucionInspeccionId = e.EjecucionInspeccionId,
            RowVersion = e.RowVersion,
            Resultados =
            {
                new() { ItemPlanInspeccionId = codigos["T2-EMP-01"], Resultado = "OK" },
                new() { ItemPlanInspeccionId = codigos["T2-EMP-06"], Resultado = "NO OK" }
            }
        }, Operador);
        Assert.False(rechazo.IsSuccess);
        Assert.Contains("Correas lentas y rápidas (film)", rechazo.Error);
        Assert.Contains("observación es obligatoria", rechazo.Error);
        Assert.Empty(_context.EjecucionesInspeccionDetalle);

        // Un estado que no es OK / NO OK
        var malo = await _carga.GuardarResultadosAsync(new GuardarResultadosInspeccionRequest
        {
            EjecucionInspeccionId = e.EjecucionInspeccionId,
            RowVersion = e.RowVersion,
            Resultados = { new() { ItemPlanInspeccionId = codigos["T2-EMP-01"], Resultado = "más o menos" } }
        }, Operador);
        Assert.False(malo.IsSuccess);

        var ok = await _carga.GuardarResultadosAsync(new GuardarResultadosInspeccionRequest
        {
            EjecucionInspeccionId = e.EjecucionInspeccionId,
            RowVersion = e.RowVersion,
            Resultados =
            {
                new() { ItemPlanInspeccionId = codigos["T2-EMP-01"], Resultado = "OK" },
                new() { ItemPlanInspeccionId = codigos["T2-EMP-06"], Resultado = "no_ok", Observacion = " Correa rápida sin grip " },
                // Punto del mecánico en una inspección del Op. Packaging: se ignora.
                new() { ItemPlanInspeccionId = codigos["T2-EMP-12"], Resultado = "OK" }
            }
        }, Operador);
        Assert.True(ok.IsSuccess, ok.Error);

        var guardada = (await _carga.ObtenerEjecucionAsync(e.EjecucionInspeccionId)).Value!;
        Assert.Equal((1, 1), (guardada.CantidadOk, guardada.CantidadNoOk));
        var film = guardada.Detalles.Single(d => d.Codigo == "T2-EMP-06");
        Assert.Equal(("NO_OK", "Correa rápida sin grip"), (film.Resultado, film.Observacion));
        Assert.Equal("Correa con \"grip\" y dibujo. Poleas con giro libre", film.CondicionEstandar);
        Assert.Equal(2, _context.EjecucionesInspeccionDetalle.Count());

        var hallazgo = Assert.Single(_context.HallazgosNoOk);
        Assert.Equal(("Correa rápida sin grip", false, _empaquetadora.EquipoId), (hallazgo.Descripcion, hallazgo.Resuelto, hallazgo.EquipoId));
        Assert.Equal(film.EjecucionInspeccionDetalleId, hallazgo.EjecucionInspeccionDetalleId);

        // El snapshot del estándar no cambia si después lo editan en Maestros.
        foreach (var cambiado in _context.ItemsPlanInspeccion.Where(i => i.Codigo == "T2-EMP-06" || i.Codigo == "T2-EMP-07"))
            cambiado.CondicionEstandar = "Otro estándar";
        await _context.SaveChangesAsync();
        var despues = (await _carga.ObtenerEjecucionAsync(e.EjecucionInspeccionId)).Value!;
        Assert.Equal("Correa con \"grip\" y dibujo. Poleas con giro libre", despues.Detalles.Single(d => d.Codigo == "T2-EMP-06").CondicionEstandar);
        Assert.Equal("Otro estándar", despues.Detalles.Single(d => d.Codigo == "T2-EMP-07").CondicionEstandar); // sin responder: el vigente

        // Guardar otra vez el mismo NO OK no duplica el hallazgo; pasarlo a OK lo da por resuelto.
        await Guardar(despues, Operador, (codigos["T2-EMP-06"], "NO OK", "Correa rápida sin grip, cambiar"));
        Assert.Single(_context.HallazgosNoOk);
        Assert.Equal("Correa rápida sin grip, cambiar", _context.HallazgosNoOk.Single().Descripcion);

        await Guardar((await _carga.ObtenerEjecucionAsync(e.EjecucionInspeccionId)).Value!, Operador, (codigos["T2-EMP-06"], "OK", null));
        var resuelto = await _context.HallazgosNoOk.AsNoTracking().SingleAsync();
        Assert.True(resuelto.Resuelto);
        Assert.Equal("Se cambió a OK en la misma inspección.", resuelto.ObservacionResolucion);
    }

    [Fact]
    public async Task Cerrar_PideLosPuntosQueVencenHoy_YDespuesNoSeEdita()
    {
        var (planId, codigos) = await CargarPlanAsync();
        var inicio = await Iniciar(planId, "PACKAGING", Operador);
        var e = (await _carga.ObtenerEjecucionAsync(inicio.EjecucionInspeccionId)).Value!;

        await Guardar(e, Operador, (codigos["T2-EMP-01"], "OK", null));
        e = (await _carga.ObtenerEjecucionAsync(e.EjecucionInspeccionId)).Value!;
        Assert.False(e.PuedeCerrar);
        var temprano = await _carga.CerrarEjecucionAsync(new CerrarEjecucionRequest { EjecucionInspeccionId = e.EjecucionInspeccionId, RowVersion = e.RowVersion }, Operador);
        Assert.False(temprano.IsSuccess);
        Assert.Contains("Faltan 10", temprano.Error);

        var todos = Enumerable.Range(2, 10).Select(n => (codigos[$"T2-EMP-{n:00}"], "OK", (string?)null)).ToArray();
        await Guardar(e, Operador, todos);
        e = (await _carga.ObtenerEjecucionAsync(e.EjecucionInspeccionId)).Value!;
        Assert.True(e.PuedeCerrar);
        Assert.Equal((11, 11), (e.CantidadVencenHoy, e.CantidadVencenHoyRespondidos));

        // Concurrencia: con un RowVersion viejo no se cierra.
        var viejo = await _carga.CerrarEjecucionAsync(new CerrarEjecucionRequest { EjecucionInspeccionId = e.EjecucionInspeccionId, RowVersion = new byte[] { 1, 2, 3 } }, Operador);
        Assert.Equal("CONCURRENCY", viejo.ErrorCode);

        var cierre = await _carga.CerrarEjecucionAsync(new CerrarEjecucionRequest
        {
            EjecucionInspeccionId = e.EjecucionInspeccionId, RowVersion = e.RowVersion, ObservacionesGenerales = "Sin novedades"
        }, Operador);
        Assert.True(cierre.IsSuccess, cierre.Error);

        var cerrada = (await _carga.ObtenerEjecucionAsync(e.EjecucionInspeccionId)).Value!;
        Assert.Equal(("CERRADA", Operador, "Sin novedades"), (cerrada.Estado, cerrada.UsuarioCierreId, cerrada.ObservacionesGenerales));
        Assert.NotNull(cerrada.FechaHoraFin);

        var tarde = await _carga.GuardarResultadosAsync(new GuardarResultadosInspeccionRequest
        {
            EjecucionInspeccionId = cerrada.EjecucionInspeccionId,
            RowVersion = cerrada.RowVersion,
            Resultados = { new() { ItemPlanInspeccionId = codigos["T2-EMP-01"], Resultado = "NO OK", Observacion = "x" } }
        }, Operador);
        Assert.Equal("YA_COMPLETADA", tarde.ErrorCode);
    }

    [Fact]
    public async Task CadaDiezDias_ElMecanicoRespondioHaceTresDias_NoVenceHastaDentroDeSiete()
    {
        var (planId, codigos) = await CargarPlanAsync();

        // Inspección del mecánico de hace 3 días, con los dos puntos respondidos.
        var vieja = new EjecucionInspeccion
        {
            PlanInspeccionId = planId, TurnoId = _turno.TurnoId, UsuarioId = Mecanico, Puesto = TipoPuesto.MECANICO,
            Fecha = Hoy.AddDays(-3), FechaHoraInicio = DateTimeOffset.UtcNow.AddDays(-3), Completada = true, UsuarioCierreId = Mecanico,
            Detalles =
            {
                new EjecucionInspeccionDetalle { ItemPlanInspeccionId = codigos["T2-EMP-12"], Resultado = ResultadoInspeccion.OK },
                new EjecucionInspeccionDetalle { ItemPlanInspeccionId = codigos["T2-EMP-13"], Resultado = ResultadoInspeccion.OK }
            }
        };
        _context.Add(vieja);
        await _context.SaveChangesAsync();

        var planes = (await _carga.ObtenerPlanesLineaAsync(_tango2.LineaId)).Value!;
        var items = Assert.Single(planes).Items.ToDictionary(i => i.Codigo!);
        Assert.Equal((false, Hoy.AddDays(7)), (items["T2-EMP-12"].VenceHoy, items["T2-EMP-12"].ProximoVencimiento));
        Assert.Equal(Hoy.AddDays(-3), items["T2-EMP-13"].UltimaRespuesta);
        Assert.True(items["T2-EMP-01"].VenceHoy);
        Assert.Equal(("Cada 10 días", "Mecánico"), (items["T2-EMP-13"].FrecuenciaDescripcion, items["T2-EMP-13"].QuienRealizaDescripcion));

        // Hoy la de "todos": los del mecánico se ven pero no hacen falta para cerrar.
        var hoy = await Iniciar(planId, null, Operador);
        var e = (await _carga.ObtenerEjecucionAsync(hoy.EjecucionInspeccionId)).Value!;
        Assert.Equal((13, 11), (e.CantidadTotal, e.CantidadVencenHoy));
        var todosOk = Enumerable.Range(1, 11).Select(n => (codigos[$"T2-EMP-{n:00}"], "OK", (string?)null)).ToArray();
        await Guardar(e, Operador, todosOk);
        Assert.True((await _carga.ObtenerEjecucionAsync(e.EjecucionInspeccionId)).Value!.PuedeCerrar);
    }

    [Fact]
    public async Task Historial_FiltraPorQuienRealizaFrecuenciaEstadoYFecha()
    {
        var (planId, codigos) = await CargarPlanAsync();
        var op = await Iniciar(planId, "PACKAGING", Operador);
        var mec = await Iniciar(planId, "MECANICO", Mecanico);
        var m = (await _carga.ObtenerEjecucionAsync(mec.EjecucionInspeccionId)).Value!;
        await Guardar(m, Mecanico, (codigos["T2-EMP-12"], "OK", null), (codigos["T2-EMP-13"], "NO OK", "Rodamiento con juego"));
        m = (await _carga.ObtenerEjecucionAsync(mec.EjecucionInspeccionId)).Value!;
        Assert.True((await _carga.CerrarEjecucionAsync(new CerrarEjecucionRequest { EjecucionInspeccionId = m.EjecucionInspeccionId, RowVersion = m.RowVersion }, Mecanico)).IsSuccess);

        async Task<List<long>> Buscar(EjecucionInspeccionFiltroRequest f)
        {
            f.LineaId = _tango2.LineaId;
            var r = await _carga.ObtenerEjecucionesPaginadoAsync(f);
            Assert.True(r.IsSuccess, r.Error);
            return r.Value!.Items.Select(i => i.EjecucionInspeccionId).ToList();
        }

        Assert.Equal(2, (await Buscar(new())).Count);
        Assert.Equal(new[] { mec.EjecucionInspeccionId }, await Buscar(new() { Puesto = "MECANICO" }));
        Assert.Equal(new[] { op.EjecucionInspeccionId }, await Buscar(new() { Puesto = "PACKAGING" }));
        Assert.Equal(new[] { mec.EjecucionInspeccionId }, await Buscar(new() { FrecuenciaDias = 10 }));
        Assert.Equal(new[] { op.EjecucionInspeccionId }, await Buscar(new() { FrecuenciaDias = 1 }));
        Assert.Equal(new[] { mec.EjecucionInspeccionId }, await Buscar(new() { Estado = "CERRADA" }));
        Assert.Equal(new[] { op.EjecucionInspeccionId }, await Buscar(new() { Estado = "EN_CURSO" }));
        Assert.Equal(new[] { mec.EjecucionInspeccionId }, await Buscar(new() { SoloConNoOk = true }));
        Assert.Equal(2, (await Buscar(new() { EquipoId = _empaquetadora.EquipoId, FechaDesde = Hoy, FechaHasta = Hoy })).Count);
        Assert.Empty(await Buscar(new() { FechaHasta = Hoy.AddDays(-1) }));

        var fila = (await _carga.ObtenerEjecucionesPaginadoAsync(new() { Puesto = "MECANICO" })).Value!.Items.Single();
        Assert.Equal(("Cerrada", "Mecánico", 1, 1), (fila.EstadoDescripcion, fila.PuestoDescripcion, fila.CantidadOk, fila.CantidadNoOk));

        var invalido = await _carga.ObtenerEjecucionesPaginadoAsync(new() { Puesto = "GERENTE" });
        Assert.False(invalido.IsSuccess);
    }

    // ---------------------------------------------------------------------------------------------
    // Maestro: se amplía a otros equipos
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task Maestro_ValidaYPermiteSumarOtroEquipo()
    {
        var (planId, _) = await CargarPlanAsync();

        var sinTareas = await _maestro.CrearItemAsync(new GuardarItemInspeccionRequest
        {
            PlanInspeccionId = planId, ParteEquipo = "Algo", Tareas = " ", Estandar = "x"
        }, Admin);
        Assert.False(sinTareas.IsSuccess);

        var frecuenciaMala = await _maestro.CrearItemAsync(new GuardarItemInspeccionRequest
        {
            PlanInspeccionId = planId, ParteEquipo = "Algo", Tareas = "Mirar", Estandar = "x", FrecuenciaDias = 0
        }, Admin);
        Assert.False(frecuenciaMala.IsSuccess);

        var codigoRepetido = await _maestro.CrearItemAsync(new GuardarItemInspeccionRequest
        {
            PlanInspeccionId = planId, Codigo = "T2-EMP-01", ParteEquipo = "Algo", Tareas = "Mirar", Estandar = "x"
        }, Admin);
        Assert.False(codigoRepetido.IsSuccess);

        var otroEquipo = new Equipo { LineaId = _tango2.LineaId, Codigo = "ENF", Nombre = "ENFARDADORA", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _context.Add(otroEquipo);
        await _context.SaveChangesAsync();

        var plan = await _maestro.CrearPlanAsync(new GuardarPlanInspeccionRequest
        {
            LineaId = _tango2.LineaId, EquipoId = otroEquipo.EquipoId, Nombre = "ENFARDADORA", PuestoResponsable = "MECANICO", Orden = 2
        }, Admin);
        Assert.True(plan.IsSuccess, plan.Error);
        var punto = await _maestro.CrearItemAsync(new GuardarItemInspeccionRequest
        {
            PlanInspeccionId = plan.Value, ParteEquipo = "Cuchilla", Tareas = "Inspeccionar el filo", Estandar = "Sin mellas",
            Metodo = "Control visual", QuienRealiza = "Mecánico", FrecuenciaDias = 7
        }, Admin);
        Assert.True(punto.IsSuccess, punto.Error);

        var planes = (await _maestro.ObtenerPlantillaAsync(_tango2.LineaId)).Value!;
        Assert.Equal(new[] { "EMPAQUETADORA TMC", "ENFARDADORA" }, planes.Select(p => p.Nombre));
        var nuevo = planes[1];
        Assert.StartsWith("INS-L", nuevo.Codigo);
        var item = Assert.Single(nuevo.Items);
        Assert.Equal((1, $"{nuevo.Codigo}-01", "Semanal", "MECANICO"), (item.NumeroItem, item.Codigo, item.FrecuenciaDescripcion, item.QuienRealiza));

        // Un equipo de otra línea no se puede asociar.
        var otraLinea = new Linea { Codigo = "T1", Nombre = "TANGO 1", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _context.Add(otraLinea);
        await _context.SaveChangesAsync();
        var cruzado = await _maestro.CrearPlanAsync(new GuardarPlanInspeccionRequest { LineaId = otraLinea.LineaId, EquipoId = otroEquipo.EquipoId, Nombre = "X" }, Admin);
        Assert.False(cruzado.IsSuccess);

        // Desactivar un punto: deja de pedirse, lo ya respondido se sigue viendo.
        var inicio = await Iniciar(plan.Value, null, Mecanico);
        var e = (await _carga.ObtenerEjecucionAsync(inicio.EjecucionInspeccionId)).Value!;
        await Guardar(e, Mecanico, (punto.Value, "OK", null));
        var baja = await _maestro.ActualizarItemAsync(punto.Value, new GuardarItemInspeccionRequest
        {
            PlanInspeccionId = plan.Value, ParteEquipo = "Cuchilla", Tareas = "Inspeccionar el filo", Estandar = "Sin mellas",
            QuienRealiza = "MECANICO", FrecuenciaDias = 7, Activo = false
        }, Admin);
        Assert.True(baja.IsSuccess, baja.Error);
        var despues = (await _carga.ObtenerEjecucionAsync(e.EjecucionInspeccionId)).Value!;
        Assert.True(Assert.Single(despues.Detalles).FueraDePlantilla);
    }

    // ---------------------------------------------------------------------------------------------

    private async Task<IniciarEjecucionResponse> Iniciar(int planId, string? puesto, string usuario)
    {
        var r = await _carga.IniciarEjecucionAsync(new IniciarEjecucionRequest { PlanInspeccionId = planId, Puesto = puesto }, usuario);
        Assert.True(r.IsSuccess, r.Error);
        return r.Value!;
    }

    private async Task Guardar(EjecucionInspeccionCompletoDto e, string usuario, params (int Item, string Resultado, string? Observacion)[] respuestas)
    {
        var request = new GuardarResultadosInspeccionRequest { EjecucionInspeccionId = e.EjecucionInspeccionId, RowVersion = e.RowVersion };
        request.Resultados.AddRange(respuestas.Select(r => new ResultadoItemRequest { ItemPlanInspeccionId = r.Item, Resultado = r.Resultado, Observacion = r.Observacion }));
        var resultado = await _carga.GuardarResultadosAsync(request, usuario);
        Assert.True(resultado.IsSuccess, resultado.Error);
    }

    /// <summary>Carga el plan del script 27 por el maestro, como lo haría un administrador.</summary>
    private async Task<(int PlanId, Dictionary<string, int> Codigos)> CargarPlanAsync()
    {
        var plan = await _maestro.CrearPlanAsync(new GuardarPlanInspeccionRequest
        {
            LineaId = _tango2.LineaId, EquipoId = _empaquetadora.EquipoId, Codigo = "INS-T2-EMP-TMC", Nombre = "EMPAQUETADORA TMC",
            PuestoResponsable = "PACKAGING", FrecuenciaDias = 1, Orden = 1
        }, Admin);
        Assert.True(plan.IsSuccess, plan.Error);

        var codigos = new Dictionary<string, int>();
        foreach (var i in LeerScript())
        {
            var r = await _maestro.CrearItemAsync(new GuardarItemInspeccionRequest
            {
                PlanInspeccionId = plan.Value, NumeroItem = i.Numero, Codigo = i.Codigo, ParteEquipo = i.Parte, Tareas = i.Tareas,
                Estandar = i.Estandar, Metodo = i.Metodo, QuienRealiza = i.Quien.ToString(), FrecuenciaDias = i.Frecuencia, Orden = i.Orden
            }, Admin);
            Assert.True(r.IsSuccess, $"{i.Codigo}: {r.Error}");
            codigos[i.Codigo] = r.Value;
        }
        return (plan.Value, codigos);
    }

    private sealed record ItemScript(string Codigo, int Numero, string Parte, string Tareas, string Estandar, string Metodo,
        TipoPuesto Quien, int Frecuencia, int Orden);

    private static List<ItemScript> LeerScript() =>
        ScriptSql.FilasDe(ScriptSql.Lineas(Script), "INSERT INTO @Items", Script)
            .Select(v => new ItemScript(v[0]!, Entero(v[1]), v[2]!, v[3]!, v[4]!, v[5]!, (TipoPuesto)Entero(v[6]), Entero(v[7]), Entero(v[8])))
            .ToList();

    private static int Entero(string? v) => int.Parse(v!, CultureInfo.InvariantCulture);
}
