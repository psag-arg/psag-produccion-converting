# HTTPS opcional (para el dictado por voz)

Chrome y Edge solo dejan usar el micrófono (y por lo tanto el botón **Dictar**) en páginas
**HTTPS** o en `localhost`. Por `http://172.16.x.x:50000` el botón aparece en gris y avisa que falta HTTPS.

El HTTPS se agrega **al lado** del HTTP actual, en puertos nuevos. Nada de lo que hoy anda cambia:

| | HTTP (como hoy) | HTTPS (nuevo, opcional) |
|---|---|---|
| Web (IIS, sitio `PSAG_Mantenimiento`) | `http://<IP>:50000` | `https://<IP>:50443` |
| API (Kestrel, servicio NSSM `PSAG_Mantenimiento_API`) | `http://<IP>:50001` | `https://<IP>:50002` |

Cómo funciona:
- La Web elige la API según cómo se abrió: por `https` usa `ApiBaseUrlHttps`; por `http`, `ApiBaseUrl` (Program.cs de la Web).
- La API escucha las dos URLs (`--urls=http://<IP>:50001;https://<IP>:50002`) y toma el certificado de
  `Kestrel:Certificates:Default` (almacén LocalMachine\My, por Subject). **No** redirige http → https salvo
  que se ponga `"Https": { "RedirigirHttp": true }`.

## 1. Certificado

**Recomendado: pedirlo a la CA interna de la empresa** `psag-2K22DCLB-CA` (servidor `2K22DCLB.psag.local`,
plantilla *Servidor web* / `WebServer`). Las PC del dominio ya confían en esa CA, así que en las PC no hay que
instalar nada. Hoy nuestro usuario no tiene permiso de inscripción en esa plantilla ("Acceso denegado"):
lo tiene que emitir IT (Leonardo / Darío) o darle permiso de inscripción a la cuenta de equipo del servidor.

Pedido (en el servidor, PowerShell como administrador). Ejemplo para el **.52**:

```ini
; C:\PSAG_Mantenimiento\https\req.inf
[NewRequest]
Subject = "CN=2k22recimgProd.psag.local"
KeyLength = 2048
KeySpec = 1
KeyUsage = 0xa0
MachineKeySet = TRUE
Exportable = FALSE
ProviderName = "Microsoft RSA SChannel Cryptographic Provider"
RequestType = PKCS10
[EnhancedKeyUsageExtension]
OID = 1.3.6.1.5.5.7.3.1
[Extensions]
2.5.29.17 = "{text}"
_continue_ = "dns=2k22recimgProd.psag.local&"
_continue_ = "dns=2k22recimgProd&"
_continue_ = "ipaddress=172.16.18.52&"
[RequestAttributes]
CertificateTemplate = WebServer
```

```powershell
cd C:\PSAG_Mantenimiento\https
certreq -new req.inf req.csr
certreq -submit -config "2K22DCLB.psag.local\psag-2K22DCLB-CA" req.csr cert.cer   # lo hace IT si hace falta aprobar
certreq -accept cert.cer                                                         # queda en LocalMachine\My
```

Para el **189** igual, con `CN=2K22RECIMG.psag.local`, `dns=2K22RECIMG.psag.local`, `dns=2K22RECIMG` e
`ipaddress=172.16.12.189`. Chrome exige que la IP con la que se entra esté en el SAN (`ipaddress=`).

**Sobre el certificado de Remitos** (`psag-logistica.crt`): sirve técnicamente para el 189 (su SAN tiene
172.16.12.189 y 2K22RECIMG) pero **no** para el .52, es autofirmado (ninguna PC ni tablet confía en él sin
instalarlo a mano) y es el que Railway tiene fijado como CA de la interna: si se renueva por Remitos, se rompe
Converting. No conviene compartirlo.

Alternativa sin IT: una CA propia con openssl (CA raíz + certificado de servidor con el SAN de arriba),
importar el .pfx en LocalMachine\My y **instalar la CA en cada PC y tablet**. Más trabajo en cada equipo.

## 2. Configurar el servidor

```powershell
cd C:\PSAG_Mantenimiento\deploy          # o donde esté la carpeta deploy del paquete
.\07-Configurar-HTTPS.ps1 -Ip 172.16.18.52 -Subject 2k22recimgProd.psag.local -NombresExtra 2k22recimgProd,2k22recimgProd.psag.local
# revisar lo que muestra y recién ahí:
.\07-Configurar-HTTPS.ps1 -Ip 172.16.18.52 -Subject 2k22recimgProd.psag.local -NombresExtra 2k22recimgProd,2k22recimgProd.psag.local -Aplicar
```

En el 189 el nssm está en `C:\serverRemitos\nssm.exe`: agregar `-Nssm C:\serverRemitos\nssm.exe`.

El script (sin `-Aplicar` no cambia nada):
1. busca el certificado en LocalMachine\My (con clave privada, uso servidor y la IP en el SAN);
2. agrega el binding `https <IP>:50443` al sitio de IIS con ese certificado (el http :50000 queda);
3. da lectura de la clave privada a la cuenta del servicio de la API;
4. en `Api\appsettings.Production.json` agrega `Kestrel:Certificates:Default` (Subject, My, LocalMachine),
   `Https:RedirigirHttp=false` y el origen `https://<IP>:50443` en CORS (con copia `.pre_https_<fecha>`);
5. cambia `AppParameters` del servicio a `--urls=http://<IP>:50001;https://<IP>:50002`;
6. agrega `ApiBaseUrlHttps` en `Web\appsettings.json` (con copia) y borra los `.br/.gz` viejos;
7. abre 50443 y 50002 en el firewall (perfil dominio/privado);
8. reinicia la API y prueba `/health` por http y por https, y la Web por los dos.

Vuelta atrás: `Remove-WebBinding -Name PSAG_Mantenimiento -Protocol https -Port 50443`, restaurar las dos
copias `.pre_https_*`, `nssm set PSAG_Mantenimiento_API AppParameters --urls=http://<IP>:50001` y reiniciar.

## 3. Tablets y celulares (no están en el dominio)

- Instalar la CA raíz `psag-2K22DCLB-CA` (exportarla como .cer desde cualquier PC del dominio:
  `certlm.msc` → Entidades de certificación raíz de confianza). En Android:
  *Ajustes → Seguridad → Más ajustes → Cifrado y credenciales → Instalar un certificado → Certificado de CA*.
  Chrome en Android confía en las CA que instala el usuario.
- Wi-Fi: hoy desde el Wi-Fi de planta no se llega a la app (pedido pendiente a IT). Hay que habilitar
  50000/50001 y, para el dictado, 50443/50002.
- Abrir `https://<IP>:50443`. La primera vez que se toca **Dictar**, Chrome pide permiso de micrófono.

## 4. Privacidad del audio

El dictado usa la Web Speech API del navegador. **En Chrome (PC y Android) el audio se manda al servicio de
reconocimiento de Google** para pasarlo a texto; en Edge, al de Microsoft. Es audio de planta (pueden
escucharse nombres, equipos, problemas). Chrome reciente puede reconocer en el propio equipo
(`processLocally`): la app lo usa si está disponible. Si se quiere **prohibir** que el audio salga del equipo,
poner en `Web\appsettings.json`:

```json
"Dictado": { "SoloEnDispositivo": true }
```

Con eso, donde el navegador no tenga reconocimiento local el botón avisa que no está disponible.
