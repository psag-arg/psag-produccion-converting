using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class EstandarParametroConfiguration : IEntityTypeConfiguration<EstandarParametro>
{
    public void Configure(EntityTypeBuilder<EstandarParametro> builder)
    {
        builder.ToTable("EstandaresParametro", "mnt");

        builder.HasKey(x => x.EstandarParametroId);

        builder.Property(x => x.ValorEstandarTexto)
            .HasMaxLength(100);

        builder.Property(x => x.ValorMin)
            .HasPrecision(18, 4);

        builder.Property(x => x.ValorMax)
            .HasPrecision(18, 4);

        builder.Property(x => x.Tolerancia)
            .HasPrecision(18, 4);

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(x => x.Linea)
            .WithMany(l => l.Estandares)
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Producto)
            .WithMany(p => p.Estandares)
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Parametro)
            .WithMany(p => p.Estandares)
            .HasForeignKey(x => x.ParametroId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.LineaId, x.ProductoId, x.ParametroId })
            .IsUnique();

        builder.HasIndex(x => new { x.LineaId, x.ProductoId });
    }
}
