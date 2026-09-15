<#
.SYNOPSIS
    Instala los prerrequisitos para PSAG.Mantenimiento en Windows Server 2022
.DESCRIPTION
    - Habilita IIS con modulos necesarios
    - Descarga e instala .NET 9 ASP.NET Core Hosting Bundle
    - Configura URL Rewrite para IIS
.NOTES
    Ejecutar como Administrador
#>

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  PSAG.Mantenimiento - Prerrequisitos" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ============================================
# 1. VERIFICAR SI YA ESTA INSTALADO
# ============================================

Write-Host "[1/4] Verificando estado actual..." -ForegroundColor Yellow

$iisInstalled = Get-WindowsFeature -Name Web-Server | Where-Object { $_.Installed }
$dotnetInstalled = Get-Command dotnet -ErrorAction SilentlyContinue

if ($iisInstalled) {
    Write-Host "  - IIS: Ya instalado" -ForegroundColor Green
} else {
    Write-Host "  - IIS: No instalado" -ForegroundColor Red
}

if ($dotnetInstalled) {
    $dotnetVersion = dotnet --version
    Write-Host "  - .NET SDK: $dotnetVersion" -ForegroundColor Green
} else {
    Write-Host "  - .NET: No instalado" -ForegroundColor Red
}

# ============================================
# 2. INSTALAR IIS CON MODULOS NECESARIOS
# ============================================

Write-Host ""
Write-Host "[2/4] Instalando IIS y modulos..." -ForegroundColor Yellow

$features = @(
    "Web-Server",                    # IIS Web Server
    "Web-Common-Http",               # Common HTTP Features
    "Web-Default-Doc",               # Default Document
    "Web-Dir-Browsing",              # Directory Browsing
    "Web-Http-Errors",               # HTTP Errors
    "Web-Static-Content",            # Static Content
    "Web-Http-Logging",              # HTTP Logging
    "Web-Stat-Compression",          # Static Content Compression
    "Web-Dyn-Compression",           # Dynamic Content Compression
    "Web-Filtering",                 # Request Filtering
    "Web-Windows-Auth",              # Windows Authentication
    "Web-Mgmt-Console",              # IIS Management Console
    "Web-Mgmt-Service",              # Management Service
    "Web-WebSockets"                 # WebSockets (para SignalR si se usa)
)

foreach ($feature in $features) {
    $installed = Get-WindowsFeature -Name $feature | Where-Object { $_.Installed }
    if (-not $installed) {
        Write-Host "  Instalando $feature..." -ForegroundColor Gray
        Install-WindowsFeature -Name $feature -IncludeManagementTools | Out-Null
    } else {
        Write-Host "  $feature ya instalado" -ForegroundColor DarkGray
    }
}

Write-Host "  IIS configurado correctamente" -ForegroundColor Green

# ============================================
# 3. DESCARGAR E INSTALAR .NET 9 HOSTING BUNDLE
# ============================================

Write-Host ""
Write-Host "[3/4] Instalando .NET 9 ASP.NET Core Hosting Bundle..." -ForegroundColor Yellow

$dotnet9Url = "https://download.visualstudio.microsoft.com/download/pr/b6a55627-1afc-4dd7-9239-13a47be5fe4c/600e2d844e87b0f8d90fd4a59a233af5/dotnet-hosting-9.0.0-win.exe"
$installerPath = "$env:TEMP\dotnet-hosting-9.0.0-win.exe"

# Verificar si ya esta instalado .NET 9 runtime
$aspnetCoreModule = Get-WebConfigurationProperty -PSPath 'MACHINE/WEBROOT/APPHOST' -Filter "system.webServer/globalModules/add[@name='AspNetCoreModuleV2']" -Name "image" -ErrorAction SilentlyContinue

if ($aspnetCoreModule) {
    Write-Host "  ASP.NET Core Module V2 ya instalado" -ForegroundColor Green

    # Verificar version
    $runtimes = dotnet --list-runtimes 2>$null | Where-Object { $_ -match "Microsoft.AspNetCore.App 9" }
    if ($runtimes) {
        Write-Host "  .NET 9 ASP.NET Core Runtime ya instalado" -ForegroundColor Green
    } else {
        Write-Host "  Descargando .NET 9 Hosting Bundle..." -ForegroundColor Gray

        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $dotnet9Url -OutFile $installerPath -UseBasicParsing

        Write-Host "  Instalando (esto puede tomar unos minutos)..." -ForegroundColor Gray
        Start-Process -FilePath $installerPath -ArgumentList "/quiet /norestart" -Wait

        Write-Host "  .NET 9 Hosting Bundle instalado" -ForegroundColor Green

        # Limpiar
        Remove-Item $installerPath -Force -ErrorAction SilentlyContinue
    }
} else {
    Write-Host "  Descargando .NET 9 Hosting Bundle..." -ForegroundColor Gray

    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $dotnet9Url -OutFile $installerPath -UseBasicParsing

    Write-Host "  Instalando (esto puede tomar unos minutos)..." -ForegroundColor Gray
    Start-Process -FilePath $installerPath -ArgumentList "/quiet /norestart" -Wait

    Write-Host "  .NET 9 Hosting Bundle instalado" -ForegroundColor Green

    # Limpiar
    Remove-Item $installerPath -Force -ErrorAction SilentlyContinue
}

# ============================================
# 4. INSTALAR URL REWRITE MODULE
# ============================================

Write-Host ""
Write-Host "[4/4] Instalando URL Rewrite Module..." -ForegroundColor Yellow

$urlRewriteInstalled = Get-WebGlobalModule -Name "RewriteModule" -ErrorAction SilentlyContinue

if ($urlRewriteInstalled) {
    Write-Host "  URL Rewrite Module ya instalado" -ForegroundColor Green
} else {
    $urlRewriteUrl = "https://download.microsoft.com/download/1/2/8/128E2E22-C1B9-44A4-BE2A-5859ED1D4592/rewrite_amd64_en-US.msi"
    $urlRewritePath = "$env:TEMP\rewrite_amd64.msi"

    Write-Host "  Descargando URL Rewrite Module..." -ForegroundColor Gray
    Invoke-WebRequest -Uri $urlRewriteUrl -OutFile $urlRewritePath -UseBasicParsing

    Write-Host "  Instalando..." -ForegroundColor Gray
    Start-Process -FilePath "msiexec.exe" -ArgumentList "/i `"$urlRewritePath`" /quiet /norestart" -Wait

    Write-Host "  URL Rewrite Module instalado" -ForegroundColor Green

    Remove-Item $urlRewritePath -Force -ErrorAction SilentlyContinue
}

# ============================================
# REINICIAR IIS
# ============================================

Write-Host ""
Write-Host "Reiniciando IIS..." -ForegroundColor Yellow
iisreset /restart | Out-Null
Write-Host "IIS reiniciado" -ForegroundColor Green

# ============================================
# RESUMEN
# ============================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  PRERREQUISITOS INSTALADOS" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Siguiente paso: Ejecutar 02-Publicar-Aplicacion.ps1" -ForegroundColor Cyan
Write-Host ""
