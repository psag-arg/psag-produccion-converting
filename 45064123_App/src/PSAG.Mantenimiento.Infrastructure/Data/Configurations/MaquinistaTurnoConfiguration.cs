using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class MaquinistaTurnoConfiguration : IEntityTypeConfiguration<MaquinistaTurno>
{
    public void Configure(EntityTypeBuilder<MaquinistaTurno> builder)
    {
        builder.ToTable("MaquinistasTurno", "mnt", t =>
            t.HasCheckConstraint("CK_MaquinistasTurno_Origen", "[Origen] IN ('LOGIN', 'SUPERVISOR')"));

        builder.HasKey(x => x.MaquinistaTurnoId);

        builder.Property(x => x.UsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.RegistradoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Observacion).HasMaxLength(500);
        builder.Property(x => x.Origen)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<OrigenMaquinistaTurno>(v))
            .HasMaxLength(20);

        builder.HasOne(x => x.Linea).WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.UsuarioLinea).WithMany().HasForeignKey(x => x.UsuarioLineaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.FechaTurno, x.LineaId, x.TurnoId }).HasDatabaseName("IX_MaquinistasTurno_Fecha_Linea_Turno");
        builder.HasIndex(x => x.UsuarioLineaId).HasDatabaseName("IX_MaquinistasTurno_UsuarioLinea");
    }
}
