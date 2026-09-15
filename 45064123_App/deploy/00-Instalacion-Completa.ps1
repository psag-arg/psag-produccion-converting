<#
.SYNOPSIS
    Script maestro de instalacion de PSAG.Mantenimiento
.DESCRIPTION
    Ejecuta todos los pasos de instalacion en secuencia:
    1. Prerrequisitos (IIS, .NET 9)
    2. Configurar IIS
    3. Crear Windows Service
    4. Configurar Base de Datos
.NOTES
    Ejecutar en el servidor como Administrador
    IMPORTANTE: Primero ejecutar 02-Publicar-Aplicacion.ps1 en desarrollo
                y copiar C:\PSAG al servidor
#>

#Requires -RunAsAdministrator

param(
    [string]$InstallPath = "C:\PSAG_Mantenimiento",
    [string]$SqlInstance = "",
    [string]$DatabaseName = "PSAG_Mantenimiento",
    [switch]$UseSqlServer = $false,
    [switch]$SkipPrerequisites = $false,
    [switch]$IncludeDemoData = $false
)

$ErrorActionPreference = "Stop"
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  PSAG.Mantenimiento - Instalacion Completa" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Este script instalara:" -ForegroundColor White
Write-Host "  1. Prerrequisitos (IIS, .NET 9 Runtime)" -ForegroundColor Gray
Write-Host "  2. Sitio web en IIS (Blazor WASM)" -ForegroundColor Gray
Write-Host "  3. Windows Service (API)" -ForegroundColor Gray
if ($UseSqlServer -or -not [string]::IsNullOrWhiteSpace($SqlInstance)) {
    Write-Host "  4. Base de datos SQL Server" -ForegroundColor Gray
} else {
    Write-Host "  4. Base de datos SQLite (embebida)" -ForegroundColor Gray
}
Write-Host ""
Write-Host "Configuracion:" -ForegroundColor Yellow
Write-Host "  InstallPath: $InstallPath" -ForegroundColor Gray
if ($UseSqlServer -or -not [string]::IsNullOrWhiteSpace($SqlInstance)) {
    $effectiveSqlInstance = if ([string]::IsNullOrWhiteSpace($SqlInstance)) { "localhost\SQLEXPRESS" } else { $SqlInstance }
    Write-Host "  SqlInstance: $effectiveSqlInstance" -ForegroundColor Gray
    Write-Host "  Database:    $DatabaseName" -ForegroundColor Gray
} else {
    Write-Host "  Database:    SQLite (auto-creada)" -ForegroundColor Gray
}
Write-Host ""

# Verificar que existen las carpetas
if (-not (Test-Path (Join-Path $InstallPath "Api"))) {
    Write-Host "ERROR: No se encontro la carpeta Api en $InstallPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Primero debes:" -ForegroundColor Yellow
    Write-Host "  1. Ejecutar 02-Publicar-Aplicacion.ps1 en la maquina de desarrollo" -ForegroundColor Gray
    Write-Host "  2. Copiar C:\PSAG al servidor" -ForegroundColor Gray
    Write-Host ""
    exit 1
}

$confirm = Read-Host "Continuar con la instalacion? (S/N)"
if ($confirm -ne "S" -and $confirm -ne "s") {
    Write-Host "Instalacion cancelada" -ForegroundColor Yellow
    exit 0
}

Write-Host ""

# ============================================
# PASO 1: PRERREQUISITOS
# ============================================

if (-not $SkipPrerequisites) {
    Write-Host "PASO 1/4: Instalando prerrequisitos..." -ForegroundColor Magenta
    Write-Host "========================================" -ForegroundColor Magenta
    & (Join-Path $scriptPath "01-Instalar-Prerrequisitos.ps1")
    Write-Host ""
} else {
    Write-Host "PASO 1/4: Prerrequisitos (OMITIDO)" -ForegroundColor DarkGray
    Write-Host ""
}

# ============================================
# PASO 2: CONFIGURAR IIS
# ============================================

Write-Host "PASO 2/4: Configurando IIS..." -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
& (Join-Path $scriptPath "03-Configurar-IIS.ps1") -InstallPath $InstallPath
Write-Host ""

# ============================================
# PASO 3: CREAR SERVICIO API
# ============================================

Write-Host "PASO 3/4: Creando servicio API..." -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta
& (Join-Path $scriptPath "04-Crear-Servicio-API.ps1") -InstallPath $InstallPath
Write-Host ""

# ============================================
# PASO 4: CONFIGURAR BASE DE DATOS
# ============================================

Write-Host "PASO 4/4: Configurando base de datos..." -ForegroundColor Magenta
Write-Host "========================================" -ForegroundColor Magenta

$dbParams = @{
    InstallPath = $InstallPath
}

if ($UseSqlServer -or -not [string]::IsNullOrWhiteSpace($SqlInstance)) {
    if (-not [string]::IsNullOrWhiteSpace($SqlInstance)) {
        $dbParams.Add("SqlInstance", $SqlInstance)
    }
    $dbParams.Add("UseSqlServer", $true)
    $dbParams.Add("DatabaseName", $DatabaseName)
}

if ($IncludeDemoData) {
    $dbParams.Add("IncludeDemoData", $true)
}

& (Join-Path $scriptPath "05-Configurar-BaseDatos.ps1") @dbParams
Write-Host ""

# ============================================
# RESUMEN FINAL
# ============================================

Write-Host ""
Write-Host "========================================================" -ForegroundColor Green
Write-Host "  INSTALACION COMPLETADA" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Servicios instalados:" -ForegroundColor White
Write-Host ""

# Estado de servicios
$apiService = Get-Service -Name "PSAG_Mantenimiento_API" -ErrorAction SilentlyContinue
$iisService = Get-Service -Name "W3SVC" -ErrorAction SilentlyContinue
$sqlService = Get-Service -Name "MSSQL`$SQLEXPRESS" -ErrorAction SilentlyContinue

Write-Host "  API Service:  $($apiService.Status)" -ForegroundColor $(if($apiService.Status -eq "Running"){"Green"}else{"Red"})
Write-Host "  IIS (W3SVC):  $($iisService.Status)" -ForegroundColor $(if($iisService.Status -eq "Running"){"Green"}else{"Red"})
Write-Host "  SQL Express:  $($sqlService.Status)" -ForegroundColor $(if($sqlService.Status -eq "Running"){"Green"}else{"Red"})
Write-Host ""

Write-Host "URLs de acceso:" -ForegroundColor Yellow
Write-Host "  Web:     http://localhost/" -ForegroundColor Cyan
Write-Host "  API:     http://localhost:5188/" -ForegroundColor Cyan
Write-Host "  Swagger: http://localhost:5188/swagger" -ForegroundColor Cyan
Write-Host ""

Write-Host "IMPORTANTE - Configurar antes de usar:" -ForegroundColor Yellow
Write-Host ""
Write-Host "  1. Editar C:\PSAG\Mantenimiento\Api\appsettings.Production.json" -ForegroundColor White
Write-Host "     - Cambiar JwtSettings.SecretKey" -ForegroundColor Gray
Write-Host "     - Agregar IPs/dominios a Cors.AllowedOrigins" -ForegroundColor Gray
Write-Host ""
Write-Host "  2. Editar C:\PSAG\Mantenimiento\Web\appsettings.json" -ForegroundColor White
Write-Host "     - Cambiar ApiBaseUrl al nombre/IP del servidor" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. Reiniciar servicios:" -ForegroundColor White
Write-Host "     Restart-Service PSAG_Mantenimiento_API" -ForegroundColor Gray
Write-Host "     iisreset" -ForegroundColor Gray
Write-Host ""
