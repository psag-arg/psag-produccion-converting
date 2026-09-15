using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class DetalleFallaConfiguration : IEntityTypeConfiguration<DetalleFalla>
{
    public void Configure(EntityTypeBuilder<DetalleFalla> builder)
    {
        builder.ToTable("DetallesFalla", "mnt");

        builder.HasKey(x => x.DetalleFallaId);

        builder.Property(x => x.Codigo)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Descripcion)
            .HasMaxLength(500);

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        // Relaciones opcionales (puede ser genérico)
        builder.HasOne(x => x.TipoFalla)
            .WithMany(x => x.DetallesFalla)
            .HasForeignKey(x => x.TipoFallaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipo)
            .WithMany(x => x.DetallesFalla)
            .HasForeignKey(x => x.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Codigo).IsUnique();
        builder.HasIndex(x => x.TipoFallaId);
        builder.HasIndex(x => x.EquipoId);
        builder.HasIndex(x => x.Activo);
        builder.HasIndex(x => new { x.TipoFallaId, x.EquipoId, x.Nombre }).IsUnique();
    }
}
