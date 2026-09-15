<#
.SYNOPSIS
    Publica PSAG.Mantenimiento API y Web para produccion
.DESCRIPTION
    - Compila en modo Release
    - Publica API como aplicacion self-contained
    - Publica Web (Blazor WASM) como static files
    - Crea estructura de carpetas en C:\PSAG\Mantenimiento
.NOTES
    Ejecutar desde la maquina de desarrollo
    Luego copiar la carpeta C:\PSAG al servidor
#>

param(
    [string]$OutputPath = "C:\PSAG\Mantenimiento",
    [string]$Environment = "Production"
)

$ErrorActionPreference = "Stop"

# Obtener ruta del proyecto
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PSAG.Mantenimiento - Publicacion" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Proyecto: $projectRoot" -ForegroundColor Gray
Write-Host "Destino:  $OutputPath" -ForegroundColor Gray
Write-Host ""

# ============================================
# 1. LIMPIAR Y CREAR ESTRUCTURA
# ============================================

Write-Host "[1/5] Preparando carpetas..." -ForegroundColor Yellow

$apiPath = Join-Path $OutputPath "Api"
$webPath = Join-Path $OutputPath "Web"
$logsPath = Join-Path $OutputPath "logs"
$backupPath = Join-Path $OutputPath "backups"

# Backup de version anterior si existe
if (Test-Path $apiPath) {
    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupDir = Join-Path $backupPath $timestamp

    Write-Host "  Creando backup de version anterior..." -ForegroundColor Gray
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null

    if (Test-Path $apiPath) {
        Copy-Item -Path $apiPath -Destination (Join-Path $backupDir "Api") -Recurse -Force
    }
    if (Test-Path $webPath) {
        Copy-Item -Path $webPath -Destination (Join-Path $backupDir "Web") -Recurse -Force
    }

    Write-Host "  Backup creado en: $backupDir" -ForegroundColor DarkGray
}

# Crear estructura
New-Item -ItemType Directory -Path $apiPath -Force | Out-Null
New-Item -ItemType Directory -Path $webPath -Force | Out-Null
New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
New-Item -ItemType Directory -Path $backupPath -Force | Out-Null

Write-Host "  Estructura creada" -ForegroundColor Green

# ============================================
# 2. RESTAURAR PAQUETES
# ============================================

Write-Host ""
Write-Host "[2/5] Restaurando paquetes NuGet..." -ForegroundColor Yellow

Push-Location $projectRoot
dotnet restore
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    throw "Error en dotnet restore"
}
Pop-Location

Write-Host "  Paquetes restaurados" -ForegroundColor Green

# ============================================
# 3. PUBLICAR API
# ============================================

Write-Host ""
Write-Host "[3/5] Publicando API..." -ForegroundColor Yellow

$apiProject = Join-Path $projectRoot "src\PSAG.Mantenimiento.Api\PSAG.Mantenimiento.Api.csproj"

# Limpiar carpeta destino
Remove-Item -Path "$apiPath\*" -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish $apiProject `
    --configuration Release `
    --output $apiPath `
    --runtime win-x64 `
    --self-contained false

if ($LASTEXITCODE -ne 0) {
    throw "Error publicando API"
}

Write-Host "  API publicada en: $apiPath" -ForegroundColor Green

# ============================================
# 4. PUBLICAR WEB (BLAZOR WASM)
# ============================================

Write-Host ""
Write-Host "[4/5] Publicando Web (Blazor WASM)..." -ForegroundColor Yellow

$webProject = Join-Path $projectRoot "src\PSAG.Mantenimiento.Web\PSAG.Mantenimiento.Web.csproj"

# Limpiar carpeta destino
Remove-Item -Path "$webPath\*" -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish $webProject `
    --configuration Release `
    --output "$webPath\publish"

if ($LASTEXITCODE -ne 0) {
    throw "Error publicando Web"
}

# Mover wwwroot al nivel correcto para IIS
$wwwrootSource = Join-Path $webPath "publish\wwwroot"
if (Test-Path $wwwrootSource) {
    Copy-Item -Path "$wwwrootSource\*" -Destination $webPath -Recurse -Force
    Remove-Item -Path (Join-Path $webPath "publish") -Recurse -Force
}

Write-Host "  Web publicada en: $webPath" -ForegroundColor Green

# ============================================
# 5. CREAR ARCHIVOS DE CONFIGURACION
# ============================================

