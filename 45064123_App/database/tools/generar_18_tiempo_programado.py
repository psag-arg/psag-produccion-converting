"""Genera database/18_t76_tiempo_programado_historico.sql desde el Excel de historial de Tango 2.

Uso (con openpyxl instalado):
    python generar_18_tiempo_programado.py "<ruta al Historial detenciones tango 2.xlsx>"

Por cada fecha desde 2026-03-01 hasta el último día del Excel, suma la columna HS PROG de
todas sus filas (0 si no tiene). Las filas con fecha anterior a 2026 (typos 1900-01-xx) se ignoran.
"""
import datetime as dt
import sys
from collections import defaultdict
from pathlib import Path

import openpyxl

LINEA = "Tango 2"
DESDE = dt.date(2026, 3, 1)
SALIDA = Path(__file__).resolve().parent.parent / "18_t76_tiempo_programado_historico.sql"


def leer_horas(ruta):
    horas = defaultdict(float)
    ultimo = None
    wb = openpyxl.load_workbook(ruta, data_only=True, read_only=True)
    for hoja in wb.worksheets:
        for fila in list(hoja.iter_rows(values_only=True))[1:]:
            fecha, _turno, _minutos, hs_prog = (list(fila) + [None] * 4)[:4]
            if not isinstance(fecha, dt.datetime) or fecha.year < DESDE.year:
                continue
            dia = fecha.date()
            ultimo = dia if ultimo is None or dia > ultimo else ultimo
            if hs_prog not in (None, ""):
                horas[dia] += float(hs_prog)
    return horas, ultimo


def generar(ruta, salida=SALIDA):
    horas, ultimo = leer_horas(ruta)
    dias = []
    dia = DESDE
    while dia <= ultimo:
        dias.append((dia, round(horas.get(dia, 0.0), 2)))
        dia += dt.timedelta(days=1)

    filas = ",\n".join(
        f"    (CAST('{d.isoformat()}' AS DATE), CAST({h:.2f} AS DECIMAL(5,2)))" for d, h in dias
    )
    total = sum(h for _, h in dias)
    con_hs = sum(1 for _, h in dias if h > 0)
    salida.write_text(f"""-- ============================================================================
-- T-76 · Tiempo programado histórico de Tango 2 (marzo a agosto 2026)
-- Generado por database/tools/generar_18_tiempo_programado.py desde el Excel
-- "Historial detenciones tango 2.xlsx" (hojas TGO2-DET, columna HS PROG).
-- NO editar los VALUES a mano: si cambia el Excel, volver a generar.
--
-- Contenido: {len(dias)} días desde {DESDE.isoformat()} hasta {ultimo.isoformat()}.
-- Horas = suma de HS PROG de cada fecha (0 si la fecha no tiene HS PROG).
-- Total {total:.1f} h programadas; {con_hs} días con HS PROG.
-- Origen = HISTORICO. Requiere 17_t76_ajustes.sql (tabla mnt.TiemposProgramadosDia).
-- Idempotente: no inserta fechas que ya tienen fila para la línea.
-- ============================================================================

USE [PSAG_Mantenimiento]
GO

DECLARE @LineaId INT = (SELECT TOP (1) [LineaId] FROM [mnt].[Lineas] WHERE [Nombre] = N'{LINEA}');

IF @LineaId IS NULL
BEGIN
    RAISERROR(N'No existe la línea {LINEA} en mnt.Lineas. Cargar las líneas antes de ejecutar este script.', 16, 1);
    RETURN;
END

INSERT INTO [mnt].[TiemposProgramadosDia]
    ([LineaId], [Fecha], [Horas], [Motivo], [Origen], [Activo],
     [FechaHoraCreacion], [FechaHoraModificacion], [CreadoPorUsuarioId], [ModificadoPorUsuarioId])
SELECT @LineaId, d.[Fecha], d.[Horas], NULL, N'HISTORICO', 1,
       SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET(), N'SYSTEM-SEED', N'SYSTEM-SEED'
FROM (VALUES
{filas}
) AS d ([Fecha], [Horas])
WHERE NOT EXISTS (
    SELECT 1
    FROM [mnt].[TiemposProgramadosDia] t
    WHERE t.[LineaId] = @LineaId AND t.[Fecha] = d.[Fecha]);

PRINT CONCAT(N'Tiempo programado histórico de {LINEA}: ', @@ROWCOUNT, N' días insertados.');
""", encoding="utf-8")
    return len(dias), total, con_hs, ultimo


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    n, total, con_hs, ultimo = generar(sys.argv[1])
    print(f"{n} días hasta {ultimo}, {total:.1f} h, {con_hs} con HS PROG -> {SALIDA.name}")
