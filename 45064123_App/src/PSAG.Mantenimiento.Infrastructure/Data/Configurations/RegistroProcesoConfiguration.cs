using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class RegistroProcesoConfiguration : IEntityTypeConfiguration<RegistroProceso>
{
    public void Configure(EntityTypeBuilder<RegistroProceso> builder)
    {
        builder.ToTable("CPR_Registros", "mnt");

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

        builder.HasOne(x => x.ProductoMaestro)
            .WithMany(p => p.RegistrosProceso)
            .HasForeignKey(x => x.ProductoMaestroId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Turno)
            .WithMany()
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        // T-798 etapa 3: NULL en los registros anteriores; se completa al crear.
        builder.HasOne(x => x.TurnoProduccion)
            .WithMany()
            .HasForeignKey(x => x.TurnoProduccionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.TurnoProduccionId);

        builder.HasIndex(x => new { x.LineaId, x.ProductoMaestroId, x.TurnoId, x.Fecha })
            .IsUnique();

        builder.HasIndex(x => x.LineaId);
        builder.HasIndex(x => x.ProductoMaestroId);
        builder.HasIndex(x => x.Fecha);
        builder.HasIndex(x => x.Estado);
    }
}
