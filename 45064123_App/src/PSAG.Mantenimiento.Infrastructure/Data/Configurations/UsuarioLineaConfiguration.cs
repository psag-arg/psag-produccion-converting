using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class UsuarioLineaConfiguration : IEntityTypeConfiguration<UsuarioLinea>
{
    public void Configure(EntityTypeBuilder<UsuarioLinea> builder)
    {
        builder.ToTable("UsuariosLineas", "mnt");

        builder.HasKey(x => x.UsuarioLineaId);

        builder.Property(x => x.UsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.AsignadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(x => x.Linea)
            .WithMany(x => x.UsuariosLineas)
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Turno)
            .WithMany()
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UsuarioId);
        builder.HasIndex(x => new { x.UsuarioId, x.LineaId }).IsUnique();
    }
}
