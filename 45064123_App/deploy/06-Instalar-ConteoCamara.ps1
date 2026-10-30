<#
.SYNOPSIS
    Instala o actualiza el servicio de conteo por cámara (T-610) en el servidor, sin internet.
.DESCRIPTION
    Ejecutar en el servidor como Administrador, desde la carpeta del paquete (Empaquetar-ConteoCamara.ps1).
      1. Copia el paquete a $InstallPath conservando zonas-camara.json, appsettings.local.json y pendientes\.
      2. Verifica ffmpeg y el archivo de credenciales del NVR (no lo lee ni lo muestra) y le restringe permisos.
      3. Registra el servicio con el NSSM que ya usa la API, con la clave de ingesta como variable de entorno
         (no queda en ningún archivo de la aplicación), logs rotados e inicio automático.
.EXAMPLE
    .\06-Instalar-ConteoCamara.ps1
    .\06-Instalar-ConteoCamara.ps1 -SoloActualizar    # copia los binarios y reinicia, sin tocar la configuración del servicio
#>
#Requires -RunAsAdministrator
param(
    [string]$InstallPath = "C:\PSAG\ConteoCamara",
    [string]$ServiceName = "PSAG_ConteoCamara",
    [string]$Nssm = "C:\PSAG\Mantenimiento\tools\nssm.exe",
    [string]$ArchivoCredenciales = "C:\PSAG\secretos\nvr.env",
    [switch]$SoloActualizar
)

$ErrorActionPreference = "Stop"
$origen = $PSScriptRoot
$exe = Join-Path $InstallPath "PSAG.Mantenimiento.ConteoCamara.exe"
$logs = Join-Path $InstallPath "logs"

if (-not (Test-Path (Join-Path $origen "PSAG.Mantenimiento.ConteoCamara.exe"))) {
    throw "Ejecutá este script desde la carpeta del paquete (falta PSAG.Mantenimiento.ConteoCamara.exe)."
}
if (-not (Test-Path $Nssm)) { throw "No está NSSM en $Nssm (es el mismo que usa la API)." }

# 1. Detener si existe
$existe = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existe -and $existe.Status -eq "Running") {
    Write-Host "Deteniendo $ServiceName..." -ForegroundColor Yellow
    & $Nssm stop $ServiceName | Out-Null
}

# 2. Copiar conservando la configuración local
New-Item -ItemType Directory -Path $InstallPath, $logs -Force | Out-Null
$conservar = @("zonas-camara.json", "appsettings.local.json")
foreach ($f in $conservar) {
    if (Test-Path (Join-Path $InstallPath $f)) { Copy-Item (Join-Path $InstallPath $f) (Join-Path $env:TEMP "conteo-$f") -Force }
}
Get-ChildItem $origen -Exclude "pendientes", "logs" | Copy-Item -Destination $InstallPath -Recurse -Force
foreach ($f in $conservar) {
    $guardado = Join-Path $env:TEMP "conteo-$f"
    if (Test-Path $guardado) { Move-Item $guardado (Join-Path $InstallPath $f) -Force }
}
Write-Host "Archivos copiados a $InstallPath" -ForegroundColor Green

# 3. Verificaciones
if (-not (Test-Path (Join-Path $InstallPath "ffmpeg\ffmpeg.exe"))) { throw "Falta ffmpeg\ffmpeg.exe en el paquete." }
if (-not (Test-Path $ArchivoCredenciales)) {
    Write-Warning "No existe $ArchivoCredenciales. Crealo con tres líneas (NVR=..., NVR_USER=..., NVR_PASS=...) y volvé a correr el script."
    throw "Faltan las credenciales del NVR."
}
# Sólo Administradores y SYSTEM pueden leer la carpeta de secretos.
$carpetaSecretos = Split-Path $ArchivoCredenciales -Parent
icacls $carpetaSecretos /inheritance:r /grant:r "*S-1-5-32-544:(OI)(CI)F" "*S-1-5-18:(OI)(CI)F" | Out-Null
Write-Host "Permisos de $carpetaSecretos restringidos a Administradores y SYSTEM" -ForegroundColor Green

if ($SoloActualizar -and $existe) {
    & $Nssm start $ServiceName | Out-Null
    Write-Host "Servicio actualizado y reiniciado." -ForegroundColor Green
    return
}

# 4. Clave de ingesta (la misma que Calidad:ClaveIngesta de la API)
$seguro = Read-Host "Clave de ingesta (la misma que Calidad__ClaveIngesta de la API)" -AsSecureString
$clave = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro))
if ($clave.Length -lt 16) { throw "La clave tiene que tener al menos 16 caracteres." }

# 5. Servicio
if ($existe) { & $Nssm remove $ServiceName confirm | Out-Null }
& $Nssm install $ServiceName $exe | Out-Null
& $Nssm set $ServiceName AppDirectory $InstallPath | Out-Null
& $Nssm set $ServiceName DisplayName "PSAG Conteo por cámara (T-610)" | Out-Null
& $Nssm set $ServiceName Description "Cuenta rollos y detecta paradas con la cámara 09 (salida de la cortadora). Sólo lectura del NVR." | Out-Null
& $Nssm set $ServiceName Start SERVICE_AUTO_START | Out-Null
& $Nssm set $ServiceName ObjectName LocalSystem | Out-Null
& $Nssm set $ServiceName AppStdout (Join-Path $logs "conteo.log") | Out-Null
& $Nssm set $ServiceName AppStderr (Join-Path $logs "conteo.log") | Out-Null
& $Nssm set $ServiceName AppStdoutCreationDisposition 4 | Out-Null
& $Nssm set $ServiceName AppStderrCreationDisposition 4 | Out-Null
& $Nssm set $ServiceName AppRotateFiles 1 | Out-Null
& $Nssm set $ServiceName AppRotateOnline 1 | Out-Null
& $Nssm set $ServiceName AppRotateBytes 10485760 | Out-Null
& $Nssm set $ServiceName AppStopMethodConsole 15000 | Out-Null
& $Nssm set $ServiceName AppEnvironmentExtra "ConteoCamara__Api__ClaveIngesta=$clave" "ConteoCamara__Fuente__ArchivoCredenciales=$ArchivoCredenciales" | Out-Null
$clave = $null
sc.exe failure $ServiceName reset= 86400 actions= restart/10000/restart/30000/restart/60000 | Out-Null

& $Nssm start $ServiceName | Out-Null
Start-Sleep -Seconds 5
Get-Service $ServiceName | Format-Table Name, Status -AutoSize
Write-Host "Logs: $logs\conteo.log. Debería aparecer 'Video recibido del NVR' en menos de 30 s." -ForegroundColor Gray
