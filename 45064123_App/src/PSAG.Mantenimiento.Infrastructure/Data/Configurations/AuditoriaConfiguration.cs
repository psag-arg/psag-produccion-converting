using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class AuditoriaConfiguration : IEntityTypeConfiguration<Auditoria>
{
    public void Configure(EntityTypeBuilder<Auditoria> builder)
    {
        builder.ToTable("Auditoria", "mnt");

        builder.HasKey(x => x.AuditoriaId);

        builder.Property(x => x.UsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Accion)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<TipoAccionAuditoria>(v))
            .HasMaxLength(50);

        builder.Property(x => x.Entidad)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.EntidadId)
            .HasMaxLength(100);

        builder.Property(x => x.DireccionIp)
            .HasMaxLength(50);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(50);

        builder.Property(x => x.Descripcion)
            .HasMaxLength(500);

        builder.HasIndex(x => x.FechaHora);
        builder.HasIndex(x => x.UsuarioId);
        builder.HasIndex(x => x.Accion);
        builder.HasIndex(x => x.Entidad);
        builder.HasIndex(x => new { x.FechaHora, x.UsuarioId });
    }
}
