using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Common;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-798 etapa 6 · ABM de Maestros → Productos (con SKU) y Parámetros línea + producto contra SQLite en memoria:
/// permisos (sólo administrador), validaciones con mensaje claro (las mismas que los CHECK) y estado de vigencia.
/// </summary>
[Collection(ColeccionBaseDeDatos.Nombre)]
public class MaestrosProduccionServiceTests : IDisposable
{
    private const string Admin = "admin-test";
    private static readonly string[] RolAdmin = [RolesSistema.Administrador];
    private static readonly TimeZoneInfo ZonaPlanta = TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");

    private readonly BaseDePrueba _base;
    private readonly MantenimientoDbContext _context;
    private readonly MaestrosProduccionService _servicio;
    private readonly Linea _tango2;

    public MaestrosProduccionServiceTests()
    {
        _base = BaseDePrueba.Crear();
        _context = _base.Context;
        _tango2 = new Linea { Codigo = "T2", Nombre = "Tango 2", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        _context.Add(_tango2);
        _context.SaveChanges();
        _servicio = new MaestrosProduccionService(new UnitOfWork(_context), ZonaPlanta, NullLogger<MaestrosProduccionService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _base.Dispose();
    }

    private static DateOnly Hoy() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaPlanta).Date);

    private async Task<ProductoMaestroDto> Sh30Async()
    {
        var r = await _servicio.CrearProductoAsync(new GuardarProductoMaestroRequest
        {
            Codigo = " SH30 ", Nombre = "SH 30", MetrosPorManga = 30, RollosPorManga = 28, RollosPorPaquete = 4
        }, Admin, RolAdmin);
        Assert.True(r.IsSuccess, r.ErrorMessage);
        return r.Value!;
    }

    [Fact]
    public async Task Producto_AltaYEdicion_SoloAdministrador()
    {
        var sinPermiso = await _servicio.CrearProductoAsync(new GuardarProductoMaestroRequest { Codigo = "X", Nombre = "X" }, Admin, [RolesSistema.Supervisor]);
        Assert.Equal("FORBIDDEN", sinPermiso.ErrorCode);

        var sh30 = await Sh30Async();
        Assert.Equal("SH30", sh30.Codigo);

        var duplicado = await _servicio.CrearProductoAsync(new GuardarProductoMaestroRequest { Codigo = "SH30", Nombre = "Otro" }, Admin, RolAdmin);
        Assert.Contains("Ya existe", duplicado.ErrorMessage);
        var cero = await _servicio.CrearProductoAsync(new GuardarProductoMaestroRequest { Codigo = "SH50", Nombre = "SH 50", MetrosPorManga = 0 }, Admin, RolAdmin);
        Assert.Contains("vacío si no se sabe", cero.ErrorMessage);

        var baja = await _servicio.ActualizarProductoAsync(sh30.ProductoMaestroId, new GuardarProductoMaestroRequest
        {
            Codigo = "SH30", Nombre = "SH 30", MetrosPorManga = 30, Activo = false
        }, Admin, RolAdmin);
        Assert.True(baja.IsSuccess, baja.ErrorMessage);
        Assert.Empty(await _servicio.ObtenerProductosAsync(incluirInactivos: false));
        Assert.Single(await _servicio.ObtenerProductosAsync(incluirInactivos: true));
    }

