using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

// T-798 etapa 2 · Turno de producción y sus hijas (docs/diseno/T-798_modelo_produccion.md, 2.6).
// Mismos nombres, tipos y CHECKs que database/34_t798_turnos_produccion.sql.

public class TurnoProduccionConfiguration : IEntityTypeConfiguration<TurnoProduccion>
{
    public void Configure(EntityTypeBuilder<TurnoProduccion> builder)
    {
        builder.ToTable("PRD_TurnosProduccion", "mnt", t =>
        {
            t.HasCheckConstraint("CK_PRD_TurnosProduccion_Estado",
                "[Estado] IN ('ABIERTO', 'CERRADO', 'CERRADO_AUTOMATICO', 'SIN_APERTURA')");
            t.HasCheckConstraint("CK_PRD_TurnosProduccion_OrigenApertura",
                "[OrigenApertura] IN ('LOGIN', 'SUPERVISOR', 'SISTEMA')");
            t.HasCheckConstraint("CK_PRD_TurnosProduccion_MinutosProgramados", "[MinutosProgramados] >= 0");
        });

        builder.HasKey(x => x.TurnoProduccionId);

        builder.Property(x => x.Estado)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<EstadoTurnoProduccion>(v))
            .HasMaxLength(20);

        builder.Property(x => x.OrigenApertura)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<OrigenAperturaTurno>(v))
            .HasMaxLength(20);

        builder.Property(x => x.AbiertoPorUsuarioId).HasMaxLength(450);
        builder.Property(x => x.CerradoPorUsuarioId).HasMaxLength(450);
        builder.Property(x => x.ObservacionCierre).HasMaxLength(TurnoProduccion.ObservacionMaxima);
        builder.Property(x => x.CreadoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ModificadoPorUsuarioId).IsRequired().HasMaxLength(450);

        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasOne(x => x.Linea)
            .WithMany()
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Turno)
            .WithMany()
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.LineaId, x.TurnoId, x.FechaProduccion })
            .IsUnique()
            .HasDatabaseName("UX_PRD_TurnosProduccion_Linea_Turno_Fecha");

        // Un solo turno ABIERTO por línea (en SQLite es un índice parcial: funciona igual).
        builder.HasIndex(x => x.LineaId)
            .IsUnique()
            .HasFilter("[Estado] = 'ABIERTO'")
            .HasDatabaseName("UX_PRD_TurnosProduccion_LineaAbierto");

        builder.HasIndex(x => x.FechaProduccion)
            .HasDatabaseName("IX_PRD_TurnosProduccion_FechaProduccion");
    }
}

public class TurnoProduccionOperadorConfiguration : IEntityTypeConfiguration<TurnoProduccionOperador>
{
    public void Configure(EntityTypeBuilder<TurnoProduccionOperador> builder)
    {
        builder.ToTable("PRD_TurnosProduccionOperadores", "mnt", t =>
        {
            t.HasCheckConstraint("CK_PRD_TurnosProduccionOperadores_Origen", "[Origen] IN ('LOGIN', 'SUPERVISOR')");
            t.HasCheckConstraint("CK_PRD_TurnosProduccionOperadores_Motivo",
                "[Origen] <> 'SUPERVISOR' OR [Motivo] IS NOT NULL");
        });

        builder.HasKey(x => x.TurnoProduccionOperadorId);

        builder.Property(x => x.OperadorUsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.RegistradoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Motivo).HasMaxLength(TurnoProduccionOperador.MotivoMaximo);
        builder.Property(x => x.Origen)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<OrigenTramoOperador>(v))
            .HasMaxLength(20);

        builder.HasOne(x => x.TurnoProduccion)
            .WithMany(t => t.Operadores)
            .HasForeignKey(x => x.TurnoProduccionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TurnoProduccionOperador>()
            .WithMany()
            .HasForeignKey(x => x.AnuladoPorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TurnoProduccionId);
        builder.HasIndex(x => x.OperadorUsuarioId);
    }
}

