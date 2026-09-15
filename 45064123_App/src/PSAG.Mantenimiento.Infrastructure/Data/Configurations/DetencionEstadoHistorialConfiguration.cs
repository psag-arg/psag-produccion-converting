using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class DetencionEstadoHistorialConfiguration : IEntityTypeConfiguration<DetencionEstadoHistorial>
{
    public void Configure(EntityTypeBuilder<DetencionEstadoHistorial> builder)
    {
        builder.ToTable("DetencionesEstadoHistorial", "mnt");

        builder.HasKey(x => x.DetencionEstadoHistorialId);

        builder.Property(x => x.EstadoAnterior)
            .HasConversion(
                v => v.HasValue ? v.Value.ToString() : null,
                v => v != null ? Enum.Parse<EstadoDetencion>(v) : null)
            .HasMaxLength(30);

        builder.Property(x => x.EstadoNuevo)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<EstadoDetencion>(v))
            .HasMaxLength(30);

        builder.Property(x => x.UsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Observacion)
            .HasMaxLength(2000);

        builder.Property(x => x.OrigenAccion)
            .HasMaxLength(50);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(50);

        builder.HasOne(x => x.Detencion)
            .WithMany(x => x.HistorialEstados)
            .HasForeignKey(x => x.DetencionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DetencionId);
        builder.HasIndex(x => x.FechaHora);
    }
}
