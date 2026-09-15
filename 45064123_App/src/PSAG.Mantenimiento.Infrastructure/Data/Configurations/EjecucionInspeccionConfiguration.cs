using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class EjecucionInspeccionConfiguration : IEntityTypeConfiguration<EjecucionInspeccion>
{
    public void Configure(EntityTypeBuilder<EjecucionInspeccion> builder)
    {
        builder.ToTable("EjecucionesInspeccion", "mnt");

        builder.HasKey(e => e.EjecucionInspeccionId);

        builder.Property(e => e.UsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(e => e.ObservacionesGenerales)
            .HasMaxLength(1000);

        builder.Property(e => e.RowVersion)
            .IsRowVersion();

        builder.HasOne(e => e.PlanInspeccion)
            .WithMany(p => p.Ejecuciones)
            .HasForeignKey(e => e.PlanInspeccionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Turno)
            .WithMany()
            .HasForeignKey(e => e.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Detalles)
            .WithOne(d => d.EjecucionInspeccion)
            .HasForeignKey(d => d.EjecucionInspeccionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.PlanInspeccionId, e.Fecha });
        builder.HasIndex(e => e.Fecha);
    }
}
