using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SqlServer.TransactSql.ScriptDom;
using PSAG.Mantenimiento.Application.DTOs.ConteoCamara;
using PSAG.Mantenimiento.Application.DTOs.Detenciones;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Reunión 17/11/2026: marca de umbral al insertar la parada (script 31), agrupador de microcortes (script 32) y
/// maquinista editable al declarar. Caso real del 13/11 en la T2-CAM10 (corrido a hace 2 días, mismo horario):
/// 11:08:23–11:11:43, 11:12:03–11:14:03 y 11:14:43–11:23:03; la última superó 5 min y generó la detención.
/// </summary>
public class AgrupadorDetencionesTests : CalidadTestBase
{
    private const string Punto = "TEST-CAM-T2";
    private readonly ConteoCamaraService _conteo;
    private readonly DeclaracionDetencionService _declaracion;
    private readonly AgrupacionDetencionService _agrupador;
    private readonly DateTimeOffset _p1, _p2, _p3;
    private readonly UsuarioLinea _maquinistaManana;
    private readonly UsuarioLinea _otroMaquinista;
    private readonly UsuarioLinea _maquinistaFutura;

    public AgrupadorDetencionesTests()
    {
        var unitOfWork = new UnitOfWork(Context);
        _conteo = new ConteoCamaraService(unitOfWork, ZonaPlanta, NullLogger<ConteoCamaraService>.Instance);
        _declaracion = new DeclaracionDetencionService(unitOfWork, new DetencionService(unitOfWork, NullLogger<DetencionService>.Instance),
            ZonaPlanta, NullLogger<DeclaracionDetencionService>.Instance);
        _agrupador = new AgrupacionDetencionService(unitOfWork, ZonaPlanta, NullLogger<AgrupacionDetencionService>.Instance);

        var dia = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaPlanta).Date.AddDays(-2);
        DateTimeOffset H(int h, int m, int s) => new(dia.AddHours(h).AddMinutes(m).AddSeconds(s), TimeSpan.FromHours(-3));
        _p1 = H(11, 8, 23);
        _p2 = H(11, 12, 3);
        _p3 = H(11, 14, 43);

