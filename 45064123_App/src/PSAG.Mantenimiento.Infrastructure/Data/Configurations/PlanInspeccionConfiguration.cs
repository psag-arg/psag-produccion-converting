using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class PlanInspeccionConfiguration : IEntityTypeConfiguration<PlanInspeccion>
{
    public void Configure(EntityTypeBuilder<PlanInspeccion> builder)
    {
        builder.ToTable("PlanesInspeccion", "mnt");

        builder.HasKey(p => p.PlanInspeccionId);

        builder.Property(p => p.Codigo)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(p => p.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Descripcion)
            .HasMaxLength(500);

        builder.Property(p => p.PuestoResponsable)
            .HasConversion<byte>();

        builder.HasIndex(p => p.Codigo)
            .IsUnique();

        builder.HasOne(p => p.Linea)
            .WithMany()
            .HasForeignKey(p => p.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Equipo)
            .WithMany()
            .HasForeignKey(p => p.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Items)
            .WithOne(i => i.PlanInspeccion)
            .HasForeignKey(i => i.PlanInspeccionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Ejecuciones)
            .WithOne(e => e.PlanInspeccion)
            .HasForeignKey(e => e.PlanInspeccionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
