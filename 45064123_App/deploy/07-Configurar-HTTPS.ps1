<#
.SYNOPSIS
    HTTPS OPCIONAL para PSAG.Mantenimiento (lo necesita el dictado por voz). El HTTP de siempre sigue igual.
.DESCRIPTION
    Chrome y Edge solo dejan usar el micrófono en páginas HTTPS (o localhost). Este script agrega, AL LADO del
    HTTP actual (Web :50000 y API :50001), dos puertos HTTPS:
        Web  https://<IP>:50443   (binding https en el mismo sitio de IIS)
        API  https://<IP>:50002   (Kestrel, mismo servicio NSSM, segunda URL)
    Cuando la Web se abre por https usa ApiBaseUrlHttps; abierta por http sigue usando ApiBaseUrl.

    Requiere un certificado YA INSTALADO en LocalMachine\My con clave privada, uso "Autenticación de servidor"
    y la IP del servidor en el SAN. Recomendado: pedirlo a la CA interna de la empresa (psag-2K22DCLB-CA,
    plantilla "Servidor web"); ver docs/HTTPS.md.

    Sin -Aplicar solo muestra lo que haría (no cambia nada).
.EXAMPLE
    .\07-Configurar-HTTPS.ps1 -Ip 172.16.18.52 -Subject 2k22recimgProd.psag.local
    .\07-Configurar-HTTPS.ps1 -Ip 172.16.18.52 -Subject 2k22recimgProd.psag.local -Aplicar
    # En el 189 el nssm está en otra carpeta:
    .\07-Configurar-HTTPS.ps1 -Ip 172.16.12.189 -Subject 2K22RECIMG.psag.local -Nssm C:\serverRemitos\nssm.exe -Aplicar
.NOTES
    Ejecutar como Administrador en el servidor. Hace copia de los appsettings antes de tocarlos.
    Vuelta atrás: ver docs/HTTPS.md (sacar el binding, volver AppParameters y restaurar las copias).
#>

#Requires -RunAsAdministrator

param(
    [Parameter(Mandatory = $true)][string]$Ip,
    [Parameter(Mandatory = $true)][string]$Subject,
    [string]$Root = 'C:\PSAG_Mantenimiento',
    [string]$Nssm = '',
    [string]$SiteName = 'PSAG_Mantenimiento',
    [string]$ServiceName = 'PSAG_Mantenimiento_API',
    [int]$ApiHttpPort = 50001,
    [int]$WebHttpsPort = 50443,
    [int]$ApiHttpsPort = 50002,
    [string[]]$NombresExtra = @(),
    [switch]$Aplicar
)

$ErrorActionPreference = 'Stop'
if (-not $Nssm) { $Nssm = Join-Path $Root 'tools\nssm.exe' }
$apiCfg = Join-Path $Root 'Api\appsettings.Production.json'
$webCfg = Join-Path $Root 'Web\appsettings.json'
$ts = Get-Date -Format 'yyyyMMdd_HHmmss'
$modo = if ($Aplicar) { 'APLICA' } else { 'SOLO MUESTRA (agregar -Aplicar para hacerlo)' }
"=== HTTPS opcional PSAG.Mantenimiento en $Ip · $modo ==="

