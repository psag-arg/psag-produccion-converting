<#
.SYNOPSIS
    Diagnostico de servidor antes de instalar PSAG.Mantenimiento
.DESCRIPTION
    Detecta stacks tecnologicos, servicios, puertos y posibles conflictos
.NOTES
    Ejecutar como Administrador en el servidor destino
#>

$ErrorActionPreference = "SilentlyContinue"

Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  DIAGNOSTICO DE SERVIDOR - PSAG.Mantenimiento" -ForegroundColor Cyan
Write-Host "  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan

# ============================================
# 1. INFORMACION DEL SISTEMA
# ============================================
Write-Host ""
Write-Host "== 1. SISTEMA OPERATIVO ==" -ForegroundColor Yellow

$os = Get-CimInstance Win32_OperatingSystem
$cs = Get-CimInstance Win32_ComputerSystem

Write-Host "  Nombre:        $($cs.Name)" -ForegroundColor White
Write-Host "  SO:            $($os.Caption)" -ForegroundColor White
Write-Host "  Version:       $($os.Version)" -ForegroundColor White
Write-Host "  Arquitectura:  $($os.OSArchitecture)" -ForegroundColor White
Write-Host "  RAM Total:     $([math]::Round($cs.TotalPhysicalMemory / 1GB, 2)) GB" -ForegroundColor White
Write-Host "  RAM Libre:     $([math]::Round(($os.FreePhysicalMemory * 1KB) / 1GB, 2)) GB" -ForegroundColor White

# ============================================
# 2. IIS - INTERNET INFORMATION SERVICES
# ============================================
Write-Host ""
Write-Host "== 2. IIS (Internet Information Services) ==" -ForegroundColor Yellow

$iisFeature = Get-WindowsFeature -Name Web-Server -ErrorAction SilentlyContinue

if ($iisFeature.Installed) {
    Write-Host "  Estado: INSTALADO" -ForegroundColor Green

    Import-Module WebAdministration -ErrorAction SilentlyContinue

    # Sitios web
    Write-Host ""
    Write-Host "  Sitios Web:" -ForegroundColor Cyan
    $sites = Get-Website -ErrorAction SilentlyContinue
    if ($sites) {
        foreach ($site in $sites) {
            $bindings = ($site.Bindings.Collection | ForEach-Object { $_.bindingInformation }) -join ", "
            $status = if ($site.State -eq "Started") { "[RUNNING]" } else { "[STOPPED]" }
            Write-Host "    - $($site.Name) $status" -ForegroundColor White
            Write-Host "      Ruta: $($site.PhysicalPath)" -ForegroundColor Gray
            Write-Host "      Bindings: $bindings" -ForegroundColor Gray
        }
    } else {
        Write-Host "    (ninguno)" -ForegroundColor Gray
    }

    # Application Pools
    Write-Host ""
    Write-Host "  Application Pools:" -ForegroundColor Cyan
    $pools = Get-ChildItem IIS:\AppPools -ErrorAction SilentlyContinue
    if ($pools) {
        foreach ($pool in $pools) {
            $status = if ($pool.State -eq "Started") { "[RUNNING]" } else { "[STOPPED]" }
            Write-Host "    - $($pool.Name) $status - Runtime: $($pool.managedRuntimeVersion)" -ForegroundColor White
        }
    }
} else {
    Write-Host "  Estado: NO INSTALADO" -ForegroundColor Red
    Write-Host "  >> Se requiere instalar IIS" -ForegroundColor Yellow
}

# ============================================
# 3. .NET FRAMEWORK Y .NET CORE/5+
# ============================================
Write-Host ""
Write-Host "== 3. .NET INSTALADOS ==" -ForegroundColor Yellow

# .NET Framework
Write-Host ""
Write-Host "  .NET Framework:" -ForegroundColor Cyan
$netFx = Get-ChildItem 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP' -Recurse -ErrorAction SilentlyContinue |
    Get-ItemProperty -Name Version, Release -ErrorAction SilentlyContinue |
    Where-Object { $_.PSChildName -match '^(?!S)\p{L}'} |
    Select-Object PSChildName, Version -Unique

if ($netFx) {
    $netFx | ForEach-Object { Write-Host "    - $($_.PSChildName): $($_.Version)" -ForegroundColor White }
} else {
    Write-Host "    (no detectado)" -ForegroundColor Gray
}

# .NET Core / .NET 5+
Write-Host ""
Write-Host "  .NET Runtimes (dotnet --list-runtimes):" -ForegroundColor Cyan
$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if ($dotnetCmd) {
    $runtimes = & dotnet --list-runtimes 2>$null
    if ($runtimes) {
        $runtimes | ForEach-Object { Write-Host "    - $_" -ForegroundColor White }
    }

    # Verificar .NET 9 específicamente
    $hasNet9 = $runtimes | Where-Object { $_ -match "Microsoft.AspNetCore.App 9" }
    if ($hasNet9) {
        Write-Host "  >> .NET 9 ASP.NET Core: INSTALADO" -ForegroundColor Green
    } else {
        Write-Host "  >> .NET 9 ASP.NET Core: NO INSTALADO (requerido)" -ForegroundColor Red
    }
} else {
    Write-Host "    dotnet CLI no encontrado" -ForegroundColor Red
    Write-Host "  >> Se requiere instalar .NET 9 Hosting Bundle" -ForegroundColor Yellow
}

