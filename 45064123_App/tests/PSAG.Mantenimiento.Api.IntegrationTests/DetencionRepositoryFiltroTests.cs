using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Filtro de duración mínima del listado paginado (lo usa el detalle de Fallos mayores).
/// </summary>
public class DetencionRepositoryFiltroTests : IDisposable
{
    private const string Usuario = "usuario-test";

    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;

    public DetencionRepositoryFiltroTests()
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

    [Fact]
    public async Task MinutosMinimos_DejaSoloLasDetencionesDeEsaDuracionOMas()
    {
        var linea = new Linea { Codigo = "T2", Nombre = "Tango 2", CreadoPorUsuarioId = Usuario, ModificadoPorUsuarioId = Usuario };
        var turno = new Turno
        {
            Codigo = "T1", Nombre = "Turno 1", HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0),
            CreadoPorUsuarioId = Usuario, ModificadoPorUsuarioId = Usuario
        };
        _context.AddRange(linea, turno);
        await _context.SaveChangesAsync();

        foreach (var (codigo, minutos) in new[] { ("DET-1", 50), ("DET-2", 60), ("DET-3", 255) })
        {
            _context.Add(new Detencion
            {
                Codigo = codigo,
                LineaId = linea.LineaId,
                TurnoId = turno.TurnoId,
                Fecha = new DateOnly(2026, 3, 27),
                OrigenTiempo = OrigenTiempo.DECLARADO,
                MinutosDeclarados = minutos,
                MinutosEfectivos = minutos,
                Descripcion = "Prueba",
                CreadoPorUsuarioId = Usuario,
                ModificadoPorUsuarioId = Usuario
            });
        }
        await _context.SaveChangesAsync();

        var (items, total) = await new DetencionRepository(_context).GetPaginadoAsync(1, 10, minutosMinimos: 60);

        Assert.Equal(2, total);
        Assert.Equal(new[] { "DET-2", "DET-3" }, items.Select(d => d.Codigo).OrderBy(c => c));
    }
}
