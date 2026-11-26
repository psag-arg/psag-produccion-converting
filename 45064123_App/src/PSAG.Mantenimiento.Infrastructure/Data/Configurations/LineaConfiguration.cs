using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class LineaConfiguration : IEntityTypeConfiguration<Linea>
{
    public void Configure(EntityTypeBuilder<Linea> builder)
    {
        builder.ToTable("ABM_Lineas", "mnt");

        builder.HasKey(x => x.LineaId);

        // Como en la base (04_create_tables.sql: NVARCHAR(50)).
        builder.Property(x => x.Codigo)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Descripcion)
            .HasMaxLength(500);

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasIndex(x => x.Nombre)
            .IsUnique();

        builder.HasIndex(x => x.Activo);
    }
}