# ASP.NET Core Module (para IIS)
Write-Host ""
Write-Host "  ASP.NET Core Module (IIS):" -ForegroundColor Cyan
$ancm = Get-WebGlobalModule -Name "AspNetCoreModuleV2" -ErrorAction SilentlyContinue
if ($ancm) {
    Write-Host "    AspNetCoreModuleV2: INSTALADO" -ForegroundColor Green
} else {
    Write-Host "    AspNetCoreModuleV2: NO INSTALADO" -ForegroundColor Red
    Write-Host "  >> Se requiere instalar ASP.NET Core Hosting Bundle" -ForegroundColor Yellow
}

# ============================================
# 4. SQL SERVER
# ============================================
Write-Host ""
Write-Host "== 4. SQL SERVER ==" -ForegroundColor Yellow

$sqlServices = Get-Service -Name "MSSQL*" -ErrorAction SilentlyContinue
if ($sqlServices) {
    Write-Host "  Instancias encontradas:" -ForegroundColor Cyan
    foreach ($svc in $sqlServices) {
        $status = if ($svc.Status -eq "Running") { "[RUNNING]" } else { "[STOPPED]" }
        Write-Host "    - $($svc.DisplayName) $status" -ForegroundColor White
    }

    # Intentar conectar y listar bases de datos
    Write-Host ""
    Write-Host "  Bases de datos:" -ForegroundColor Cyan

    $sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
    if ($sqlcmd) {
        $instances = @("localhost", "localhost\SQLEXPRESS", ".\SQLEXPRESS")
        foreach ($instance in $instances) {
            $dbs = & sqlcmd -S $instance -Q "SET NOCOUNT ON; SELECT name FROM sys.databases WHERE name NOT IN ('master','tempdb','model','msdb')" -h -1 -W 2>$null
            if ($LASTEXITCODE -eq 0 -and $dbs) {
                Write-Host "    Instancia: $instance" -ForegroundColor Green
                $dbs | Where-Object { $_ -and $_.Trim() } | ForEach-Object {
                    Write-Host "      - $_" -ForegroundColor White
                }
                break
            }
        }
    } else {
        Write-Host "    sqlcmd no disponible (instalar SSMS para ver detalles)" -ForegroundColor Gray
    }
} else {
    Write-Host "  SQL Server: NO ENCONTRADO" -ForegroundColor Red
}

# ============================================
# 5. SERVICIOS DE WINDOWS (Apps custom)
# ============================================
Write-Host ""
Write-Host "== 5. SERVICIOS DE WINDOWS (aplicaciones) ==" -ForegroundColor Yellow

$customServices = Get-Service | Where-Object {
    $_.Name -match "PSAG|Node|Python|API|Web|App" -or
    $_.DisplayName -match "PSAG|Node|Python|API|Web|App"
} | Sort-Object DisplayName

if ($customServices) {
    foreach ($svc in $customServices) {
        $status = switch ($svc.Status) {
            "Running" { "[RUNNING]" }
            "Stopped" { "[STOPPED]" }
            default { "[$($svc.Status)]" }
        }
        $color = if ($svc.Status -eq "Running") { "Green" } else { "Gray" }
        Write-Host "    - $($svc.DisplayName) $status" -ForegroundColor $color
        Write-Host "      Nombre: $($svc.Name)" -ForegroundColor Gray
    }
} else {
    Write-Host "  (ninguno detectado con patron PSAG/Node/Python/API/Web)" -ForegroundColor Gray
}

# ============================================
# 6. PUERTOS EN USO
# ============================================
Write-Host ""
Write-Host "== 6. PUERTOS EN USO (relevantes) ==" -ForegroundColor Yellow

$relevantPorts = @(80, 443, 5000, 5001, 5188, 8080, 8443, 3000, 1433)
$listeners = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
    Where-Object { $_.LocalPort -in $relevantPorts } |
    Select-Object LocalPort, OwningProcess -Unique |
    Sort-Object LocalPort

if ($listeners) {
    foreach ($l in $listeners) {
        $proc = Get-Process -Id $l.OwningProcess -ErrorAction SilentlyContinue
        $procName = if ($proc) { $proc.ProcessName } else { "N/A" }

        $portUse = switch ($l.LocalPort) {
            80 { "(HTTP - IIS/Web)" }
            443 { "(HTTPS)" }
            5188 { "(PSAG API - CONFLICTO!)" }
            5000 { "(ASP.NET default)" }
            5001 { "(ASP.NET HTTPS)" }
            8080 { "(HTTP alternativo)" }
            3000 { "(Node.js)" }
            1433 { "(SQL Server)" }
            default { "" }
        }

        $color = if ($l.LocalPort -eq 5188) { "Red" } else { "White" }
        Write-Host "    Puerto $($l.LocalPort) $portUse -> $procName (PID: $($l.OwningProcess))" -ForegroundColor $color
    }
} else {
    Write-Host "  (ninguno de los puertos relevantes en uso)" -ForegroundColor Gray
}

