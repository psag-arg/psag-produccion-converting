using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class ParametroConfiguration : IEntityTypeConfiguration<Parametro>
{
    public void Configure(EntityTypeBuilder<Parametro> builder)
    {
        // Mismo CHECK que database/21 (con OK_NOOK).
        builder.ToTable("Parametros", "mnt", t =>
            t.HasCheckConstraint("CK_Parametros_TipoDato", "[TipoDato] IN ('NUMERICO', 'RANGO', 'TEXTO', 'OK_NOOK')"));

        builder.HasKey(x => x.ParametroId);

        builder.Property(x => x.Codigo)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Nombre)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Seccion)
            .HasMaxLength(100);

        builder.Property(x => x.UnidadMedida)
            .HasMaxLength(50);

        builder.Property(x => x.TipoDato)
            .IsRequired()
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<TipoDatoParametro>(v));

        // T-545: rango y frecuencia de la plantilla de Control de Proceso por línea
        builder.Property(x => x.ValorMin)
            .HasPrecision(18, 4);

        builder.Property(x => x.ValorMax)
            .HasPrecision(18, 4);

        builder.Property(x => x.Frecuencia)
            .IsRequired()
            .HasMaxLength(30)
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<FrecuenciaControl>(v));

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.HasOne(x => x.GrupoParametro)
            .WithMany(g => g.Parametros)
            .HasForeignKey(x => x.GrupoParametroId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.GrupoParametroId);
        builder.HasIndex(x => x.Activo);
    }
}
