-- ==============================================================================
-- T-798_verificacion.sql · Consultas de verificación después del deploy de T-798 (scripts 31 a 38).
-- SOLO LECTURA: sólo SELECT (no cambia nada). Se puede correr las veces que haga falta.
--   sqlcmd -S <servidor> -d PSAG_Mantenimiento -I -f 65001 -W -s "|" -i T-798_verificacion.sql -o verificacion.txt
-- Cada bloque dice qué valor se espera. Si algo da distinto, NO publicar la API: avisar a Emiliano con la salida.
-- Los conteos "antes" son los del archivo conteos_antes.txt que se sacó antes de correr los scripts (paso 3 del
-- T-798_DEPLOY.md): tienen que coincidir con los de las tablas nuevas.
-- ==============================================================================

SET NOCOUNT ON;

PRINT '== 1. Tablas nuevas presentes (esperado: todas con Existe = 1)';
SELECT v.[Tabla], CASE WHEN OBJECT_ID(N'mnt.' + v.[Tabla], N'U') IS NULL THEN 0 ELSE 1 END AS [Existe]
FROM (VALUES (N'ABM_Lineas'), (N'ABM_Turnos'), (N'ABM_ProductosMaestro'), (N'ABM_ProductosSku'), (N'ABM_TiposFalla'),
             (N'ABM_OperadoresLinea'), (N'PRD_ProgramacionLinea'), (N'PRD_ProgramacionLineaTurnos'),
             (N'PRD_ExcepcionesHorasDia'), (N'PRD_ParametrosLineaProducto'), (N'PRD_TurnosProduccion'),
             (N'PRD_TurnosProduccionOperadores'), (N'PRD_TurnosProduccionProductos'), (N'PRD_ProduccionesTurno'),
             (N'CAM_Camaras'), (N'CAM_ConteosMinuto'), (N'CAM_Paradas'), (N'CAM_Alertas'), (N'DET_Detenciones'),
             (N'CPR_Registros'), (N'CAL_PuntosControl'), (N'CAL_Registros')) v([Tabla])
ORDER BY [Existe], v.[Tabla];

PRINT '== 2. Tablas viejas que ya no deben existir (esperado: ninguna fila)';
SELECT s.[name] + N'.' + t.[name] AS [TablaViejaQueSigue]
FROM sys.tables t INNER JOIN sys.schemas s ON s.[schema_id] = t.[schema_id]
WHERE s.[name] = N'mnt' AND t.[name] IN (N'Lineas', N'Equipos', N'Turnos', N'Productos', N'TiposFalla', N'Detenciones',
      N'ConteosCamaraMinuto', N'ParadasCamara', N'AlertasCamara', N'PuntosControlCalidad', N'RegistrosProceso',
      N'RegistrosCalidad', N'CalendariosLinea', N'LineasProductos', N'UsuariosLineas');

