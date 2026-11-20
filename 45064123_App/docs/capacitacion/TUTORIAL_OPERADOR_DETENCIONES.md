# Tutorial del operador: registro de detenciones (borrador T-787)

> Es un borrador para que Emiliano lo valide con Ferretti. Está pensado para una capacitación presencial de **30 minutos**:
> la semana del 24/11 el tutorial, y la semana del 1/12 el kick-off de Tango 2 con los 3 turnos.
>
> **Ojo, depende de lo que esté instalado.** Los pasos marcados con ⚙️ necesitan versiones que todavía no están en
> producción:
> - ⚙️ T-783: confirmarse como maquinista al entrar;
> - ⚙️ T-784: solo se piden declarar las paradas largas;
> - ⚙️ T-786: el supervisor declara a mano los cortes de energía.
>
> Si la capacitación es antes de instalarlas, se saltean esas partes o se muestran en DEV.

---

## Parte A — Guion para quien da la capacitación (30 min)

| Min | Tema | Qué se muestra | Qué hace el operador |
|---|---|---|---|
| 0–3 | Para qué sirve | La cámara ve cuándo para la máquina y arma la detención sola. **El operador cuenta POR QUÉ paró.** Sin eso, no se puede mejorar. | Escucha |
| 3–8 | Entrar y confirmarse maquinista ⚙️ | Login → "¿Vas a ser el maquinista del Turno X en la Tango 2?" → *Sí, soy el maquinista*. Qué pasa si aparece otro nombre o el aviso "no tenés asignación". | Cada uno entra con su usuario y confirma |
| 8–13 | Qué ve en la pantalla | Menú: **Mis pendientes** con el número rojo. La solapa **Pendiente de declaración**. El Dashboard con la eficiencia del turno. El aviso amarillo de fin de turno. | Abre Mis pendientes |
| 13–22 | Declarar una detención | Abrir una pendiente → equipo, **tipo de falla** (operativo / equipo / suministro), detalle, hora de arranque ("Usar la de la cámara") y descripción → Guardar. | Declara 2 detenciones de práctica (en DEV) |
| 22–27 | Corte de energía | Qué hacer cuando se corta la luz: anotar las horas, avisar al supervisor y, cuando vuelve, revisar las pendientes. | Recorre el caso con la tarjeta de bolsillo |
| 27–30 | Preguntas y repaso | Las 3 preguntas para elegir el tipo de falla. Dónde pedir ayuda. | Pregunta |

**Antes de la capacitación**:
1. Tener la Web de DEV, o una PC con producción, con 2 o 3 detenciones pendientes de práctica.
2. Tener un usuario de práctica por operador, asignado a la línea y al turno.
3. Imprimir la **tarjeta de bolsillo** (Parte C), una por operador.
4. Confirmar con Ferretti la lista real de tipos de falla (ver "Queda por definir").

---

## Parte B — Tutorial para el operador

### 1. Entrar y confirmarte como maquinista del turno ⚙️

1. Abrí el sistema en la PC de la línea o en el celular y entrá con **tu usuario y tu clave**. No uses el de un compañero.
2. Aparece la pregunta: **"¿Vas a ser el maquinista del Turno … en la …?"**
   - Si sos vos: tocá **Sí, soy el maquinista**. Desde ahí, las detenciones del turno quedan a tu nombre.
   - Si hoy no sos el maquinista: tocá **No, seguir sin confirmar**.
3. Si dice que **ya figura otro maquinista**:
   - si hoy lo reemplazás vos, confirmá igual (queda registrado el cambio);
   - si no, no confirmes y avisale al supervisor.
4. Si aparece el aviso **"No tenés asignación como maquinista para este turno"**: no es un error tuyo. Avisale al
   supervisor: él te carga en *Maquinistas del turno*.

> [Captura: pantalla "Maquinista del turno" con el botón *Sí, soy el maquinista*]

### 2. Qué ves en la pantalla

- **Menú → Detenciones → Mis pendientes**: el número rojo dice cuántas detenciones tenés para completar.
- **Solapa "Pendiente de declaración"** (la primera): son las paradas que **detectó la cámara** y esperan que digas por qué
  pararon. Cada fila muestra la línea, el turno, desde qué hora paró y a qué hora vio la cámara que arrancó.
- **Aviso rojo de cámara** ("Cámara congelada" o "sin video"): mientras esté, la cámara **no** detecta las paradas. Si la
  máquina para, cargala a mano (*Detenciones → Ver detenciones → botón **Nueva detención***) o avisale al supervisor.
- **Aviso amarillo "Estás por terminar el turno"**: aparece 30 minutos antes del cambio de turno si te quedan pendientes.
  Tocá **Declarar ahora**.
- **Dashboard → Eficiencia (cámara)**: lo que contó la cámara en el turno contra el objetivo, los minutos de parada y
  los microcortes.

> **Paradas cortas ⚙️**: la cámara registra **todas** las paradas, pero solo te pide declarar las que pasan el tiempo
> configurado para la máquina (hoy 5 minutos; en prueba, 30 segundos). Las más cortas quedan registradas solas como
> *microparadas*.

> [Captura: Mis pendientes con la solapa "Pendiente de declaración" y el número rojo]

### 3. Declarar una detención pendiente

1. En **Pendiente de declaración**, tocá **Declarar** en la detención.
2. Arriba está **lo que completó el sistema**: línea, fecha, turno, hora de parada y maquinista. No hace falta tocarlo.
3. Completá **lo tuyo**:
   1. **Equipo**: la parte de la máquina donde estuvo el problema (empaquetadora, gofrador…). Si fue toda la máquina, dejá el que propone.
   2. **Tipo de falla**: usá las 3 preguntas de abajo.
   3. **Detalle de falla**: elegí el más parecido.
   4. **Hora de arranque**: tocá **"Usar la de la cámara"** si coincide con lo que pasó. Si no, escribí la hora real. Si no te acordás de la hora, poné los minutos que estuvo parada.
   5. **Descripción** (obligatoria): una frase con **qué pasó y qué hiciste**. Ej.: *"Se trabó el film en la empaquetadora, lo saqué y reinicié"*.
