-- =============================================
-- Datos de demostración para Eje 3: Inspecciones
-- =============================================

USE PSAG_Mantenimiento;
GO

-- =============================================
-- Planes de Inspección para Línea 1 (Futura 2)
-- =============================================

-- Plan para Maquinista - Inspección diaria de inicio de turno
IF NOT EXISTS (SELECT 1 FROM [mnt].[PlanesInspeccion] WHERE Codigo = 'INS-L1-MAQ-001')
BEGIN
    INSERT INTO [mnt].[PlanesInspeccion] (LineaId, EquipoId, PuestoResponsable, Codigo, Nombre, Descripcion, FrecuenciaDias, Orden)
    VALUES (1, NULL, 1, 'INS-L1-MAQ-001', 'Inspección Inicio Turno - Maquinista',
            'Checklist de inspección al inicio de cada turno para el maquinista', 1, 1);

    DECLARE @PlanMaq1 INT = SCOPE_IDENTITY();

    INSERT INTO [mnt].[ItemsPlanInspeccion] (PlanInspeccionId, NumeroItem, Descripcion, CondicionEstandar, Metodo, Orden) VALUES
    (@PlanMaq1, 1, 'Verificar presión de aire comprimido', 'Entre 6 y 8 bar', 3, 1),
    (@PlanMaq1, 2, 'Verificar nivel de aceite hidráulico', 'Entre marcas MIN y MAX', 1, 2),
    (@PlanMaq1, 3, 'Inspeccionar bandas transportadoras', 'Sin cortes, desgaste excesivo ni desalineación', 1, 3),
    (@PlanMaq1, 4, 'Verificar funcionamiento de paradas de emergencia', 'Todas las paradas deben funcionar correctamente', 2, 4),
    (@PlanMaq1, 5, 'Verificar estado de guardas de seguridad', 'Todas las guardas en posición y sin daños', 1, 5),
    (@PlanMaq1, 6, 'Verificar limpieza general del área', 'Área libre de derrames y obstáculos', 1, 6);

    PRINT 'Plan INS-L1-MAQ-001 creado con 6 items.';
END
GO

-- Plan para Ayudante - Inspección de gofrador
IF NOT EXISTS (SELECT 1 FROM [mnt].[PlanesInspeccion] WHERE Codigo = 'INS-L1-AYU-001')
BEGIN
    -- Obtener el EquipoId del Gofrador de Línea 1
    DECLARE @EquipoGofrador INT = (SELECT EquipoId FROM [mnt].[Equipos] WHERE LineaId = 1 AND Codigo LIKE '%GOF%');

    INSERT INTO [mnt].[PlanesInspeccion] (LineaId, EquipoId, PuestoResponsable, Codigo, Nombre, Descripcion, FrecuenciaDias, Orden)
    VALUES (1, @EquipoGofrador, 2, 'INS-L1-AYU-001', 'Inspección Gofrador - Ayudante',
            'Inspección del gofrador por el ayudante', 1, 2);

    DECLARE @PlanAyu1 INT = SCOPE_IDENTITY();

    INSERT INTO [mnt].[ItemsPlanInspeccion] (PlanInspeccionId, NumeroItem, Descripcion, CondicionEstandar, Metodo, Orden) VALUES
    (@PlanAyu1, 1, 'Verificar temperatura de rodillos gofradores', 'Entre 140°C y 160°C', 3, 1),
    (@PlanAyu1, 2, 'Verificar presión de gofradores', 'Según especificación del producto', 3, 2),
    (@PlanAyu1, 3, 'Inspeccionar desgaste de rodillos', 'Sin desgaste visible ni marcas', 1, 3),
    (@PlanAyu1, 4, 'Verificar alineación del papel', 'Centrado y sin pliegues', 1, 4);

    PRINT 'Plan INS-L1-AYU-001 creado con 4 items.';
END
GO

