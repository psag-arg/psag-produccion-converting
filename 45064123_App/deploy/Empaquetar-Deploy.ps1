<#
.SYNOPSIS
    Empaqueta todo lo necesario para deploy en servidor
.DESCRIPTION
    - Publica la aplicacion
    - Copia scripts de deploy
    - Copia scripts de base de datos
    - Crea ZIP listo para copiar al servidor
.NOTES
    Ejecutar en la maquina de desarrollo
#>

param(
    [string]$OutputPath = "C:\PSAG",
    [string]$ZipPath = "C:\PSAG_Mantenimiento_Deploy.zip"
)

$ErrorActionPreference = "Stop"

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptPath

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Empaquetar Deploy" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# ============================================
# 1. PUBLICAR APLICACION
# ============================================

Write-Host "[1/3] Publicando aplicacion..." -ForegroundColor Yellow
& (Join-Path $scriptPath "02-Publicar-Aplicacion.ps1") -OutputPath (Join-Path $OutputPath "Mantenimiento")

# ============================================
# 2. COPIAR SCRIPTS
# ============================================

Write-Host ""
Write-Host "[2/3] Copiando scripts de instalacion..." -ForegroundColor Yellow

$deployTarget = Join-Path $OutputPath "Mantenimiento\deploy"
New-Item -ItemType Directory -Path $deployTarget -Force | Out-Null

# Copiar scripts PowerShell
Copy-Item -Path (Join-Path $scriptPath "*.ps1") -Destination $deployTarget -Force
Copy-Item -Path (Join-Path $scriptPath "README.md") -Destination $deployTarget -Force

# Copiar scripts SQL
$databaseSource = Join-Path $projectRoot "database"
$databaseTarget = Join-Path $deployTarget "database"
New-Item -ItemType Directory -Path $databaseTarget -Force | Out-Null
Copy-Item -Path (Join-Path $databaseSource "*.sql") -Destination $databaseTarget -Force

Write-Host "  Scripts copiados a: $deployTarget" -ForegroundColor Green

# ============================================
# 3. CREAR ZIP
# ============================================

Write-Host ""
Write-Host "[3/3] Creando archivo ZIP..." -ForegroundColor Yellow

$sourceFolder = Join-Path $OutputPath "Mantenimiento"

if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Compress-Archive -Path $sourceFolder -DestinationPath $ZipPath -CompressionLevel Optimal

$zipSize = [math]::Round((Get-Item $ZipPath).Length / 1MB, 2)
Write-Host "  ZIP creado: $ZipPath ($zipSize MB)" -ForegroundColor Green

# ============================================
# RESUMEN
# ============================================

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  EMPAQUETADO COMPLETO" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Archivos generados:" -ForegroundColor White
Write-Host "  Carpeta: $sourceFolder" -ForegroundColor Gray
Write-Host "  ZIP:     $ZipPath" -ForegroundColor Gray
Write-Host ""
Write-Host "Para instalar en el servidor:" -ForegroundColor Yellow
Write-Host "  1. Copiar $ZipPath al servidor" -ForegroundColor Gray
Write-Host "  2. Descomprimir en C:\" -ForegroundColor Gray
Write-Host "  3. Ejecutar: C:\Mantenimiento\deploy\00-Instalacion-Completa.ps1" -ForegroundColor Gray
Write-Host ""
