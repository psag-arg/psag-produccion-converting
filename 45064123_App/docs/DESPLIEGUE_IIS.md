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

### Fotos del análisis de falla (T-544)

Las fotos se guardan en disco, en la carpeta de `Adjuntos:Carpeta` (appsettings de la API),
con nombre generado por la aplicación. No se sirven como archivos estáticos: solo salen por
`GET api/analisis/{id}/adjuntos/{adjuntoId}`, con sesión iniciada.

```json
"Adjuntos": {
  "Carpeta": "C:\\PSAG\\Mantenimiento\\Adjuntos"
}
```

- Usar una ruta **absoluta y fuera de la carpeta `Api`**: `02-Publicar-Aplicacion.ps1` borra
  `Api\*` en cada publicación (el script ya genera este valor en `appsettings.Production.json`).
- Dar permiso de **modificar** sobre esa carpeta al usuario del app pool
  (`IIS AppPool\<nombre del pool>`). La carpeta se crea sola con la primera foto si el permiso alcanza.
- Incluirla en el backup del servidor junto con la base: la tabla `mnt.AnalisisAdjuntos` solo guarda el nombre.
- Sin configurar, el default es `App_Data/AdjuntosAnalisis` dentro de la carpeta de la API (sirve para desarrollo).
- Límites: JPG o PNG reales (se valida el contenido), hasta 10 MB por foto y 5 por análisis.
  Cada foto se sube en un pedido aparte, así que alcanza el límite por defecto de IIS (30 MB).

### Imágenes y clave del detector de calidad (T-611)

Las imágenes de los registros de calidad usan el mismo mecanismo que las fotos del análisis, en su
propia carpeta `Calidad:CarpetaImagenes` (el publicador genera `C:\PSAG\Mantenimiento\ImagenesCalidad`).
Mismos cuidados: fuera de `Api`, permiso de modificar para el usuario del servicio y en el backup.
Una cámara puede generar muchas imágenes: vigilar el espacio en disco.

La ingesta del detector (`POST api/calidad/ingesta`) queda **deshabilitada (503)** hasta configurar
`Calidad:ClaveIngesta`. No ponerla en `appsettings.Production.json` (se regenera en cada publicación):
cargarla como variable de entorno del servicio NSSM. Pasos en `docs/CALIDAD.md` → Configuración.

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