# 1) Certificado
$certs = Get-ChildItem Cert:\LocalMachine\My | Where-Object {
    $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) -and $_.Subject -like "*$Subject*" -and
    ($_.EnhancedKeyUsageList.ObjectId -contains '1.3.6.1.5.5.7.3.1')
} | Sort-Object NotAfter -Descending
if (-not $certs) { throw "No hay un certificado válido con clave privada y 'Autenticación de servidor' para '$Subject' en LocalMachine\My." }
$cert = $certs[0]
$san = ($cert.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.17' } | ForEach-Object { $_.Format($false) }) -join ' '
if ($san -notmatch [regex]::Escape($Ip)) { throw "El certificado $($cert.Thumbprint) no tiene la IP $Ip en el SAN ($san). Chrome lo rechazaría." }
"certificado: $($cert.Subject) · vence $($cert.NotAfter.ToString('dd/MM/yyyy')) · $($cert.Thumbprint)"
"SAN: $san"

# 2) IIS: binding https en el mismo sitio (el http :50000 no se toca)
Import-Module WebAdministration
$binding = Get-WebBinding -Name $SiteName -Protocol https -Port $WebHttpsPort -IPAddress $Ip
"IIS: binding https ${Ip}:$WebHttpsPort " + $(if ($binding) { 'ya existe' } else { 'se agrega' })
if ($Aplicar) {
    if (-not $binding) { New-WebBinding -Name $SiteName -Protocol https -Port $WebHttpsPort -IPAddress $Ip | Out-Null }
    $sslPath = "IIS:\SslBindings\$Ip!$WebHttpsPort"
    if (Test-Path $sslPath) { Remove-Item $sslPath }
    Get-Item "Cert:\LocalMachine\My\$($cert.Thumbprint)" | New-Item $sslPath | Out-Null
}

# 3) La cuenta del servicio de la API tiene que poder leer la clave privada
$cuenta = ((& $Nssm get $ServiceName ObjectName) -join '' -replace "`0", '').Trim()
$rsa = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
$archivoClave = $null
if ($rsa -is [System.Security.Cryptography.RSACng]) {
    $archivoClave = Join-Path $env:ProgramData "Microsoft\Crypto\Keys\$($rsa.Key.UniqueName)"
} elseif ($rsa -is [System.Security.Cryptography.RSACryptoServiceProvider]) {
    $archivoClave = Join-Path $env:ProgramData "Microsoft\Crypto\RSA\MachineKeys\$($rsa.CspKeyContainerInfo.UniqueKeyContainerName)"
}
"clave privada: $archivoClave · lectura para '$cuenta'"
if ($Aplicar -and $archivoClave -and $cuenta -and $cuenta -ne 'LocalSystem') { icacls $archivoClave /grant "${cuenta}:R" | Out-Null }

# 4) API: certificado de Kestrel, CORS y sin redirección forzada
$cfg = Get-Content $apiCfg -Raw | ConvertFrom-Json
$kestrel = [pscustomobject]@{ Certificates = [pscustomobject]@{ Default = [pscustomobject]@{
    Subject = $Subject; Store = 'My'; Location = 'LocalMachine'; AllowInvalid = $false } } }
$cfg | Add-Member -NotePropertyName Kestrel -NotePropertyValue $kestrel -Force
$cfg | Add-Member -NotePropertyName Https -NotePropertyValue ([pscustomobject]@{ RedirigirHttp = $false }) -Force
$origenes = @($cfg.Cors.AllowedOrigins) + @("https://${Ip}:$WebHttpsPort") + ($NombresExtra | ForEach-Object { "https://${_}:$WebHttpsPort" }) | Select-Object -Unique
$cfg.Cors.AllowedOrigins = [string[]]$origenes
"API: Kestrel con el certificado '$Subject'; CORS = $($origenes -join ', ')"
if ($Aplicar) {
    Copy-Item $apiCfg "$apiCfg.pre_https_$ts" -Force
    [IO.File]::WriteAllText($apiCfg, ($cfg | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding($false)))
}

# 5) Servicio: la misma API escucha http (como siempre) y https
$urls = "http://${Ip}:$ApiHttpPort;https://${Ip}:$ApiHttpsPort"
"NSSM: AppParameters --urls=$urls (antes: " + ((& $Nssm get $ServiceName AppParameters) -join '' -replace "`0", '').Trim() + ")"
if ($Aplicar) { & $Nssm set $ServiceName AppParameters "--urls=$urls" | Out-Null }

# 6) Web: URL https de la API (la http queda como estaba)
$web = Get-Content $webCfg -Raw | ConvertFrom-Json
$web | Add-Member -NotePropertyName ApiBaseUrlHttps -NotePropertyValue "https://${Ip}:$ApiHttpsPort/" -Force
"Web: ApiBaseUrl = $($web.ApiBaseUrl) (igual) · ApiBaseUrlHttps = https://${Ip}:$ApiHttpsPort/"
if ($Aplicar) {
    Copy-Item $webCfg "$webCfg.pre_https_$ts" -Force
    [IO.File]::WriteAllText($webCfg, ($web | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
    Remove-Item "$webCfg.br", "$webCfg.gz" -Force -ErrorAction SilentlyContinue
}

# 7) Firewall
foreach ($p in $WebHttpsPort, $ApiHttpsPort) {
    $n = "PSAG Mantenimiento $p"
    $existe = Get-NetFirewallRule -DisplayName $n -ErrorAction SilentlyContinue
    "firewall: $n " + $(if ($existe) { 'ya existe' } else { 'se agrega (dominio/privado)' })
    if ($Aplicar -and -not $existe) {
        New-NetFirewallRule -DisplayName $n -Direction Inbound -Protocol TCP -LocalPort $p -Action Allow -Profile Domain,Private | Out-Null
    }
}

if (-not $Aplicar) { "=== No se cambió nada. Revisar lo de arriba y correr con -Aplicar. ==="; return }

# 8) Reinicio de la API y pruebas (http y https)
& $Nssm restart $ServiceName | Out-Null
$h = $null
for ($i = 0; $i -lt 30 -and -not $h; $i++) { try { $h = (Invoke-WebRequest "https://${Ip}:$ApiHttpsPort/health" -UseBasicParsing -TimeoutSec 5).Content } catch { Start-Sleep 3 } }
"HEALTH https API: $h"
"HEALTH http  API: " + (Invoke-WebRequest "http://${Ip}:$ApiHttpPort/health" -UseBasicParsing -TimeoutSec 10).Content
"WEB https: " + (Invoke-WebRequest "https://${Ip}:$WebHttpsPort/" -UseBasicParsing -TimeoutSec 10).StatusCode
"WEB http : " + (Invoke-WebRequest "http://${Ip}:50000/" -UseBasicParsing -TimeoutSec 10).StatusCode
if (-not $h) { Get-Content (Join-Path $Root 'logs\api-stderr.log') -Tail 20 -ErrorAction SilentlyContinue }