-- Plan para Packaging - Inspección de empaquetadora
IF NOT EXISTS (SELECT 1 FROM [mnt].[PlanesInspeccion] WHERE Codigo = 'INS-L1-PKG-001')
BEGIN
    -- Obtener el EquipoId de la Empaquetadora de Línea 1
    DECLARE @EquipoEmpaq INT = (SELECT EquipoId FROM [mnt].[Equipos] WHERE LineaId = 1 AND Codigo LIKE '%EMP%');

    INSERT INTO [mnt].[PlanesInspeccion] (LineaId, EquipoId, PuestoResponsable, Codigo, Nombre, Descripcion, FrecuenciaDias, Orden)
    VALUES (1, @EquipoEmpaq, 3, 'INS-L1-PKG-001', 'Inspección Empaquetadora - Packaging',
            'Inspección de la empaquetadora por operador packaging', 1, 3);

    DECLARE @PlanPkg1 INT = SCOPE_IDENTITY();

    INSERT INTO [mnt].[ItemsPlanInspeccion] (PlanInspeccionId, NumeroItem, Descripcion, CondicionEstandar, Metodo, Orden) VALUES
    (@PlanPkg1, 1, 'Verificar alimentación de film', 'Rollo correctamente montado y tensión adecuada', 1, 1),
    (@PlanPkg1, 2, 'Verificar temperatura de sellado', 'Según especificación del producto', 3, 2),
    (@PlanPkg1, 3, 'Verificar calidad de sellado', 'Sello uniforme sin fugas', 2, 3),
    (@PlanPkg1, 4, 'Verificar contador de paquetes', 'Funcionando correctamente', 1, 4),
    (@PlanPkg1, 5, 'Verificar etiquetado', 'Etiquetas legibles y bien posicionadas', 1, 5);

    PRINT 'Plan INS-L1-PKG-001 creado con 5 items.';
END
GO

-- Plan semanal de lubricación para Maquinista
IF NOT EXISTS (SELECT 1 FROM [mnt].[PlanesInspeccion] WHERE Codigo = 'INS-L1-MAQ-002')
BEGIN
    INSERT INTO [mnt].[PlanesInspeccion] (LineaId, EquipoId, PuestoResponsable, Codigo, Nombre, Descripcion, FrecuenciaDias, Orden)
    VALUES (1, NULL, 1, 'INS-L1-MAQ-002', 'Inspección Semanal Lubricación',
            'Verificación semanal de puntos de lubricación', 7, 4);

    DECLARE @PlanMaq2 INT = SCOPE_IDENTITY();

    INSERT INTO [mnt].[ItemsPlanInspeccion] (PlanInspeccionId, NumeroItem, Descripcion, CondicionEstandar, Metodo, Orden) VALUES
    (@PlanMaq2, 1, 'Verificar nivel de grasa en engrasadores', 'Todos los puntos con grasa visible', 1, 1),
    (@PlanMaq2, 2, 'Verificar estado de cadenas', 'Sin óxido, tensión correcta', 1, 2),
    (@PlanMaq2, 3, 'Verificar rodamientos principales', 'Sin ruidos anormales ni vibración', 2, 3),
    (@PlanMaq2, 4, 'Verificar estado de correas', 'Sin grietas ni desgaste excesivo', 1, 4);

    PRINT 'Plan INS-L1-MAQ-002 creado con 4 items.';
END
GO

-- =============================================
-- Planes de Inspección para Línea 2 (Tango 1)
-- =============================================

IF NOT EXISTS (SELECT 1 FROM [mnt].[PlanesInspeccion] WHERE Codigo = 'INS-L2-MAQ-001')
BEGIN
    INSERT INTO [mnt].[PlanesInspeccion] (LineaId, EquipoId, PuestoResponsable, Codigo, Nombre, Descripcion, FrecuenciaDias, Orden)
    VALUES (2, NULL, 1, 'INS-L2-MAQ-001', 'Inspección Inicio Turno - Maquinista L2',
            'Checklist de inspección al inicio de cada turno para el maquinista de Línea 2', 1, 1);

    DECLARE @PlanL2Maq1 INT = SCOPE_IDENTITY();

    INSERT INTO [mnt].[ItemsPlanInspeccion] (PlanInspeccionId, NumeroItem, Descripcion, CondicionEstandar, Metodo, Orden) VALUES
    (@PlanL2Maq1, 1, 'Verificar presión de aire comprimido', 'Entre 6 y 8 bar', 3, 1),
    (@PlanL2Maq1, 2, 'Verificar nivel de aceite hidráulico', 'Entre marcas MIN y MAX', 1, 2),
    (@PlanL2Maq1, 3, 'Inspeccionar bandas transportadoras', 'Sin cortes, desgaste excesivo ni desalineación', 1, 3),
    (@PlanL2Maq1, 4, 'Verificar funcionamiento de paradas de emergencia', 'Todas las paradas deben funcionar correctamente', 2, 4),
    (@PlanL2Maq1, 5, 'Verificar estado de guardas de seguridad', 'Todas las guardas en posición y sin daños', 1, 5);

    PRINT 'Plan INS-L2-MAQ-001 creado con 5 items.';
END
GO

PRINT 'Datos de demostración de Inspecciones insertados correctamente.';
GO
