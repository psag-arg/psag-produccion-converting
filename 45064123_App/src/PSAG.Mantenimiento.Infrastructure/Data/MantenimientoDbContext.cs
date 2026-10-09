using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Infrastructure.Data.Conversions;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Infrastructure.Data;

public class MantenimientoDbContext : IdentityDbContext<ApplicationUser>
{
    public MantenimientoDbContext(DbContextOptions<MantenimientoDbContext> options) : base(options)
    {
    }

    // Eje 1: Detenciones
    public DbSet<Linea> Lineas => Set<Linea>();
    public DbSet<Equipo> Equipos => Set<Equipo>();
    public DbSet<TipoFalla> TiposFalla => Set<TipoFalla>();
    public DbSet<DetalleFalla> DetallesFalla => Set<DetalleFalla>();
    public DbSet<Turno> Turnos => Set<Turno>();
    public DbSet<Detencion> Detenciones => Set<Detencion>();
    public DbSet<DetencionEstadoHistorial> DetencionesEstadoHistorial => Set<DetencionEstadoHistorial>();
    public DbSet<DetencionNota> DetencionesNotas => Set<DetencionNota>();
    public DbSet<UsuarioLinea> UsuariosLineas => Set<UsuarioLinea>();
    public DbSet<Auditoria> Auditoria => Set<Auditoria>();
    public DbSet<ReglaAnalisis> ReglasAnalisis => Set<ReglaAnalisis>();
    public DbSet<TiempoProgramadoDia> TiemposProgramadosDia => Set<TiempoProgramadoDia>();

    // Eje 2: Variables de proceso
    public DbSet<Producto> Productos => Set<Producto>();
    public DbSet<LineaProducto> LineasProductos => Set<LineaProducto>();
    public DbSet<GrupoParametro> GruposParametro => Set<GrupoParametro>();
    public DbSet<Parametro> Parametros => Set<Parametro>();
    public DbSet<EstandarParametro> EstandaresParametro => Set<EstandarParametro>();
    public DbSet<RegistroProceso> RegistrosProceso => Set<RegistroProceso>();
    public DbSet<RegistroProcesoDetalle> RegistrosProcesoDetalle => Set<RegistroProcesoDetalle>();

    // Eje 3: Inspecciones de máquina
    public DbSet<PlanInspeccion> PlanesInspeccion => Set<PlanInspeccion>();
    public DbSet<ItemPlanInspeccion> ItemsPlanInspeccion => Set<ItemPlanInspeccion>();
    public DbSet<EjecucionInspeccion> EjecucionesInspeccion => Set<EjecucionInspeccion>();
    public DbSet<EjecucionInspeccionDetalle> EjecucionesInspeccionDetalle => Set<EjecucionInspeccionDetalle>();
    public DbSet<HallazgoNoOk> HallazgosNoOk => Set<HallazgoNoOk>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        if (Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetUtcConverter>();
            configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetUtcConverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar todas las configuraciones del ensamblado
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MantenimientoDbContext).Assembly);

        // Detectar si es SQLite
        var isSqlite = Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

        if (isSqlite)
        {
            // SQLite no soporta esquemas - remover todos los esquemas
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                entityType.SetSchema(null);
            }
        }
        else
        {
            // SQL Server - configurar esquema para tablas de Identity
            modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers", "dbo");
        }
    }
}
