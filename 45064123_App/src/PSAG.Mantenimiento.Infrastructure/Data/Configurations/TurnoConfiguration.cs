using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class TurnoConfiguration : IEntityTypeConfiguration<Turno>
{
    public void Configure(EntityTypeBuilder<Turno> builder)
    {
        // Turno automático (script 44): tolerancia del operario, por turno.
        builder.ToTable("ABM_Turnos", "mnt", t =>
            t.HasCheckConstraint("CK_ABM_Turnos_ToleranciaCierreOperador",
                "[ToleranciaCierreOperadorMinutos] >= 0 AND [ToleranciaCierreOperadorMinutos] <= 240"));

        builder.HasKey(x => x.TurnoId);

        builder.Property(x => x.Codigo)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.Nombre)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        // El DEFAULT 30 está en el script 44 (filas existentes); acá no se declara: con HasDefaultValue EF no mandaría el 0.
        builder.Property(x => x.ToleranciaCierreOperadorMinutos).IsRequired();

        builder.HasIndex(x => x.Codigo).IsUnique();
        builder.HasIndex(x => x.Nombre).IsUnique();
        builder.HasIndex(x => x.Activo);
    }
}
