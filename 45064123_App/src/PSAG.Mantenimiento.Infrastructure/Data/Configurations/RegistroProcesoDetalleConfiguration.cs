using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class RegistroProcesoDetalleConfiguration : IEntityTypeConfiguration<RegistroProcesoDetalle>
{
    public void Configure(EntityTypeBuilder<RegistroProcesoDetalle> builder)
    {
        builder.ToTable("RegistrosProcesoDetalle", "mnt");

        builder.HasKey(x => x.RegistroProcesoDetalleId);

        builder.Property(x => x.ValorEstandarSnapshot)
            .HasMaxLength(100);

        builder.Property(x => x.ValorReal)
            .HasPrecision(18, 4);

        builder.Property(x => x.ValorRealTexto)
            .HasMaxLength(200);

        builder.Property(x => x.Observacion)
            .HasMaxLength(1000);

        builder.HasOne(x => x.RegistroProceso)
            .WithMany(r => r.Detalles)
            .HasForeignKey(x => x.RegistroProcesoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Parametro)
            .WithMany(p => p.RegistrosDetalle)
            .HasForeignKey(x => x.ParametroId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RegistroProcesoId, x.ParametroId })
            .IsUnique();

        builder.HasIndex(x => x.FueraDeEstandar)
            .HasFilter("[FueraDeEstandar] = 1");
    }
}
