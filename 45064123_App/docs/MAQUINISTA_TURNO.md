# Maquinista del turno (T-783)

Pedido de Ferretti (16/11, punto 3): que el maquinista del turno sea editable, o que lo sea el usuario logueado.
Definición de Emiliano (20/11): cuando entra un usuario **OPERARIO**, se le pregunta si va a ser el maquinista de ese
turno en esa máquina (línea). Antes se valida que tenga la relación turno – maquinista – línea. Si confirma, queda como
maquinista del turno de esa línea, y las detenciones del turno lo toman.

## Modelo

- **Relación** (ya existía): `mnt.UsuariosLineas`, en Maestros → Maquinistas. Es usuario + línea + turno (o sin turno fijo).
  La carga el administrador. Un usuario tiene como mucho una asignación por línea.
- **Maquinista de un turno concreto** (nuevo): `mnt.MaquinistasTurno` (script 31). Sus campos:
  - línea, turno y **día en que empezó el turno**: un nocturno de 22:00 a 06:00 a las 02:00 es del día anterior;
  - la asignación elegida;
  - el origen (`LOGIN` o `SUPERVISOR`);
  - quién lo registró, cuándo y el motivo.
  
  Es **INSERT-ONLY**: cada confirmación o corrección es un registro nuevo. El **vigente es el más reciente** y los anteriores quedan como historial.

## Flujo

1. **Login del operario** (`Login.razor` → `/maquinista-turno/confirmar`). Para los OPERARIO, después de entrar se llama a `GET api/maquinistas-turno/propuesta`.
   - Se buscan sus asignaciones que **corresponden** al turno en curso: las de ese turno o las sin turno fijo (`MaquinistaTurnoReglas.Corresponde`).
   - Por cada una se pregunta: "¿Vas a ser el maquinista del Turno X en la Línea Y?". Si ya es el maquinista vigente, no se le pregunta.
   - Con "Sí, soy el maquinista" se llama a `POST api/maquinistas-turno/confirmar` y queda un registro `LOGIN`. Con "No, seguir sin confirmar", sigue sin registrar nada.
   - Si falla la consulta, el login sigue igual: nunca se traba la entrada.
2. **Detenciones**:
   - Las **automáticas de la cámara** toman al vigente del turno de la parada (`DetencionCamaraReglas.ElegirMaquinista`, primera regla).
     Si no hay vigente, se sigue la regla de siempre: la asignación de la línea para el turno, después la única sin turno fijo, y si no, vacío.
   - Las **manuales sin maquinista elegido** toman al vigente del turno de la línea, y si no hay, la asignación propia, como antes.
     Si el admin o el supervisor eligen un maquinista a mano, manda su elección.
   - Al confirmar o corregir, las detenciones **pendientes de declaración** de esa línea que pararon en ese turno pasan a tomar al nuevo maquinista.
     Cada una suma una entrada en su historial (`OrigenAccion = MAQUINISTA_TURNO`).
     Las **ya declaradas no se tocan**: son dato completo, y cambiarlas sería una corrección administrativa.

## Casos que pidió resolver la tarea

| Caso | Qué pasa |
|---|---|
| **No tiene relación cargada** para el turno en curso (ninguna asignación de ese turno ni sin turno fijo) | **No se lo asigna.** Ve el aviso: "No tenés asignación como maquinista para este turno… Pedile a tu supervisor que te cargue en *Maquinistas del turno* (o al administrador que te asigne en Maestros → Maquinistas)". Si igual llama a confirmar, la API lo rechaza (400). **Quién lo corrige**: el **supervisor**, para ese turno, desde *Maquinistas del turno*, con una asignación de la línea. Si el usuario no tiene ninguna asignación en la línea, el **administrador** lo asigna en Maestros → Maquinistas. |
| **Ya hay otro maquinista** confirmado (o cargado por el supervisor) para ese turno | Se le muestra: "Ya figura *X* como maquinista de este turno (confirmó a las HH:mm). Si confirmás, quedás vos y el cambio queda registrado". Si confirma, **lo reemplaza**: hay un registro nuevo, el anterior queda en el historial y las pendientes del turno pasan a él. Si fue un error, lo corrige el supervisor. |
| **Corregir después** | Pantalla **Detenciones → Maquinistas del turno** (`/detenciones/maquinistas-turno`), para ADMINISTRADOR, MANTENIMIENTO y SUPERVISOR. El supervisor ve solo sus líneas, o todas si no tiene líneas asignadas. Elige el día; ve una fila por línea y turno con el vigente, cómo quedó y el historial; con "Cambiar" elige una asignación de la línea, de cualquier turno, y **escribe el motivo** (obligatorio). No se puede cargar un día que no empezó. |

## API

| Método | Ruta | Quién |
|---|---|---|
| GET | `api/maquinistas-turno/propuesta` | cualquiera con sesión (solo pregunta a OPERARIO) |
| POST | `api/maquinistas-turno/confirmar` `{ lineaId }` | OPERARIO, SUPERVISOR, MANTENIMIENTO, ADMINISTRADOR (con relación) |
| GET | `api/maquinistas-turno?fecha=AAAA-MM-DD` | SUPERVISOR, MANTENIMIENTO, ADMINISTRADOR |
| POST | `api/maquinistas-turno/corregir` `{ lineaId, turnoId, fechaTurno, usuarioLineaId, observacion }` | SUPERVISOR (sus líneas), MANTENIMIENTO, ADMINISTRADOR |

Todas las validaciones (relación, permisos por línea, motivo) están en el backend: `MaquinistaTurnoService` y `MaquinistaTurnoReglas`.

## Queda por definir

- Si al **operario sin relación** también se le debe ofrecer elegir una línea, y que quede "a revisar" por el supervisor en vez de solo avisarle. Hoy solo se le avisa.
- Si el **reemplazo por otro operario** en el login debe pedir el OK del supervisor. Hoy se permite y queda registrado.
- Si la corrección del supervisor debe cambiar también las detenciones **ya declaradas** del turno. Hoy no las cambia.
- Si "Maquinistas del turno" debe avisar algo, por ejemplo con un banner, cuando un turno en curso no tiene maquinista confirmado.
