using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class ReglaAnalisisConfiguration : IEntityTypeConfiguration<ReglaAnalisis>
{
    public void Configure(EntityTypeBuilder<ReglaAnalisis> builder)
    {
        builder.ToTable("ReglasAnalisis", "mnt", t =>
        {
            t.HasCheckConstraint("CK_ReglasAnalisis_MinutosMinimos",
                "[MinutosMinimos] >= 0");
            t.HasCheckConstraint("CK_ReglasAnalisis_Responsable",
                "([ResponsableRol] IS NULL AND [ResponsableUsuarioId] IS NOT NULL) OR ([ResponsableRol] IS NOT NULL AND [ResponsableUsuarioId] IS NULL)");
        });

        builder.HasKey(x => x.ReglaAnalisisId);

        builder.Property(x => x.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.MinutosMinimos)
            .IsRequired();

        builder.Property(x => x.ResponsableRol)
            .HasMaxLength(50);

        builder.Property(x => x.ResponsableUsuarioId)
            .HasMaxLength(450);

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

        builder.HasOne(x => x.Equipo)
            .WithMany()
            .HasForeignKey(x => x.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TipoFalla)
            .WithMany()
            .HasForeignKey(x => x.TipoFallaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.LineaId);
        builder.HasIndex(x => x.TipoFallaId);
        builder.HasIndex(x => x.EquipoId);
    }
}
