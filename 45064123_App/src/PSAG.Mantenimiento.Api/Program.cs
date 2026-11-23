using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PSAG.Mantenimiento.Api.Configuration;
using PSAG.Mantenimiento.Api.Jobs;
using PSAG.Mantenimiento.Api.Middleware;
using PSAG.Mantenimiento.Application.DTOs.Produccion;
using PSAG.Mantenimiento.Application;
using PSAG.Mantenimiento.Infrastructure;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Identity;
using PSAG.Mantenimiento.Infrastructure.Services;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .CreateLogger();

builder.Host.UseSerilog();

// Configuración
var configuration = builder.Configuration;
var jwtSettings = configuration.GetSection("JwtSettings");

// Agregar servicios de capas
builder.Services.AddInfrastructure(configuration);
builder.Services.AddApplication();
builder.Services.AddAlmacenAdjuntos(configuration, builder.Environment.ContentRootPath);
builder.Services.AddAlmacenImagenesCalidad(configuration, builder.Environment.ContentRootPath);
var zonaPlanta = ZonaHorariaPlanta.Resolver(configuration);
builder.Services.AddSingleton(zonaPlanta);

// Turnos de producción (T-798 etapa 2): tolerancias y job de cierre automático / SIN_APERTURA.
builder.Services.AddSingleton(configuration.GetSection(TurnosProduccionOpciones.Seccion).Get<TurnosProduccionOpciones>() ?? new TurnosProduccionOpciones());
builder.Services.AddHostedService<TurnosProduccionJob>();

// Configurar Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.Password.RequiredUniqueChars = 4;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    // SignIn settings
    options.SignIn.RequireConfirmedEmail = false;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<MantenimientoDbContext>()
.AddDefaultTokenProviders()
.AddErrorDescriber<ErroresIdentityEnCastellano>();

// Configurar JWT Authentication
var jwtKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey no configurada");
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.RequireHttpsMetadata = false; // En producción cambiar a true
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            if (context.Exception.GetType() == typeof(SecurityTokenExpiredException))
            {
                context.Response.Headers.Append("Token-Expired", "true");
            }
            return Task.CompletedTask;
        }
    };
});

// Autorización (T-802): roles y permisos configurables. Cada acción de los controllers lleva [Permiso("OPCION", Accion)]
// (ver Autorizacion/ y docs/PERMISOS.md); las políticas fijas por rol de antes se reemplazaron por la matriz CFG_PermisosRol.
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, PSAG.Mantenimiento.Api.Autorizacion.PermisoHandler>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, PSAG.Mantenimiento.Api.Autorizacion.PermisoResultHandler>();
builder.Services.AddScoped<PSAG.Mantenimiento.Application.Services.Interfaces.IPermisosUsuarioActual, PSAG.Mantenimiento.Api.Autorizacion.PermisosUsuarioActualHttp>();

// Configurar CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:5000"];
        policy.WithOrigins(origins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Configurar Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.Converters.Add(new FechaHoraPlantaJsonConverter(zonaPlanta));
    });

// Configurar Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "PSAG Mantenimiento API",
        Version = "v1",
        Description = "API para el sistema de gestión de paradas de máquina - Papelera San Andrés de Giles",
        Contact = new OpenApiContact
        {
            Name = "PSAG IT",
            Email = "it@psag.local"
        }
    });

    // Configurar autenticación JWT en Swagger
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Ejemplo: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<MantenimientoDbContext>("database");

var app = builder.Build();

// Pipeline de middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PSAG Mantenimiento API v1");
        c.RoutePrefix = "swagger";
    });
}

// Middleware de manejo de excepciones global
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Middleware de correlación ID
app.UseMiddleware<CorrelationIdMiddleware>();

// Con HTTPS opcional (puerto aparte) el HTTP de siempre tiene que seguir andando: solo se redirige si se pide.
if (configuration.GetValue<bool>("Https:RedirigirHttp"))
{
    app.UseHttpsRedirection();
}

app.UseCors("AllowBlazorClient");

app.UseAuthentication();
app.UseAuthorization();

// Middleware de logging de requests
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId", httpContext.User?.FindFirst("sub")?.Value ?? "anonymous");
        diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]?.ToString());
    };
});

app.MapControllers();

app.MapHealthChecks("/health");

// Endpoint de información
app.MapGet("/", () => Results.Ok(new
{
    Application = "PSAG Mantenimiento API",
    Version = "1.0.0",
    Environment = app.Environment.EnvironmentName,
    Timestamp = DateTimeOffset.Now
}));

// Logging de inicio
Log.Information("PSAG Mantenimiento API iniciando en {Environment}", app.Environment.EnvironmentName);

// Inicializar base de datos (para SQLite o primera ejecucion)
try
{
    await PSAG.Mantenimiento.Infrastructure.Data.DatabaseInitializer.InitializeAsync(app.Services);
}
catch (Exception ex)
{
    Log.Warning(ex, "Error durante la inicializacion de la base de datos. La aplicacion continuara.");
}

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "La aplicación falló al iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Visible para los tests de integración (WebApplicationFactory).</summary>
public partial class Program;
