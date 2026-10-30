using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PSAG.Mantenimiento.Domain.Common;
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

    // Tareas de mantenimiento (T-546/T-547)
    public DbSet<TareaMantenimiento> TareasMantenimiento => Set<TareaMantenimiento>();

    // Control de calidad por cámaras (T-611)
    public DbSet<PuntoControlCalidad> PuntosControlCalidad => Set<PuntoControlCalidad>();
    public DbSet<TipoDefectoCalidad> TiposDefectoCalidad => Set<TipoDefectoCalidad>();
    public DbSet<RegistroCalidad> RegistrosCalidad => Set<RegistroCalidad>();
    public DbSet<RegistroCalidadRevision> RegistrosCalidadRevisiones => Set<RegistroCalidadRevision>();

    // Conteo de producción y paradas por cámara (T-610)
    public DbSet<ConteoCamaraMinuto> ConteosCamaraMinuto => Set<ConteoCamaraMinuto>();
    public DbSet<ParadaCamara> ParadasCamara => Set<ParadaCamara>();

    private bool EsSqlite => Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        if (EsSqlite)
        {
            configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetUtcConverter>();
            configurationBuilder.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetUtcConverter>();
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AplicarValoresAutomaticos();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AplicarValoresAutomaticos();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Completa en cada guardado lo que la aplicación no envía: fechas de auditoría
    /// y, en SQLite, el valor de rowversion (SQL Server lo genera solo).
    /// </summary>
    private void AplicarValoresAutomaticos()
    {
        ChangeTracker.DetectChanges();
        var ahora = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            AplicarFechasDeAuditoria(entry, ahora);

            if (EsSqlite && entry.Metadata.FindProperty("RowVersion")?.ClrType == typeof(byte[]))
            {
                // EF lo envía en insert y update (ver OnModelCreating).
                entry.Property("RowVersion").CurrentValue = RandomNumberGenerator.GetBytes(8);
            }
        }
    }

    /// <summary>
    /// Alta: completa sólo las fechas que llegan por defecto. Edición: actualiza la fecha de modificación.
    /// Las fechas de negocio (inicio, parada, hallazgo) no son auditoría y no se tocan.
    /// </summary>
    private static void AplicarFechasDeAuditoria(EntityEntry entry, DateTimeOffset ahora)
    {
        if (entry.State == EntityState.Modified)
        {
            if (entry.Entity is EntityBase editable)
                editable.FechaHoraModificacion = ahora;
            return;
        }

        switch (entry.Entity)
        {
            case EntityBase creable:
                if (creable.FechaHoraCreacion == default) creable.FechaHoraCreacion = ahora;
                if (creable.FechaHoraModificacion == default) creable.FechaHoraModificacion = ahora;
                break;
            case DetencionEstadoHistorial historial when historial.FechaHora == default:
                historial.FechaHora = ahora;
                break;
            case DetencionNota nota when nota.FechaHora == default:
                nota.FechaHora = ahora;
                break;
            case Auditoria auditoria when auditoria.FechaHora == default:
                auditoria.FechaHora = ahora;
                break;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Aplicar todas las configuraciones del ensamblado
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MantenimientoDbContext).Assembly);

        if (EsSqlite)
        {
            // SQLite no genera rowversion: lo asigna SaveChanges, así que EF debe enviarlo en el insert.
            foreach (var entityType in modelBuilder.Model.GetEntityTypes().ToList())
            {
                if (entityType.FindProperty("RowVersion")?.ClrType == typeof(byte[]))
                {
                    modelBuilder.Entity(entityType.ClrType).Property("RowVersion").ValueGeneratedNever();
                }
            }

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
