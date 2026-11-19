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
            t.HasCheckConstraint("CK_ParadasCamara_Tipo", "[Tipo] IN ('PARADA', 'ACUMULACION', 'SIN_MANGAS')");
            t.HasCheckConstraint("CK_ParadasCamara_Duracion", "[DuracionSegundos] > 0");
            // Script 31: marca de umbral, las dos juntas (o las dos NULL en las paradas anteriores al script).
            t.HasCheckConstraint("CK_ParadasCamara_MarcaUmbral",
                "([SuperaUmbral] IS NULL AND [UmbralSegundos] IS NULL) OR ([SuperaUmbral] IS NOT NULL AND [UmbralSegundos] BETWEEN 30 AND 14400)");
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
        builder.HasIndex(x => new { x.PuntoControlCalidadId, x.FechaHoraInicio }).HasDatabaseName("IX_ParadasCamara_Punto_Inicio");
    }
}

/// <summary>Agrupador de detenciones: misma tabla, CHECK, FKs e índices que database/32_agrupador_detenciones.sql.</summary>
public class DetencionAgrupacionConfiguration : IEntityTypeConfiguration<DetencionAgrupacion>
{
    public void Configure(EntityTypeBuilder<DetencionAgrupacion> builder)
    {
        builder.ToTable("DetencionAgrupacion", "mnt", t =>
        {
            t.HasCheckConstraint("CK_DetencionAgrupacion_Origen", "[Origen] IN ('OPERADOR', 'AUTOMATICO')");
            t.HasCheckConstraint("CK_DetencionAgrupacion_Desactivacion",
                "([Activo] = 1 AND [DesactivadoPorUsuarioId] IS NULL AND [FechaHoraDesactivacion] IS NULL) OR ([Activo] = 0 AND [DesactivadoPorUsuarioId] IS NOT NULL AND [FechaHoraDesactivacion] IS NOT NULL)");
        });

        builder.HasKey(x => x.DetencionAgrupacionId);

        builder.Property(x => x.Origen)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<OrigenAgrupacion>(v))
            .HasMaxLength(20);
        builder.Property(x => x.CreadoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.DesactivadoPorUsuarioId).HasMaxLength(450);

        builder.HasOne(x => x.Detencion).WithMany(d => d.Agrupaciones).HasForeignKey(x => x.DetencionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ParadaCamara).WithMany().HasForeignKey(x => x.ParadaCamaraId).OnDelete(DeleteBehavior.Restrict);

        // Una parada sólo puede estar activa en UN grupo.
        builder.HasIndex(x => x.ParadaCamaraId)
            .IsUnique()
            .HasFilter("[Activo] = 1")
            .HasDatabaseName("UX_DetencionAgrupacion_ParadaActiva");
        builder.HasIndex(x => new { x.DetencionId, x.Activo }).HasDatabaseName("IX_DetencionAgrupacion_DetencionId_Activo");
    }
}

public class AlertaCamaraConfiguration : IEntityTypeConfiguration<AlertaCamara>
{
    public void Configure(EntityTypeBuilder<AlertaCamara> builder)
    {
        builder.ToTable("AlertasCamara", "mnt", t =>
        {
            t.HasCheckConstraint("CK_AlertasCamara_Tipo", "[Tipo] IN ('CONGELADA', 'SIN_VIDEO')");
            t.HasCheckConstraint("CK_AlertasCamara_Fin", "[FechaHoraFin] IS NULL OR [FechaHoraFin] >= [FechaHoraInicio]");
        });

        builder.HasKey(x => x.AlertaCamaraId);
        builder.Ignore(x => x.Activa);

        builder.Property(x => x.Tipo)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<TipoAlertaCamara>(v))
            .HasMaxLength(20);
        builder.Property(x => x.IdentificadorExterno).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Detalle).HasMaxLength(500);

        builder.HasOne(x => x.PuntoControl).WithMany().HasForeignKey(x => x.PuntoControlCalidadId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Linea).WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IdentificadorExterno).IsUnique().HasDatabaseName("UX_AlertasCamara_IdentificadorExterno");
        builder.HasIndex(x => new { x.PuntoControlCalidadId, x.FechaHoraInicio }).HasDatabaseName("IX_AlertasCamara_Punto_Inicio");
    }
}