    [Fact]
    public async Task Sku_BolsonYPallet_ConJerarquia()
    {
        var sh30 = await Sh30Async();
        var bolson = await _servicio.CrearSkuAsync(new GuardarProductoSkuRequest
        {
            ProductoMaestroId = sh30.ProductoMaestroId, CodigoSku = "B-SH30", TipoSku = "bolson", PaquetesPorBolson = 12,
            BolsonesPorPallet = 99   // se ignora: no es dato del bolsón
        }, RolAdmin);
        Assert.True(bolson.IsSuccess, bolson.ErrorMessage);
        Assert.Null(bolson.Value!.BolsonesPorPallet);

        var pallet = await _servicio.CrearSkuAsync(new GuardarProductoSkuRequest
        {
            ProductoMaestroId = sh30.ProductoMaestroId, CodigoSku = "P-SH30", TipoSku = "PALLET", SkuContenidoId = bolson.Value.ProductoSkuId,
            BolsonesPorPallet = 40, PaquetesPorBolson = 12
        }, RolAdmin);
        Assert.True(pallet.IsSuccess, pallet.ErrorMessage);
        Assert.Equal("B-SH30", pallet.Value!.SkuContenidoCodigo);
        Assert.Null(pallet.Value.PaquetesPorBolson);

        // No se desactiva un bolsón que lleva un pallet activo.
        var baja = await _servicio.ActualizarSkuAsync(bolson.Value.ProductoSkuId, new GuardarProductoSkuRequest
        {
            ProductoMaestroId = sh30.ProductoMaestroId, CodigoSku = "B-SH30", TipoSku = "BOLSON", PaquetesPorBolson = 12, Activo = false
        }, RolAdmin);
        Assert.Contains("pallet activo", baja.ErrorMessage);

        var palletDePallet = await _servicio.CrearSkuAsync(new GuardarProductoSkuRequest
        {
            ProductoMaestroId = sh30.ProductoMaestroId, CodigoSku = "P2", TipoSku = "PALLET", SkuContenidoId = pallet.Value.ProductoSkuId
        }, RolAdmin);
        Assert.Contains("SKU bolsón", palletDePallet.ErrorMessage);

        var producto = (await _servicio.ObtenerProductosAsync(false)).Single();
        Assert.Equal(["B-SH30", "P-SH30"], producto.Skus.Select(s => s.CodigoSku));
    }

    [Fact]
    public async Task Parametro_Vigencias_YUnicidad()
    {
        var sh30 = await Sh30Async();
        GuardarParametroLineaProductoRequest Req(DateOnly desde, int? mangas) => new()
        {
            LineaId = _tango2.LineaId, ProductoMaestroId = sh30.ProductoMaestroId, VigenteDesde = desde, MangasObjetivoTurno = mangas, BolsonesObjetivoTurno = 6500
        };

        Assert.True((await _servicio.CrearParametroAsync(Req(Hoy().AddDays(-30), 11000), RolAdmin)).IsSuccess);
        Assert.True((await _servicio.CrearParametroAsync(Req(Hoy().AddDays(-1), 11200), RolAdmin)).IsSuccess);
        var futura = await _servicio.CrearParametroAsync(Req(Hoy().AddDays(10), 12000), RolAdmin);
        Assert.True(futura.IsSuccess, futura.ErrorMessage);
        Assert.Equal(EstadoVigenciaParametro.Futura, futura.Value!.EstadoVigencia);

        var repetida = await _servicio.CrearParametroAsync(Req(Hoy().AddDays(-1), 1), RolAdmin);
        Assert.Contains("Ya hay una vigencia", repetida.ErrorMessage);
        var negativo = await _servicio.CrearParametroAsync(Req(Hoy().AddDays(2), -1), RolAdmin);
        Assert.Contains("negativo", negativo.ErrorMessage);
        Assert.Equal("FORBIDDEN", (await _servicio.CrearParametroAsync(Req(Hoy().AddDays(3), 1), [RolesSistema.Supervisor])).ErrorCode);

        var lista = await _servicio.ObtenerParametrosAsync(_tango2.LineaId);
        Assert.Equal([12000, 11200, 11000], lista.Select(p => p.MangasObjetivoTurno!.Value));
        Assert.Equal([EstadoVigenciaParametro.Futura, EstadoVigenciaParametro.Vigente, EstadoVigenciaParametro.Reemplazada], lista.Select(p => p.EstadoVigencia));

        // Corregir la vigente (sin objetivo de bolsones = no se mide).
        var vigente = lista[1];
        var corregida = await _servicio.ActualizarParametroAsync(vigente.ParametroLineaProductoId, Req(vigente.VigenteDesde, 11300), RolAdmin);
        Assert.True(corregida.IsSuccess, corregida.ErrorMessage);
        Assert.Equal(11300, corregida.Value!.MangasObjetivoTurno);
    }
}
