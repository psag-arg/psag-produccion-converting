---
description: Revisa y actualiza el contexto del proyecto tras un hito
---

Revisá el estado del contexto de este proyecto y proponeme (sin aplicar
todavía) los cambios que haga falta:

1. Mirá el diff desde el último checkpoint. Qué se construyó, qué se
   decidió, qué patrones nuevos aparecieron.

2. CLAUDE.md: qué agregar (trampas, convenciones nuevas), qué borrar
   (quedó viejo o ya es deducible del código).

3. .claude/rules/: qué instrucción creció tanto o quedó tan atada a un
   módulo que conviene sacarla de CLAUDE.md a una regla con paths:
   apuntando a esos archivos.

4. .claude/agents/: qué tarea repetitiva, verbosa o riesgosa apareció que
   justifique un agente nuevo. Y al revés: qué agente existente no usé
   nunca, o tiene una description que no dispara.
   Regla: máximo 5 agentes. Si proponés uno nuevo con 5 activos, decime
   cuál retirar.

5. docs/DECISIONES.md: agregá las decisiones de arquitectura tomadas en
   este tramo, en una línea cada una, con la fecha y el motivo.

Presentá todo como una lista numerada de cambios propuestos. Espero mi OK
por cada bloque antes de tocar archivos.
