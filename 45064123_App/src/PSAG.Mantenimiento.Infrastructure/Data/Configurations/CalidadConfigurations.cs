using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

// T-611 Control de calidad. Mismas tablas, CHECK, FKs e índices que database/23_t611_calidad_defectos.sql.

public class PuntoControlCalidadConfiguration : IEntityTypeConfiguration<PuntoControlCalidad>
{
    public void Configure(EntityTypeBuilder<PuntoControlCalidad> builder)
    {
        builder.ToTable("PuntosControlCalidad", "mnt");

        builder.HasKey(x => x.PuntoControlCalidadId);

        builder.Property(x => x.Codigo).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Descripcion).HasMaxLength(500);
        builder.Property(x => x.CreadoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ModificadoPorUsuarioId).IsRequired().HasMaxLength(450);

        builder.HasOne(x => x.Linea)
            .WithMany()
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Codigo).IsUnique();
        builder.HasIndex(x => x.LineaId);
    }
}

public class TipoDefectoCalidadConfiguration : IEntityTypeConfiguration<TipoDefectoCalidad>
{
    public void Configure(EntityTypeBuilder<TipoDefectoCalidad> builder)
    {
        builder.ToTable("TiposDefectoCalidad", "mnt");

        builder.HasKey(x => x.TipoDefectoCalidadId);

        builder.Property(x => x.Codigo).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Descripcion).HasMaxLength(500);
        builder.Property(x => x.UnidadMedida).HasMaxLength(20);
        builder.Property(x => x.CreadoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ModificadoPorUsuarioId).IsRequired().HasMaxLength(450);

        builder.HasIndex(x => x.Codigo).IsUnique();
    }
}

public class RegistroCalidadConfiguration : IEntityTypeConfiguration<RegistroCalidad>
{
    public void Configure(EntityTypeBuilder<RegistroCalidad> builder)
    {
        builder.ToTable("RegistrosCalidad", "mnt", t =>
        {
            t.HasCheckConstraint("CK_RegistrosCalidad_Origen",
                "[Origen] IN ('AUTOMATICO', 'MANUAL')");
            t.HasCheckConstraint("CK_RegistrosCalidad_EstadoRevision",
                "[EstadoRevision] IN ('PENDIENTE', 'CONFIRMADO', 'FALSO_POSITIVO')");
            t.HasCheckConstraint("CK_RegistrosCalidad_Confianza",
                "[Confianza] IS NULL OR ([Confianza] >= 0 AND [Confianza] <= 1)");
            t.HasCheckConstraint("CK_RegistrosCalidad_ValorMedido",
                "[ValorMedido] IS NULL OR [ValorMedido] >= 0");
            t.HasCheckConstraint("CK_RegistrosCalidad_ImagenContentType",
                "[ImagenContentType] IS NULL OR [ImagenContentType] IN ('image/jpeg', 'image/png')");
            t.HasCheckConstraint("CK_RegistrosCalidad_Revision",
                "([EstadoRevision] = 'PENDIENTE' AND [RevisadoPorUsuarioId] IS NULL AND [FechaHoraRevision] IS NULL) OR ([EstadoRevision] <> 'PENDIENTE' AND [RevisadoPorUsuarioId] IS NOT NULL AND [FechaHoraRevision] IS NOT NULL)");
        });

        builder.HasKey(x => x.RegistroCalidadId);

        builder.Property(x => x.Origen)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<OrigenRegistroCalidad>(v))
            .HasMaxLength(20);

        builder.Property(x => x.EstadoRevision)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<EstadoRevisionCalidad>(v))
            .HasMaxLength(20);

        builder.Property(x => x.Confianza).HasPrecision(5, 4);
        builder.Property(x => x.ValorMedido).HasPrecision(18, 4);
        builder.Property(x => x.UnidadMedida).HasMaxLength(20);
        builder.Property(x => x.Observaciones).HasMaxLength(1000);
        builder.Property(x => x.IdentificadorExterno).HasMaxLength(100);
        builder.Property(x => x.ImagenNombreArchivo).HasMaxLength(100);
        builder.Property(x => x.ImagenNombreOriginal).HasMaxLength(255);
        builder.Property(x => x.ImagenContentType).HasMaxLength(50);
        builder.Property(x => x.RevisadoPorUsuarioId).HasMaxLength(450);
        builder.Property(x => x.CreadoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ModificadoPorUsuarioId).IsRequired().HasMaxLength(450);

        builder.Property(x => x.RowVersion).IsRowVersion();

        // Relaciones (sin colección del otro lado para no tocar las entidades existentes)
        builder.HasOne(x => x.Linea).WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PuntoControl).WithMany().HasForeignKey(x => x.PuntoControlCalidadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TipoDefecto).WithMany().HasForeignKey(x => x.TipoDefectoCalidadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Producto).WithMany().HasForeignKey(x => x.ProductoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Detencion).WithMany().HasForeignKey(x => x.DetencionId).OnDelete(DeleteBehavior.Restrict);

        // Índices: la grilla filtra por fecha y, dentro, por línea / estado.
        builder.HasIndex(x => x.FechaHoraDeteccion);
        builder.HasIndex(x => new { x.LineaId, x.FechaHoraDeteccion });
        builder.HasIndex(x => new { x.EstadoRevision, x.FechaHoraDeteccion });
        builder.HasIndex(x => x.PuntoControlCalidadId);
        builder.HasIndex(x => x.TipoDefectoCalidadId);
        builder.HasIndex(x => x.DetencionId);
        builder.HasIndex(x => x.ImagenNombreArchivo).IsUnique().HasFilter("[ImagenNombreArchivo] IS NOT NULL");
        builder.HasIndex(x => x.IdentificadorExterno).IsUnique().HasFilter("[IdentificadorExterno] IS NOT NULL");
    }
}

public class RegistroCalidadRevisionConfiguration : IEntityTypeConfiguration<RegistroCalidadRevision>
{
    public void Configure(EntityTypeBuilder<RegistroCalidadRevision> builder)
    {
        builder.ToTable("RegistrosCalidadRevisiones", "mnt", t =>
        {
            t.HasCheckConstraint("CK_RegistrosCalidadRevisiones_EstadoAnterior",
                "[EstadoAnterior] IN ('PENDIENTE', 'CONFIRMADO', 'FALSO_POSITIVO')");
            t.HasCheckConstraint("CK_RegistrosCalidadRevisiones_EstadoNuevo",
                "[EstadoNuevo] IN ('CONFIRMADO', 'FALSO_POSITIVO')");
        });

        builder.HasKey(x => x.RegistroCalidadRevisionId);

        builder.Property(x => x.EstadoAnterior)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<EstadoRevisionCalidad>(v))
            .HasMaxLength(20);

        builder.Property(x => x.EstadoNuevo)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<EstadoRevisionCalidad>(v))
            .HasMaxLength(20);

        builder.Property(x => x.Comentario).HasMaxLength(500);
        builder.Property(x => x.UsuarioId).IsRequired().HasMaxLength(450);

        builder.HasOne(x => x.RegistroCalidad)
            .WithMany(x => x.Revisiones)
            .HasForeignKey(x => x.RegistroCalidadId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RegistroCalidadId);
    }
}
