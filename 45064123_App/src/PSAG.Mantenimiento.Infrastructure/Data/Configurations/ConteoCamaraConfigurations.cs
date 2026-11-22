using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

// T-610 Conteo de producción y paradas por cámara. Mismas tablas, CHECK, FKs e índices que
// database/25_t610_conteo_camara.sql, con los cambios de T-798 etapa 4 (database/36_t798_camaras.sql):
// maestro único CAM_Camaras; conteos, paradas y alertas cuelgan de la cámara y del turno de producción.

public class CamaraConfiguration : IEntityTypeConfiguration<Camara>
{
    public void Configure(EntityTypeBuilder<Camara> builder)
    {
        builder.ToTable("CAM_Camaras", "mnt", t =>
        {
            t.HasCheckConstraint("CK_CAM_Camaras_Funcion", "[Funcion] IN ('CONTEO', 'CALIDAD')");
            t.HasCheckConstraint("CK_CAM_Camaras_Magnitud",
                "[Magnitud] IS NULL OR [Magnitud] IN ('MANGAS', 'ROLLOS', 'PAQUETES', 'BOLSONES', 'PALLETS')");
            t.HasCheckConstraint("CK_CAM_Camaras_MetodoDeteccion", "[MetodoDeteccion] IS NULL OR [MetodoDeteccion] IN ('CARRILES', 'PASOS')");
            t.HasCheckConstraint("CK_CAM_Camaras_CantidadCarriles", "[CantidadCarriles] IS NULL OR [CantidadCarriles] BETWEEN 1 AND 16");
            t.HasCheckConstraint("CK_CAM_Camaras_Umbral", "[UmbralDetencionSegundos] IS NULL OR [UmbralDetencionSegundos] BETWEEN 30 AND 14400");
            t.HasCheckConstraint("CK_CAM_Camaras_Canal", "[NumeroCanalGrabador] IS NULL OR [NumeroCanalGrabador] BETWEEN 1 AND 999");
            // Por función: CONTEO necesita magnitud y método; CALIDAD no tiene nada del conteo.
            t.HasCheckConstraint("CK_CAM_Camaras_PorFuncion",
                "([Funcion] = 'CONTEO' AND [Magnitud] IS NOT NULL AND [MetodoDeteccion] IS NOT NULL AND ([CreaDetenciones] = 0 OR [DetectaParadas] = 1)) " +
                "OR ([Funcion] = 'CALIDAD' AND [Magnitud] IS NULL AND [MetodoDeteccion] IS NULL AND [CantidadCarriles] IS NULL " +
                "AND [DetectaParadas] = 0 AND [CreaDetenciones] = 0 AND [UmbralDetencionSegundos] IS NULL AND [CuentaProduccionOficial] = 0)");
        });

        builder.HasKey(x => x.CamaraId);
        builder.Ignore(x => x.EsConteo);

        builder.Property(x => x.Codigo).IsRequired().HasMaxLength(Camara.LargoCodigo);
        builder.Property(x => x.Nombre).IsRequired().HasMaxLength(Camara.LargoNombre);
        builder.Property(x => x.Funcion).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Magnitud).HasMaxLength(10);
        builder.Property(x => x.MetodoDeteccion).HasMaxLength(10);
        builder.Property(x => x.DetectaParadas).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CreaDetenciones).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.CuentaProduccionOficial).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.Habilitada).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.Activo).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.CreadoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ModificadoPorUsuarioId).IsRequired().HasMaxLength(450);

        builder.HasOne(x => x.Linea).WithMany().HasForeignKey(x => x.LineaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Equipo).WithMany().HasForeignKey(x => x.EquipoId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.Codigo).IsUnique().HasDatabaseName("UX_CAM_Camaras_Codigo");
        builder.HasIndex(x => x.LineaId).HasDatabaseName("IX_CAM_Camaras_LineaId");
        // Una sola cámara oficial por línea y magnitud.
        builder.HasIndex(x => new { x.LineaId, x.Magnitud })
            .IsUnique()
            .HasFilter("[CuentaProduccionOficial] = 1")
            .HasDatabaseName("UX_CAM_Camaras_Oficial_Linea_Magnitud");
    }
}

