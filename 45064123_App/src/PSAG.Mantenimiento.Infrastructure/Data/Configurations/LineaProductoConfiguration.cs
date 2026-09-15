using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class LineaProductoConfiguration : IEntityTypeConfiguration<LineaProducto>
{
    public void Configure(EntityTypeBuilder<LineaProducto> builder)
    {
        builder.ToTable("LineasProductos", "mnt");

        builder.HasKey(x => new { x.LineaId, x.ProductoId });

        builder.HasOne(x => x.Linea)
            .WithMany(l => l.LineasProductos)
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Producto)
            .WithMany(p => p.LineasProductos)
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
