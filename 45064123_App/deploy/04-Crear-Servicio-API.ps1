<#
.SYNOPSIS
    Crea el Windows Service para PSAG.Mantenimiento API
.DESCRIPTION
    - Descarga NSSM (Non-Sucking Service Manager) si no existe
    - Crea el servicio de Windows para ejecutar el API
    - Configura inicio automatico y recuperacion
.NOTES
    Ejecutar en el servidor como Administrador
    Prerrequisito: 02-Publicar-Aplicacion.ps1
#>

#Requires -RunAsAdministrator

param(
    [string]$InstallPath = "C:\PSAG\Mantenimiento",
    [string]$ServiceName = "PSAG_Mantenimiento_API",
    [string]$ServiceDisplayName = "PSAG Mantenimiento API",
    [int]$ApiPort = 5188
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PSAG.Mantenimiento - Crear Servicio" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$apiPath = Join-Path $InstallPath "Api"
$toolsPath = Join-Path $InstallPath "tools"
$logsPath = Join-Path $InstallPath "logs"

# Verificar que existe la carpeta API
if (-not (Test-Path $apiPath)) {
    throw "No se encontro la carpeta API en: $apiPath"
}

$apiExe = Join-Path $apiPath "PSAG.Mantenimiento.Api.exe"
if (-not (Test-Path $apiExe)) {
    throw "No se encontro el ejecutable: $apiExe"
}

# ============================================
# 1. DESCARGAR NSSM
# ============================================

Write-Host "[1/4] Verificando NSSM..." -ForegroundColor Yellow

New-Item -ItemType Directory -Path $toolsPath -Force | Out-Null

$nssmPath = Join-Path $toolsPath "nssm.exe"

if (-not (Test-Path $nssmPath)) {
    Write-Host "  Descargando NSSM..." -ForegroundColor Gray

    $nssmUrl = "https://nssm.cc/release/nssm-2.24.zip"
    $nssmZip = Join-Path $env:TEMP "nssm.zip"
    $nssmExtract = Join-Path $env:TEMP "nssm-extract"

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $nssmUrl -OutFile $nssmZip -UseBasicParsing

    Expand-Archive -Path $nssmZip -DestinationPath $nssmExtract -Force

    # Copiar el exe de 64 bits
    $nssmSrc = Join-Path $nssmExtract "nssm-2.24\win64\nssm.exe"
    Copy-Item -Path $nssmSrc -Destination $nssmPath -Force

    # Limpiar
    Remove-Item $nssmZip -Force -ErrorAction SilentlyContinue
    Remove-Item $nssmExtract -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "  NSSM descargado" -ForegroundColor Green
} else {
    Write-Host "  NSSM ya existe" -ForegroundColor Green
}

# ============================================
# 2. DETENER SERVICIO EXISTENTE
# ============================================

Write-Host ""
Write-Host "[2/4] Verificando servicio existente..." -ForegroundColor Yellow

$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host "  Deteniendo servicio existente..." -ForegroundColor Gray

    if ($existingService.Status -eq "Running") {
        Stop-Service -Name $ServiceName -Force
        Start-Sleep -Seconds 2
    }

    # Eliminar servicio existente
    & $nssmPath remove $ServiceName confirm 2>$null
    Start-Sleep -Seconds 1

    Write-Host "  Servicio anterior eliminado" -ForegroundColor DarkGray
}

# ============================================
# 3. CREAR NUEVO SERVICIO
# ============================================

Write-Host ""
Write-Host "[3/4] Creando servicio..." -ForegroundColor Yellow

# Instalar servicio con NSSM
& $nssmPath install $ServiceName $apiExe
if ($LASTEXITCODE -ne 0) {
    throw "Error instalando servicio"
}

