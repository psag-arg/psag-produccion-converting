<#
.SYNOPSIS
    Arma el paquete offline del servicio de conteo por cámara (T-610) para copiar al servidor.
.DESCRIPTION
    En la PC de desarrollo (con el SDK de .NET 9):
      - publica PSAG.Mantenimiento.ConteoCamara (framework-dependent: usa el runtime .NET 9 que ya tiene el
        servidor por la API, no necesita paquetes NuGet ni internet en el servidor),
      - copia ffmpeg.exe portable (estático, sin DLLs) a ffmpeg\ffmpeg.exe,
      - copia 06-Instalar-ConteoCamara.ps1 y arma un ZIP.
    El paquete NO lleva credenciales ni la clave de ingesta.
.PARAMETER Ffmpeg
    Ruta a un ffmpeg.exe estático para Windows x64 con soporte HEVC (p. ej. el "essentials" de gyan.dev).
.EXAMPLE
    .\deploy\Empaquetar-ConteoCamara.ps1 -Ffmpeg C:\herramientas\ffmpeg.exe
#>
param(
    [Parameter(Mandatory = $true)][string]$Ffmpeg,
    [string]$Salida = (Join-Path $PSScriptRoot "..\publish\ConteoCamara")
)

$ErrorActionPreference = "Stop"
$raiz = Resolve-Path (Join-Path $PSScriptRoot "..")
$proyecto = Join-Path $raiz "src\PSAG.Mantenimiento.ConteoCamara\PSAG.Mantenimiento.ConteoCamara.csproj"

if (-not (Test-Path $Ffmpeg)) { throw "No existe $Ffmpeg" }
if (-not (& $Ffmpeg -hide_banner -decoders 2>$null | Select-String -Quiet " hevc ")) {
    throw "Ese ffmpeg no decodifica HEVC (H.265), que es lo que manda la cámara 09."
}

if (Test-Path $Salida) { Remove-Item $Salida -Recurse -Force }
New-Item -ItemType Directory -Path $Salida -Force | Out-Null

Write-Host "Publicando el servicio..." -ForegroundColor Yellow
dotnet publish $proyecto -c Release -o $Salida --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falló" }

New-Item -ItemType Directory -Path (Join-Path $Salida "ffmpeg") -Force | Out-Null
Copy-Item $Ffmpeg (Join-Path $Salida "ffmpeg\ffmpeg.exe") -Force
Copy-Item (Join-Path $PSScriptRoot "06-Instalar-ConteoCamara.ps1") $Salida -Force

# Nada de secretos en el paquete
Get-ChildItem $Salida -Recurse -Include "appsettings.local.json", "*.env" | Remove-Item -Force

$zip = "$Salida-$(Get-Date -Format yyyyMMdd).zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $Salida "*") -DestinationPath $zip
Write-Host "Paquete listo: $zip" -ForegroundColor Green
Write-Host "Copialo al servidor y seguí docs/CONTEO_CAMARA.md (sección Instalación)." -ForegroundColor Gray
