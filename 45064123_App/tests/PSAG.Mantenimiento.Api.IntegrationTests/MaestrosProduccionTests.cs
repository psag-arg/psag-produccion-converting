using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-798 etapa 1 · Maestros de producción contra SQLite en memoria: SKU (FK NOT NULL al maestro y CHECKs por tipo),
/// parametrización línea-producto (lista de productos de Control de Proceso) y turnos de la semilla de DEV.
/// </summary>
public class MaestrosProduccionTests : IDisposable
{
    private const string Admin = "admin-test";

    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;
    private readonly Linea _tango2;
    private readonly Linea _futura2;
    private readonly ProductoMaestro _sh30;
    private readonly ProductoMaestro _sh50;

    public MaestrosProduccionTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(_conexion).Options);
        _context.Database.EnsureCreated();

        _tango2 = new Linea { Codigo = "T2", Nombre = "Tango 2", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _futura2 = new Linea { Codigo = "F2", Nombre = "Futura 2", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _sh30 = new ProductoMaestro
        {
            Codigo = "HIGIENICO 30 MTS", Nombre = "Higiénico 30 mts", Orden = 1, MetrosPorManga = 30, RollosPorManga = 28, RollosPorPaquete = 4,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        _sh50 = new ProductoMaestro
        {
            Codigo = "HIGIENICO 50 MTS", Nombre = "Higiénico 50 mts", Orden = 2, MetrosPorManga = 50, RollosPorPaquete = 4,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        _context.AddRange(_tango2, _futura2, _sh30, _sh50);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _conexion.Dispose();
    }

    // ===================== SKU =====================

    private ProductoSku Bolson(string codigo, int? paquetes = 12) => new()
    {
        ProductoMaestroId = _sh30.ProductoMaestroId, CodigoSku = codigo, TipoSku = ProductoSku.TipoBolson, PaquetesPorBolson = paquetes
    };

    [Fact]
    public async Task Sku_SinMaestro_SeRechaza()
    {
        _context.ProductosSku.Add(new ProductoSku { ProductoMaestroId = 9999, CodigoSku = "SKU-HUERFANO", TipoSku = ProductoSku.TipoBolson });

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task Sku_ConMaestroNulo_SeRechaza()
    {
        // La columna es NOT NULL: ni por SQL directo se puede cargar un SKU sin maestro.
        var ex = await Assert.ThrowsAsync<SqliteException>(() => _context.Database.ExecuteSqlRawAsync(
            "INSERT INTO ABM_ProductosSku (ProductoMaestroId, CodigoSku, TipoSku, Activo) VALUES (NULL, 'SKU-NULO', 'BOLSON', 1)"));
        Assert.Contains("NOT NULL", ex.Message);
    }

    [Fact]
    public async Task Sku_BolsonYPalletValidos_SeGuardan()
    {
        var bolson = Bolson("SH30-BOLSON");
        _context.ProductosSku.Add(bolson);
        await _context.SaveChangesAsync();

        var pallet = new ProductoSku
        {
            ProductoMaestroId = _sh30.ProductoMaestroId, CodigoSku = "SH30-PALLET", TipoSku = ProductoSku.TipoPallet,
            SkuContenidoId = bolson.ProductoSkuId, BolsonesPorPallet = 40
        };
        _context.ProductosSku.Add(pallet);
        await _context.SaveChangesAsync();

        var leido = await _context.ProductosSku.AsNoTracking().Include(s => s.SkuContenido).SingleAsync(s => s.CodigoSku == "SH30-PALLET");
        Assert.Equal("SH30-BOLSON", leido.SkuContenido!.CodigoSku);
        Assert.Equal(2, await _context.ProductosSku.CountAsync(s => s.ProductoMaestroId == _sh30.ProductoMaestroId));
    }

    [Fact]
    public async Task Sku_CantidadesSinDato_SeAceptan()
    {
        _context.ProductosSku.Add(Bolson("SH30-BOLSON-SIN-DATO", paquetes: null));
        _context.ProductosSku.Add(new ProductoSku { ProductoMaestroId = _sh30.ProductoMaestroId, CodigoSku = "SH30-PALLET-SIN-DATO", TipoSku = ProductoSku.TipoPallet });

        Assert.Equal(2, await _context.SaveChangesAsync());
    }

    public static TheoryData<string, int?, int?, int?> SkusInvalidos => new()
    {
        // tipo, paquetes por bolsón, bolsones por pallet, contenido (1 = el bolsón de prueba)
        { "CAJA", null, null, null },               // tipo desconocido
        { "BOLSON", 12, 40, null },                 // bolsón con bolsones por pallet
        { "BOLSON", 12, null, 1 },                  // bolsón con SKU contenido
        { "PALLET", 12, 40, 1 },                    // pallet con paquetes por bolsón
        { "BOLSON", 0, null, null },                // cantidad no positiva
        { "PALLET", null, -1, 1 },
    };

    [Theory]
    [MemberData(nameof(SkusInvalidos))]
    public async Task Sku_ChecksPorTipo_RechazanCombinacionesInvalidas(string tipo, int? paquetes, int? bolsonesPorPallet, int? contenido)
    {
        var bolson = Bolson("SH30-BOLSON");
        _context.ProductosSku.Add(bolson);
        await _context.SaveChangesAsync();

        _context.ProductosSku.Add(new ProductoSku
        {
            ProductoMaestroId = _sh30.ProductoMaestroId, CodigoSku = "SKU-INVALIDO", TipoSku = tipo,
            PaquetesPorBolson = paquetes, BolsonesPorPallet = bolsonesPorPallet, SkuContenidoId = contenido is null ? null : bolson.ProductoSkuId
        });

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
        Assert.Contains("CHECK", ex.InnerException!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sku_CodigoRepetido_SeRechaza()
    {
        _context.ProductosSku.Add(Bolson("SH30-BOLSON"));
        await _context.SaveChangesAsync();
        _context.ProductosSku.Add(Bolson("SH30-BOLSON"));

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    // ===================== Parametrización línea-producto =====================

    private ParametroLineaProducto Parametro(Linea linea, ProductoMaestro producto, DateOnly desde, bool habilitado = true) => new()
    {
        LineaId = linea.LineaId, ProductoMaestroId = producto.ProductoMaestroId, VigenteDesde = desde, Habilitado = habilitado
    };

    [Fact]
    public async Task ControlDeProceso_ListaLosProductosHabilitadosYVigentesDeLaLinea()
    {
        var otro = new ProductoMaestro { Codigo = "COCINA", Nombre = "Rollo cocina", Orden = 3, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        var inactivo = new ProductoMaestro { Codigo = "VIEJO", Nombre = "Producto discontinuado", Orden = 4, Activo = false, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _context.AddRange(otro, inactivo);
        await _context.SaveChangesAsync();
        _context.ParametrosLineaProducto.AddRange(
            Parametro(_tango2, _sh30, new DateOnly(2026, 1, 1)),
            Parametro(_tango2, _sh50, new DateOnly(2026, 11, 1)),                     // todavía no vigente el 31/10
            Parametro(_tango2, otro, new DateOnly(2026, 1, 1)),
            Parametro(_tango2, otro, new DateOnly(2026, 6, 1), habilitado: false),   // deshabilitado desde junio
            Parametro(_tango2, inactivo, new DateOnly(2026, 1, 1)),
            Parametro(_futura2, _sh50, new DateOnly(2026, 1, 1)));                   // otra línea
        await _context.SaveChangesAsync();

        var repo = new UnitOfWork(_context).ProductosMaestro;

        var octubre = await repo.GetByLineaIdAsync(_tango2.LineaId, new DateOnly(2026, 10, 31));
        Assert.Equal(["HIGIENICO 30 MTS"], octubre.Select(p => p.Codigo));

        var noviembre = await repo.GetByLineaIdAsync(_tango2.LineaId, new DateOnly(2026, 11, 21));
        Assert.Equal(["HIGIENICO 30 MTS", "HIGIENICO 50 MTS"], noviembre.Select(p => p.Codigo));

        var mayo = await repo.GetByLineaIdAsync(_tango2.LineaId, new DateOnly(2026, 5, 31));
        Assert.Equal(["HIGIENICO 30 MTS", "COCINA"], mayo.Select(p => p.Codigo));
    }

    [Fact]
    public async Task Parametro_MismaLineaProductoYVigencia_SeRechaza()
    {
        _context.ParametrosLineaProducto.Add(Parametro(_tango2, _sh30, new DateOnly(2026, 11, 1)));
        await _context.SaveChangesAsync();
        _context.ParametrosLineaProducto.Add(Parametro(_tango2, _sh30, new DateOnly(2026, 11, 1)));

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task Parametro_SinMinutosDeReferencia_Usa480()
    {
        var p = Parametro(_tango2, _sh30, new DateOnly(2026, 11, 1));
        p.MangasObjetivoTurno = 11200;
        p.BolsonesObjetivoTurno = 6500;
        _context.ParametrosLineaProducto.Add(p);
        await _context.SaveChangesAsync();

        var leido = await _context.ParametrosLineaProducto.AsNoTracking().SingleAsync();
        Assert.Equal(480, leido.MinutosTurnoReferencia);
        Assert.Equal(11200, leido.MangasObjetivoTurno);
        Assert.Equal(6500, leido.BolsonesObjetivoTurno);
        Assert.True(leido.Habilitado);
    }

    // ===================== Maestros =====================

    [Fact]
    public async Task ProductoMaestro_GuardaMetrosRollosYTipo()
    {
        _sh30.TipoProducto = "HIGIENICO";
        await _context.SaveChangesAsync();

        var leido = await _context.ProductosMaestro.AsNoTracking().SingleAsync(p => p.Codigo == "HIGIENICO 30 MTS");
        Assert.Equal(30m, leido.MetrosPorManga);
        Assert.Equal(28, leido.RollosPorManga);
        Assert.Equal(4, leido.RollosPorPaquete);
        Assert.Equal("HIGIENICO", leido.TipoProducto);
        Assert.Null((await _context.ProductosMaestro.AsNoTracking().SingleAsync(p => p.Codigo == "HIGIENICO 50 MTS")).RollosPorManga);
    }

    [Fact]
    public async Task TipoFalla_CuentaParaIndicadoresPorDefecto_YSeGuardaEnCero()
    {
        _context.TiposFalla.AddRange(
            new TipoFalla { Codigo = "TF-MEC", Nombre = "Mecánica", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin },
            new TipoFalla { Codigo = "TF-PRV", Nombre = "Preventivo", CuentaParaIndicadores = false, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin });
        await _context.SaveChangesAsync();

        var tipos = await _context.TiposFalla.AsNoTracking().ToDictionaryAsync(t => t.Codigo, t => t.CuentaParaIndicadores);
        Assert.True(tipos["TF-MEC"]);
        Assert.False(tipos["TF-PRV"]);
    }

    // ===================== Turnos de la semilla de DEV =====================

    [Fact]
    public async Task SemillaDev_TurnosDePlanta_05_13_21_DeOchoHoras()
    {
        await DatabaseInitializer.SeedCatalogsAsync(_context, NullLogger.Instance);

        var turnos = await _context.Turnos.AsNoTracking().OrderBy(t => t.Orden).ToListAsync();

        Assert.Equal(["T1", "T2", "T3"], turnos.Select(t => t.Codigo));
        Assert.Equal([new TimeOnly(5, 0), new TimeOnly(13, 0), new TimeOnly(21, 0)], turnos.Select(t => t.HoraInicio));
        Assert.Equal([new TimeOnly(13, 0), new TimeOnly(21, 0), new TimeOnly(5, 0)], turnos.Select(t => t.HoraFin));
        Assert.All(turnos, t => Assert.Equal(480, t.DuracionMinutos()));
        Assert.Equal(3, turnos.Single(t => t.ContieneHora(new TimeOnly(4, 59))).Orden);
        Assert.Equal(1, turnos.Single(t => t.ContieneHora(new TimeOnly(5, 0))).Orden);
    }
}
