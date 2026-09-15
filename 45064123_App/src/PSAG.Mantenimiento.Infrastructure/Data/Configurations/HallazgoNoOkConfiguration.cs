using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class HallazgoNoOkConfiguration : IEntityTypeConfiguration<HallazgoNoOk>
{
    public void Configure(EntityTypeBuilder<HallazgoNoOk> builder)
    {
        builder.ToTable("HallazgosNoOk", "mnt");

        builder.HasKey(h => h.HallazgoNoOkId);

        builder.Property(h => h.UsuarioReporteId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(h => h.Descripcion)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(h => h.UsuarioResolucionId)
            .HasMaxLength(450);

        builder.Property(h => h.ObservacionResolucion)
            .HasMaxLength(1000);

        builder.Property(h => h.AccionCorrectivaDescripcion)
            .HasMaxLength(500);

        builder.HasOne(h => h.EjecucionInspeccionDetalle)
            .WithMany()
            .HasForeignKey(h => h.EjecucionInspeccionDetalleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Linea)
            .WithMany()
            .HasForeignKey(h => h.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.Equipo)
            .WithMany()
            .HasForeignKey(h => h.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(h => new { h.LineaId, h.Resuelto })
            .HasFilter("[Resuelto] = 0");

        builder.HasIndex(h => new { h.Resuelto, h.FechaHoraHallazgo })
            .HasFilter("[Resuelto] = 0");
    }
}
