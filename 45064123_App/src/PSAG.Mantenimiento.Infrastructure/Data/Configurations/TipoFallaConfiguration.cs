using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class TipoFallaConfiguration : IEntityTypeConfiguration<TipoFalla>
{
    public void Configure(EntityTypeBuilder<TipoFalla> builder)
    {
        builder.ToTable("TiposFalla", "mnt");

        builder.HasKey(x => x.TipoFallaId);

        builder.Property(x => x.Codigo)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Descripcion)
            .HasMaxLength(500);

        builder.Property(x => x.Color)
            .HasMaxLength(7);

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasIndex(x => x.Codigo).IsUnique();
        builder.HasIndex(x => x.Nombre).IsUnique();
        builder.HasIndex(x => x.Activo);
    }
}
