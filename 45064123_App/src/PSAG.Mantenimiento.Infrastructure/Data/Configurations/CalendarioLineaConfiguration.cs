using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class CalendarioLineaConfiguration : IEntityTypeConfiguration<CalendarioLinea>
{
    public void Configure(EntityTypeBuilder<CalendarioLinea> builder)
    {
        builder.ToTable("CalendariosLinea", "mnt");

        builder.HasKey(x => x.CalendarioLineaId);

        builder.Property(x => x.Activo)
            .IsRequired()
            .HasDefaultValue(true);

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

        builder.HasMany(x => x.Turnos)
            .WithOne(x => x.CalendarioLinea)
            .HasForeignKey(x => x.CalendarioLineaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Una versión activa por línea y fecha; las reemplazadas quedan inactivas como historial.
        builder.HasIndex(x => new { x.LineaId, x.VigenteDesde })
            .IsUnique()
            .HasFilter("[Activo] = 1")
            .HasDatabaseName("UX_CalendariosLinea_LineaId_VigenteDesde");
    }
}

public class CalendarioLineaTurnoConfiguration : IEntityTypeConfiguration<CalendarioLineaTurno>
{
    public void Configure(EntityTypeBuilder<CalendarioLineaTurno> builder)
    {
        builder.ToTable("CalendariosLineaTurnos", "mnt", t =>
            t.HasCheckConstraint("CK_CalendariosLineaTurnos_DiaSemana", "[DiaSemana] BETWEEN 0 AND 6"));

        builder.HasKey(x => x.CalendarioLineaTurnoId);

        // 0 = domingo ... 6 = sábado (System.DayOfWeek).
        builder.Property(x => x.DiaSemana)
            .HasConversion<int>();

        builder.HasOne(x => x.Turno)
            .WithMany()
            .HasForeignKey(x => x.TurnoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CalendarioLineaId, x.DiaSemana, x.TurnoId })
            .IsUnique()
            .HasDatabaseName("UX_CalendariosLineaTurnos_Calendario_Dia_Turno");
    }
}
