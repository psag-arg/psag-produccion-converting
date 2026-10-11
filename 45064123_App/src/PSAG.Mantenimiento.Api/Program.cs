using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PSAG.Mantenimiento.Api.Configuration;
using PSAG.Mantenimiento.Api.Middleware;
using PSAG.Mantenimiento.Application;
using PSAG.Mantenimiento.Infrastructure;
using PSAG.Mantenimiento.Infrastructure.Data;
using PSAG.Mantenimiento.Infrastructure.Identity;
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
builder.Services.AddSingleton(ZonaHorariaPlanta.Resolver(configuration));

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

// Configurar Authorization con políticas
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("RequireAdministrador", policy => policy.RequireRole("ADMINISTRADOR"))
    .AddPolicy("RequireGerencia", policy => policy.RequireRole("ADMINISTRADOR", "GERENCIA"))
    .AddPolicy("RequireMantenimiento", policy => policy.RequireRole("ADMINISTRADOR", "MANTENIMIENTO"))
    .AddPolicy("RequireSupervisor", policy => policy.RequireRole("ADMINISTRADOR", "MANTENIMIENTO", "SUPERVISOR"))
    .AddPolicy("RequireOperario", policy => policy.RequireRole("ADMINISTRADOR", "MANTENIMIENTO", "SUPERVISOR", "OPERARIO"))
    // Políticas para Detenciones (nuevo nombre)
    .AddPolicy("CanCreateDetenciones", policy => policy.RequireRole("ADMINISTRADOR", "MANTENIMIENTO", "SUPERVISOR", "OPERARIO"))
    .AddPolicy("CanEditDetenciones", policy => policy.RequireRole("ADMINISTRADOR", "MANTENIMIENTO"))
    .AddPolicy("CanCloseDetenciones", policy => policy.RequireRole("ADMINISTRADOR", "MANTENIMIENTO"))
    // Políticas legacy (compatibilidad)
    .AddPolicy("CanCreateEventos", policy => policy.RequireRole("ADMINISTRADOR", "MANTENIMIENTO", "SUPERVISOR", "OPERARIO"))
    .AddPolicy("CanEditEventos", policy => policy.RequireRole("ADMINISTRADOR", "MANTENIMIENTO"))
    .AddPolicy("CanCloseEventos", policy => policy.RequireRole("ADMINISTRADOR", "MANTENIMIENTO"))
    .AddPolicy("CanManageCatalogs", policy => policy.RequireRole("ADMINISTRADOR"))
    .AddPolicy("CanManageUsers", policy => policy.RequireRole("ADMINISTRADOR"))
    .AddPolicy("CanExport", policy => policy.RequireRole("ADMINISTRADOR", "GERENCIA", "MANTENIMIENTO"));

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

app.UseHttpsRedirection();

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
