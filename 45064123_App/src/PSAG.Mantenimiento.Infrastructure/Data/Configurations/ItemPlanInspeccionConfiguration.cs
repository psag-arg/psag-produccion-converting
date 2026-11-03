using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Entities;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

public class ItemPlanInspeccionConfiguration : IEntityTypeConfiguration<ItemPlanInspeccion>
{
    public void Configure(EntityTypeBuilder<ItemPlanInspeccion> builder)
    {
        builder.ToTable("ItemsPlanInspeccion", "mnt");

        builder.HasKey(i => i.ItemPlanInspeccionId);

        builder.Property(i => i.Descripcion)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(i => i.CondicionEstandar)
            .IsRequired()
            .HasMaxLength(300);

        builder.Property(i => i.Metodo)
            .HasConversion<byte>();

        // Script 27: columnas de la planilla "Estándares de inspección".
        builder.Property(i => i.Codigo)
            .HasMaxLength(50);

        builder.Property(i => i.ParteEquipo)
            .HasMaxLength(150);

        builder.Property(i => i.MetodoDetalle)
            .HasMaxLength(200);

        builder.Property(i => i.QuienRealiza)
            .HasConversion<byte>();
        // FrecuenciaDias: INT NOT NULL DEFAULT 1 en el script; EF siempre manda el valor.
    }
}
