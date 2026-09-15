using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class DetencionNotaConfiguration : IEntityTypeConfiguration<DetencionNota>
{
    public void Configure(EntityTypeBuilder<DetencionNota> builder)
    {
        builder.ToTable("DetencionesNotas", "mnt");

        builder.HasKey(x => x.DetencionNotaId);

        builder.Property(x => x.Texto)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.UsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.TipoNota)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<TipoNota>(v))
            .HasMaxLength(20);

        builder.HasOne(x => x.Detencion)
            .WithMany(x => x.Notas)
            .HasForeignKey(x => x.DetencionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DetencionId);
        builder.HasIndex(x => x.FechaHora);
    }
}
