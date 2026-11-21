using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class UsuarioLineaConfiguration : IEntityTypeConfiguration<OperadorLinea>
{
    public void Configure(EntityTypeBuilder<OperadorLinea> builder)
    {
        builder.ToTable("ABM_OperadoresLinea", "mnt");

        builder.HasKey(x => x.UsuarioLineaId);

        builder.Property(x => x.UsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.AsignadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(x => x.Linea)
            .WithMany(x => x.OperadoresLinea)
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.TurnoHabitual)
            .WithMany()
            .HasForeignKey(x => x.TurnoHabitualId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UsuarioId);
        builder.HasIndex(x => new { x.UsuarioId, x.LineaId }).IsUnique();
    }
}
