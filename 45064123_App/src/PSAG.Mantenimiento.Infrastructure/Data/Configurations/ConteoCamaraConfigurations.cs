using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

// T-610 Conteo de producción y paradas por cámara. Mismas tablas, CHECK, FKs e índices que
// database/25_t610_conteo_camara.sql.

public class ConteoCamaraMinutoConfiguration : IEntityTypeConfiguration<ConteoCamaraMinuto>
{
    public void Configure(EntityTypeBuilder<ConteoCamaraMinuto> builder)
    {
        builder.ToTable("ConteosCamaraMinuto", "mnt", t =>
        {
            t.HasCheckConstraint("CK_ConteosCamaraMinuto_Estado",
                "[Estado] IN ('MARCHA', 'PARADA', 'ACUMULACION', 'SIN_SENAL')");
            t.HasCheckConstraint("CK_ConteosCamaraMinuto_Carril", "[Carril] BETWEEN 1 AND 16");
            t.HasCheckConstraint("CK_ConteosCamaraMinuto_Rollos", "[Rollos] >= 0");
            t.HasCheckConstraint("CK_ConteosCamaraMinuto_Segundos",
                "[SegundosMarcha] >= 0 AND [SegundosParada] >= 0 AND [SegundosAcumulacion] >= 0 AND [SegundosSinSenal] >= 0 AND [SegundosMarcha] + [SegundosParada] + [SegundosAcumulacion] + [SegundosSinSenal] = 60");
        });

        builder.HasKey(x => x.ConteoCamaraMinutoId);

        builder.Property(x => x.Rollos).HasPrecision(9, 1);
        builder.Property(x => x.Estado)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<EstadoCarrilCamara>(v))
            .HasMaxLength(20);

        builder.HasOne(x => x.Linea).WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PuntoControl).WithMany().HasForeignKey(x => x.PuntoControlCalidadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PuntoControlCalidadId, x.Minuto, x.Carril })
            .IsUnique()
            .HasDatabaseName("UX_ConteosCamaraMinuto_Punto_Minuto_Carril");
        builder.HasIndex(x => new { x.LineaId, x.Minuto }).HasDatabaseName("IX_ConteosCamaraMinuto_Linea_Minuto");
    }
}

public class ParadaCamaraConfiguration : IEntityTypeConfiguration<ParadaCamara>
{
    public void Configure(EntityTypeBuilder<ParadaCamara> builder)
    {
        builder.ToTable("ParadasCamara", "mnt", t =>
        {
            t.HasCheckConstraint("CK_ParadasCamara_Tipo", "[Tipo] IN ('PARADA', 'ACUMULACION')");
            t.HasCheckConstraint("CK_ParadasCamara_Duracion", "[DuracionSegundos] > 0");
        });

        builder.HasKey(x => x.ParadaCamaraId);

        builder.Property(x => x.Tipo)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<TipoParadaCamara>(v))
            .HasMaxLength(20);
        builder.Property(x => x.Carriles).IsRequired().HasMaxLength(50);
        builder.Property(x => x.IdentificadorExterno).IsRequired().HasMaxLength(100);

        builder.HasOne(x => x.Linea).WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PuntoControl).WithMany().HasForeignKey(x => x.PuntoControlCalidadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Turno).WithMany().HasForeignKey(x => x.TurnoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IdentificadorExterno).IsUnique().HasDatabaseName("UX_ParadasCamara_IdentificadorExterno");
        builder.HasIndex(x => new { x.LineaId, x.FechaHoraInicio }).HasDatabaseName("IX_ParadasCamara_Linea_Inicio");
    }
}