PRINT '== 3. Conteos por tabla (comparar con conteos_antes.txt: tabla vieja = tabla nueva)';
SELECT N'DET_Detenciones (ex Detenciones)' AS [Tabla], COUNT_BIG(*) AS [Filas] FROM [mnt].[DET_Detenciones]
UNION ALL SELECT N'DET_HistorialEstados (ex DetencionesEstadoHistorial)', COUNT_BIG(*) FROM [mnt].[DET_HistorialEstados]
UNION ALL SELECT N'DET_Notas (ex DetencionesNotas)', COUNT_BIG(*) FROM [mnt].[DET_Notas]
UNION ALL SELECT N'CAM_ConteosMinuto (ex ConteosCamaraMinuto)', COUNT_BIG(*) FROM [mnt].[CAM_ConteosMinuto]
UNION ALL SELECT N'CAM_Paradas (ex ParadasCamara)', COUNT_BIG(*) FROM [mnt].[CAM_Paradas]
UNION ALL SELECT N'CAM_Alertas (ex AlertasCamara)', COUNT_BIG(*) FROM [mnt].[CAM_Alertas]
UNION ALL SELECT N'CPR_Registros (ex RegistrosProceso)', COUNT_BIG(*) FROM [mnt].[CPR_Registros]
UNION ALL SELECT N'CAL_Registros (ex RegistrosCalidad)', COUNT_BIG(*) FROM [mnt].[CAL_Registros]
UNION ALL SELECT N'CAL_PuntosControl (ex PuntosControlCalidad)', COUNT_BIG(*) FROM [mnt].[CAL_PuntosControl]
UNION ALL SELECT N'ABM_ProductosMaestro (ex Productos)', COUNT_BIG(*) FROM [mnt].[ABM_ProductosMaestro]
UNION ALL SELECT N'ABM_Lineas (ex Lineas)', COUNT_BIG(*) FROM [mnt].[ABM_Lineas]
UNION ALL SELECT N'ABM_OperadoresLinea (ex UsuariosLineas)', COUNT_BIG(*) FROM [mnt].[ABM_OperadoresLinea]
UNION ALL SELECT N'DET_DetencionesRespaldoT798 (= detenciones antes)', COUNT_BIG(*) FROM [mnt].[DET_DetencionesRespaldoT798]
UNION ALL SELECT N'CAM_Camaras (nueva: = puntos que eran cámara)', COUNT_BIG(*) FROM [mnt].[CAM_Camaras]
UNION ALL SELECT N'PRD_TurnosProduccion (nueva)', COUNT_BIG(*) FROM [mnt].[PRD_TurnosProduccion];

PRINT '== 4. Huérfanos: registros sin turno de producción (esperado: 0 en todas)';
SELECT N'Detenciones sin turno' AS [Control], COUNT_BIG(*) AS [Cantidad] FROM [mnt].[DET_Detenciones] WHERE [TurnoProduccionId] IS NULL
UNION ALL SELECT N'Conteos de cámara sin turno', COUNT_BIG(*) FROM [mnt].[CAM_ConteosMinuto] WHERE [TurnoProduccionId] IS NULL
UNION ALL SELECT N'Paradas de cámara sin turno', COUNT_BIG(*) FROM [mnt].[CAM_Paradas] WHERE [TurnoProduccionId] IS NULL
UNION ALL SELECT N'Registros de proceso sin turno', COUNT_BIG(*) FROM [mnt].[CPR_Registros] WHERE [TurnoProduccionId] IS NULL
UNION ALL SELECT N'Registros de calidad sin turno', COUNT_BIG(*) FROM [mnt].[CAL_Registros] WHERE [TurnoProduccionId] IS NULL
UNION ALL SELECT N'Conteos/paradas/alertas sin cámara', (SELECT COUNT_BIG(*) FROM [mnt].[CAM_ConteosMinuto] WHERE [CamaraId] IS NULL)
          + (SELECT COUNT_BIG(*) FROM [mnt].[CAM_Paradas] WHERE [CamaraId] IS NULL) + (SELECT COUNT_BIG(*) FROM [mnt].[CAM_Alertas] WHERE [CamaraId] IS NULL);

PRINT '== 5. Líneas con más de un turno ABIERTO (esperado: ninguna fila)';
SELECT [LineaId], COUNT(*) AS [Abiertos] FROM [mnt].[PRD_TurnosProduccion] WHERE [Estado] = N'ABIERTO'
GROUP BY [LineaId] HAVING COUNT(*) > 1;

PRINT '== 6. Turnos duplicados línea + turno + fecha (esperado: ninguna fila)';
SELECT [LineaId], [TurnoId], [FechaProduccion], COUNT(*) AS [Veces] FROM [mnt].[PRD_TurnosProduccion]
GROUP BY [LineaId], [TurnoId], [FechaProduccion] HAVING COUNT(*) > 1;

PRINT '== 7. Horarios de turno (esperado: T1 05:00-13:00, T2 13:00-21:00, T3 21:00-05:00, 480 min c/u)';
SELECT [Codigo], [Nombre], [HoraInicio], [HoraFin], [Activo],
       CASE WHEN DATEDIFF(MINUTE, [HoraInicio], [HoraFin]) <= 0 THEN DATEDIFF(MINUTE, [HoraInicio], [HoraFin]) + 1440
            ELSE DATEDIFF(MINUTE, [HoraInicio], [HoraFin]) END AS [Minutos]
