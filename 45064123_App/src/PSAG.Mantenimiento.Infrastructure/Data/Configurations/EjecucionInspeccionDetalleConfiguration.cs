using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class EjecucionInspeccionDetalleConfiguration : IEntityTypeConfiguration<EjecucionInspeccionDetalle>
{
    public void Configure(EntityTypeBuilder<EjecucionInspeccionDetalle> builder)
    {
        builder.ToTable("EjecucionesInspeccionDetalle", "mnt");

        builder.HasKey(d => d.EjecucionInspeccionDetalleId);

        builder.Property(d => d.Resultado)
            .HasConversion<byte>();

        builder.Property(d => d.Observacion)
            .HasMaxLength(500);

        builder.HasOne(d => d.ItemPlanInspeccion)
            .WithMany()
            .HasForeignKey(d => d.ItemPlanInspeccionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.EjecucionInspeccionId);
    }
}
