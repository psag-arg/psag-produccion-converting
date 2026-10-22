using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class AnalisisCambioConfiguration : IEntityTypeConfiguration<AnalisisCambio>
{
    public void Configure(EntityTypeBuilder<AnalisisCambio> builder)
    {
        // Mismo CHECK que database/20.
        builder.ToTable("AnalisisCambios", "mnt", t =>
            t.HasCheckConstraint("CK_AnalisisCambios_Campo", "[Campo] IN ('SOLUCION', 'CAUSA', 'PLAN_ACCION', 'FOTO')"));

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
