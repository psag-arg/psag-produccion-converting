using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class ProductoSkuConfiguration : IEntityTypeConfiguration<ProductoSku>
{
    public void Configure(EntityTypeBuilder<ProductoSku> builder)
    {
        // Mismos CHECKs que database/32_t798_maestros_productos.sql.
        builder.ToTable("ABM_ProductosSku", "mnt", t =>
        {
            t.HasCheckConstraint("CK_ABM_ProductosSku_TipoSku", "[TipoSku] IN ('BOLSON', 'PALLET')");
            t.HasCheckConstraint("CK_ABM_ProductosSku_Bolson",
                "[TipoSku] <> 'BOLSON' OR ([SkuContenidoId] IS NULL AND [BolsonesPorPallet] IS NULL)");
            t.HasCheckConstraint("CK_ABM_ProductosSku_Pallet",
                "[TipoSku] <> 'PALLET' OR [PaquetesPorBolson] IS NULL");
            t.HasCheckConstraint("CK_ABM_ProductosSku_PaquetesPorBolson",
                "[PaquetesPorBolson] IS NULL OR [PaquetesPorBolson] > 0");
            t.HasCheckConstraint("CK_ABM_ProductosSku_BolsonesPorPallet",
                "[BolsonesPorPallet] IS NULL OR [BolsonesPorPallet] > 0");
        });

        builder.HasKey(x => x.ProductoSkuId);

        builder.Property(x => x.CodigoSku)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Descripcion)
            .HasMaxLength(200);

        builder.Property(x => x.TipoSku)
            .IsRequired()
            .HasMaxLength(10);


        builder.HasOne(x => x.ProductoMaestro)
            .WithMany(p => p.Skus)
            .HasForeignKey(x => x.ProductoMaestroId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SkuContenido)
            .WithMany()
            .HasForeignKey(x => x.SkuContenidoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.CodigoSku).IsUnique();
        builder.HasIndex(x => x.ProductoMaestroId);
    }
}
