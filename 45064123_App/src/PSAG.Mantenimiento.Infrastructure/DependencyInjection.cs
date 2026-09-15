using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PSAG.Mantenimiento.Application.Services.Interfaces;
using PSAG.Mantenimiento.Domain.Interfaces;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Repositories;
using PSAG.Mantenimiento.Infrastructure.Services;

namespace PSAG.Mantenimiento.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
        => AddInfrastructureServices(services, configuration);

    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";

        // Detectar si es SQLite o SQL Server basado en el connection string
        var useSqlite = connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
                        && connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase);

        // Configurar DbContext
        services.AddDbContext<MantenimientoDbContext>(options =>
        {
            if (useSqlite)
            {
                options.UseSqlite(connectionString);
            }
            else
            {
                options.UseSqlServer(
                    connectionString,
                    b => b.MigrationsAssembly(typeof(MantenimientoDbContext).Assembly.FullName));
            }
        });

        // Registrar repositorios
        services.AddScoped<IDetencionRepository, DetencionRepository>();
        services.AddScoped<ILineaRepository, LineaRepository>();
        services.AddScoped<IEquipoRepository, EquipoRepository>();
        services.AddScoped<ITipoFallaRepository, TipoFallaRepository>();
        services.AddScoped<IDetalleFallaRepository, DetalleFallaRepository>();
        services.AddScoped<ITurnoRepository, TurnoRepository>();
        services.AddScoped<IAuditoriaRepository, AuditoriaRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Registrar servicios
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditoriaService, AuditoriaService>();

        return services;
    }
}
