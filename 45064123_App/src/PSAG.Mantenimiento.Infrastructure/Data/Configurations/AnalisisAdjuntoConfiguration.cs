using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class AnalisisAdjuntoConfiguration : IEntityTypeConfiguration<AnalisisAdjunto>
{
    public void Configure(EntityTypeBuilder<AnalisisAdjunto> builder)
    {
        // Mismo CHECK que database/20. El tamaño máximo no va en la base: lo controla AdjuntoImagenReglas.
        builder.ToTable("AnalisisAdjuntos", "mnt", t =>
            t.HasCheckConstraint("CK_AnalisisAdjuntos_ContentType", "[ContentType] IN ('image/jpeg', 'image/png')"));

        builder.HasKey(x => x.AnalisisAdjuntoId);

        builder.Property(x => x.NombreArchivo)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.NombreOriginal)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.UsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(x => x.Detencion)
            .WithMany(x => x.AdjuntosAnalisis)
            .HasForeignKey(x => x.DetencionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DetencionId);
        builder.HasIndex(x => x.NombreArchivo).IsUnique();
    }
}