Write-Host ""
Write-Host "[5/5] Creando archivos de configuracion..." -ForegroundColor Yellow

# appsettings.Production.json para API
$apiSettings = @"
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=PSAG_Mantenimiento;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "JwtSettings": {
    "SecretKey": "PSAG_Mantenimiento_Production_Key_2024_Must_Be_At_Least_32_Characters_Long!",
    "Issuer": "PSAG.Mantenimiento.Api",
    "Audience": "PSAG.Mantenimiento.Web",
    "AccessTokenExpirationMinutes": 480,
    "RefreshTokenExpirationDays": 30
  },
  "Cors": {
    "AllowedOrigins": [
      "http://localhost",
      "http://2K22RECIMG",
      "http://2K22RECIMG.psag.local"
    ]
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "C:\\PSAG\\Mantenimiento\\logs\\api-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  },
  "AllowedHosts": "*"
}
"@

$apiSettingsPath = Join-Path $apiPath "appsettings.Production.json"
$apiSettings | Out-File -FilePath $apiSettingsPath -Encoding UTF8
Write-Host "  Creado: appsettings.Production.json (API)" -ForegroundColor DarkGray

# appsettings.json para Web (Blazor WASM)
$webSettings = @"
{
  "ApiBaseUrl": "http://localhost:5188/"
}
"@

$webSettingsPath = Join-Path $webPath "appsettings.json"
$webSettings | Out-File -FilePath $webSettingsPath -Encoding UTF8
Write-Host "  Creado: appsettings.json (Web)" -ForegroundColor DarkGray

# web.config para Web (IIS static files + SPA fallback)
$webConfig = @"
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <staticContent>
      <remove fileExtension=".blat" />
      <remove fileExtension=".dat" />
      <remove fileExtension=".dll" />
      <remove fileExtension=".json" />
      <remove fileExtension=".wasm" />
      <remove fileExtension=".woff" />
      <remove fileExtension=".woff2" />
      <mimeMap fileExtension=".blat" mimeType="application/octet-stream" />
      <mimeMap fileExtension=".dll" mimeType="application/octet-stream" />
      <mimeMap fileExtension=".dat" mimeType="application/octet-stream" />
      <mimeMap fileExtension=".json" mimeType="application/json" />
      <mimeMap fileExtension=".wasm" mimeType="application/wasm" />
      <mimeMap fileExtension=".woff" mimeType="font/woff" />
      <mimeMap fileExtension=".woff2" mimeType="font/woff2" />
    </staticContent>
    <httpCompression>
      <dynamicTypes>
        <add mimeType="application/wasm" enabled="true" />
      </dynamicTypes>
    </httpCompression>
    <rewrite>
      <rules>
        <rule name="SPA Fallback" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
            <add input="{REQUEST_URI}" pattern="^/api" negate="true" />
          </conditions>
          <action type="Rewrite" url="/index.html" />
        </rule>
      </rules>
    </rewrite>
  </system.webServer>
</configuration>
"@

$webConfigPath = Join-Path $webPath "web.config"
$webConfig | Out-File -FilePath $webConfigPath -Encoding UTF8
Write-Host "  Creado: web.config (Web)" -ForegroundColor DarkGray

# ============================================
# RESUMEN
# ============================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  PUBLICACION COMPLETADA" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Archivos publicados en:" -ForegroundColor White
Write-Host "  API: $apiPath" -ForegroundColor Gray
Write-Host "  Web: $webPath" -ForegroundColor Gray
Write-Host "  Logs: $logsPath" -ForegroundColor Gray
Write-Host ""
Write-Host "IMPORTANTE: Revisar y ajustar:" -ForegroundColor Yellow
Write-Host "  1. $apiSettingsPath" -ForegroundColor Gray
Write-Host "     - ConnectionStrings (servidor SQL)" -ForegroundColor Gray
Write-Host "     - JwtSettings.SecretKey (cambiar en produccion!)" -ForegroundColor Gray
Write-Host "     - Cors.AllowedOrigins (agregar IPs/dominios)" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. $webSettingsPath" -ForegroundColor Gray
Write-Host "     - ApiBaseUrl (URL del API en produccion)" -ForegroundColor Gray
Write-Host ""
Write-Host "Siguiente paso:" -ForegroundColor Cyan
Write-Host "  - Copiar C:\PSAG al servidor" -ForegroundColor White
Write-Host "  - Ejecutar 03-Configurar-IIS.ps1 en el servidor" -ForegroundColor White
Write-Host ""
