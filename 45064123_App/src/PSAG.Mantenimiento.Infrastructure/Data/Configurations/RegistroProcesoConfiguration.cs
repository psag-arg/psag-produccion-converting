using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class RegistroProcesoConfiguration : IEntityTypeConfiguration<RegistroProceso>
{
    public void Configure(EntityTypeBuilder<RegistroProceso> builder)
    {
        builder.ToTable("RegistrosProceso", "mnt");

        builder.HasKey(x => x.RegistroProcesoId);

        builder.Property(x => x.UsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Estado)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<EstadoRegistroProceso>(v));

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasOne(x => x.Linea)
            .WithMany(l => l.RegistrosProceso)
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Producto)
            .WithMany(p => p.RegistrosProceso)
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Turno)
            .WithMany()
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.LineaId, x.ProductoId, x.TurnoId, x.Fecha })
            .IsUnique();

        builder.HasIndex(x => x.LineaId);
        builder.HasIndex(x => x.ProductoId);
        builder.HasIndex(x => x.Fecha);
        builder.HasIndex(x => x.Estado);
    }
}
