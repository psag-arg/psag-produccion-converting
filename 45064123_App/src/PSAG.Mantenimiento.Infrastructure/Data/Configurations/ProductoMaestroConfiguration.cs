using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class ProductoMaestroConfiguration : IEntityTypeConfiguration<ProductoMaestro>
{
    public void Configure(EntityTypeBuilder<ProductoMaestro> builder)
    {
        builder.ToTable("ABM_ProductosMaestro", "mnt");

        builder.HasKey(x => x.ProductoMaestroId);

        builder.Property(x => x.Codigo)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Descripcion)
            .HasMaxLength(500);

        builder.Property(x => x.TipoProducto)
            .HasMaxLength(30);

        builder.Property(x => x.MetrosPorManga)
            .HasPrecision(9, 2);

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasIndex(x => x.Codigo)
            .IsUnique();

        builder.HasIndex(x => x.Activo);
    }
}