public class TurnoProduccionProductoConfiguration : IEntityTypeConfiguration<TurnoProduccionProducto>
{
    public void Configure(EntityTypeBuilder<TurnoProduccionProducto> builder)
    {
        builder.ToTable("PRD_TurnosProduccionProductos", "mnt", t =>
        {
            t.HasCheckConstraint("CK_PRD_TurnosProduccionProductos_Origen", "[Origen] IN ('LOGIN', 'CAMBIO', 'SUPERVISOR')");
            t.HasCheckConstraint("CK_PRD_TurnosProduccionProductos_Motivo",
                "[Origen] <> 'SUPERVISOR' OR [Motivo] IS NOT NULL");
            t.HasCheckConstraint("CK_PRD_TurnosProduccionProductos_MinutosTurno", "[MinutosTurnoReferencia] > 0");
        });

        builder.HasKey(x => x.TurnoProduccionProductoId);

        builder.Property(x => x.DeclaradoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Motivo).HasMaxLength(TurnoProduccionProducto.MotivoMaximo);
        builder.Property(x => x.MetrosPorManga).HasPrecision(9, 2);
        builder.Property(x => x.Origen)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<OrigenTramoProducto>(v))
            .HasMaxLength(20);

        builder.HasOne(x => x.TurnoProduccion)
            .WithMany(t => t.Productos)
            .HasForeignKey(x => x.TurnoProduccionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductoMaestro)
            .WithMany()
            .HasForeignKey(x => x.ProductoMaestroId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductoSku)
            .WithMany()
            .HasForeignKey(x => x.ProductoSkuId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ParametroLineaProducto)
            .WithMany()
            .HasForeignKey(x => x.ParametroLineaProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<TurnoProduccionProducto>()
            .WithMany()
            .HasForeignKey(x => x.AnuladoPorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TurnoProduccionId);
    }
}

public class ProduccionTurnoConfiguration : IEntityTypeConfiguration<ProduccionTurno>
{
    public void Configure(EntityTypeBuilder<ProduccionTurno> builder)
    {
        builder.ToTable("PRD_ProduccionesTurno", "mnt", t =>
        {
            t.HasCheckConstraint("CK_PRD_ProduccionesTurno_Magnitud",
                "[Magnitud] IN ('MANGAS', 'ROLLOS', 'PAQUETES', 'BOLSONES', 'PALLETS')");
            t.HasCheckConstraint("CK_PRD_ProduccionesTurno_Origen", "[Origen] IN ('CAMARA', 'CAMARA_CORREGIDA', 'MANUAL')");
            t.HasCheckConstraint("CK_PRD_ProduccionesTurno_EstadoValidacion",
                "[EstadoValidacion] IN ('PENDIENTE', 'VALIDADA', 'SIN_DATOS')");
        });

        builder.HasKey(x => x.ProduccionTurnoId);

        builder.Property(x => x.Magnitud).IsRequired().HasMaxLength(10);
        builder.Property(x => x.CantidadCamara).HasPrecision(12, 1);
        builder.Property(x => x.CantidadOficial).HasPrecision(12, 1);
        builder.Property(x => x.ValidadoPorUsuarioId).HasMaxLength(450);
        builder.Property(x => x.RegistradoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Origen)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<OrigenProduccionTurno>(v))
            .HasMaxLength(20);
        builder.Property(x => x.EstadoValidacion)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<EstadoValidacionProduccion>(v))
            .HasMaxLength(20);

        builder.HasOne(x => x.TurnoProduccionProducto)
            .WithMany(p => p.Producciones)
            .HasForeignKey(x => x.TurnoProduccionProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProduccionTurno>()
            .WithMany()
            .HasForeignKey(x => x.AnuladoPorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.TurnoProduccionProductoId, x.Magnitud });
    }
}
