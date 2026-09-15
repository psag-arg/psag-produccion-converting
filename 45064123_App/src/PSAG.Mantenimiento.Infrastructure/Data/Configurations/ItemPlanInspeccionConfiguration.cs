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
    }
}
