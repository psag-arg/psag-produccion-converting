# Control de Calidad por cámaras (T-611)

Registro de cada detección de calidad con su imagen y la revisión humana. Primer caso: línea
**Tango 2**, cámara 1 a la salida de la cortadora (rabo del rollo mayor a 1 cm y manchas de
grasa / marcas).

**Ante un defecto el sistema SOLO registra.** No rechaza producto, no frena la línea y no manda
alertas. La detección automática todavía no existe (se evalúa el modelo en otra tarea): mientras
tanto se puede cargar a mano y el endpoint de ingesta queda listo para cuando el detector esté.

## Modelo

| Tabla (`mnt`) | Qué guarda |
|---|---|
| `PuntosControlCalidad` | Cámaras / puntos de control por línea. `Codigo` único (lo manda el detector). |
| `TiposDefectoCalidad` | Defectos: código, nombre, descripción (criterio), unidad opcional, activo. |
| `RegistrosCalidad` | Cada detección: fecha y hora, línea, punto, turno, defecto, origen, confianza, dato medido + unidad, producto/SKU opcional, detención opcional, imagen, estado de revisión, quién y cuándo revisó. |
| `RegistrosCalidadRevisiones` | Historial de revisión (solo inserción): estado anterior → nuevo, comentario, usuario, fecha. |

Script: `database/23_t611_calidad_defectos.sql`. Nada se borra (no hay DELETE); los maestros se desactivan.

### Reglas

- **Origen**: `AUTOMATICO` (detector, nace `PENDIENTE`) o `MANUAL` (lo carga una persona y nace
  `CONFIRMADO` a su nombre, con una fila en el historial "Carga manual").
- **Revisión**: `PENDIENTE` → `CONFIRMADO` o `FALSO_POSITIVO`. Se puede corregir de confirmado a falso
  positivo (y al revés); cada cambio deja una fila en el historial. Nunca vuelve a `PENDIENTE`.
- **Turno**: si no viene, se calcula con la hora de la detección **en la planta**, igual que la
  detención con hora de inicio, y se guarda (no se recalcula al consultar). Si a esa hora no hay
  turno activo, el registro se guarda sin turno: una detección no se pierde por eso.
- **Línea**: es la del punto de control al momento de registrar (se guarda en el registro).
- **Unidad**: la que manda el detector o, si no manda, la del tipo de defecto. Queda como snapshot.
- **Detención**: opcional, por código `DET-...`, tiene que ser de la misma línea. Se puede
  relacionar o quitar después desde el detalle.
- **Imagen**: mismo mecanismo seguro que las fotos del análisis: JPG o PNG reales (se mira el
  contenido, no la extensión), hasta 10 MB, nombre generado (GUID), guardada en disco fuera de
  wwwroot (`Calidad:CarpetaImagenes`) y servida solo con sesión por
  `GET api/calidad/registros/{id}/imagen` con `nosniff`.

### Permisos

| Acción | Roles |
|---|---|
| Ver grilla, detalle e imagen | Todos los autenticados (operario y gerencia solo ven) |
| Confirmar / falso positivo, relacionar detención | SUPERVISOR, MANTENIMIENTO, ADMINISTRADOR |
| Carga manual | SUPERVISOR, ADMINISTRADOR |
| Maestros (puntos de control y defectos) | ADMINISTRADOR |
| Ingesta del detector | Clave de servicio (sin usuario) |

Se validan en el servicio (`CalidadReglas`), no solo en la pantalla.

## Cómo cargar los maestros cuando llegue la ficha de criterios

El maestro de defectos **arranca vacío a propósito**: no se cargan defectos inventados.

1. Entrar como administrador a **Maestros → Calidad (cámaras y defectos)**.
2. **Puntos de control**: uno por cámara. Línea, código (ej.: `T2-CAM1`, sin espacios; se guarda en
   mayúsculas), nombre visible (ej.: "Cámara 1 - salida de cortadora") y descripción.
3. **Tipos de defecto**: uno por criterio de la ficha. Código (el que va a mandar el detector),
   nombre, descripción con el criterio (ej.: el umbral) y unidad si se mide (`mm`, `paquetes`...).
4. Pasarle a quien configure el detector la lista de códigos de puntos y defectos.

Alternativa por script: el final de `23_t611_calidad_defectos.sql` trae un bloque comentado con el
formato para cargar puntos y defectos en lote (usuario de auditoría `SCRIPT-23`).

## Contrato de ingesta del detector

```
POST {API}/api/calidad/ingesta
Content-Type: multipart/form-data
X-Clave-Ingesta: <clave de servicio>
```

### Autenticación

- Header `X-Clave-Ingesta` con la clave configurada en `Calidad:ClaveIngesta`. Se compara en tiempo
  constante (SHA-256 de ambas + `FixedTimeEquals`). No usa el JWT de los usuarios.
