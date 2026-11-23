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
        services.AddScoped<IInspeccionPlantillaService, InspeccionPlantillaService>();

        // Control de calidad por cámaras (T-611)
        services.AddScoped<ICalidadMaestrosService, CalidadMaestrosService>();
        services.AddScoped<IRegistroCalidadService, RegistroCalidadService>();

        // Conteo de producción y paradas por cámara (T-610)
        services.AddScoped<IConteoCamaraService, ConteoCamaraService>();
        services.AddScoped<ICamaraService, CamaraService>();   // maestro de cámaras (T-798 etapa 4)
        services.AddScoped<IDeclaracionDetencionService, DeclaracionDetencionService>();

        // Turno de producción (T-798 etapa 2). TurnosProduccionOpciones la registra la API desde appsettings.
        services.AddScoped<ITurnoProduccionService, TurnoProduccionService>();

        // Indicadores: presupuesto vs. real desde el turno y el parámetro (T-798 etapa 5)
        services.AddScoped<IIndicadoresService, IndicadoresService>();

        // Tablero del operador (T-770): todo el turno en proceso en una llamada
        services.AddScoped<ITableroService, TableroService>();

        // Maestros de producción: productos + SKU y parametrización línea + producto (T-798 etapa 6)
        services.AddScoped<IMaestrosProduccionService, MaestrosProduccionService>();

        // Dashboard consolidado
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }
}
