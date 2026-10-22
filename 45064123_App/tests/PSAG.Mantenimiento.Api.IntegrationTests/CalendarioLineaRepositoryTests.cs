using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-542: el calendario base por línea se guarda y se lee con sus turnos; una sola versión activa por fecha.
/// </summary>
public class CalendarioLineaRepositoryTests : IDisposable
{
    private const string Usuario = "usuario-test";

    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;

    public CalendarioLineaRepositoryTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(_conexion).Options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _conexion.Dispose();
    }

    private async Task<(Linea Linea, Turno Turno)> SembrarAsync()
    {
        var linea = new Linea { Codigo = "T2", Nombre = "Tango 2", CreadoPorUsuarioId = Usuario, ModificadoPorUsuarioId = Usuario };
        var turno = new Turno
        {
            Codigo = "T1", Nombre = "Turno 1", HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0),
            CreadoPorUsuarioId = Usuario, ModificadoPorUsuarioId = Usuario
        };
        _context.AddRange(linea, turno);
        await _context.SaveChangesAsync();
        return (linea, turno);
    }

    private static CalendarioLinea Version(int lineaId, int turnoId, DateOnly desde, bool activo = true) => new()
    {
        LineaId = lineaId,
        VigenteDesde = desde,
        Activo = activo,
        CreadoPorUsuarioId = Usuario,
        ModificadoPorUsuarioId = Usuario,
        Turnos = [new CalendarioLineaTurno { DiaSemana = DayOfWeek.Saturday, TurnoId = turnoId }]
    };

    [Fact]
    public async Task GetActivos_DevuelveLasVersionesActivasConSusTurnos()
    {
        var (linea, turno) = await SembrarAsync();
        _context.AddRange(
            Version(linea.LineaId, turno.TurnoId, new DateOnly(2026, 10, 1)),
            Version(linea.LineaId, turno.TurnoId, new DateOnly(2026, 10, 1), activo: false));
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var versiones = await new CalendarioLineaRepository(_context).GetActivosAsync(linea.LineaId);

        var version = Assert.Single(versiones);
        var fila = Assert.Single(version.Turnos);
        Assert.Equal(DayOfWeek.Saturday, fila.DiaSemana);
        Assert.Equal(turno.TurnoId, fila.TurnoId);
    }

    [Fact]
    public async Task DosVersionesActivasMismaFecha_SeRechazan()
    {
        var (linea, turno) = await SembrarAsync();
        _context.AddRange(
            Version(linea.LineaId, turno.TurnoId, new DateOnly(2026, 10, 1)),
            Version(linea.LineaId, turno.TurnoId, new DateOnly(2026, 10, 1)));

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }
}