FROM [mnt].[ABM_Turnos] ORDER BY [HoraInicio];

PRINT '== 8. Turnos convertidos (CERRADO por SISTEMA, "a completar"): ninguno debe seguir "a recalcular"';
SELECT CASE WHEN [ObservacionCierre] LIKE N'%a recalcular en la etapa 7%' THEN N'SIN RECALCULAR (mal)'
            WHEN [MinutosProgramados] = 0 THEN N'Recalculado: fuera de programación (0 min)'
            ELSE N'Recalculado' END AS [Situacion],
       COUNT(*) AS [Turnos], MIN([FechaProduccion]) AS [Desde], MAX([FechaProduccion]) AS [Hasta]
FROM [mnt].[PRD_TurnosProduccion]
WHERE [OrigenApertura] = N'SISTEMA' AND [ObservacionCierre] LIKE N'Convertido (T-798 etapa %'
GROUP BY CASE WHEN [ObservacionCierre] LIKE N'%a recalcular en la etapa 7%' THEN N'SIN RECALCULAR (mal)'
              WHEN [MinutosProgramados] = 0 THEN N'Recalculado: fuera de programación (0 min)' ELSE N'Recalculado' END;

PRINT '== 9. Turnos convertidos con tramo de producto o producción oficial (esperado: 0; 2.11 no inventa producto)';
SELECT COUNT(*) AS [ConvertidosConProducto]
FROM [mnt].[PRD_TurnosProduccion] tp
WHERE tp.[ObservacionCierre] LIKE N'Convertido (T-798 etapa %'
  AND EXISTS (SELECT 1 FROM [mnt].[PRD_TurnosProduccionProductos] p WHERE p.[TurnoProduccionId] = tp.[TurnoProduccionId]);

PRINT N'== 10. Excepción 05:00-06:00 (T3 viejo → T1): registros movidos por el 38 (los del 35/36 se ven en su log)';
SELECT [Tabla], COUNT(*) AS [MovidosAlT1] FROM [mnt].[PRD_ConversionRegistrosT798] WHERE [Excepcion0506] = 1 GROUP BY [Tabla];

PRINT '== 11. Detenciones colgadas de un turno cuya ventana no contiene la hora de parada (esperado: 0 o muy pocas: si hay, son detenciones a las que el operario les eligió otro turno; listarlas y revisar con Emiliano)';
SELECT COUNT(*) AS [FueraDeVentana]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[PRD_TurnosProduccion] tp ON tp.[TurnoProduccionId] = d.[TurnoProduccionId]
WHERE d.[FechaHoraParada] IS NOT NULL
  AND (d.[FechaHoraParada] < tp.[InicioProgramado] OR d.[FechaHoraParada] >= tp.[FinProgramado]);

PRINT '== 12. Cámaras: habilitada / oficial (revisar con Francisco: sólo las que tienen que correr; una oficial por línea y magnitud)';
SELECT c.[Codigo], l.[Nombre] AS [Linea], c.[NumeroCanalGrabador] AS [Canal], c.[Funcion], c.[Magnitud], c.[MetodoDeteccion],
       c.[DetectaParadas], c.[CreaDetenciones], c.[UmbralDetencionSegundos], c.[CuentaProduccionOficial], c.[Habilitada], c.[Activo],
       c.[UltimoDatoRecibido]
FROM [mnt].[CAM_Camaras] c INNER JOIN [mnt].[ABM_Lineas] l ON l.[LineaId] = c.[LineaId]
ORDER BY l.[Nombre], c.[Codigo];

PRINT '== 13. Más de una cámara oficial por línea y magnitud (esperado: ninguna fila)';
SELECT [LineaId], [Magnitud], COUNT(*) AS [Oficiales] FROM [mnt].[CAM_Camaras]
WHERE [CuentaProduccionOficial] = 1 GROUP BY [LineaId], [Magnitud] HAVING COUNT(*) > 1;