# Configurar parametros
& $nssmPath set $ServiceName AppDirectory $apiPath
& $nssmPath set $ServiceName AppParameters "--urls http://localhost:$ApiPort"
& $nssmPath set $ServiceName DisplayName $ServiceDisplayName
& $nssmPath set $ServiceName Description "API REST para el sistema de control de paradas PSAG Mantenimiento"
& $nssmPath set $ServiceName Start SERVICE_AUTO_START
& $nssmPath set $ServiceName ObjectName LocalSystem

# Configurar logs
& $nssmPath set $ServiceName AppStdout (Join-Path $logsPath "api-stdout.log")
& $nssmPath set $ServiceName AppStderr (Join-Path $logsPath "api-stderr.log")
& $nssmPath set $ServiceName AppStdoutCreationDisposition 4
& $nssmPath set $ServiceName AppStderrCreationDisposition 4
& $nssmPath set $ServiceName AppRotateFiles 1
& $nssmPath set $ServiceName AppRotateOnline 1
& $nssmPath set $ServiceName AppRotateBytes 10485760

# Configurar variables de entorno
& $nssmPath set $ServiceName AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"

Write-Host "  Servicio creado" -ForegroundColor Green

# ============================================
# 4. CONFIGURAR RECUPERACION
# ============================================

Write-Host ""
Write-Host "[4/4] Configurando recuperacion ante fallos..." -ForegroundColor Yellow

# Configurar acciones de recuperacion: reiniciar en 1er, 2do y 3er fallo
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

Write-Host "  Recuperacion configurada" -ForegroundColor Green

# ============================================
# INICIAR SERVICIO
# ============================================

Write-Host ""
Write-Host "Iniciando servicio..." -ForegroundColor Yellow

Start-Service -Name $ServiceName
Start-Sleep -Seconds 3

$service = Get-Service -Name $ServiceName
if ($service.Status -eq "Running") {
    Write-Host "Servicio iniciado correctamente" -ForegroundColor Green
} else {
    Write-Host "ADVERTENCIA: El servicio no inicio. Revisar logs en:" -ForegroundColor Red
    Write-Host "  $logsPath" -ForegroundColor Gray
}

# ============================================
# VERIFICAR CONEXION
# ============================================

Write-Host ""
Write-Host "Verificando API..." -ForegroundColor Yellow

Start-Sleep -Seconds 2

try {
    $response = Invoke-WebRequest -Uri "http://localhost:$ApiPort/health" -UseBasicParsing -TimeoutSec 10 -ErrorAction SilentlyContinue
    if ($response.StatusCode -eq 200) {
        Write-Host "  API respondiendo en http://localhost:$ApiPort" -ForegroundColor Green
    }
} catch {
    # Intentar con swagger
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:$ApiPort/swagger" -UseBasicParsing -TimeoutSec 10 -ErrorAction SilentlyContinue
        Write-Host "  API respondiendo en http://localhost:$ApiPort" -ForegroundColor Green
    } catch {
        Write-Host "  API aun iniciando... verificar en unos segundos" -ForegroundColor Yellow
    }
}

# ============================================
# RESUMEN
# ============================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  SERVICIO CREADO" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Servicio:" -ForegroundColor White
Write-Host "  Nombre:  $ServiceName" -ForegroundColor Gray
Write-Host "  Display: $ServiceDisplayName" -ForegroundColor Gray
Write-Host "  Estado:  $($service.Status)" -ForegroundColor Gray
Write-Host "  URL:     http://localhost:$ApiPort" -ForegroundColor Cyan
Write-Host ""
Write-Host "Comandos utiles:" -ForegroundColor Yellow
Write-Host "  Iniciar:   Start-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Detener:   Stop-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Reiniciar: Restart-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Estado:    Get-Service $ServiceName" -ForegroundColor Gray
Write-Host "  Logs:      Get-Content $logsPath\api-stdout.log -Tail 50" -ForegroundColor Gray
Write-Host ""
Write-Host "Siguiente paso: 05-Configurar-BaseDatos.ps1" -ForegroundColor Cyan
Write-Host ""