# Verificar puerto 5188 específicamente
$port5188 = Get-NetTCPConnection -LocalPort 5188 -State Listen -ErrorAction SilentlyContinue
if ($port5188) {
    Write-Host ""
    Write-Host "  >> ALERTA: Puerto 5188 en uso (requerido para PSAG API)" -ForegroundColor Red
} else {
    Write-Host ""
    Write-Host "  >> Puerto 5188 disponible para PSAG API" -ForegroundColor Green
}

# ============================================
# 7. NODE.JS / PYTHON
# ============================================
Write-Host ""
Write-Host "== 7. OTROS RUNTIMES ==" -ForegroundColor Yellow

# Node.js
$nodeCmd = Get-Command node -ErrorAction SilentlyContinue
if ($nodeCmd) {
    $nodeVer = & node --version 2>$null
    Write-Host "  Node.js: $nodeVer" -ForegroundColor Green
} else {
    Write-Host "  Node.js: No instalado" -ForegroundColor Gray
}

# Python
$pythonCmd = Get-Command python -ErrorAction SilentlyContinue
if ($pythonCmd) {
    $pythonVer = & python --version 2>$null
    Write-Host "  Python: $pythonVer" -ForegroundColor Green
} else {
    Write-Host "  Python: No instalado" -ForegroundColor Gray
}

# ============================================
# 8. DISCO
# ============================================
Write-Host ""
Write-Host "== 8. ESPACIO EN DISCO ==" -ForegroundColor Yellow

$drives = Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Used -ne $null }
foreach ($drive in $drives) {
    $total = [math]::Round($drive.Used / 1GB + $drive.Free / 1GB, 2)
    $free = [math]::Round($drive.Free / 1GB, 2)
    $pct = [math]::Round(($drive.Free / ($drive.Used + $drive.Free)) * 100, 1)
    $color = if ($pct -lt 10) { "Red" } elseif ($pct -lt 20) { "Yellow" } else { "Green" }
    Write-Host "  $($drive.Name): $free GB libres de $total GB ($pct% libre)" -ForegroundColor $color
}

# ============================================
# 9. URL REWRITE MODULE
# ============================================
Write-Host ""
Write-Host "== 9. IIS URL REWRITE MODULE ==" -ForegroundColor Yellow

$urlRewrite = Get-WebGlobalModule -Name "RewriteModule" -ErrorAction SilentlyContinue
if ($urlRewrite) {
    Write-Host "  URL Rewrite: INSTALADO" -ForegroundColor Green
} else {
    Write-Host "  URL Rewrite: NO INSTALADO" -ForegroundColor Red
    Write-Host "  >> Se requiere para Blazor SPA routing" -ForegroundColor Yellow
}

# ============================================
# 10. RESUMEN Y RECOMENDACIONES
# ============================================
Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  RESUMEN - COMPATIBILIDAD CON PSAG.Mantenimiento" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

$issues = @()

# Verificar IIS
if (-not $iisFeature.Installed) {
    $issues += "Instalar IIS (Web-Server)"
}

# Verificar .NET 9
if (-not $hasNet9) {
    $issues += "Instalar .NET 9 ASP.NET Core Hosting Bundle"
}

# Verificar ASP.NET Core Module
if (-not $ancm) {
    $issues += "Instalar ASP.NET Core Hosting Bundle (incluye ANCM)"
}

# Verificar URL Rewrite
if (-not $urlRewrite) {
    $issues += "Instalar IIS URL Rewrite Module"
}

# Verificar SQL Server
if (-not $sqlServices) {
    $issues += "Instalar SQL Server Express"
}

# Verificar puerto 5188
if ($port5188) {
    $issues += "Puerto 5188 en uso - cambiar puerto del API o liberar"
}

if ($issues.Count -eq 0) {
    Write-Host "  SERVIDOR COMPATIBLE - Listo para instalar" -ForegroundColor Green
} else {
    Write-Host "  ACCIONES REQUERIDAS:" -ForegroundColor Yellow
    foreach ($issue in $issues) {
        Write-Host "    - $issue" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "  El script 01-Instalar-Prerrequisitos.ps1 instalara:" -ForegroundColor Cyan
Write-Host "    - IIS con modulos necesarios" -ForegroundColor White
Write-Host "    - .NET 9 ASP.NET Core Hosting Bundle" -ForegroundColor White
Write-Host "    - URL Rewrite Module" -ForegroundColor White
Write-Host ""
Write-Host "================================================================" -ForegroundColor Cyan