4. Tocá **Guardar**. La detención deja de estar pendiente y ya cuenta en los reportes.

**Las 3 preguntas para elegir el tipo de falla** (en este orden):

| Pregunta | Si la respuesta es SÍ → | Ejemplos |
|---|---|---|
| 1. ¿**Faltó algo que viene de afuera** de la máquina? | **Suministro** | Corte de energía · baja de tensión · falta de aire comprimido · no llegó la bobina o el film desde depósito · falta de cajas o insumos |
| 2. ¿**Se rompió o falló** una parte de la máquina? | **Equipo** | Se cortó una correa · falla del motor · sensor que no lee · la empaquetadora no sella · pérdida de aire en un cilindro |
| 3. ¿Fue por **la operación** (algo que se hace al trabajar)? | **Operativo** | Cambio de bobina o de formato · atasco por mala carga · ajuste o regulación · limpieza · espera del supervisor o de calidad · error de operación |

**Casos dudosos**:
- **Atasco**:
  - si se trabó porque una pieza está gastada o rota → **Equipo**;
  - si fue por cómo se cargó el material → **Operativo**.
- **Falta de bobina**:
  - si no había en depósito o no la trajeron → **Suministro**;
  - si estaba pero no se preparó a tiempo → **Operativo**.
- **Mantenimiento**:
  - preventivo programado → **Operativo** (o el tipo de parada programada que defina Ferretti);
  - reparación por una rotura → **Equipo**.
- **No sabés**: elegí lo más parecido y **contalo bien en la descripción**. El supervisor lo puede revisar.

> [Captura: pantalla "Declarar detención" con "Lo completó el sistema" y "Lo completás vos"]

### 4. Si se corta la energía

Cuando se corta la luz, la máquina, las cámaras y el sistema se apagan juntos. **La cámara no puede ver esa parada.**

1. **Anotá la hora** en que se cortó (reloj, celular o papel).
2. **Avisale al supervisor** enseguida.
3. Cuando vuelve la luz, **anotá la hora** en que la máquina volvió a andar.
4. Entrá al sistema y mirá **Mis pendientes**.
   - Puede aparecer una detención que creó la cámara al volver.
   - **No la declares sin hablar con el supervisor**: puede ser parte del mismo corte.
5. El **supervisor** carga la detención del corte en *Detenciones → Declarar detención manual* ⚙️, con:
   - la máquina, desde y hasta (tus horas anotadas);
   - tipo de falla **Suministro** y motivo **"Corte de energía"**.
   El sistema le avisa si ya hay una detención de la cámara en ese horario.
6. Esa detención queda marcada como **Manual** y cuenta igual que las demás.

> Si el supervisor no está, cargá vos la detención en *Detenciones → Ver detenciones → botón **Nueva detención*** con las horas anotadas, tipo
> **Suministro** y en la descripción "Corte de energía". Avisale después.

---

## Parte C — Tarjeta de bolsillo (para imprimir)

```
REGISTRO DE DETENCIONES – OPERADOR

AL ENTRAR
  Tu usuario → "¿Sos el maquinista del turno?" → Sí, soy el maquinista

DURANTE EL TURNO
  Mis pendientes → Pendiente de declaración → Declarar
  Equipo · Tipo de falla · Detalle · Hora de arranque · Descripción → Guardar

TIPO DE FALLA (preguntar en este orden)
  1. ¿Faltó algo de afuera? (luz, aire, bobina, insumos) → SUMINISTRO
  2. ¿Se rompió o falló la máquina?                      → EQUIPO
  3. ¿Fue por la operación? (cambio, ajuste, atasco)     → OPERATIVO

CORTE DE ENERGÍA
  Anotar hora de corte → Avisar al supervisor → Anotar hora de vuelta
  → El supervisor lo declara a mano (Suministro / Corte de energía)

ANTES DE IRTE
  Aviso amarillo = te quedan pendientes → Declarar ahora
```

---

## Queda por definir (para validar con Ferretti)

1. **Lista real de tipos de falla.** El criterio es operativo / equipo / suministro, pero el catálogo cargado en
   Maestros puede tener otros nombres (el del repo trae ejemplos: Mecánica, Eléctrica, Falta de material…). Antes del
   1/12 hay que dejar en Maestros → Tipos de falla los 3, o mapear los existentes a estos 3, con sus detalles por línea.
2. **Paradas programadas** (cambio de formato, mantenimiento preventivo, limpieza). ¿Van como *Operativo* o como un
   cuarto tipo "Programada", que no cuente contra la eficiencia?
3. **Corte de energía con una pendiente de la cámara superpuesta.** ¿Quién la cierra y cómo (declararla citando la
   manual, o que el administrador la elimine)? Hoy el sistema avisa, pero no las une.
4. **Umbral para declarar**: confirmar el tiempo con el que la Tango 2 arranca (5 min por defecto, 30 s en prueba).
5. **Usuarios y asignaciones**: antes del kick-off, cada operador tiene que tener usuario y estar cargado en
   Maestros → Maquinistas con su línea y su turno. Si no, va a ver el aviso "No tenés asignación".
6. **Capturas**: completar las marcadas con *[Captura: …]* en DEV (`http://localhost:6401`, usuario `oper.noche` / `Prueba123!`).
