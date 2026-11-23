using PSAG.Mantenimiento.Application.DTOs.Produccion;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;
using PSAG.Mantenimiento.Infrastructure.Services;

namespace PSAG.Mantenimiento.Api.IntegrationTests;

/// <summary>
/// Base de los tests de calidad (T-611): base de prueba (SQLite en memoria o SQL Server, ver BaseDePrueba) y carpeta temporal de imágenes.
/// Los datos de prueba viven SOLO acá, nunca en los scripts de producción.
/// </summary>
public abstract class CalidadTestBase : IDisposable
{
    protected const string Admin = "admin-1";
    protected const string Supervisor = "sup-1";
    protected const string Mantenimiento = "mant-1";
    protected const string Operario = "oper-1";

    protected static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    ];

    protected static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    /// <summary>Zona fija UTC-3, como la planta, sin depender de la zona del equipo que corre los tests.</summary>
    protected static readonly TimeZoneInfo ZonaPlanta =
        TimeZoneInfo.CreateCustomTimeZone("Planta-Test", TimeSpan.FromHours(-3), "Planta", "Planta");

    protected readonly BaseDePrueba Base;
    protected readonly MantenimientoDbContext Context;
    protected readonly string Carpeta;
    protected readonly AlmacenImagenesCalidadDisco Almacen;
    protected readonly RegistroCalidadService Service;
    protected readonly CalidadMaestrosService Maestros;

    /// <summary>Maestro de cámaras (T-798 etapa 4).</summary>
    protected readonly CamaraService Camaras;

    protected Linea Tango2 = null!;
    protected Linea Futura2 = null!;
    protected Turno TurnoManana = null!;
    protected Turno TurnoTarde = null!;
    /// <summary>Puntos de control de calidad (usan las cámaras de abajo, como deja la migración del script 36).</summary>
    protected PuntoControlCalidad CamaraTango = null!;
    protected PuntoControlCalidad CamaraFutura = null!;

    /// <summary>T-798 etapa 4: cámaras del maestro único (CAM_Camaras), de conteo, habilitadas, con los mismos códigos.</summary>
    protected Camara CamTango = null!;
    protected Camara CamFutura = null!;
    protected TipoDefectoCalidad Rabo = null!;
    protected TipoDefectoCalidad Mancha = null!;
    protected ProductoMaestro Producto = null!;
    protected Detencion DetencionTango = null!;

    protected CalidadTestBase()
    {
        Base = BaseDePrueba.Crear();
        Context = Base.Context;

        Carpeta = Path.Combine(Path.GetTempPath(), "psag-calidad-test-" + Guid.NewGuid().ToString("N"));
        Almacen = new AlmacenImagenesCalidadDisco(Carpeta);

        var unitOfWork = new UnitOfWork(Context);
        Service = new RegistroCalidadService(unitOfWork, Almacen, ZonaPlanta, NullLogger<RegistroCalidadService>.Instance);
        Maestros = new CalidadMaestrosService(unitOfWork, NullLogger<CalidadMaestrosService>.Instance);
        Camaras = new CamaraService(unitOfWork, ZonaPlanta, NullLogger<CamaraService>.Instance);

        CrearDatosDePrueba();
    }

    public void Dispose()
    {
        Context.Dispose();
        Base.Dispose();
        if (Directory.Exists(Carpeta))
            Directory.Delete(Carpeta, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Hora de pared de la planta, hace <paramref name="diasAtras"/> días, en el formato que manda el detector sin offset.
    /// </summary>
    protected static string HoraPlantaTexto(int diasAtras, int hora, int minuto = 0)
    {
        var hoyPlanta = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaPlanta).Date;
        return hoyPlanta.AddDays(-diasAtras).AddHours(hora).AddMinutes(minuto).ToString("yyyy-MM-dd'T'HH:mm:ss");
    }

    protected static DateOnly DiaPlanta(int diasAtras) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ZonaPlanta).Date.AddDays(-diasAtras));

    /// <summary>Servicio de turnos de producción (T-798) sobre el mismo contexto; reloj real salvo que se pase otro.</summary>
    protected static TurnoProduccionService NuevoTurnosProduccion(UnitOfWork unitOfWork, Func<DateTimeOffset>? ahora = null) =>
        new(unitOfWork, ZonaPlanta, new TurnosProduccionOpciones(), NullLogger<TurnoProduccionService>.Instance,
            ahora ?? (() => DateTimeOffset.UtcNow));

    protected static Application.DTOs.Calidad.ImagenEntrante ImagenDe(byte[] bytes, string nombre = "frame.png") =>
        new(nombre, bytes.Length, new MemoryStream(bytes));

    /// <summary>Cámara de conteo de prueba: rollos por carriles (como la 09), habilitada, sin detenciones automáticas.</summary>
    protected static Camara NuevaCamara(int lineaId, string codigo, string nombre, string magnitud = MagnitudProduccion.Rollos) => new()
    {
        LineaId = lineaId, Codigo = codigo, Nombre = nombre, Funcion = FuncionCamara.Conteo, Magnitud = magnitud,
        MetodoDeteccion = magnitud == MagnitudProduccion.Rollos ? MetodoDeteccionCamara.Carriles : MetodoDeteccionCamara.Pasos,
        CantidadCarriles = (byte)(magnitud == MagnitudProduccion.Rollos ? 4 : 1), DetectaParadas = true, Habilitada = true,
        CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
    };

    /// <summary>Request de Maestros → Cámaras con los datos actuales de la cámara (para cambiarle algo y guardarla).</summary>
    protected static Application.DTOs.Camaras.GuardarCamaraRequest RequestDe(Camara c) => new()
    {
        Codigo = c.Codigo, Nombre = c.Nombre, LineaId = c.LineaId, EquipoId = c.EquipoId, NumeroCanalGrabador = c.NumeroCanalGrabador,
        Funcion = c.Funcion, Magnitud = c.Magnitud, MetodoDeteccion = c.MetodoDeteccion, CantidadCarriles = c.CantidadCarriles,
        DetectaParadas = c.DetectaParadas, CreaDetenciones = c.CreaDetenciones, UmbralDetencionSegundos = c.UmbralDetencionSegundos,
        CuentaProduccionOficial = c.CuentaProduccionOficial, Habilitada = c.Habilitada, Activo = c.Activo
    };

    private void CrearDatosDePrueba()
    {
        Tango2 = new Linea { Codigo = "T2", Nombre = "Tango 2", Orden = 1, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        Futura2 = new Linea { Codigo = "F2", Nombre = "Futura 2", Orden = 2, CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        TurnoManana = new Turno
        {
            Codigo = "T1", Nombre = "Turno 1 (Mañana)", HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0), Orden = 1,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        TurnoTarde = new Turno
        {
            Codigo = "T2", Nombre = "Turno 2 (Tarde)", HoraInicio = new TimeOnly(14, 0), HoraFin = new TimeOnly(22, 0), Orden = 2,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        Producto = new ProductoMaestro { Codigo = "SKU-TEST", Nombre = "Producto de prueba", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        Context.AddRange(Tango2, Futura2, TurnoManana, TurnoTarde, Producto);
        Context.SaveChanges();

        CamTango = NuevaCamara(Tango2.LineaId, "TEST-CAM-T2", "Cámara de prueba Tango");
        CamFutura = NuevaCamara(Futura2.LineaId, "TEST-CAM-F2", "Cámara de prueba Futura");
        Context.AddRange(CamTango, CamFutura);
        Context.SaveChanges();

        CamaraTango = new PuntoControlCalidad
        {
            LineaId = Tango2.LineaId, Codigo = "TEST-CAM-T2", Nombre = "Cámara de prueba Tango", CamaraId = CamTango.CamaraId,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        CamaraFutura = new PuntoControlCalidad
        {
            LineaId = Futura2.LineaId, Codigo = "TEST-CAM-F2", Nombre = "Cámara de prueba Futura", CamaraId = CamFutura.CamaraId,
            CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin
        };
        // Defectos de prueba: no son los de la ficha del cliente (que todavía no llegó).
        Rabo = new TipoDefectoCalidad { Codigo = "TEST-RABO", Nombre = "Defecto de prueba con medida", UnidadMedida = "mm", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        Mancha = new TipoDefectoCalidad { Codigo = "TEST-MANCHA", Nombre = "Defecto de prueba sin medida", CreadoPorUsuarioId = Admin, ModificadoPorUsuarioId = Admin };
        DetencionTango = new Detencion
        {
            Codigo = "DET-20261020-000001",
            LineaId = Tango2.LineaId,
            TurnoProduccionId = Context.TurnoProduccionId(Tango2.LineaId, TurnoManana.TurnoId, new DateOnly(2026, 10, 20)),
            OrigenTiempo = OrigenTiempo.DECLARADO,
            MinutosDeclarados = 15,
            MinutosEfectivos = 15,
            Descripcion = "Parada de prueba",
            EstadoActual = EstadoDetencion.REGISTRADA,
            CreadoPorUsuarioId = Admin,
            ModificadoPorUsuarioId = Admin
        };
        Context.AddRange(CamaraTango, CamaraFutura, Rabo, Mancha, DetencionTango);
        Context.SaveChanges();
        Context.ChangeTracker.Clear();
    }
}
