using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Filtro de duración mínima del listado paginado (lo usa el detalle de Fallos mayores).
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class DetencionRepositoryFiltroTests : IDisposable
{
    private const string Usuario = "usuario-test";

    private readonly BaseDePrueba _base;
    private readonly MantenimientoDbContext _context;

    public DetencionRepositoryFiltroTests()
    {
        _base = BaseDePrueba.Crear();
        _context = _base.Context;
    }

    public void Dispose()
    {
        _context.Dispose();
        _base.Dispose();
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
                TurnoProduccionId = _context.TurnoProduccionId(linea.LineaId, turno.TurnoId, new DateOnly(2026, 3, 27)),
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

    private async Task<(Linea Linea, Turno Turno)> LineaYTurnoAsync()
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

    private Detencion Nueva(Linea linea, Turno turno, string codigo, OrigenRegistroDetencion origen, DateTimeOffset? parada = null) => new()
    {
        Codigo = codigo, LineaId = linea.LineaId, TurnoProduccionId = _context.TurnoProduccionId(linea.LineaId, turno.TurnoId, new DateOnly(2026, 11, 13)),
        OrigenTiempo = parada == null ? OrigenTiempo.DECLARADO : OrigenTiempo.CALCULADO, FechaHoraParada = parada,
        MinutosEfectivos = 10, Descripcion = "Prueba", OrigenRegistro = origen,
        EstadoActual = origen == OrigenRegistroDetencion.CAMARA ? EstadoDetencion.PENDIENTE_JUSTIFICACION : EstadoDetencion.REGISTRADA,
        CreadoPorUsuarioId = Usuario, ModificadoPorUsuarioId = Usuario
    };

    /// <summary>T-786: el listado filtra por origen (manual / cámara).</summary>
    [Fact]
    public async Task OrigenRegistro_FiltraManualesYDeCamara()
    {
        var (linea, turno) = await LineaYTurnoAsync();
        _context.AddRange(
            Nueva(linea, turno, "DET-M1", OrigenRegistroDetencion.MANUAL),
            Nueva(linea, turno, "DET-M2", OrigenRegistroDetencion.MANUAL),
            Nueva(linea, turno, "DET-C1", OrigenRegistroDetencion.CAMARA));
        await _context.SaveChangesAsync();
        var repo = new DetencionRepository(_context);

        var (manuales, totalManuales) = await repo.GetPaginadoAsync(1, 10, origenRegistro: OrigenRegistroDetencion.MANUAL);
        var (camara, _) = await repo.GetPaginadoAsync(1, 10, origenRegistro: OrigenRegistroDetencion.CAMARA);
        var (todas, totalTodas) = await repo.GetPaginadoAsync(1, 10);

        Assert.Equal(2, totalManuales);
        Assert.All(manuales, d => Assert.Equal(OrigenRegistroDetencion.MANUAL, d.OrigenRegistro));
        Assert.Equal("DET-C1", Assert.Single(camara).Codigo);
        Assert.Equal(3, totalTodas);
    }

    /// <summary>T-786: candidatas a superponerse con una carga manual: de la línea, con hora de parada en el rango, sin eliminadas.</summary>
    [Fact]
    public async Task ConParadaEntre_DeLaLineaEnElRango()
    {
        var (linea, turno) = await LineaYTurnoAsync();
        var otra = new Linea { Codigo = "F2", Nombre = "Futura 2", CreadoPorUsuarioId = Usuario, ModificadoPorUsuarioId = Usuario };
        _context.Add(otra);
        await _context.SaveChangesAsync();
        var t0 = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.FromHours(-3));
        var eliminada = Nueva(linea, turno, "DET-ELIM", OrigenRegistroDetencion.MANUAL, t0.AddMinutes(20));
        eliminada.Eliminada = true;
        _context.AddRange(
            Nueva(linea, turno, "DET-ADENTRO", OrigenRegistroDetencion.CAMARA, t0.AddMinutes(10)),
            Nueva(linea, turno, "DET-ANTES", OrigenRegistroDetencion.MANUAL, t0.AddHours(-2)),
            Nueva(linea, turno, "DET-SIN-HORA", OrigenRegistroDetencion.MANUAL),
            Nueva(otra, turno, "DET-OTRA-LINEA", OrigenRegistroDetencion.MANUAL, t0.AddMinutes(10)),
            eliminada);
        await _context.SaveChangesAsync();

        var r = await new DetencionRepository(_context).GetConParadaEntreAsync(linea.LineaId, t0.AddHours(-1), t0.AddHours(1));

        Assert.Equal("DET-ADENTRO", Assert.Single(r).Codigo);
    }
}
