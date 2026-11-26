using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-770 · GET api/tablero/turno con la API entera (WebApplicationFactory) contra SQLite o SQL Server: el operario
/// (obligado a turno) ve SOLO su turno aunque pida otra línea; las paradas de cámara se separan en detenciones reales
/// (superan el umbral) y microcortes; pendientes de declarar y detenciones son las del turno; último dato y "sin datos"
/// de la cámara. El supervisor lo ve por línea en sólo lectura; sin permiso 403; el obligado sin turno 409.
/// Iteración 2: modo gerencial por turnoProduccionId (permiso PRODUCCION.TABLERO_GERENCIAL y alcance por línea), los 4
/// bloques de Ferretti en la respuesta, y el operador no puede pedir otro turno.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public sealed class TableroApiTests : IDisposable
{
    private const string Oper = "oper-tab";

    private readonly BaseDePrueba _base;
    private readonly ApiDePrueba _api;
    private readonly int _linea1;
    private readonly int _linea2;
    private readonly int _linea3;

    public TableroApiTests()
    {
        _base = BaseDePrueba.Crear();
        _api = new ApiDePrueba(_base);
        _ = _api.Server;   // semilla: roles, permisos (con PRODUCCION.TABLERO), líneas y turnos

        using var ctx = _api.NuevoContexto();
        var lineas = ctx.Lineas.OrderBy(l => l.Orden).Select(l => l.LineaId).Take(3).ToList();
        (_linea1, _linea2, _linea3) = (lineas[0], lineas[1], lineas[2]);
        ctx.OperadoresLinea.AddRange(
            new OperadorLinea { UsuarioId = Oper, LineaId = _linea1, FechaAsignacion = DateTimeOffset.UtcNow.AddDays(-1), AsignadoPorUsuarioId = "admin" },
            new OperadorLinea { UsuarioId = Oper, LineaId = _linea2, FechaAsignacion = DateTimeOffset.UtcNow.AddDays(-1), AsignadoPorUsuarioId = "admin" });
        ctx.SaveChanges();
    }

    public void Dispose()
    {
        _api.Dispose();
        _base.Dispose();
    }

    private HttpClient Operario() => _api.Cliente(Oper, RolesSistema.Operario);

    private async Task<TurnoProduccionDto> AbrirAsync()
    {
        var r = await Operario().PostAsJsonAsync("api/turnos-produccion/abrir", new { lineaId = _linea1 });
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        return (await r.Content.ReadFromJsonAsync<AperturaTurnoProduccionDto>())!.Turno!;
    }

    /// <summary>Cámara oficial de mangas de la línea 1, con paradas, minutos y detenciones del turno (y de otro turno).</summary>
    private void CargarDatos(long turnoProduccionId)
    {
        using var ctx = _api.NuevoContexto();
        var tp = ctx.TurnosProduccion.Single(t => t.TurnoProduccionId == turnoProduccionId);
        var ahora = DateTimeOffset.UtcNow;
        var camara = new Camara
        {
            LineaId = _linea1, Codigo = "TAB-CAM10", Nombre = "Cámara tablero", Funcion = FuncionCamara.Conteo,
            Magnitud = MagnitudProduccion.Mangas, MetodoDeteccion = MetodoDeteccionCamara.Pasos, CantidadCarriles = 1,
            DetectaParadas = true, CreaDetenciones = true, UmbralDetencionSegundos = 300, CuentaProduccionOficial = true,
            Habilitada = true, UltimoDatoRecibido = ahora.AddMinutes(-10), CreadoPorUsuarioId = "admin", ModificadoPorUsuarioId = "admin"
        };
        ctx.Camaras.Add(camara);
        ctx.SaveChanges();

        var minuto = new DateTimeOffset(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0, TimeSpan.Zero);
        if (minuto < tp.InicioProgramado)
            minuto = tp.InicioProgramado;
        ctx.ConteosCamaraMinuto.Add(new ConteoCamaraMinuto
        {
            Minuto = minuto, CamaraId = camara.CamaraId, TurnoProduccionId = turnoProduccionId, Carril = 1, Cantidad = 42,
            SegundosMarcha = 60, Estado = EstadoCarrilCamara.MARCHA, FechaHoraCreacion = ahora
        });

        var inicio = ahora.AddMinutes(-30);
        ctx.ParadasCamara.AddRange(
            Parada(camara.CamaraId, turnoProduccionId, inicio, 6 * 60, "TAB-1", supera: true),
            Parada(camara.CamaraId, turnoProduccionId, inicio.AddMinutes(10), 40, "TAB-2", supera: false),
            Parada(camara.CamaraId, turnoProduccionId, inicio.AddMinutes(12), 70, "TAB-3", supera: false));

        var otroTurno = ctx.TurnoProduccionId(_linea2, tp.TurnoId, tp.FechaProduccion.AddDays(-1));
        ctx.Detenciones.AddRange(
            new Detencion
            {
                Codigo = "DET-TAB-PEND", LineaId = _linea1, TurnoProduccionId = turnoProduccionId, FechaHoraParada = inicio,
                OrigenTiempo = OrigenTiempo.CALCULADO, MinutosEfectivos = 0, Descripcion = string.Empty,
                EstadoActual = EstadoDetencion.PENDIENTE_JUSTIFICACION, OrigenRegistro = OrigenRegistroDetencion.CAMARA,
                IdentificadorParadaCamara = "TAB-1", CamaraId = camara.CamaraId, CreadoPorUsuarioId = "SISTEMA", ModificadoPorUsuarioId = "SISTEMA"
            },
            Registrada("DET-TAB-REG", _linea1, turnoProduccionId, 12),
            Registrada("DET-TAB-OTRO", _linea2, otroTurno, 30));
        ctx.SaveChanges();
    }

    private static ParadaCamara Parada(int camaraId, long turno, DateTimeOffset inicio, int segundos, string id, bool supera) => new()
    {
        CamaraId = camaraId, TurnoProduccionId = turno, FechaHoraInicio = inicio, FechaHoraFin = inicio.AddSeconds(segundos),
        DuracionSegundos = segundos, Tipo = TipoParadaCamara.PARADA, Carriles = "1", IdentificadorExterno = id,
        UmbralSegundos = 300, SuperaUmbral = supera, FechaHoraCreacion = DateTimeOffset.UtcNow
    };

    private static Detencion Registrada(string codigo, int lineaId, long turno, int minutos) => new()
    {
        Codigo = codigo, LineaId = lineaId, TurnoProduccionId = turno, MinutosDeclarados = minutos, MinutosEfectivos = minutos,
        OrigenTiempo = OrigenTiempo.DECLARADO, Descripcion = "Prueba T-770", EstadoActual = EstadoDetencion.REGISTRADA,
        CreadoPorUsuarioId = "admin", ModificadoPorUsuarioId = "admin"
    };

    [Fact]
    public async Task Operario_VeSoloSuTurno_ConRealesSeparadasDeMicrocortes_YPendientes()
    {
        var turno = await AbrirAsync();
        CargarDatos(turno.TurnoProduccionId);

        // Aunque pida otra línea, el filtro de turno obligatorio lo deja en la suya
        foreach (var url in new[] { "api/tablero/turno", $"api/tablero/turno?lineaId={_linea2}" })
        {
            var r = await Operario().GetAsync(url);
            Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            var t = (await r.Content.ReadFromJsonAsync<TableroTurnoDto>())!;

            Assert.Equal(turno.TurnoProduccionId, t.TurnoProduccionId);
            Assert.Equal(_linea1, t.LineaId);
            Assert.False(t.SoloLectura);
            Assert.True(t.Abierto);
            Assert.Equal(turno.TurnoProduccionId, t.Indicadores.TurnoProduccionId);
            Assert.Equal(Oper, t.OperadorUsuarioId);

            // Reales (superan el umbral) vs. microcortes
            Assert.Equal(1, t.DetencionesReales.Cantidad);
            Assert.Equal(360, t.DetencionesReales.Segundos);
            Assert.Equal(6, t.DetencionesReales.Minutos);
            Assert.All(t.DetencionesReales.Items, p => Assert.True(p.SuperaUmbral));
            Assert.Equal(2, t.Microcortes.Cantidad);
            Assert.Equal(110, t.Microcortes.Segundos);
            Assert.All(t.Microcortes.Items, p => Assert.False(p.SuperaUmbral));
            Assert.Contains("DET-TAB-PEND", t.DetencionesReales.Items.Single().CodigosDetencion);

            // Detenciones y pendientes: sólo las del turno
            Assert.Equal(["DET-TAB-PEND", "DET-TAB-REG"], t.Detenciones.Select(d => d.Codigo).OrderBy(c => c));
            var pendiente = Assert.Single(t.PendientesDeclarar);
            Assert.Equal("DET-TAB-PEND", pendiente.Codigo);
            Assert.Equal("TAB-CAM10", pendiente.CamaraCodigo);
            Assert.Equal(12, t.Indicadores.MinutosParadaQueCuentan);   // la pendiente cuenta con 0 hasta declararla

            // Cámara: último dato hace 10 min con el turno abierto = sin datos
            Assert.True(t.Camara.TieneCamara);
            Assert.NotNull(t.Camara.UltimoDato);
            Assert.True(t.Camara.SinDatos);
            Assert.InRange(t.Camara.MinutosDesdeUltimoDato!.Value, 9, 11);

            // Mangas por hora: la hora en curso tiene el minuto de la cámara
            Assert.NotEmpty(t.MangasPorHora);
            Assert.Equal(42, t.MangasPorHora.Sum(h => h.Real));
            Assert.Single(t.MangasPorHora, h => h.EnCurso);
        }
    }

    [Fact]
    public async Task Supervisor_LoVePorLineaEnSoloLectura_SinPermisoNo_YElObligadoSinTurno409()
    {
        // Obligado sin turno: 409 (como todo lo de negocio)
        var sinTurno = await Operario().GetAsync("api/tablero/turno");
        Assert.Equal(HttpStatusCode.Conflict, sinTurno.StatusCode);
        Assert.Contains("TURNO_REQUERIDO", await sinTurno.Content.ReadAsStringAsync());

        var turno = await AbrirAsync();
        var sup = _api.Cliente("sup-tab", RolesSistema.Supervisor);

        Assert.Equal(HttpStatusCode.BadRequest, (await sup.GetAsync("api/tablero/turno")).StatusCode);
        var r = await sup.GetAsync($"api/tablero/turno?lineaId={_linea1}");
        Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
        var t = (await r.Content.ReadFromJsonAsync<TableroTurnoDto>())!;
        Assert.Equal(turno.TurnoProduccionId, t.TurnoProduccionId);
        Assert.True(t.SoloLectura);
        Assert.False(t.Camara.TieneCamara);
        Assert.False(t.Camara.SinDatos);

        // Línea sin turno en curso: 204
        Assert.Equal(HttpStatusCode.NoContent, (await sup.GetAsync($"api/tablero/turno?lineaId={_linea3}")).StatusCode);

        // Mantenimiento no tiene VER del tablero ni del gerencial en la matriz migrada: 403 "Sin permiso".
        // (Gerencia y administrador tienen el gerencial desde la iteración 2: ver Gerencial_*.)
        var prohibido = await _api.Cliente("usr-mant", RolesSistema.Mantenimiento).GetAsync($"api/tablero/turno?lineaId={_linea1}");
        Assert.Equal(HttpStatusCode.Forbidden, prohibido.StatusCode);
        Assert.Contains("Sin permiso", await prohibido.Content.ReadAsStringAsync());
    }

    private void AsignarLineas(string usuario, params int[] lineas)
    {
        using var ctx = _api.NuevoContexto();
        foreach (var l in lineas)
            ctx.OperadoresLinea.Add(new OperadorLinea { UsuarioId = usuario, LineaId = l, FechaAsignacion = DateTimeOffset.UtcNow.AddDays(-1), AsignadoPorUsuarioId = "admin" });
        ctx.SaveChanges();
    }

    [Fact]
    public async Task Gerencial_CualquierTurnoPorId_SoloLectura_ConPermisoYAlcance_YLosCuatroBloques()
    {
        var turno = await AbrirAsync();
        CargarDatos(turno.TurnoProduccionId);
        long cerrado;
        using (var ctx = _api.NuevoContexto())
        {
            var tp = ctx.TurnosProduccion.Single(t => t.TurnoProduccionId == turno.TurnoProduccionId);
            cerrado = ctx.TurnoProduccionId(_linea2, tp.TurnoId, tp.FechaProduccion.AddDays(-1));   // CERRADO, del día anterior
        }

        // Gerencia y administrador: cualquier turno (en curso o cerrado), sólo lectura, con los 4 indicadores
        foreach (var rol in new[] { RolesSistema.Gerencia, RolesSistema.Administrador })
        {
            var cliente = _api.Cliente("ger-" + rol, rol);
            var r = await cliente.GetAsync($"api/tablero/turno?turnoProduccionId={turno.TurnoProduccionId}");
            Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            var t = (await r.Content.ReadFromJsonAsync<TableroTurnoDto>())!;
            Assert.Equal(turno.TurnoProduccionId, t.TurnoProduccionId);
            Assert.True(t.SoloLectura);
            Assert.True(t.Abierto);
            Assert.Equal(65m, t.Eficiencia.UmbralRojo);
            Assert.Equal("SIN_DATOS", t.Produccion.Bolsones.Estado);          // sin cámara de bolsones: nunca 0 %
            Assert.NotEmpty(t.Velocidad.MetrosPorMinuto);
            Assert.Equal(670m, t.Velocidad.Referencia);
            var sinDeclarar = t.DetencionesPorTipo.Barras.Single(b => b.SinDeclarar);
            Assert.Equal(1, sinDeclarar.Cantidad);
            Assert.InRange(t.DetencionesPorTipo.MinutosPendientes, 29, 31); // parada de cámara hace 30 min, sigue parada
            Assert.Equal(t.DetencionesPorTipo.MinutosPendientes + 12, t.DetencionesPorTipo.MinutosTotal);

            var c = await cliente.GetAsync($"api/tablero/turno?turnoProduccionId={cerrado}");
            Assert.True(c.StatusCode == HttpStatusCode.OK, await c.Content.ReadAsStringAsync());
            var tc = (await c.Content.ReadFromJsonAsync<TableroTurnoDto>())!;
            Assert.False(tc.Abierto);
            Assert.Equal(_linea2, tc.LineaId);
            Assert.Equal(30, tc.DetencionesPorTipo.MinutosTotal);

            Assert.Equal(HttpStatusCode.NotFound, (await cliente.GetAsync("api/tablero/turno?turnoProduccionId=999999")).StatusCode);
        }

        // Supervisor: alcance de T-802. Con la línea 2 asignada no ve la 1; sin líneas asignadas, todas.
        AsignarLineas("sup-l2", _linea2);
        var supL2 = _api.Cliente("sup-l2", RolesSistema.Supervisor);
        Assert.Equal(HttpStatusCode.Forbidden, (await supL2.GetAsync($"api/tablero/turno?turnoProduccionId={turno.TurnoProduccionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await supL2.GetAsync($"api/tablero/turno?lineaId={_linea1}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await supL2.GetAsync($"api/tablero/turno?turnoProduccionId={cerrado}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _api.Cliente("sup-todas", RolesSistema.Supervisor)
            .GetAsync($"api/tablero/turno?turnoProduccionId={turno.TurnoProduccionId}")).StatusCode);

        // Mantenimiento: sin el permiso gerencial
        Assert.Equal(HttpStatusCode.Forbidden, (await _api.Cliente("mant-ger", RolesSistema.Mantenimiento)
            .GetAsync($"api/tablero/turno?turnoProduccionId={turno.TurnoProduccionId}")).StatusCode);
    }

    [Fact]
    public async Task Gerencial_ElOperadorNoPuedePedirOtroTurno()
    {
        var turno = await AbrirAsync();
        long otro;
        using (var ctx = _api.NuevoContexto())
        {
            var tp = ctx.TurnosProduccion.Single(t => t.TurnoProduccionId == turno.TurnoProduccionId);
            otro = ctx.TurnoProduccionId(_linea2, tp.TurnoId, tp.FechaProduccion.AddDays(-1));
        }

        // Pida el turno que pida, el filtro de turno obligatorio (T-802: en las lecturas fija turnoProduccionId al suyo)
        // le devuelve SU turno, con carga habilitada: nunca ve el otro.
        foreach (var pedido in new[] { otro, turno.TurnoProduccionId })
        {
            var r = await Operario().GetAsync($"api/tablero/turno?turnoProduccionId={pedido}");
            Assert.True(r.StatusCode == HttpStatusCode.OK, await r.Content.ReadAsStringAsync());
            var t = (await r.Content.ReadFromJsonAsync<TableroTurnoDto>())!;
            Assert.Equal(turno.TurnoProduccionId, t.TurnoProduccionId);
            Assert.False(t.SoloLectura);
        }

        // Ni el listado del gerencial
        Assert.Equal(HttpStatusCode.Forbidden, (await Operario().GetAsync("api/tablero/turnos")).StatusCode);
    }

    [Fact]
    public async Task Gerencial_ListaDeTurnosDelDia_PorAlcance()
    {
        var turno = await AbrirAsync();
        DateOnly fecha;
        using (var ctx = _api.NuevoContexto())
            fecha = ctx.TurnosProduccion.Single(t => t.TurnoProduccionId == turno.TurnoProduccionId).FechaProduccion;

        var ger = await _api.Cliente("ger-lista", RolesSistema.Gerencia).GetFromJsonAsync<TurnosTableroDto>($"api/tablero/turnos?fecha={fecha:yyyy-MM-dd}");
        Assert.Equal(fecha, ger!.Fecha);
        Assert.Contains(ger.Lineas, l => l.LineaId == _linea1);
        var item = Assert.Single(ger.Turnos, t => t.TurnoProduccionId == turno.TurnoProduccionId);
        Assert.True(item.Abierto);
        Assert.Equal(_linea1, item.LineaId);

        AsignarLineas("sup-lista", _linea2);
        var sup = await _api.Cliente("sup-lista", RolesSistema.Supervisor).GetFromJsonAsync<TurnosTableroDto>($"api/tablero/turnos?fecha={fecha:yyyy-MM-dd}");
        Assert.Equal([_linea2], sup!.Lineas.Select(l => l.LineaId));
        Assert.DoesNotContain(sup.Turnos, t => t.TurnoProduccionId == turno.TurnoProduccionId);
    }
}
