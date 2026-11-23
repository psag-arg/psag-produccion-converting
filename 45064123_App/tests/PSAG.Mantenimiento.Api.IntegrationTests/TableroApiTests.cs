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

        // Gerencia y administrador no tienen VER del tablero en la matriz migrada: 403 "Sin permiso"
        foreach (var rol in new[] { RolesSistema.Gerencia, RolesSistema.Administrador })
        {
            var prohibido = await _api.Cliente("usr-" + rol, rol).GetAsync($"api/tablero/turno?lineaId={_linea1}");
            Assert.Equal(HttpStatusCode.Forbidden, prohibido.StatusCode);
            Assert.Contains("Sin permiso", await prohibido.Content.ReadAsStringAsync());
        }
    }
}
