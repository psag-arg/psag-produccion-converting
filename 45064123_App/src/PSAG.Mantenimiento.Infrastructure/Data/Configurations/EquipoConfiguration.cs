using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class EquipoConfiguration : IEntityTypeConfiguration<Equipo>
{
    public void Configure(EntityTypeBuilder<Equipo> builder)
    {
        builder.ToTable("Equipos", "mnt");

        builder.HasKey(x => x.EquipoId);

        builder.Property(x => x.Codigo)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Descripcion)
            .HasMaxLength(500);

        builder.Property(x => x.Critico)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(x => x.Linea)
            .WithMany(x => x.Equipos)
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.LineaId);
        builder.HasIndex(x => x.Activo);
        builder.HasIndex(x => x.Codigo).IsUnique();
        builder.HasIndex(x => new { x.LineaId, x.Nombre }).IsUnique();
    }
}