- Sin clave configurada (vacía o con menos de 16 caracteres) el endpoint responde **503** y la
  ingesta queda deshabilitada. La clave se valida **antes** de leer el cuerpo.

### Campos del formulario

| Campo | Obligatorio | Formato | Notas |
|---|---|---|---|
| `imagen` | Sí | archivo JPG o PNG, hasta 10 MB | Se valida por contenido. |
| `puntoControl` | Sí | texto | Código del punto de control (ej.: `T2-CAM1`). Mayúsculas/minúsculas da igual. |
| `tipoDefecto` | Sí | texto | Código del tipo de defecto. |
| `fechaHora` | No | ISO 8601 | Con offset (`2026-10-29T14:35:12-03:00` o `...Z`) se respeta; **sin offset es hora de la planta**. Vacío = hora del servidor al recibir. No puede ser más de 10 minutos posterior a ahora. |
| `confianza` | No | decimal con punto, 0 a 1 | Ej.: `0.87`. |
| `valorMedido` | No | decimal con punto, ≥ 0 | Ej.: `14.5` (mm del rabo), `3` (paquetes). |
| `unidadMedida` | No | texto (20) | Si no viene, se usa la del tipo de defecto. |
| `producto` | No | texto | Código del producto/SKU; tiene que existir en Productos. |
| `turnoId` | No | entero | Si no viene, se calcula por la hora (recomendado: no mandarlo). |
| `codigoDetencion` | No | `DET-AAAAMMDD-NNNNNN` | Detención de la misma línea. |
| `identificadorExterno` | Recomendado | texto (100) | Id del evento en el detector. Si se reintenta con el mismo id **no se duplica**. |
| `observaciones` | No | texto (1000) | |

### Respuestas

| Código | Cuándo | Cuerpo |
|---|---|---|
| **201** | Registro creado (origen `AUTOMATICO`, estado `PENDIENTE`) | `{ "registroCalidadId": 123, "duplicado": false, "estadoRevision": "PENDIENTE", "lineaNombre": "Tango 2", "turnoNombre": "Turno 1 (Mañana)", "fechaHoraDeteccion": "2026-10-29T14:35:12-03:00" }` |
| **200** | Ya existía un registro con ese `identificadorExterno` (reintento) | Igual, con `"duplicado": true` y el id existente |
| **400** | Datos inválidos: falta un campo, código desconocido o inactivo, imagen que no es JPG/PNG, número mal escrito, fecha futura | ProblemDetails (`detail` en castellano) |
| **401** | Falta el header o la clave no coincide | ProblemDetails |
| **413** | Pedido de más de 11 MB | — |
| **503** | El servidor no tiene `Calidad:ClaveIngesta` configurada | ProblemDetails |

Reintentos: ante 5xx o error de red, reintentar con el **mismo** `identificadorExterno`. Ante 400 no
reintentar (corregir los datos). Ante 401/503 avisar a sistemas.

### Ejemplo

```bash
curl -X POST "http://<servidor>/api/calidad/ingesta" \
  -H "X-Clave-Ingesta: $CLAVE" \
  -F "imagen=@frame_000123.jpg" \
  -F "puntoControl=T2-CAM1" \
  -F "tipoDefecto=<CODIGO>" \
  -F "fechaHora=2026-10-29T14:35:12-03:00" \
  -F "confianza=0.87" \
  -F "valorMedido=14.5" \
  -F "identificadorExterno=cam1-000123"
```

## Configuración (appsettings de la API)

```json
"Calidad": {
  "ClaveIngesta": "",
  "CarpetaImagenes": "C:\\PSAG\\Mantenimiento\\ImagenesCalidad"
}
```

- `CarpetaImagenes`: ruta absoluta fuera de la carpeta `Api` (que se borra en cada publicación).
  Permiso de modificar para el usuario del servicio de la API. Incluirla en el backup junto con la
  base (la tabla solo guarda el nombre del archivo). Sin configurar: `App_Data/ImagenesCalidad`.
- `ClaveIngesta`: **no va en archivos versionados**. En producción conviene la variable de entorno
  del servicio (sobrevive a las publicaciones, porque `02-Publicar-Aplicacion.ps1` regenera
  `appsettings.Production.json`):

  ```powershell
  # Generar una clave de 32 bytes
  $clave = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
  # Cargarla en el servicio NSSM (mantener ASPNETCORE_ENVIRONMENT) y reiniciar
  C:\PSAG\Mantenimiento\tools\nssm.exe set PSAG_Mantenimiento_API AppEnvironmentExtra ASPNETCORE_ENVIRONMENT=Production "Calidad__ClaveIngesta=$clave"
  Restart-Service PSAG_Mantenimiento_API
  ```

  La misma clave se configura en el detector. Para rotarla: cambiarla en los dos lados.
