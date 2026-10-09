using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class DetencionConfiguration : IEntityTypeConfiguration<Detencion>
{
    public void Configure(EntityTypeBuilder<Detencion> builder)
    {
        builder.ToTable("Detenciones", "mnt", t =>
        {
            t.HasCheckConstraint("CK_Detenciones_FechaCierre",
                "[FechaHoraCierre] IS NULL OR [FechaHoraCierre] >= [FechaHoraCreacion]");
            t.HasCheckConstraint("CK_Detenciones_MinutosEfectivos",
                "[MinutosEfectivos] >= 0");
            t.HasCheckConstraint("CK_Detenciones_OrigenTiempo",
                "[OrigenTiempo] IN ('DECLARADO', 'CALCULADO', 'PLC')");
        });

        builder.HasKey(x => x.DetencionId);

        builder.Property(x => x.Codigo)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.Descripcion)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.EstadoActual)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<EstadoDetencion>(v))
            .HasMaxLength(30);

        builder.Property(x => x.OrigenTiempo)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<OrigenTiempo>(v))
            .HasMaxLength(20);

        builder.Property(x => x.AccionesRealizadas)
            .HasMaxLength(4000);

        builder.Property(x => x.ResolucionDescripcion)
            .HasMaxLength(4000);

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.CerradoPorUsuarioId)
            .HasMaxLength(450);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Eliminación lógica: las eliminadas quedan fuera de todas las consultas.
        // Sólo se ven con IgnoreQueryFilters (administración).
        builder.HasQueryFilter(d => !d.Eliminada);

        builder.Property(x => x.Eliminada)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.EliminadaPorUsuarioId)
            .HasMaxLength(450);

        builder.Property(x => x.MotivoEliminacion)
            .HasMaxLength(500);

        // Análisis de falla
        builder.Property(x => x.RequiereAnalisis)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.ResponsableRol)
            .HasMaxLength(50);

        builder.Property(x => x.ResponsableUsuarioId)
            .HasMaxLength(450);

        builder.Property(x => x.CausaFalla)
            .HasMaxLength(2000);

        builder.Property(x => x.PlanAccion)
            .HasMaxLength(2000);

        builder.Property(x => x.AnalisisPorUsuarioId)
            .HasMaxLength(450);

        // Relaciones
        builder.HasOne(x => x.Linea)
            .WithMany(x => x.Detenciones)
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipo)
            .WithMany(x => x.Detenciones)
            .HasForeignKey(x => x.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TipoFalla)
            .WithMany(x => x.Detenciones)
            .HasForeignKey(x => x.TipoFallaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DetalleFalla)
            .WithMany(x => x.Detenciones)
            .HasForeignKey(x => x.DetalleFallaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Turno)
            .WithMany(x => x.Detenciones)
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.UsuarioLinea)
            .WithMany()
            .HasForeignKey(x => x.UsuarioLineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ReglaAnalisis)
            .WithMany()
            .HasForeignKey(x => x.ReglaAnalisisId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índices
        builder.HasIndex(x => x.Codigo).IsUnique();
        builder.HasIndex(x => x.EstadoActual);
        builder.HasIndex(x => x.Fecha);
        builder.HasIndex(x => x.LineaId);
        builder.HasIndex(x => x.EquipoId);
        builder.HasIndex(x => x.TipoFallaId);
        builder.HasIndex(x => x.TurnoId);
        builder.HasIndex(x => x.UsuarioLineaId);
        builder.HasIndex(x => x.ReglaAnalisisId);
        builder.HasIndex(x => new { x.LineaId, x.EstadoActual });
        builder.HasIndex(x => new { x.Fecha, x.EstadoActual });
        builder.HasIndex(x => new { x.Fecha, x.LineaId, x.TurnoId });
    }
}
