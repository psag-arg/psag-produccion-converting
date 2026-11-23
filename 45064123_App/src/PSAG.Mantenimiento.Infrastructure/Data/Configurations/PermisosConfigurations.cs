using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PSAG.Mantenimiento.Domain.Permisos;

namespace PSAG.Mantenimiento.Infrastructure.Data.Configurations;

/// <summary>T-802: catálogo de opciones (script 39). Las tablas de Identity no se tocan.</summary>
public class OpcionSistemaConfiguration : IEntityTypeConfiguration<OpcionSistema>
{
    public void Configure(EntityTypeBuilder<OpcionSistema> builder)
    {
        builder.ToTable("CFG_Opciones", "mnt");
        builder.HasKey(x => x.OpcionId);
        builder.Property(x => x.Codigo).IsRequired().HasMaxLength(100);
        builder.HasIndex(x => x.Codigo).IsUnique().HasDatabaseName("UX_CFG_Opciones_Codigo");
        builder.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Ruta).HasMaxLength(200);
        builder.Property(x => x.AccionesAplicables).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Activo).HasDefaultValue(true);
        builder.HasOne(x => x.Padre).WithMany().HasForeignKey(x => x.PadreId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CFG_Opciones_Padre");
    }
}

public class PermisoRolConfiguration : IEntityTypeConfiguration<PermisoRol>
{
    public void Configure(EntityTypeBuilder<PermisoRol> builder)
    {
        builder.ToTable("CFG_PermisosRol", "mnt");
        builder.HasKey(x => x.PermisoRolId);
        builder.Property(x => x.RoleId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ModificadoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.HasIndex(x => new { x.RoleId, x.OpcionId }).IsUnique().HasDatabaseName("UX_CFG_PermisosRol_Rol_Opcion");
        builder.HasOne<IdentityRole>().WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_CFG_PermisosRol_Rol");
        builder.HasOne(x => x.Opcion).WithMany().HasForeignKey(x => x.OpcionId).OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CFG_PermisosRol_Opcion");
        builder.Ignore(x => x.Acciones);
    }
}

public class RolConfiguracionConfiguration : IEntityTypeConfiguration<RolConfiguracion>
{
    public void Configure(EntityTypeBuilder<RolConfiguracion> builder)
    {
        builder.ToTable("CFG_RolesConfiguracion", "mnt");
        builder.HasKey(x => x.RoleId);
        builder.Property(x => x.RoleId).HasMaxLength(450);
        builder.Property(x => x.Descripcion).HasMaxLength(300);
        builder.Property(x => x.ModificadoPorUsuarioId).IsRequired().HasMaxLength(450);
        builder.HasOne<IdentityRole>().WithOne().HasForeignKey<RolConfiguracion>(x => x.RoleId).OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_CFG_RolesConfiguracion_Rol");
    }
}