PRINT '== 14. Líneas activas sin programación vigente (todas sus aperturas faltantes darán SIN_APERTURA)';
SELECT l.[LineaId], l.[Nombre]
FROM [mnt].[ABM_Lineas] l
WHERE l.[Activo] = 1
  AND NOT EXISTS (SELECT 1 FROM [mnt].[PRD_ProgramacionLinea] p
                  WHERE p.[LineaId] = l.[LineaId] AND p.[Activo] = 1 AND p.[VigenteDesde] <= CAST(SYSDATETIMEOFFSET() AS DATE));

PRINT '== 15. Productos y parámetros de Tango 2 (esperado: SH 30 11.200 / 6.500 y SH 50 6.720 / 4.700 desde 2026-11-01)';
SELECT l.[Nombre] AS [Linea], p.[Codigo], p.[Nombre] AS [Producto], p.[MetrosPorManga], p.[RollosPorManga], p.[RollosPorPaquete],
       x.[VigenteDesde], x.[Habilitado], x.[MangasObjetivoTurno], x.[BolsonesObjetivoTurno], x.[MinutosTurnoReferencia]
FROM [mnt].[PRD_ParametrosLineaProducto] x
INNER JOIN [mnt].[ABM_Lineas] l ON l.[LineaId] = x.[LineaId]
INNER JOIN [mnt].[ABM_ProductosMaestro] p ON p.[ProductoMaestroId] = x.[ProductoMaestroId]
ORDER BY l.[Nombre], p.[Nombre], x.[VigenteDesde];

PRINT '== 16. Tipos de falla que NO cuentan para indicadores (los define el cliente; puede estar vacío)';
SELECT [Codigo], [Nombre] FROM [mnt].[ABM_TiposFalla] WHERE [CuentaParaIndicadores] = 0 ORDER BY [Nombre];

PRINT '== 17. Detenciones: minutos por fecha de producción, últimos 14 días (comparar a ojo con el reporte anterior)';
SELECT tp.[FechaProduccion], t.[Codigo] AS [Turno], COUNT(*) AS [Detenciones], SUM(d.[MinutosEfectivos]) AS [MinutosEfectivos]
FROM [mnt].[DET_Detenciones] d
INNER JOIN [mnt].[PRD_TurnosProduccion] tp ON tp.[TurnoProduccionId] = d.[TurnoProduccionId]
INNER JOIN [mnt].[ABM_Turnos] t ON t.[TurnoId] = tp.[TurnoId]
WHERE d.[Eliminada] = 0 AND tp.[FechaProduccion] >= DATEADD(DAY, -14, CAST(SYSDATETIMEOFFSET() AS DATE))
GROUP BY tp.[FechaProduccion], t.[Codigo]
ORDER BY tp.[FechaProduccion], t.[Codigo];

PRINT '== 18. Columnas viejas que no deben estar (esperado: ninguna fila)';
SELECT OBJECT_NAME(c.[object_id]) AS [Tabla], c.[name] AS [ColumnaVieja]
FROM sys.columns c
WHERE (c.[object_id] = OBJECT_ID(N'mnt.DET_Detenciones') AND c.[name] IN (N'TurnoId', N'Fecha', N'UsuarioLineaId', N'PuntoControlCalidadId'))
   OR (c.[object_id] = OBJECT_ID(N'mnt.CAM_ConteosMinuto') AND c.[name] IN (N'Rollos', N'LineaId', N'PuntoControlCalidadId', N'TurnoId'))
   OR (c.[object_id] = OBJECT_ID(N'mnt.CAM_Paradas') AND c.[name] IN (N'LineaId', N'PuntoControlCalidadId', N'TurnoId'))
   OR (c.[object_id] = OBJECT_ID(N'mnt.CAL_PuntosControl') AND c.[name] IN (N'NumeroCamara', N'MetodoConteo', N'ObjetivoPorHora'));

PRINT '== Fin de la verificación.';
