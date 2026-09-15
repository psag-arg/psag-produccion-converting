# Despliegue en IIS - PSAG.Mantenimiento

## Requisitos del Servidor

- Windows Server 2019+
- IIS 10+
- .NET 9 Hosting Bundle
- SQL Server 2019+ (puede ser otro servidor)

## Instalación del Hosting Bundle

```powershell
# Descargar desde https://dotnet.microsoft.com/download/dotnet/9.0
# Ejecutar dotnet-hosting-9.0.x-win.exe
# Reiniciar IIS
iisreset
```

## Estructura de Carpetas

```
C:\inetpub\wwwroot\PSAGMantenimiento\
├── api\                 # ASP.NET Core API
│   ├── web.config
│   ├── appsettings.json
│   ├── appsettings.Production.json
│   └── ...
└── web\                 # Blazor WebAssembly (archivos estáticos)
    ├── index.html
    ├── _framework\
    └── ...
```

## Configuración de Sitios en IIS

### Sitio API (puerto 5001)
1. Crear Application Pool:
   - Nombre: `PSAGMantenimientoApi`
   - .NET CLR Version: `No Managed Code`
   - Pipeline: `Integrated`

2. Crear sitio:
   - Physical Path: `C:\inetpub\wwwroot\PSAGMantenimiento\api`
   - Binding: `https`, puerto `5001`
   - SSL Certificate: según política de PSAG

### Sitio Web (puerto 5000)
1. Crear Application Pool:
   - Nombre: `PSAGMantenimientoWeb`
   - .NET CLR Version: `No Managed Code`
   - Pipeline: `Integrated`

2. Crear sitio:
   - Physical Path: `C:\inetpub\wwwroot\PSAGMantenimiento\web`
   - Binding: `https`, puerto `5000`

## Publicación

### Desde línea de comandos
```powershell
# API
dotnet publish src/PSAG.Mantenimiento.Api -c Release -o C:\publish\api

# Web
dotnet publish src/PSAG.Mantenimiento.Web -c Release -o C:\publish\web
```

### Copiar a servidor
```powershell
# Detener sitios
Stop-WebSite -Name "PSAGMantenimientoApi"
Stop-WebSite -Name "PSAGMantenimientoWeb"

# Copiar archivos
robocopy C:\publish\api C:\inetpub\wwwroot\PSAGMantenimiento\api /MIR
robocopy C:\publish\web C:\inetpub\wwwroot\PSAGMantenimiento\web /MIR

# Iniciar sitios
Start-WebSite -Name "PSAGMantenimientoApi"
Start-WebSite -Name "PSAGMantenimientoWeb"
```

## Configuración de Producción

### appsettings.Production.json (NO versionar)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=SQLSERVER;Database=PSAGMantenimiento;User Id=app_user;Password=***;TrustServerCertificate=True"
  },
  "JwtSettings": {
    "SecretKey": "clave-secreta-de-produccion-min-32-chars",
    "Issuer": "PSAGMantenimiento",
    "Audience": "PSAGMantenimientoClients"
  },
  "Cors": {
    "AllowedOrigins": ["https://psag-mantenimiento.local:5000"]
  }
}
```

### web.config (API)
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\PSAG.Mantenimiento.Api.dll"
                  stdoutLogEnabled="true"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="inprocess">
        <environmentVariables>
          <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
        </environmentVariables>
      </aspNetCore>
    </system.webServer>
  </location>
</configuration>
```

## Logs

- Ubicación: `C:\inetpub\wwwroot\PSAGMantenimiento\api\logs\`
- Rotación: diaria
- Retención: configurar según política

## Verificación

1. Abrir `https://servidor:5001/health` → debe responder OK
2. Abrir `https://servidor:5001/swagger` → documentación API
3. Abrir `https://servidor:5000` → aplicación web

## Troubleshooting

### Error 500.19
- Verificar permisos de la carpeta
- Verificar Hosting Bundle instalado

### Error 502.5
- Revisar `stdout` logs
- Verificar .NET runtime correcto

### Blazor no carga
- Verificar que `_framework` se copió correctamente
- Verificar MIME types en IIS para `.wasm` y `.dll`
