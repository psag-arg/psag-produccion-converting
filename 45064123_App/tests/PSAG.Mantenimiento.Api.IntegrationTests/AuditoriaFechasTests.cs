using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Data;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Fechas de auditoría completadas por MantenimientoDbContext al guardar (SQLite en memoria).
/// </summary>
public class AuditoriaFechasTests : IDisposable
{
    private const string Usuario = "usuario-test";

    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;

    public AuditoriaFechasTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();

        var options = new DbContextOptionsBuilder<MantenimientoDbContext>()
            .UseSqlite(_conexion)
            .Options;

        _context = new MantenimientoDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _conexion.Dispose();
    }

    private static Linea NuevaLinea(string codigo) => new()
    {
        Codigo = codigo,
        Nombre = $"Linea {codigo}",
        CreadoPorUsuarioId = Usuario,
        ModificadoPorUsuarioId = Usuario
    };

    [Fact]
    public async Task Alta_CompletaFechaCreacionYModificacion_CuandoLleganPorDefecto()
    {
        var antes = DateTimeOffset.UtcNow;
        _context.Lineas.Add(NuevaLinea("L1"));
        await _context.SaveChangesAsync();
        var despues = DateTimeOffset.UtcNow;

        var linea = await _context.Lineas.AsNoTracking().SingleAsync();

        Assert.InRange(linea.FechaHoraCreacion, antes.AddSeconds(-1), despues.AddSeconds(1));
        Assert.InRange(linea.FechaHoraModificacion, antes.AddSeconds(-1), despues.AddSeconds(1));
    }

    [Fact]
    public async Task Alta_RespetaFechaCreacionExplicita()
    {
        var fechaFija = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var linea = NuevaLinea("L2");
        linea.FechaHoraCreacion = fechaFija;
        _context.Lineas.Add(linea);
        await _context.SaveChangesAsync();

        var guardada = await _context.Lineas.AsNoTracking().SingleAsync();

        Assert.Equal(fechaFija, guardada.FechaHoraCreacion);
        Assert.InRange(guardada.FechaHoraModificacion, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Edicion_ActualizaFechaModificacion_yNoTocaCreacion()
    {
        var fechaFija = new DateTimeOffset(2020, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var linea = NuevaLinea("L3");
        linea.FechaHoraCreacion = fechaFija;
        linea.FechaHoraModificacion = fechaFija;
        _context.Lineas.Add(linea);
        await _context.SaveChangesAsync();

        linea.Nombre = "Linea L3 editada";
        var antes = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        var despues = DateTimeOffset.UtcNow;

        var guardada = await _context.Lineas.AsNoTracking().SingleAsync();

        Assert.Equal(fechaFija, guardada.FechaHoraCreacion);
        Assert.InRange(guardada.FechaHoraModificacion, antes.AddSeconds(-1), despues.AddSeconds(1));
    }

    [Fact]
    public async Task Detencion_Historial_Nota_yAuditoria_CompletanSuFecha()
    {
        var linea = NuevaLinea("L4");
        var turno = new Turno
        {
            Codigo = "T1",
            Nombre = "Turno 1",
            HoraInicio = new TimeOnly(6, 0),
            HoraFin = new TimeOnly(14, 0),
            CreadoPorUsuarioId = Usuario,
            ModificadoPorUsuarioId = Usuario
        };
        _context.Lineas.Add(linea);
        _context.Turnos.Add(turno);
        await _context.SaveChangesAsync();

        var detencion = new Detencion
        {
            Codigo = "DET-TEST-000001",
            LineaId = linea.LineaId,
            TurnoId = turno.TurnoId,
            Fecha = DateOnly.FromDateTime(DateTime.UtcNow),
            MinutosEfectivos = 30,
            OrigenTiempo = OrigenTiempo.DECLARADO,
            Descripcion = "Prueba de auditoría",
            CreadoPorUsuarioId = Usuario,
            ModificadoPorUsuarioId = Usuario
        };
        detencion.HistorialEstados.Add(new DetencionEstadoHistorial
        {
            EstadoNuevo = EstadoDetencion.REGISTRADA,
            UsuarioId = Usuario,
            OrigenAccion = "API"
        });
        detencion.Notas.Add(new DetencionNota
        {
            Texto = "Nota de prueba",
            UsuarioId = Usuario,
            TipoNota = TipoNota.Operativa
        });
        _context.Detenciones.Add(detencion);
        _context.Auditoria.Add(new Auditoria
        {
            UsuarioId = Usuario,
            Accion = TipoAccionAuditoria.Crear,
            Entidad = "Detencion"
        });

        var antes = DateTimeOffset.UtcNow;
        await _context.SaveChangesAsync();
        var despues = DateTimeOffset.UtcNow;

        var guardada = await _context.Detenciones
            .Include(d => d.HistorialEstados)
            .Include(d => d.Notas)
            .AsNoTracking()
            .SingleAsync();
        var auditoria = await _context.Auditoria.AsNoTracking().SingleAsync();

        Assert.InRange(guardada.FechaHoraCreacion, antes.AddSeconds(-1), despues.AddSeconds(1));
        Assert.InRange(guardada.FechaHoraModificacion, antes.AddSeconds(-1), despues.AddSeconds(1));
        Assert.All(guardada.HistorialEstados, h => Assert.InRange(h.FechaHora, antes.AddSeconds(-1), despues.AddSeconds(1)));
        Assert.All(guardada.Notas, n => Assert.InRange(n.FechaHora, antes.AddSeconds(-1), despues.AddSeconds(1)));
        Assert.InRange(auditoria.FechaHora, antes.AddSeconds(-1), despues.AddSeconds(1));
    }
}