        var camara = Context.PuntosControlCalidad.Single(p => p.PuntoControlCalidadId == CamaraTango.PuntoControlCalidadId);
        camara.CreaDetenciones = true;
        camara.MinutosUmbralDetencion = 5;
        _maquinistaManana = new UsuarioLinea { UsuarioId = Operario, LineaId = Tango2.LineaId, TurnoId = TurnoManana.TurnoId, FechaAsignacion = DateTimeOffset.UtcNow.AddDays(-30), AsignadoPorUsuarioId = Admin };
        _otroMaquinista = new UsuarioLinea { UsuarioId = "oper-2", LineaId = Tango2.LineaId, TurnoId = TurnoTarde.TurnoId, FechaAsignacion = DateTimeOffset.UtcNow.AddDays(-30), AsignadoPorUsuarioId = Admin };
        _maquinistaFutura = new UsuarioLinea { UsuarioId = "oper-futura", LineaId = Futura2.LineaId, TurnoId = TurnoManana.TurnoId, FechaAsignacion = DateTimeOffset.UtcNow.AddDays(-30), AsignadoPorUsuarioId = Admin };
        Context.AddRange(_maquinistaManana, _otroMaquinista, _maquinistaFutura);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }

    private static string Id(DateTimeOffset inicio) => $"{Punto}-{inicio.UtcDateTime:yyyyMMddTHHmmss}Z";

    private static ParadaCamaraIngesta Cerrada(DateTimeOffset inicio, int segundos) => new()
    {
        IdentificadorExterno = Id(inicio), Inicio = inicio, Fin = inicio.AddSeconds(segundos), Tipo = "SIN_MANGAS", Carriles = [1]
    };

    /// <summary>Ingesta del caso real: 200 s, 120 s y 500 s (8:20). Devuelve la detención automática y los ids de las paradas.</summary>
    private async Task<(Detencion Detencion, long P1, long P2, long P3)> CasoRealAsync()
    {
        var r = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = Punto,
            Paradas = [Cerrada(_p1, 200), Cerrada(_p2, 120), Cerrada(_p3, 500)]
        });
        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(1, r.Value!.DetencionesCreadas);
        Context.ChangeTracker.Clear();
        var paradas = await Context.ParadasCamara.AsNoTracking().ToListAsync();
        var detencion = await Context.Detenciones.AsNoTracking().SingleAsync(d => d.OrigenRegistro == OrigenRegistroDetencion.CAMARA);
        return (detencion,
            paradas.Single(p => p.IdentificadorExterno == Id(_p1)).ParadaCamaraId,
            paradas.Single(p => p.IdentificadorExterno == Id(_p2)).ParadaCamaraId,
            paradas.Single(p => p.IdentificadorExterno == Id(_p3)).ParadaCamaraId);
    }

    private async Task<Detencion> RecargarAsync(long id)
    {
        Context.ChangeTracker.Clear();
        return await Context.Detenciones.AsNoTracking().Include(d => d.HistorialEstados).SingleAsync(d => d.DetencionId == id);
    }

    // ===================== Marca de umbral (script 31) =====================

    [Fact]
    public async Task Ingesta_MarcaSuperaUmbralYUmbralUnaSolaVez()
    {
        await CasoRealAsync();
        var paradas = (await Context.ParadasCamara.AsNoTracking().ToListAsync()).ToDictionary(p => p.IdentificadorExterno);

        Assert.False(paradas[Id(_p1)].SuperaUmbral);
        Assert.False(paradas[Id(_p2)].SuperaUmbral);
        Assert.True(paradas[Id(_p3)].SuperaUmbral);
        Assert.All(paradas.Values, p => Assert.Equal(300, p.UmbralSegundos));

        // Cambia el umbral y se reenvía el lote: la marca guardada no se toca (sin UPDATE posteriores).
        var camara = Context.PuntosControlCalidad.Single(p => p.PuntoControlCalidadId == CamaraTango.PuntoControlCalidadId);
        camara.SegundosUmbralDetencion = 60;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        var reenvio = await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { PuntoControl = Punto, Paradas = [Cerrada(_p1, 200)] });
        Assert.Equal(1, reenvio.Value!.ParadasDuplicadas);
        Context.ChangeTracker.Clear();
        var p1 = await Context.ParadasCamara.AsNoTracking().SingleAsync(p => p.IdentificadorExterno == Id(_p1));
        Assert.False(p1.SuperaUmbral);
        Assert.Equal(300, p1.UmbralSegundos);
    }

    [Fact]
    public async Task Ingesta_ConUmbralEnSegundos_JustoEnElUmbralSupera()
    {
        var camara = Context.PuntosControlCalidad.Single(p => p.PuntoControlCalidadId == CamaraTango.PuntoControlCalidadId);
        camara.SegundosUmbralDetencion = 120;
        camara.CreaDetenciones = false;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();

        await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { PuntoControl = Punto, Paradas = [Cerrada(_p1, 119), Cerrada(_p2, 120)] });

        var paradas = (await Context.ParadasCamara.AsNoTracking().ToListAsync()).ToDictionary(p => p.IdentificadorExterno);
        Assert.False(paradas[Id(_p1)].SuperaUmbral);
        Assert.True(paradas[Id(_p2)].SuperaUmbral);
        Assert.All(paradas.Values, p => Assert.Equal(120, p.UmbralSegundos));
    }

    // ===================== Microcortes en la ventana =====================

    [Fact]
    public async Task Buscar_CasoReal_DosMicrocortesConLaPregunta()
    {
        var (d, p1, p2, _) = await CasoRealAsync();

        var r = await _agrupador.BuscarMicrocortesAsync(d.DetencionId);

        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal([p1, p2], r.Value!.Microcortes.Select(m => m.ParadaCamaraId));
        Assert.Equal("Hubo 2 microcortes antes de esta parada (11:08, 11:12). ¿Mismo motivo?", r.Value.Pregunta);
        Assert.Equal(new TimeSpan(11, 4, 43), r.Value.Desde!.Value.TimeOfDay);
    }

    [Fact]
    public async Task Buscar_ParaUnaDetencionQueSeEstaCargando_PorLineaEInicio()
    {
        var (_, p1, p2, _) = await CasoRealAsync();

        var r = await _agrupador.BuscarMicrocortesAsync(Tango2.LineaId, null, _p3);

        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal([p1, p2], r.Value!.Microcortes.Select(m => m.ParadaCamaraId));
        Assert.False((await _agrupador.BuscarMicrocortesAsync(Tango2.LineaId, CamaraFutura.PuntoControlCalidadId, _p3)).IsSuccess);
    }

    [Fact]
    public async Task Buscar_DetencionSinHoraDeParada_AvisaYNoDevuelveNada()
    {
        var r = await _agrupador.BuscarMicrocortesAsync(DetencionTango.DetencionId);
        Assert.True(r.IsSuccess);
        Assert.Empty(r.Value!.Microcortes);
        Assert.NotNull(r.Value.Aviso);
    }

    // ===================== Agrupar =====================

    [Fact]
    public async Task Agrupar_CasoReal_InicioRealYTotal_SinTocarMinutosEfectivos_YQuedaEnElHistorial()
    {
        var (d, p1, p2, _) = await CasoRealAsync();

        var r = await _agrupador.AgruparAsync(d.DetencionId, [p1, p2], Operario);

        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal(2, r.Value!.CantidadMicrocortes);
        Assert.Equal(new TimeSpan(11, 8, 23), r.Value.InicioReal!.Value.TimeOfDay);
        Assert.Equal(320, r.Value.SegundosMicrocortes);
        Assert.Equal(320, r.Value.SegundosTotales);   // pendiente de declaración: 0 minutos efectivos todavía

        var detencion = await RecargarAsync(d.DetencionId);
        Assert.Equal(d.MinutosEfectivos, detencion.MinutosEfectivos);
        Assert.Equal(d.RowVersion, detencion.RowVersion);   // la detención no se modificó
        var h = detencion.HistorialEstados.Single(x => x.OrigenAccion == AgrupacionDetencionService.OrigenHistorial);
        Assert.Equal(Operario, h.UsuarioId);
        Assert.Equal(h.EstadoAnterior, h.EstadoNuevo);
        Assert.Contains("11:08:23, 11:12:03", h.Observacion);

        var filas = await Context.AgrupacionesDetencion.AsNoTracking().ToListAsync();
        Assert.All(filas, f => Assert.Equal(OrigenAgrupacion.OPERADOR, f.Origen));
        Assert.All(filas, f => Assert.True(f.Activo));
        Assert.Empty((await _agrupador.BuscarMicrocortesAsync(d.DetencionId)).Value!.Microcortes);
    }

    [Fact]
    public async Task Agrupar_NoDobleAgrupacion_NiEnLaMismaNiEnOtraDetencion()
    {
        var (d, p1, _, _) = await CasoRealAsync();
        Assert.True((await _agrupador.AgruparAsync(d.DetencionId, [p1], Operario)).IsSuccess);

        var otraVez = await _agrupador.AgruparAsync(d.DetencionId, [p1], Operario);
        Assert.False(otraVez.IsSuccess);
        Assert.Contains("ya está agrupada", otraVez.ErrorMessage);

        // Una detención manual de la misma línea con la parada a las 11:15 tampoco la puede tomar.
        var manual = new Detencion
        {
            Codigo = "DET-TEST-MANUAL", LineaId = Tango2.LineaId, TurnoId = TurnoManana.TurnoId, Fecha = DateOnly.FromDateTime(_p3.DateTime),
            FechaHoraParada = _p3.AddSeconds(17), FechaHoraArranque = _p3.AddMinutes(10), OrigenTiempo = OrigenTiempo.CALCULADO,
            MinutosEfectivos = 10, Descripcion = "Manual", EstadoActual = EstadoDetencion.REGISTRADA, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        Context.Add(manual);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
        var enOtra = await _agrupador.AgruparAsync(manual.DetencionId, [p1], Operario);
        Assert.False(enOtra.IsSuccess);
        Assert.Contains("ya está agrupada", enOtra.ErrorMessage);
        Assert.Single(await Context.AgrupacionesDetencion.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Agrupar_TodoONada_UnaParadaQueSuperoElUmbralRechazaElPedido()
    {
        var (d, p1, _, p3) = await CasoRealAsync();

        var r = await _agrupador.AgruparAsync(d.DetencionId, [p1, p3], Operario);

        Assert.False(r.IsSuccess);
        Assert.Equal("VALIDATION", r.ErrorCode);
        Assert.Empty(await Context.AgrupacionesDetencion.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Agrupar_ParadaDeOtraLineaOFueraDeVentana_SeRechaza()
    {
        var (d, _, _, _) = await CasoRealAsync();
        await _conteo.IngestarAsync(new IngestaConteoCamaraRequest
        {
            PuntoControl = "TEST-CAM-F2",
            Paradas = [new ParadaCamaraIngesta { IdentificadorExterno = "TEST-CAM-F2-x", Inicio = _p2, Fin = _p2.AddSeconds(40), Tipo = "PARADA", Carriles = [1] }]
        });
        await _conteo.IngestarAsync(new IngestaConteoCamaraRequest { PuntoControl = Punto, Paradas = [Cerrada(_p1.AddMinutes(-20), 40)] });
        Context.ChangeTracker.Clear();
        var otraLinea = (await Context.ParadasCamara.AsNoTracking().SingleAsync(p => p.IdentificadorExterno == "TEST-CAM-F2-x")).ParadaCamaraId;
        var vieja = (await Context.ParadasCamara.AsNoTracking().SingleAsync(p => p.IdentificadorExterno == Id(_p1.AddMinutes(-20)))).ParadaCamaraId;

        Assert.Contains("misma cámara o línea", (await _agrupador.AgruparAsync(d.DetencionId, [otraLinea], Operario)).ErrorMessage);
        Assert.Contains("10 minutos previos", (await _agrupador.AgruparAsync(d.DetencionId, [vieja], Operario)).ErrorMessage);
        Assert.Contains("al menos un", (await _agrupador.AgruparAsync(d.DetencionId, [], Operario)).ErrorMessage);
        Assert.Equal("NOT_FOUND", (await _agrupador.AgruparAsync(999999, [vieja], Operario)).ErrorCode);
    }

    // ===================== Desagrupar =====================

    [Fact]
    public async Task Desagrupar_DesactivaConQuienYCuando_SinBorrar_YSePuedeVolverAAgrupar()
    {
        var (d, p1, p2, _) = await CasoRealAsync();
        Assert.True((await _agrupador.AgruparAsync(d.DetencionId, [p1, p2], Operario)).IsSuccess);

        var r = await _agrupador.DesagruparAsync(d.DetencionId, [p1], Supervisor);

        Assert.True(r.IsSuccess, r.ErrorMessage);
        Assert.Equal([p2], r.Value!.Agrupados.Select(a => a.ParadaCamaraId));
        Context.ChangeTracker.Clear();
        var fila = await Context.AgrupacionesDetencion.AsNoTracking().SingleAsync(a => a.ParadaCamaraId == p1);
        Assert.False(fila.Activo);
        Assert.Equal(Supervisor, fila.DesactivadoPorUsuarioId);
        Assert.NotNull(fila.FechaHoraDesactivacion);
        Assert.Contains((await RecargarAsync(d.DetencionId)).HistorialEstados, h => h.Observacion!.StartsWith("Microcortes desagrupados: 11:08:23"));

        // Vuelve a aparecer como candidato y se puede agrupar de nuevo (la fila vieja queda como historial).
        Assert.Equal([p1], (await _agrupador.BuscarMicrocortesAsync(d.DetencionId)).Value!.Microcortes.Select(m => m.ParadaCamaraId));
        Assert.True((await _agrupador.AgruparAsync(d.DetencionId, [p1], Operario)).IsSuccess);
        Assert.Equal(3, await Context.AgrupacionesDetencion.CountAsync());
    }

    [Fact]
    public async Task Desagrupar_UnaParadaNoAgrupadaEnEstaDetencion_SeRechaza()
    {
        var (d, p1, _, _) = await CasoRealAsync();
        var r = await _agrupador.DesagruparAsync(d.DetencionId, [p1], Operario);
        Assert.False(r.IsSuccess);
        Assert.Contains("no está agrupada", r.ErrorMessage);
    }

    [Fact]
    public async Task DetencionCerrada_NoSeAgrupa()
    {
        var (d, p1, _, _) = await CasoRealAsync();
        var cerrada = Context.Detenciones.Single(x => x.DetencionId == d.DetencionId);
        cerrada.EstadoActual = EstadoDetencion.CERRADA;
        Context.SaveChanges();
        Context.ChangeTracker.Clear();

        Assert.Contains("cerrada", (await _agrupador.AgruparAsync(d.DetencionId, [p1], Operario)).ErrorMessage);
    }

    // ===================== Maquinista editable al declarar =====================

    [Fact]
    public async Task Declarar_ConOtroMaquinista_LoCambiaYQuedaEnElHistorial()
    {
        var (d, _, _, _) = await CasoRealAsync();
        Assert.Equal(_maquinistaManana.UsuarioLineaId, d.UsuarioLineaId);   // el sugerido: el del turno

        var r = await _declaracion.DeclararAsync(d.DetencionId, new DeclararDetencionRequest
        {
            Descripcion = "Manga trabada", MinutosDeclarados = 8, RowVersion = d.RowVersion, UsuarioLineaId = _otroMaquinista.UsuarioLineaId
        }, Operario, _maquinistaManana, new MaquinistaElegido(_otroMaquinista, "Oper Dos", "Oper Uno"));

        Assert.True(r.IsSuccess, r.ErrorMessage);
        var declarada = await RecargarAsync(d.DetencionId);
        Assert.Equal(_otroMaquinista.UsuarioLineaId, declarada.UsuarioLineaId);
        var h = declarada.HistorialEstados.Single(x => x.OrigenAccion == DetencionMaquinistaReglas.OrigenCambioMaquinista);
        Assert.Equal(Operario, h.UsuarioId);
        Assert.StartsWith("Maquinista cambiado: Oper Uno → Oper Dos", h.Observacion);
    }

    [Fact]
    public async Task Declarar_ConMaquinistaDeOtraLinea_SeRechaza()
    {
        var (d, _, _, _) = await CasoRealAsync();

        var r = await _declaracion.DeclararAsync(d.DetencionId, new DeclararDetencionRequest
        {
            Descripcion = "Manga trabada", MinutosDeclarados = 8, RowVersion = d.RowVersion
        }, Operario, _maquinistaManana, new MaquinistaElegido(_maquinistaFutura, "Oper Futura", null));

        Assert.False(r.IsSuccess);
        Assert.Contains("no está asignado a la línea", r.ErrorMessage);
        var sinCambios = await RecargarAsync(d.DetencionId);
        Assert.Equal(_maquinistaManana.UsuarioLineaId, sinCambios.UsuarioLineaId);
        Assert.Equal(EstadoDetencion.PENDIENTE_JUSTIFICACION, sinCambios.EstadoActual);
    }

    // ===================== Scripts 31 y 32 =====================

    [Theory]
    [InlineData("31_marca_umbral_paradas.sql")]
    [InlineData("32_agrupador_detenciones.sql")]
    public void Script_EsTSqlValidoParaSqlServer2022(string script)
    {
        var parser = new TSql160Parser(initialQuotedIdentifiers: true);
        using var lector = new StringReader(ScriptSql.Texto(script));
        parser.Parse(lector, out var errores);

        Assert.True(errores.Count == 0, string.Join("\n", errores.Select(e => $"línea {e.Line}: {e.Message}")));
    }

    [Theory]
    [InlineData("31_marca_umbral_paradas.sql")]
    [InlineData("32_agrupador_detenciones.sql")]
    public void Script_EsIdempotente_CadaCambioTieneSuGuarda(string script)
    {
        var sql = ScriptSql.Texto(script);
        // Los comentarios (y el bloque comentado de backfill opcional) no cuentan.
        var activo = Regex.Replace(sql, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        activo = Regex.Replace(activo, @"--.*$", string.Empty, RegexOptions.Multiline);

        foreach (var lote in Regex.Split(activo, @"^\s*GO\s*$", RegexOptions.Multiline))
        {
            foreach (Match m in Regex.Matches(lote, @"CREATE TABLE \[mnt\]\.\[(\w+)\]"))
                Assert.Contains($"OBJECT_ID(N'[mnt].[{m.Groups[1].Value}]', N'U') IS NULL", lote);
            foreach (Match m in Regex.Matches(lote, @"ALTER TABLE \[mnt\]\.\[(\w+)\] ADD \[(\w+)\]"))
                Assert.Contains($"COL_LENGTH(N'mnt.{m.Groups[1].Value}', N'{m.Groups[2].Value}') IS NULL", lote);
            foreach (Match m in Regex.Matches(lote, @"ADD CONSTRAINT \[(CK_\w+)\]"))
                Assert.Contains($"[name] = N'{m.Groups[1].Value}'", lote);
            foreach (Match m in Regex.Matches(lote, @"CREATE (?:UNIQUE )?INDEX \[(\w+)\]"))
                Assert.Contains($"[name] = N'{m.Groups[1].Value}'", lote);
            Assert.DoesNotMatch(@"CREATE VIEW", lote);   // las vistas, con CREATE OR ALTER
        }

        Assert.DoesNotMatch(@"(?im)^\s*DELETE\s", activo);   // "ON DELETE NO ACTION" de las FK no cuenta
        Assert.DoesNotContain("DROP ", activo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UPDATE ", activo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Script32_TablaEIndicesComoLosMapeaEF()
    {
        var sql = ScriptSql.Texto("32_agrupador_detenciones.sql");
        // Los CHECK sólo están en el modelo de diseño (el de ejecución está optimizado).
        var entidad = Context.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model
            .FindEntityType(typeof(DetencionAgrupacion))!;
        foreach (var indice in entidad.GetIndexes())
            Assert.Contains($"[{indice.GetDatabaseName()}]", sql);
        foreach (var check in entidad.GetCheckConstraints())
            Assert.Contains($"[{check.Name}]", sql);
        Assert.Contains("WHERE [Activo] = 1", sql);
        Assert.Contains("CREATE OR ALTER VIEW [mnt].[vw_DetencionesAgrupadas]", sql);
    }
}
