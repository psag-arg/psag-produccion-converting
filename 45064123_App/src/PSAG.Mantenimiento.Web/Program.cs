using System.Globalization;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PSAG.Mantenimiento.Web;
using PSAG.Mantenimiento.Web.Services;

// Números, fechas y días en castellano argentino para toda la app
var castellano = new CultureInfo("es-AR");
CultureInfo.DefaultThreadCurrentCulture = castellano;
CultureInfo.DefaultThreadCurrentUICulture = castellano;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configurar HttpClient con la URL de la API
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5000/";
// Un 401 de la API cierra la sesión y lleva al login avisando que venció.
builder.Services.AddScoped(sp => new HttpClient(new SesionVencidaHandler(sp) { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = new Uri(apiBaseUrl)
});

// Servicios de almacenamiento local
builder.Services.AddBlazoredLocalStorage();

// Servicios de autenticación
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddScoped<IAuthService, AuthService>();

// Servicios de negocio
builder.Services.AddScoped<IDetencionesService, DetencionesService>();
builder.Services.AddScoped<IAnalisisService, AnalisisService>();
builder.Services.AddScoped<ITareasMantenimientoService, TareasMantenimientoService>();
builder.Services.AddScoped<ICatalogosService, CatalogosService>();
builder.Services.AddScoped<ICalendarioLineaService, CalendarioLineaService>();
builder.Services.AddScoped<IUsuariosService, UsuariosService>();
builder.Services.AddScoped<IRegistroProcesoService, RegistroProcesoService>();
builder.Services.AddScoped<IControlProcesoPlantillaService, ControlProcesoPlantillaService>();
builder.Services.AddScoped<IInspeccionService, InspeccionService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Servicio de temas
builder.Services.AddScoped<IThemeService, ThemeService>();

// Configurar autorización
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
