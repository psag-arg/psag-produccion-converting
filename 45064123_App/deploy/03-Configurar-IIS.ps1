<#
.SYNOPSIS
    Configura IIS para PSAG.Mantenimiento
.DESCRIPTION
    - Crea Application Pool para la aplicacion
    - Crea sitio web para Blazor WASM (static files)
    - Configura reverse proxy para API (opcional)
.NOTES
    Ejecutar en el servidor como Administrador
    Prerrequisito: 01-Instalar-Prerrequisitos.ps1
#>

#Requires -RunAsAdministrator

param(
    [string]$InstallPath = "C:\PSAG\Mantenimiento",
    [string]$SiteName = "PSAG_Mantenimiento",
    [int]$WebPort = 80,
    [int]$ApiPort = 5188,
    [string]$HostHeader = ""
)

$ErrorActionPreference = "Stop"
Import-Module WebAdministration

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PSAG.Mantenimiento - Configurar IIS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$webPath = Join-Path $InstallPath "Web"
$apiPath = Join-Path $InstallPath "Api"

# Verificar que existen las carpetas
if (-not (Test-Path $webPath)) {
    throw "No se encontro la carpeta Web en: $webPath"
}
if (-not (Test-Path $apiPath)) {
    throw "No se encontro la carpeta Api en: $apiPath"
}

# ============================================
# 1. CREAR APPLICATION POOL
# ============================================

Write-Host "[1/4] Creando Application Pool..." -ForegroundColor Yellow

$appPoolName = "${SiteName}_Pool"

if (Test-Path "IIS:\AppPools\$appPoolName") {
    Write-Host "  Application Pool '$appPoolName' ya existe" -ForegroundColor DarkGray
} else {
    New-WebAppPool -Name $appPoolName | Out-Null
    Write-Host "  Application Pool '$appPoolName' creado" -ForegroundColor Green
}

# Configurar el pool
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "managedRuntimeVersion" -Value ""  # No managed code (static files)
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "startMode" -Value "AlwaysRunning"
Set-ItemProperty "IIS:\AppPools\$appPoolName" -Name "processModel.idleTimeout" -Value "00:00:00"

Write-Host "  Application Pool configurado" -ForegroundColor Green

# ============================================
# 2. CREAR SITIO WEB (Blazor WASM)
# ============================================

Write-Host ""
Write-Host "[2/4] Creando sitio web..." -ForegroundColor Yellow

# Verificar si el sitio existe
$existingSite = Get-Website -Name $SiteName -ErrorAction SilentlyContinue

if ($existingSite) {
    Write-Host "  Sitio '$SiteName' ya existe, actualizando..." -ForegroundColor DarkGray
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name "physicalPath" -Value $webPath
    Set-ItemProperty "IIS:\Sites\$SiteName" -Name "applicationPool" -Value $appPoolName
} else {
    # Crear binding
    $binding = @{
        Protocol = "http"
        Port = $WebPort
        HostHeader = $HostHeader
    }

    New-Website -Name $SiteName `
        -PhysicalPath $webPath `
        -ApplicationPool $appPoolName `
        -Port $WebPort `
        -HostHeader $HostHeader | Out-Null

    Write-Host "  Sitio '$SiteName' creado" -ForegroundColor Green
}

Write-Host "  Sitio configurado en puerto $WebPort" -ForegroundColor Green

# ============================================
# 3. CONFIGURAR PERMISOS
# ============================================

Write-Host ""
Write-Host "[3/4] Configurando permisos..." -ForegroundColor Yellow

# Dar permisos a IIS_IUSRS sobre las carpetas
$acl = Get-Acl $InstallPath
$identity = "IIS_IUSRS"
$rights = "ReadAndExecute, ListDirectory, Read"
$inheritance = "ContainerInherit, ObjectInherit"
$propagation = "None"
$type = "Allow"

$rule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, $rights, $inheritance, $propagation, $type)
$acl.AddAccessRule($rule)
Set-Acl $InstallPath $acl

# Permisos de escritura en logs
$logsPath = Join-Path $InstallPath "logs"
if (Test-Path $logsPath) {
    $aclLogs = Get-Acl $logsPath
    $rightsLogs = "Modify, ReadAndExecute, ListDirectory, Read, Write"
    $ruleLogs = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, $rightsLogs, $inheritance, $propagation, $type)
    $aclLogs.AddAccessRule($ruleLogs)
    Set-Acl $logsPath $aclLogs
}

Write-Host "  Permisos configurados para IIS_IUSRS" -ForegroundColor Green

# ============================================
# 4. CONFIGURAR DEFAULT DOCUMENT
# ============================================

Write-Host ""
Write-Host "[4/4] Configurando documentos predeterminados..." -ForegroundColor Yellow

# Asegurar que index.html es el default
$defaultDocs = Get-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" -Filter "system.webServer/defaultDocument/files" -Name "Collection"
$hasIndex = $defaultDocs | Where-Object { $_.value -eq "index.html" }

if (-not $hasIndex) {
    Add-WebConfigurationProperty -PSPath "IIS:\Sites\$SiteName" -Filter "system.webServer/defaultDocument/files" -Name "." -Value @{value='index.html'}
}

Write-Host "  Default document configurado" -ForegroundColor Green

# ============================================
# INICIAR SITIO
# ============================================

Write-Host ""
Write-Host "Iniciando sitio web..." -ForegroundColor Yellow

Start-Website -Name $SiteName -ErrorAction SilentlyContinue
Start-WebAppPool -Name $appPoolName -ErrorAction SilentlyContinue

Write-Host "Sitio iniciado" -ForegroundColor Green

# ============================================
# RESUMEN
# ============================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  IIS CONFIGURADO" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Sitio Web (Blazor WASM):" -ForegroundColor White
Write-Host "  Nombre: $SiteName" -ForegroundColor Gray
Write-Host "  Puerto: $WebPort" -ForegroundColor Gray
Write-Host "  Ruta:   $webPath" -ForegroundColor Gray
Write-Host "  URL:    http://localhost:$WebPort/" -ForegroundColor Cyan
Write-Host ""
Write-Host "NOTA: El API debe ejecutarse como Windows Service" -ForegroundColor Yellow
Write-Host "      Ejecutar: 04-Crear-Servicio-API.ps1" -ForegroundColor Yellow
Write-Host ""

# Mostrar si el Default Web Site esta en el mismo puerto
$defaultSite = Get-Website -Name "Default Web Site" -ErrorAction SilentlyContinue
if ($defaultSite -and $defaultSite.State -eq "Started") {
    $defaultBindings = Get-WebBinding -Name "Default Web Site"
    $conflictBinding = $defaultBindings | Where-Object { $_.bindingInformation -like "*:${WebPort}:*" -or $_.bindingInformation -like "*:${WebPort}" }
    if ($conflictBinding) {
        Write-Host "ADVERTENCIA: 'Default Web Site' usa el puerto $WebPort" -ForegroundColor Red
        Write-Host "             Detenerlo o cambiar el puerto de PSAG_Mantenimiento" -ForegroundColor Red
        Write-Host ""
        Write-Host "Para detener Default Web Site:" -ForegroundColor Yellow
        Write-Host "  Stop-Website -Name 'Default Web Site'" -ForegroundColor Gray
        Write-Host ""
    }
}
