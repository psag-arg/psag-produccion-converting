using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Domain.Enums;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class TareaMantenimientoConfiguration : IEntityTypeConfiguration<TareaMantenimiento>
{
    public void Configure(EntityTypeBuilder<TareaMantenimiento> builder)
    {
        builder.ToTable("TareasMantenimiento", "mnt", t =>
        {
            t.HasCheckConstraint("CK_TareasMantenimiento_TipoTarea",
                "[TipoTarea] IN ('MECANICA', 'ELECTRICA', 'ELECTRONICA')");
            t.HasCheckConstraint("CK_TareasMantenimiento_Criticidad",
                "[Criticidad] IN ('ALTA', 'MEDIA', 'BAJA')");
            t.HasCheckConstraint("CK_TareasMantenimiento_Estado",
                "[Estado] IN ('PENDIENTE', 'PROGRAMADA', 'CERRADA')");
            t.HasCheckConstraint("CK_TareasMantenimiento_FechaCierre",
                "[FechaHoraCierre] IS NULL OR [FechaHoraCierre] >= [FechaHoraCreacion]");
        });

        builder.HasKey(x => x.TareaMantenimientoId);

        builder.Property(x => x.ParteEquipo)
            .HasMaxLength(200);

        builder.Property(x => x.Detalle)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.TipoTarea)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<TipoTareaMantenimiento>(v))
            .HasMaxLength(20);

        builder.Property(x => x.Criticidad)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<CriticidadTarea>(v))
            .HasMaxLength(10);

        builder.Property(x => x.Estado)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<EstadoTareaMantenimiento>(v))
            .HasMaxLength(20);

        builder.Property(x => x.CreadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ModificadoPorUsuarioId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.ProgramadaPorUsuarioId)
            .HasMaxLength(450);

        builder.Property(x => x.CerradaPorUsuarioId)
            .HasMaxLength(450);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Relaciones (sin colección del otro lado para no tocar Linea ni Equipo)
        builder.HasOne(x => x.Linea)
            .WithMany()
            .HasForeignKey(x => x.LineaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Equipo)
            .WithMany()
            .HasForeignKey(x => x.EquipoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índices
        builder.HasIndex(x => x.EquipoId);
        builder.HasIndex(x => new { x.Estado, x.LineaId });
        builder.HasIndex(x => x.FechaHoraCierre);
    }
}