public class ConteoCamaraMinutoConfiguration : IEntityTypeConfiguration<ConteoCamaraMinuto>
{
    public void Configure(EntityTypeBuilder<ConteoCamaraMinuto> builder)
    {
        builder.ToTable("CAM_ConteosMinuto", "mnt", t =>
        {
            t.HasCheckConstraint("CK_ConteosCamaraMinuto_Estado",
                "[Estado] IN ('MARCHA', 'PARADA', 'ACUMULACION', 'SIN_SENAL')");
            t.HasCheckConstraint("CK_ConteosCamaraMinuto_Carril", "[Carril] BETWEEN 1 AND 16");
            t.HasCheckConstraint("CK_ConteosCamaraMinuto_Cantidad", "[Cantidad] >= 0");
            t.HasCheckConstraint("CK_ConteosCamaraMinuto_Segundos",
                "[SegundosMarcha] >= 0 AND [SegundosParada] >= 0 AND [SegundosAcumulacion] >= 0 AND [SegundosSinSenal] >= 0 AND [SegundosMarcha] + [SegundosParada] + [SegundosAcumulacion] + [SegundosSinSenal] = 60");
        });

        builder.HasKey(x => x.ConteoCamaraMinutoId);

        builder.Property(x => x.Cantidad).HasPrecision(9, 1);
        builder.Property(x => x.Estado)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<EstadoCarrilCamara>(v))
            .HasMaxLength(20);

        builder.HasOne(x => x.Camara).WithMany().HasForeignKey(x => x.CamaraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TurnoProduccion).WithMany().HasForeignKey(x => x.TurnoProduccionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CamaraId, x.Minuto, x.Carril })
            .IsUnique()
            .HasDatabaseName("UX_CAM_ConteosMinuto_Camara_Minuto_Carril");
        builder.HasIndex(x => x.TurnoProduccionId).HasDatabaseName("IX_CAM_ConteosMinuto_TurnoProduccionId");
    }
}

public class ParadaCamaraConfiguration : IEntityTypeConfiguration<ParadaCamara>
{
    public void Configure(EntityTypeBuilder<ParadaCamara> builder)
    {
        builder.ToTable("CAM_Paradas", "mnt", t =>
        {
            t.HasCheckConstraint("CK_ParadasCamara_Tipo", "[Tipo] IN ('PARADA', 'ACUMULACION', 'SIN_MANGAS')");
            t.HasCheckConstraint("CK_ParadasCamara_Duracion", "[DuracionSegundos] > 0");
        });

        builder.HasKey(x => x.ParadaCamaraId);

        builder.Property(x => x.Tipo)
            .IsRequired()
            .HasConversion(v => v.ToString(), v => Enum.Parse<TipoParadaCamara>(v))
            .HasMaxLength(20);
        builder.Property(x => x.Carriles).IsRequired().HasMaxLength(50);
        builder.Property(x => x.IdentificadorExterno).IsRequired().HasMaxLength(100);

        builder.HasOne(x => x.Camara).WithMany().HasForeignKey(x => x.CamaraId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TurnoProduccion).WithMany().HasForeignKey(x => x.TurnoProduccionId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IdentificadorExterno).IsUnique().HasDatabaseName("UX_ParadasCamara_IdentificadorExterno");
        builder.HasIndex(x => new { x.CamaraId, x.FechaHoraInicio }).HasDatabaseName("IX_CAM_Paradas_Camara_Inicio");
        builder.HasIndex(x => x.TurnoProduccionId).HasDatabaseName("IX_CAM_Paradas_TurnoProduccionId");
    }
}

public class AlertaCamaraConfiguration : IEntityTypeConfiguration<AlertaCamara>
{
    public void Configure(EntityTypeBuilder<AlertaCamara> builder)
    {
        builder.ToTable("CAM_Alertas", "mnt", t =>
        {
            t.HasCheckConstraint("CK_AlertasCamara_Tipo", "[Tipo] IN ('CONGELADA', 'SIN_VIDEO', 'SIN_DATOS', 'SIN_CONFIGURACION')");
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

        builder.HasOne(x => x.Camara).WithMany().HasForeignKey(x => x.CamaraId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IdentificadorExterno).IsUnique().HasDatabaseName("UX_AlertasCamara_IdentificadorExterno");
        builder.HasIndex(x => new { x.CamaraId, x.FechaHoraInicio }).HasDatabaseName("IX_CAM_Alertas_Camara_Inicio");
    }
}
