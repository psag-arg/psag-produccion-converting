using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class AnalisisCambioConfiguration : IEntityTypeConfiguration<AnalisisCambio>
{
    public void Configure(EntityTypeBuilder<AnalisisCambio> builder)
    {
        builder.ToTable("AnalisisCambios", "mnt");

        builder.HasKey(x => x.AnalisisCambioId);

        builder.Property(x => x.Campo)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.ValorAnterior)
            .HasMaxLength(4000);

        builder.Property(x => x.ValorNuevo)
            .HasMaxLength(4000);

        builder.Property(x => x.UsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(x => x.Detencion)
            .WithMany(x => x.CambiosAnalisis)
            .HasForeignKey(x => x.DetencionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DetencionId);
    }
}
