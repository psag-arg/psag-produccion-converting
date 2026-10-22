using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Api.Controllers;
using PSAG.Mantenimiento.Application.DTOs.Analisis;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Domain.Services;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;
using PSAG.Mantenimiento.Infrastructure.Services;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// T-543 (editar análisis cerrado con historial) y T-544 (fotos del análisis) contra SQLite y disco reales.
/// </summary>
public class AnalisisEdicionTests : IDisposable
{
    private const string Usuario = "usuario-test";
    private const string Admin = "admin-1";
    private const string Supervisor = "super-1";
    private const string Operario = "oper-1";
    private const string CerradoPor = "mant-1";

    private static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    ];

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    private readonly SqliteConnection _conexion;
    private readonly MantenimientoDbContext _context;
    private readonly string _carpeta;
    private readonly AlmacenAdjuntosDisco _almacen;
    private readonly AnalisisService _analisisService;
    private readonly AnalisisEdicionService _edicionService;

    private int _lineaId;
    private int _turnoId;

    public AnalisisEdicionTests()
    {
        _conexion = new SqliteConnection("DataSource=:memory:");
        _conexion.Open();
        _context = new MantenimientoDbContext(new DbContextOptionsBuilder<MantenimientoDbContext>().UseSqlite(_conexion).Options);
        _context.Database.EnsureCreated();

        _carpeta = Path.Combine(Path.GetTempPath(), "psag-adjuntos-test-" + Guid.NewGuid().ToString("N"));
        _almacen = new AlmacenAdjuntosDisco(_carpeta);

        var unitOfWork = new UnitOfWork(_context);
        _analisisService = new AnalisisService(unitOfWork, NullLogger<AnalisisService>.Instance);
        _edicionService = new AnalisisEdicionService(unitOfWork, _analisisService, _almacen, NullLogger<AnalisisEdicionService>.Instance);

        CrearCatalogos();
    }

    public void Dispose()
    {
        _context.Dispose();
        _conexion.Dispose();
        if (Directory.Exists(_carpeta))
            Directory.Delete(_carpeta, recursive: true);
    }

    // ===================== T-543: edición de análisis cerrado =====================

    [Fact]
    public async Task Administrador_EditaElAnalisisCerradoOriginal_YQuedaHistorial()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.CERRADA);

        var resultado = await _edicionService.EditarCerradoAsync(detencion.DetencionId, new GuardarAnalisisRequest
        {
            AccionesRealizadas = "Se cambió el rodillo",
            CausaFalla = "Rodamiento sin lubricar",
            PlanAccion = "Lubricar cada semana",
            RowVersion = detencion.RowVersion
        }, Admin, ["ADMINISTRADOR"]);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);

        // Se guardó sobre la misma detención, no en una copia
        _context.ChangeTracker.Clear();
        var guardada = await _context.Detenciones.Include(d => d.CambiosAnalisis).SingleAsync();
        Assert.Equal(detencion.DetencionId, guardada.DetencionId);
        Assert.Equal("Rodamiento sin lubricar", guardada.CausaFalla);
        Assert.Equal("Lubricar cada semana", guardada.PlanAccion);
        Assert.Equal(EstadoDetencion.CERRADA, guardada.EstadoActual);
        // La firma de cierre no cambia
        Assert.Equal(CerradoPor, guardada.AnalisisPorUsuarioId);

        Assert.Equal(2, guardada.CambiosAnalisis.Count);
        var causa = guardada.CambiosAnalisis.Single(c => c.Campo == AnalisisCambio.CampoCausa);
        Assert.Equal("Desgaste", causa.ValorAnterior);
        Assert.Equal("Rodamiento sin lubricar", causa.ValorNuevo);
        Assert.Equal(Admin, causa.UsuarioId);
        Assert.True(causa.FechaHora > DateTimeOffset.UtcNow.AddMinutes(-1));

        // Se ve en el detalle de la detención...
        var detalle = await _analisisService.ObtenerAsync(detencion.DetencionId, Operario, ["OPERARIO"]);
        Assert.Equal("Rodamiento sin lubricar", detalle.Value!.CausaFalla);
        Assert.Equal(2, detalle.Value.Cambios.Count);

        // ...y en la base de soluciones
        var soluciones = await _analisisService.BuscarSolucionesAsync(new BuscarSolucionesRequest(), ["OPERARIO"]);
        var ficha = Assert.Single(soluciones.Value!.Items);
        Assert.Equal("Rodamiento sin lubricar", ficha.CausaFalla);
        Assert.Equal("Lubricar cada semana", ficha.PlanAccion);
    }

    [Fact]
    public async Task Supervisor_PuedeEditarElAnalisisCerrado()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.CERRADA);

        var resultado = await _edicionService.EditarCerradoAsync(detencion.DetencionId,
            Pedido(detencion, causa: "Otra causa"), Supervisor, ["SUPERVISOR"]);

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Single(resultado.Value!.Cambios);
    }

    [Theory]
    [InlineData("OPERARIO")]
    [InlineData("MANTENIMIENTO")]
    [InlineData("GERENCIA")]
    public async Task OtrosRoles_NoPuedenEditar_NiCambiaNada(string rol)
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.CERRADA);

        var resultado = await _edicionService.EditarCerradoAsync(detencion.DetencionId,
            Pedido(detencion, causa: "Intento"), Operario, [rol]);

        Assert.Equal("FORBIDDEN", resultado.ErrorCode);
        _context.ChangeTracker.Clear();
        var guardada = await _context.Detenciones.Include(d => d.CambiosAnalisis).SingleAsync();
        Assert.Equal("Desgaste", guardada.CausaFalla);
        Assert.Empty(guardada.CambiosAnalisis);
    }

    [Fact]
    public async Task Operario_LaApiDevuelve403()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.CERRADA);
        var controller = Controlador(Operario, "OPERARIO");

        var respuesta = await controller.EditarCerrado(detencion.DetencionId, Pedido(detencion, causa: "Intento"));

        var objeto = Assert.IsType<ObjectResult>(respuesta);
        Assert.Equal(StatusCodes.Status403Forbidden, objeto.StatusCode);
    }

    [Fact]
    public async Task BaseDeSoluciones_IndicaSiElUsuarioPuedeEditar()
    {
        await CrearDetencionAsync(EstadoDetencion.CERRADA);

        var operario = await _analisisService.BuscarSolucionesAsync(new BuscarSolucionesRequest(), ["OPERARIO"]);
        var supervisor = await _analisisService.BuscarSolucionesAsync(new BuscarSolucionesRequest(), ["SUPERVISOR"]);

        Assert.False(operario.Value!.PuedeEditar);
        Assert.True(supervisor.Value!.PuedeEditar);
    }

    [Fact]
    public async Task AnalisisAbierto_NoSeEditaComoCerrado()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.EN_SEGUIMIENTO);

        var resultado = await _edicionService.EditarCerradoAsync(detencion.DetencionId,
            Pedido(detencion, causa: "Otra"), Admin, ["ADMINISTRADOR"]);

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    [Fact]
    public async Task SolucionVacia_SeRechaza()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.CERRADA);
        var pedido = Pedido(detencion, causa: "Otra");
        pedido.AccionesRealizadas = "  ";

        var resultado = await _edicionService.EditarCerradoAsync(detencion.DetencionId, pedido, Admin, ["ADMINISTRADOR"]);

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    [Fact]
    public async Task RowVersionVieja_DaConflicto()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.CERRADA);
        var pedido = Pedido(detencion, causa: "Otra");
        pedido.RowVersion = [1, 2, 3];

        var resultado = await _edicionService.EditarCerradoAsync(detencion.DetencionId, pedido, Admin, ["ADMINISTRADOR"]);

        Assert.Equal("CONCURRENCY", resultado.ErrorCode);
    }

    [Fact]
    public async Task SinCambios_NoAgregaHistorial()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.CERRADA);

        var resultado = await _edicionService.EditarCerradoAsync(detencion.DetencionId,
            Pedido(detencion, causa: detencion.CausaFalla!), Admin, ["ADMINISTRADOR"]);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(resultado.Value!.Cambios);
    }

    [Theory]
    [InlineData(3, 3, 3)]
    [InlineData(50, 50, 50)]
    [InlineData(55, 50, 55)]
    public async Task ContadorDeLaBase_CoincideConLasFichasOAvisaElTotal(int cerradas, int fichas, int total)
    {
        for (var i = 0; i < cerradas; i++)
            await CrearDetencionAsync(EstadoDetencion.CERRADA);
        // Las abiertas y las eliminadas no cuentan
        await CrearDetencionAsync(EstadoDetencion.EN_SEGUIMIENTO);
        var eliminada = await CrearDetencionAsync(EstadoDetencion.CERRADA);
        eliminada.Eliminada = true;
        await _context.SaveChangesAsync();

        var resultado = await _analisisService.BuscarSolucionesAsync(new BuscarSolucionesRequest(), ["OPERARIO"]);

        Assert.Equal(fichas, resultado.Value!.Items.Count);
        Assert.Equal(total, resultado.Value.Total);
    }

    // ===================== T-544: fotos =====================

    [Fact]
    public async Task Responsable_AdjuntaFotoEnBorrador_SeGuardaEnDiscoConNombreGenerado()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.EN_SEGUIMIENTO);

        var resultado = await SubirAsync(detencion, "C:\\fotos\\rodillo roto.png", Png, CerradoPor, "MANTENIMIENTO");

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        Assert.Equal("rodillo roto.png", resultado.Value!.NombreOriginal);
        Assert.Equal("image/png", resultado.Value.ContentType);

        var registro = await _context.Set<AnalisisAdjunto>().SingleAsync();
        Assert.Matches("^[0-9a-f]{32}\\.png$", registro.NombreArchivo);
        Assert.True(File.Exists(Path.Combine(_carpeta, registro.NombreArchivo)));

        var abierto = await _edicionService.AbrirAdjuntoAsync(detencion.DetencionId, registro.AnalisisAdjuntoId);
        Assert.True(abierto.IsSuccess);
        await using var stream = abierto.Value!.Contenido;
        using var copia = new MemoryStream();
        await stream.CopyToAsync(copia);
        Assert.Equal(Png, copia.ToArray());

        // En borrador no es una corrección: no va al historial
        Assert.Empty(await _context.Set<AnalisisCambio>().ToListAsync());
    }

    [Fact]
    public async Task AnalisisCerrado_AdministradorAdjunta_YQuedaEnElHistorial()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.CERRADA);

        var resultado = await SubirAsync(detencion, "foto.jpg", Jpeg, Admin, "ADMINISTRADOR");

        Assert.True(resultado.IsSuccess, resultado.ErrorMessage);
        var cambio = await _context.Set<AnalisisCambio>().SingleAsync();
        Assert.Equal(AnalisisCambio.CampoFoto, cambio.Campo);
        Assert.Equal("foto.jpg", cambio.ValorNuevo);

        var soluciones = await _analisisService.BuscarSolucionesAsync(new BuscarSolucionesRequest(), ["OPERARIO"]);
        Assert.Single(soluciones.Value!.Items.Single().Adjuntos);
    }

    [Fact]
    public async Task AnalisisCerrado_OperarioNoPuedeAdjuntar()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.CERRADA);

        var resultado = await SubirAsync(detencion, "foto.jpg", Jpeg, Operario, "OPERARIO");

        Assert.Equal("FORBIDDEN", resultado.ErrorCode);
        Assert.False(Directory.Exists(_carpeta) && Directory.EnumerateFiles(_carpeta).Any());
    }

    [Fact]
    public async Task ArchivoQueNoEsImagen_SeRechaza_YNoSeGuarda()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.EN_SEGUIMIENTO);

        var resultado = await SubirAsync(detencion, "foto.jpg", Encoding.UTF8.GetBytes("<script>alert(1)</script>"), Admin, "ADMINISTRADOR");

        Assert.Equal("VALIDATION", resultado.ErrorCode);
        Assert.Equal("El archivo no es una imagen JPG o PNG válida.", resultado.ErrorMessage);
        Assert.Empty(await _context.Set<AnalisisAdjunto>().ToListAsync());
        Assert.False(Directory.Exists(_carpeta) && Directory.EnumerateFiles(_carpeta).Any());
    }

    [Fact]
    public async Task FotoDeMasDeDiezMegas_SeRechaza()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.EN_SEGUIMIENTO);
        var grande = new byte[AdjuntoImagenReglas.TamanoMaximoBytes + 1];
        Jpeg.CopyTo(grande, 0);

        var resultado = await SubirAsync(detencion, "grande.jpg", grande, Admin, "ADMINISTRADOR");

        Assert.Equal("VALIDATION", resultado.ErrorCode);
        Assert.Contains("10 MB", resultado.ErrorMessage);
    }

    [Fact]
    public async Task TamanoDeclaradoMenorQueElReal_IgualSeCortaEnDiezMegas()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.EN_SEGUIMIENTO);
        var grande = new byte[AdjuntoImagenReglas.TamanoMaximoBytes + 500];
        Jpeg.CopyTo(grande, 0);

        using var contenido = new MemoryStream(grande);
        var resultado = await _edicionService.AgregarAdjuntoAsync(
            detencion.DetencionId, "grande.jpg", 1000, contenido, Admin, ["ADMINISTRADOR"]);

        Assert.Equal("VALIDATION", resultado.ErrorCode);
    }

    [Fact]
    public async Task SextaFoto_SeRechaza()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.EN_SEGUIMIENTO);
        for (var i = 0; i < AdjuntoImagenReglas.MaximoPorAnalisis; i++)
            Assert.True((await SubirAsync(detencion, $"f{i}.png", Png, Admin, "ADMINISTRADOR")).IsSuccess);

        _context.ChangeTracker.Clear();
        var resultado = await SubirAsync(detencion, "f6.png", Png, Admin, "ADMINISTRADOR");

        Assert.Equal("VALIDATION", resultado.ErrorCode);
        Assert.Equal(AdjuntoImagenReglas.MaximoPorAnalisis, await _context.Set<AnalisisAdjunto>().CountAsync());
    }

    [Fact]
    public async Task AnalisisSinFotos_SigueIgual()
    {
        var detencion = await CrearDetencionAsync(EstadoDetencion.CERRADA);

        var detalle = await _analisisService.ObtenerAsync(detencion.DetencionId, Operario, ["OPERARIO"]);

        Assert.Empty(detalle.Value!.Adjuntos);
        Assert.False(detalle.Value.PuedeAdjuntar);
        Assert.False(detalle.Value.PuedeEditarCerrado);
    }

    [Theory]
    [InlineData("..\\secreto.txt")]
    [InlineData("../secreto.txt")]
    [InlineData("sub/archivo.png")]
    public void Almacen_RechazaNombresConCarpetas(string nombre)
    {
        Assert.Throws<ArgumentException>(() => _almacen.Abrir(nombre));
    }

    // ===================== Ayudas =====================

    private async Task<Application.Common.Result<AdjuntoAnalisisDto>> SubirAsync(
        Detencion detencion, string nombre, byte[] bytes, string usuario, string rol)
    {
        using var contenido = new MemoryStream(bytes);
        return await _edicionService.AgregarAdjuntoAsync(
            detencion.DetencionId, nombre, bytes.Length, contenido, usuario, [rol]);
    }

    private static GuardarAnalisisRequest Pedido(Detencion detencion, string causa) => new()
    {
        AccionesRealizadas = detencion.AccionesRealizadas,
        CausaFalla = causa,
        PlanAccion = detencion.PlanAccion,
        RowVersion = detencion.RowVersion
    };

    private AnalisisController Controlador(string usuarioId, string rol)
    {
        var identidad = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, usuarioId), new Claim(ClaimTypes.Role, rol)], "Test");

        // UserManager no se usa en los caminos de error que se prueban acá.
        return new AnalisisController(_analisisService, _edicionService, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) }
            }
        };
    }

    private void CrearCatalogos()
    {
        var linea = new Linea { Codigo = "T2", Nombre = "Tango 2", CreadoPorUsuarioId = Usuario, ModificadoPorUsuarioId = Usuario };
        var turno = new Turno
        {
            Codigo = "T1", Nombre = "Turno 1", HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0),
            CreadoPorUsuarioId = Usuario, ModificadoPorUsuarioId = Usuario
        };
        _context.AddRange(linea, turno);
        _context.SaveChanges();
        _lineaId = linea.LineaId;
        _turnoId = turno.TurnoId;
    }

    private int _secuencia;

    private async Task<Detencion> CrearDetencionAsync(EstadoDetencion estado)
    {
        _secuencia++;
        var cerrada = estado == EstadoDetencion.CERRADA;
        var detencion = new Detencion
        {
            Codigo = $"DET-20261022-{_secuencia:000000}",
            LineaId = _lineaId,
            TurnoId = _turnoId,
            Fecha = new DateOnly(2026, 10, 22),
            OrigenTiempo = OrigenTiempo.DECLARADO,
            MinutosDeclarados = 40,
            MinutosEfectivos = 40,
            Descripcion = "Se trabó el rodillo",
            EstadoActual = estado,
            RequiereAnalisis = true,
            ResponsableRol = "MANTENIMIENTO",
            AccionesRealizadas = "Se cambió el rodillo",
            CausaFalla = "Desgaste",
            AnalisisPorUsuarioId = cerrada ? CerradoPor : null,
            FechaHoraAnalisis = cerrada ? DateTimeOffset.UtcNow.AddDays(-1) : null,
            CreadoPorUsuarioId = Usuario,
            ModificadoPorUsuarioId = Usuario
        };
        _context.Add(detencion);
        await _context.SaveChangesAsync();
        return detencion;
    }
}
