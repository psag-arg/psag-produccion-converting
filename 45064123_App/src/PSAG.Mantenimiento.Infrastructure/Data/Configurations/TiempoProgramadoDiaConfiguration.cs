using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class TiempoProgramadoDiaConfiguration : IEntityTypeConfiguration<TiempoProgramadoDia>
{
    public void Configure(EntityTypeBuilder<TiempoProgramadoDia> builder)
    {
        builder.ToTable("TiemposProgramadosDia", "mnt", t =>
        {
            // CAST: SQLite guarda decimal como TEXT y compararía como texto.
            t.HasCheckConstraint("CK_TiemposProgramadosDia_Horas",
                "CAST([Horas] AS FLOAT) >= 0 AND CAST([Horas] AS FLOAT) <= 24");
            t.HasCheckConstraint("CK_TiemposProgramadosDia_Origen",
                "[Origen] IN ('EXCEPCION', 'HISTORICO')");
        });

        builder.HasKey(x => x.TiempoProgramadoDiaId);

        builder.Property(x => x.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.Horas)
            .IsRequired()
            .HasPrecision(5, 2);

        builder.Property(x => x.Motivo)
            .HasMaxLength(200);

        builder.Property(x => x.Origen)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue("EXCEPCION");

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(x => x.Linea)
            .WithMany()
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una excepción por línea y fecha. Este índice también cubre las búsquedas por LineaId.
        builder.HasIndex(x => new { x.LineaId, x.Fecha })
            .IsUnique()
            .HasDatabaseName("UX_TiemposProgramadosDia_LineaId_Fecha");
    }
}
