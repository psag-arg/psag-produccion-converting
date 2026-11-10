using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Application.DTOs.Proceso;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-545 · Control de Proceso: plantilla por línea cargada desde el maestro y carga del maquinista validada por tipo.
/// </summary>
public class ControlProcesoTests : IDisposable
{
    private const string Admin = "admin-test";
    private const string Maquinista = "maquinista-test";

    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;
    private readonly ControlProcesoPlantillaService _plantilla;
    private readonly RegistroProcesoService _carga;
    private readonly Linea _tango2;

    public ControlProcesoTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(_conexion).Options);
        _context.Database.EnsureCreated();

        var uow = new UnitOfWork(_context);
        _plantilla = new ControlProcesoPlantillaService(uow, NullLogger<ControlProcesoPlantillaService>.Instance);
        _carga = new RegistroProcesoService(uow, NullLogger<RegistroProcesoService>.Instance);

        _tango2 = new Linea { Codigo = "T2", Nombre = "Tango 2", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        // 00:00 a 00:00 = turno que cubre todo el día: el test no depende de la hora en que corre.
        var turno = new Turno
        {
            Codigo = "T1", Nombre = "Turno 1 (Mañana)", HoraInicio = new TimeOnly(0, 0), HoraFin = new TimeOnly(0, 0),
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        _context.AddRange(_tango2, turno);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _conexion.Dispose();
    }

    [Fact]
    public async Task SinPuntosCargados_NoSePuedeIniciarElControl()
    {
        var r = await _carga.IniciarRegistroAsync(new IniciarRegistroProcesoRequest { LineaId = _tango2.LineaId }, Maquinista);

        Assert.False(r.IsSuccess);
        Assert.Equal("SIN_PLANTILLA", r.ErrorCode);
    }

    [Fact]
    public async Task Maestro_RechazaPuntoConMinimoMayorQueMaximo()
    {
        var grupoId = (await _plantilla.CrearGrupoAsync(new GuardarGrupoControlRequest { LineaId = _tango2.LineaId, Nombre = "Rebobinadora" }, Admin)).Value;

        var r = await _plantilla.CrearPuntoAsync(new GuardarPuntoControlRequest
        {
            GrupoParametroId = grupoId, Nombre = "Presión de soplo", TipoDato = "NUMERICO", ValorMin = 5, ValorMax = 4
        }, Admin);

        Assert.False(r.IsSuccess);
    }

    [Fact]
    public async Task ConPuntosCargados_ElMaquinistaCargaTodoConValidacionPorTipo()
    {
        var (presionId, aireId, loteId) = await CargarPlantillaAsync();

        var inicio = await _carga.IniciarRegistroAsync(new IniciarRegistroProcesoRequest { LineaId = _tango2.LineaId }, Maquinista);
        Assert.True(inicio.IsSuccess, inicio.Error);

        var control = (await _carga.ObtenerCompletoAsync(inicio.Value!.RegistroProcesoId)).Value!;
        Assert.Equal("Sin producto", control.ProductoNombre);
        Assert.Equal(3, control.CantidadParametros);
        var presion = control.Grupos.SelectMany(g => g.Detalles).Single(d => d.ParametroId == presionId);
        Assert.Equal(3.8m, presion.ValorMin);
        Assert.Equal("CADA_2_HORAS", presion.Frecuencia);

        // Fuera de rango sin observación: no se guarda nada (todo o nada).
        var rechazo = await _carga.GuardarValoresAsync(new GuardarValoresRequest
        {
            RegistroProcesoId = control.RegistroProcesoId,
            RowVersion = control.RowVersion,
            Valores =
            {
                new ValorParametroRequest { ParametroId = presionId, ValorReal = 6m },
                new ValorParametroRequest { ParametroId = loteId, ValorRealTexto = "Lote 1" }
            }
        }, Maquinista);
        Assert.False(rechazo.IsSuccess);
        Assert.Contains("Presión de soplo", rechazo.Error);
        Assert.Equal(0, (await _carga.ObtenerCompletoAsync(control.RegistroProcesoId)).Value!.CantidadCompletados);

        // OK / NO OK con un valor que no corresponde
        var malOk = await _carga.GuardarValoresAsync(new GuardarValoresRequest
        {
            RegistroProcesoId = control.RegistroProcesoId,
            RowVersion = control.RowVersion,
            Valores = { new ValorParametroRequest { ParametroId = aireId, ValorRealTexto = "más o menos" } }
        }, Maquinista);
        Assert.False(malOk.IsSuccess);

        // Todo válido
        var ok = await _carga.GuardarValoresAsync(new GuardarValoresRequest
        {
            RegistroProcesoId = control.RegistroProcesoId,
            RowVersion = control.RowVersion,
            Valores =
            {
                new ValorParametroRequest { ParametroId = presionId, ValorReal = 6m, Observacion = "Ajuste por cambio de bobina" },
                new ValorParametroRequest { ParametroId = aireId, ValorRealTexto = "no ok", Observacion = "Pérdida en la manguera" },
                new ValorParametroRequest { ParametroId = loteId, ValorRealTexto = " Lote 1 " }
            }
        }, Maquinista);
        Assert.True(ok.IsSuccess, ok.Error);

        var guardado = (await _carga.ObtenerCompletoAsync(control.RegistroProcesoId)).Value!;
        var detalles = guardado.Grupos.SelectMany(g => g.Detalles).ToDictionary(d => d.ParametroId);
        Assert.Equal("COMPLETO", guardado.Estado);
        Assert.Equal(2, guardado.CantidadDesvios);
        Assert.Equal("3,8 - 4,1 bar", detalles[presionId].ValorEstandarSnapshot);
        Assert.Equal("NO OK", detalles[aireId].ValorRealTexto);
        Assert.Equal("Lote 1", detalles[loteId].ValorRealTexto);
    }

    [Fact]
    public async Task PuntoDesactivadoConValores_SigueVisibleYNoCambiaDeTipo()
    {
        var (presionId, aireId, loteId) = await CargarPlantillaAsync();
        var inicio = (await _carga.IniciarRegistroAsync(new IniciarRegistroProcesoRequest { LineaId = _tango2.LineaId }, Maquinista)).Value!;
        var control = (await _carga.ObtenerCompletoAsync(inicio.RegistroProcesoId)).Value!;
        await _carga.GuardarValoresAsync(new GuardarValoresRequest
        {
            RegistroProcesoId = control.RegistroProcesoId,
            RowVersion = control.RowVersion,
            Valores = { new ValorParametroRequest { ParametroId = loteId, ValorRealTexto = "Lote 9" } }
        }, Maquinista);

        var grupoId = _context.Parametros.Single(p => p.ParametroId == loteId).GrupoParametroId;
        var cambioTipo = await _plantilla.ActualizarPuntoAsync(loteId, new GuardarPuntoControlRequest
        {
            GrupoParametroId = grupoId, Nombre = "Lote de papel", TipoDato = "NUMERICO"
        }, Admin);
        Assert.False(cambioTipo.IsSuccess);

        var baja = await _plantilla.ActualizarPuntoAsync(loteId, new GuardarPuntoControlRequest
        {
            GrupoParametroId = grupoId, Nombre = "Lote de papel", TipoDato = "TEXTO", Activo = false
        }, Admin);
        Assert.True(baja.IsSuccess, baja.Error);

        var despues = (await _carga.ObtenerCompletoAsync(control.RegistroProcesoId)).Value!;
        var lote = despues.Grupos.SelectMany(g => g.Detalles).Single(d => d.ParametroId == loteId);
        Assert.True(lote.FueraDePlantilla);
        Assert.Equal("Lote 9", lote.ValorRealTexto);
    }

    [Fact]
    public async Task ValorCargadoSinEstandar_SigueFaltaEstandarAunqueDespuesSeCargueElEstandar()
    {
        // Auditoría C: 160 cargado sin estándar (sin desvío); después se carga 152 - 153. Reabrir el registro viejo
        // no puede decir OK: ese día no había contra qué comparar.
        var grupo = (await _plantilla.CrearGrupoAsync(new GuardarGrupoControlRequest { LineaId = _tango2.LineaId, Nombre = "Cortadora" }, Admin)).Value;
        var velocidadId = (await _plantilla.CrearPuntoAsync(new GuardarPuntoControlRequest
        {
            GrupoParametroId = grupo, Nombre = "Velocidad", TipoDato = "NUMERICO", Orden = 1
        }, Admin)).Value;
        var otroId = (await _plantilla.CrearPuntoAsync(new GuardarPuntoControlRequest
        {
            GrupoParametroId = grupo, Nombre = "Tensión", TipoDato = "NUMERICO", Orden = 2
        }, Admin)).Value;

        var inicio = (await _carga.IniciarRegistroAsync(new IniciarRegistroProcesoRequest { LineaId = _tango2.LineaId }, Maquinista)).Value!;
        var control = (await _carga.ObtenerCompletoAsync(inicio.RegistroProcesoId)).Value!;
        var guardar = await _carga.GuardarValoresAsync(new GuardarValoresRequest
        {
            RegistroProcesoId = control.RegistroProcesoId,
            RowVersion = control.RowVersion,
            Valores = { new ValorParametroRequest { ParametroId = velocidadId, ValorReal = 160m } }
        }, Maquinista);
        Assert.True(guardar.IsSuccess, guardar.Error);

        var conEstandar = await _plantilla.ActualizarPuntoAsync(velocidadId, new GuardarPuntoControlRequest
        {
            GrupoParametroId = grupo, Nombre = "Velocidad", TipoDato = "NUMERICO", ValorMin = 152m, ValorMax = 153m, Orden = 1
        }, Admin);
        Assert.True(conEstandar.IsSuccess, conEstandar.Error);
        await _plantilla.ActualizarPuntoAsync(otroId, new GuardarPuntoControlRequest
        {
            GrupoParametroId = grupo, Nombre = "Tensión", TipoDato = "NUMERICO", ValorMin = 1m, ValorMax = 2m, Orden = 2
        }, Admin);
        _context.ChangeTracker.Clear();

        var reabierto = (await _carga.ObtenerCompletoAsync(control.RegistroProcesoId)).Value!;
        var velocidad = reabierto.Grupos.SelectMany(g => g.Detalles).Single(d => d.ParametroId == velocidadId);
        Assert.Equal(160m, velocidad.ValorReal);
        Assert.False(velocidad.FueraDeEstandar);
        Assert.Null(velocidad.ValorEstandarSnapshot);
        Assert.False(velocidad.TieneRango);
        Assert.Equal("SIN_ESTANDAR", velocidad.Evaluacion);
        // El punto sin valor sí muestra el estándar de hoy (todavía no se cargó).
        Assert.True(reabierto.Grupos.SelectMany(g => g.Detalles).Single(d => d.ParametroId == otroId).TieneRango);

        // Datos guardados antes del script 29 (EstandarConRango NULL): sin texto de estándar, tampoco es OK.
        var detalle = _context.RegistrosProcesoDetalle.Single(d => d.ParametroId == velocidadId);
        Assert.False(detalle.EstandarConRango);
        detalle.EstandarConRango = null;
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        var legado = (await _carga.ObtenerCompletoAsync(control.RegistroProcesoId)).Value!;
        Assert.Equal("SIN_ESTANDAR", legado.Grupos.SelectMany(g => g.Detalles).Single(d => d.ParametroId == velocidadId).Evaluacion);

        // Si el maquinista corrige el valor, se vuelve a comparar contra el estándar de ahora y queda ese snapshot.
        var corregido = await _carga.GuardarValoresAsync(new GuardarValoresRequest
        {
            RegistroProcesoId = control.RegistroProcesoId,
            RowVersion = legado.RowVersion,
            Valores = { new ValorParametroRequest { ParametroId = velocidadId, ValorReal = 152.5m } }
        }, Maquinista);
        Assert.True(corregido.IsSuccess, corregido.Error);
        _context.ChangeTracker.Clear();
        var final = (await _carga.ObtenerCompletoAsync(control.RegistroProcesoId)).Value!
            .Grupos.SelectMany(g => g.Detalles).Single(d => d.ParametroId == velocidadId);
        Assert.Equal("152 - 153", final.ValorEstandarSnapshot);
        Assert.Equal("OK", final.Evaluacion);
    }

    private async Task<(int Presion, int Aire, int Lote)> CargarPlantillaAsync()
    {
        var grupo = await _plantilla.CrearGrupoAsync(new GuardarGrupoControlRequest { LineaId = _tango2.LineaId, Nombre = "Rebobinadora", Orden = 1 }, Admin);
        Assert.True(grupo.IsSuccess, grupo.Error);

        async Task<int> Punto(GuardarPuntoControlRequest req)
        {
            req.GrupoParametroId = grupo.Value;
            var r = await _plantilla.CrearPuntoAsync(req, Admin);
            Assert.True(r.IsSuccess, r.Error);
            return r.Value;
        }

        var presion = await Punto(new GuardarPuntoControlRequest
        {
            Nombre = "Presión de soplo", TipoDato = "NUMERICO", UnidadMedida = "bar", ValorMin = 3.8m, ValorMax = 4.1m, Frecuencia = "CADA_2_HORAS", Orden = 1
        });
        var aire = await Punto(new GuardarPuntoControlRequest { Nombre = "Sin pérdidas de aire", TipoDato = "OK_NOOK", Orden = 2 });
        var lote = await Punto(new GuardarPuntoControlRequest { Nombre = "Lote de papel", TipoDato = "TEXTO", Orden = 3 });
        return (presion, aire, lote);
    }
}
