using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PSAG.Mantenimiento.Application.Services.Implementations;
using PSAG.Mantenimiento.Application.Services.Interfaces;

namespace PSAG.Mantenimiento.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Registrar todos los validadores del ensamblado
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        // Registrar servicios de aplicación - Eje 1: Detenciones
        services.AddScoped<IDetencionService, DetencionService>();
        services.AddScoped<IExportacionService, ExportacionService>();
        services.AddScoped<IAnalisisService, AnalisisService>();
        services.AddScoped<IAnalisisEdicionService, AnalisisEdicionService>();
        services.AddScoped<ITareaMantenimientoService, TareaMantenimientoService>();

        // Registrar servicios de aplicación - Eje 2: Variables de proceso
        services.AddScoped<IRegistroProcesoService, RegistroProcesoService>();
        services.AddScoped<IControlProcesoPlantillaService, ControlProcesoPlantillaService>();

        // Registrar servicios de aplicación - Eje 3: Inspecciones de máquina
        services.AddScoped<IInspeccionService, InspeccionService>();

        // Dashboard consolidado
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
