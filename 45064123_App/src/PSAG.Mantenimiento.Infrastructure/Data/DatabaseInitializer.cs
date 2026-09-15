using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PSAG.Mantenimiento.Domain.Entities;
using PSAG.Mantenimiento.Infrastructure.Identity;

namespace PSAG.Mantenimiento.Infrastructure.Data;

/// <summary>
/// Inicializador de base de datos.
/// Crea el esquema y siembra datos iniciales para SQLite o SQL Server.
/// </summary>
public static class DatabaseInitializer
{
    private const string SystemUserId = "SYSTEM-SEED";

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<MantenimientoDbContext>>();

        try
        {
            var context = services.GetRequiredService<MantenimientoDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            // Detectar si es SQLite
            var connectionString = context.Database.GetConnectionString() ?? "";
            var isSqlite = connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase);

            if (isSqlite)
            {
                logger.LogInformation("Inicializando base de datos SQLite...");

                // Crear directorio si no existe
                var dbPath = connectionString.Replace("Data Source=", "", StringComparison.OrdinalIgnoreCase);
                var directory = Path.GetDirectoryName(dbPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                    logger.LogInformation("Directorio creado: {Directory}", directory);
                }

                // Crear esquema de base de datos
                await context.Database.EnsureCreatedAsync();
                logger.LogInformation("Esquema de base de datos creado/verificado.");
            }
            else
            {
                // SQL Server - solo verificar que puede conectar
                var canConnect = await context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    logger.LogWarning("No se puede conectar a la base de datos SQL Server.");
                    return;
                }
            }

            // Sembrar datos
            await SeedRolesAsync(roleManager, logger);
            await SeedAdminUserAsync(userManager, roleManager, logger);
            await SeedCatalogsAsync(context, logger);

            logger.LogInformation("Inicializacion de base de datos completada.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error durante la inicializacion de la base de datos.");
            throw;
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        var roles = Roles.TodosLosRoles;

        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (result.Succeeded)
                {
                    logger.LogInformation("Rol {Role} creado.", roleName);
                }
                else
                {
                    logger.LogWarning("Error creando rol {Role}: {Errors}", roleName,
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }

    private static async Task SeedAdminUserAsync(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger logger)
    {
        const string adminEmail = "admin@psag.local";
        const string adminPassword = "Admin123!";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new ApplicationUser
            {
                UserName = "admin",
                Email = adminEmail,
                NombreCompleto = "Administrador del Sistema",
                EmailConfirmed = true,
                Activo = true,
                RequiereCambioPassword = true,
                FechaCreacion = DateTimeOffset.UtcNow
            };

            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, Roles.Administrador);
                logger.LogInformation("Usuario administrador creado: {Email}", adminEmail);
            }
            else
            {
                logger.LogWarning("Error creando usuario admin: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private static async Task SeedCatalogsAsync(MantenimientoDbContext context, ILogger logger)
    {
        var now = DateTimeOffset.UtcNow;

        // Lineas
        if (!await context.Lineas.AnyAsync())
        {
            var lineas = new[]
            {
                new Linea { Codigo = "L01", Nombre = "Linea 1", Descripcion = "Linea de produccion Converting 1", Orden = 1, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new Linea { Codigo = "L02", Nombre = "Linea 2", Descripcion = "Linea de produccion Converting 2", Orden = 2, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new Linea { Codigo = "L03", Nombre = "Linea 3", Descripcion = "Linea de produccion Converting 3", Orden = 3, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new Linea { Codigo = "L04", Nombre = "Linea 4", Descripcion = "Linea de produccion Converting 4", Orden = 4, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new Linea { Codigo = "L05", Nombre = "Linea 5", Descripcion = "Linea de produccion Converting 5", Orden = 5, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new Linea { Codigo = "L06", Nombre = "Linea 6", Descripcion = "Linea de produccion Converting 6", Orden = 6, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
            };
            await context.Lineas.AddRangeAsync(lineas);
            await context.SaveChangesAsync();
            logger.LogInformation("Lineas sembradas: {Count}", lineas.Length);
        }

        // Turnos
        if (!await context.Turnos.AnyAsync())
        {
            var turnos = new[]
            {
                new Turno { Codigo = "T1", Nombre = "Turno 1 (Manana)", HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0), Orden = 1, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new Turno { Codigo = "T2", Nombre = "Turno 2 (Tarde)", HoraInicio = new TimeOnly(14, 0), HoraFin = new TimeOnly(22, 0), Orden = 2, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new Turno { Codigo = "T3", Nombre = "Turno 3 (Noche)", HoraInicio = new TimeOnly(22, 0), HoraFin = new TimeOnly(6, 0), Orden = 3, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
            };
            await context.Turnos.AddRangeAsync(turnos);
            await context.SaveChangesAsync();
            logger.LogInformation("Turnos sembrados: {Count}", turnos.Length);
        }

        // Tipos de Falla
        if (!await context.TiposFalla.AnyAsync())
        {
            var tiposFalla = new[]
            {
                new TipoFalla { Codigo = "TF-MEC", Nombre = "Falla Mecanica", Descripcion = "Fallas relacionadas con componentes mecanicos", Orden = 1, RequiereDescripcion = false, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new TipoFalla { Codigo = "TF-ELE", Nombre = "Falla Electrica", Descripcion = "Fallas relacionadas con el sistema electrico", Orden = 2, RequiereDescripcion = false, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new TipoFalla { Codigo = "TF-CTL", Nombre = "Falla de Control", Descripcion = "Fallas en sistemas de control y automatizacion", Orden = 3, RequiereDescripcion = false, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new TipoFalla { Codigo = "TF-CPR", Nombre = "Cambio de Producto", Descripcion = "Parada programada por cambio de producto", Orden = 4, RequiereDescripcion = false, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new TipoFalla { Codigo = "TF-MPR", Nombre = "Mantenimiento Preventivo", Descripcion = "Parada programada por mantenimiento preventivo", Orden = 5, RequiereDescripcion = false, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new TipoFalla { Codigo = "TF-MAT", Nombre = "Falta de Material", Descripcion = "Parada por falta de materia prima o insumos", Orden = 6, RequiereDescripcion = false, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new TipoFalla { Codigo = "TF-CAL", Nombre = "Problema de Calidad", Descripcion = "Parada por problemas de calidad del producto", Orden = 7, RequiereDescripcion = false, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new TipoFalla { Codigo = "TF-AJU", Nombre = "Ajuste de Maquina", Descripcion = "Parada por ajustes menores de la maquina", Orden = 8, RequiereDescripcion = false, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new TipoFalla { Codigo = "TF-OTR", Nombre = "Otro", Descripcion = "Otros tipos de parada no clasificados", Orden = 99, RequiereDescripcion = true, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
            };
            await context.TiposFalla.AddRangeAsync(tiposFalla);
            await context.SaveChangesAsync();
            logger.LogInformation("Tipos de Falla sembrados: {Count}", tiposFalla.Length);
        }

        // Detalles de Falla (genericos)
        if (!await context.DetallesFalla.AnyAsync())
        {
            var detallesFalla = new[]
            {
                new DetalleFalla { EquipoId = null, TipoFallaId = null, Codigo = "DF-DES", Nombre = "Desgaste de componente", Descripcion = "Desgaste natural de piezas o componentes", Orden = 1, RequiereDescripcion = true, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new DetalleFalla { EquipoId = null, TipoFallaId = null, Codigo = "DF-ROT", Nombre = "Rotura de pieza", Descripcion = "Rotura o fractura de componentes", Orden = 2, RequiereDescripcion = true, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new DetalleFalla { EquipoId = null, TipoFallaId = null, Codigo = "DF-LUB", Nombre = "Falta de lubricacion", Descripcion = "Deficiencia en la lubricacion de componentes", Orden = 3, RequiereDescripcion = false, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new DetalleFalla { EquipoId = null, TipoFallaId = null, Codigo = "DF-COR", Nombre = "Cortocircuito", Descripcion = "Cortocircuito en el sistema electrico", Orden = 4, RequiereDescripcion = true, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new DetalleFalla { EquipoId = null, TipoFallaId = null, Codigo = "DF-SOB", Nombre = "Sobrecalentamiento", Descripcion = "Temperatura excesiva en componentes", Orden = 5, RequiereDescripcion = true, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new DetalleFalla { EquipoId = null, TipoFallaId = null, Codigo = "DF-SEN", Nombre = "Error de sensor", Descripcion = "Falla o desconfiguracion de sensores", Orden = 6, RequiereDescripcion = true, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new DetalleFalla { EquipoId = null, TipoFallaId = null, Codigo = "DF-OPE", Nombre = "Error de operacion", Descripcion = "Error humano en la operacion del equipo", Orden = 7, RequiereDescripcion = true, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new DetalleFalla { EquipoId = null, TipoFallaId = null, Codigo = "DF-MPD", Nombre = "Materia prima defectuosa", Descripcion = "Problemas con la calidad de la materia prima", Orden = 8, RequiereDescripcion = true, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new DetalleFalla { EquipoId = null, TipoFallaId = null, Codigo = "DF-REP", Nombre = "Falta de repuestos", Descripcion = "No se dispone de repuestos necesarios", Orden = 9, RequiereDescripcion = true, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new DetalleFalla { EquipoId = null, TipoFallaId = null, Codigo = "DF-DET", Nombre = "Causa por determinar", Descripcion = "La causa raiz aun no ha sido identificada", Orden = 98, RequiereDescripcion = true, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
                new DetalleFalla { EquipoId = null, TipoFallaId = null, Codigo = "DF-OTR", Nombre = "Otro", Descripcion = "Otros detalles no clasificados", Orden = 99, RequiereDescripcion = true, Activo = true, CreadoPorUsuarioId = SystemUserId, ModificadoPorUsuarioId = SystemUserId, FechaHoraCreacion = now, FechaHoraModificacion = now },
            };
            await context.DetallesFalla.AddRangeAsync(detallesFalla);
            await context.SaveChangesAsync();
            logger.LogInformation("Detalles de Falla sembrados: {Count}", detallesFalla.Length);
        }

        // Equipos por linea
        if (!await context.Equipos.AnyAsync())
        {
            var lineas = await context.Lineas.ToListAsync();
            var equipos = new List<Equipo>();

            var tiposEquipo = new[]
            {
                ("DES", "Desenrollador", "Equipo desenrollador de bobina", 1),
                ("IMP", "Impresora", "Equipo de impresion", 2),
                ("COR", "Cortadora", "Equipo de corte", 3),
                ("PLE", "Plegadora", "Equipo de plegado", 4),
                ("EMP", "Empaquetadora", "Equipo de empaquetado", 5)
            };

            foreach (var linea in lineas)
            {
                foreach (var (codigo, nombre, descripcion, orden) in tiposEquipo)
                {
                    equipos.Add(new Equipo
                    {
                        LineaId = linea.LineaId,
                        Codigo = $"{codigo}-{linea.LineaId}",
                        Nombre = nombre,
                        Descripcion = descripcion,
                        Orden = orden,
                        Activo = true,
                        CreadoPorUsuarioId = SystemUserId,
                        ModificadoPorUsuarioId = SystemUserId,
                        FechaHoraCreacion = now,
                        FechaHoraModificacion = now
                    });
                }
            }

            await context.Equipos.AddRangeAsync(equipos);
            await context.SaveChangesAsync();
            logger.LogInformation("Equipos sembrados: {Count}", equipos.Count);
        }
    }
}
