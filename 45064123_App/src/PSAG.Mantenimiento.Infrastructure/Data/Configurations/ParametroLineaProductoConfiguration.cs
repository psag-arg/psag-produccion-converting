using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class ParametroLineaProductoConfiguration : IEntityTypeConfiguration<ParametroLineaProducto>
{
    public void Configure(EntityTypeBuilder<ParametroLineaProducto> builder)
    {
        builder.ToTable("PRD_ParametrosLineaProducto", "mnt", t =>
        {
            t.HasCheckConstraint("CK_PRD_ParametrosLineaProducto_Mangas", "[MangasObjetivoTurno] IS NULL OR [MangasObjetivoTurno] >= 0");
            t.HasCheckConstraint("CK_PRD_ParametrosLineaProducto_Bolsones", "[BolsonesObjetivoTurno] IS NULL OR [BolsonesObjetivoTurno] >= 0");
            t.HasCheckConstraint("CK_PRD_ParametrosLineaProducto_MinutosTurno", "[MinutosTurnoReferencia] > 0");
        });

        builder.HasKey(x => x.ParametroLineaProductoId);


        builder.Property(x => x.MinutosTurnoReferencia)
            .HasDefaultValue(ParametroLineaProducto.MinutosTurnoReferenciaPorDefecto);

        builder.HasOne(x => x.Linea)
            .WithMany(l => l.ParametrosLineaProducto)
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ProductoMaestro)
            .WithMany(p => p.ParametrosLineaProducto)
            .HasForeignKey(x => x.ProductoMaestroId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.LineaId, x.ProductoMaestroId, x.VigenteDesde })
            .IsUnique();
    }
}
