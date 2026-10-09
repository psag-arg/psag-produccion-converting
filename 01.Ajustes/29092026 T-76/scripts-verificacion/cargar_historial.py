"""Carga el historial de Tango 2 (Excel del cliente) en una base SQLite local de desarrollo.

Uso: python cargar_historial.py <excel> <db.sqlite>
Crea línea Tango 2, equipos, tipos de falla y detalles que falten, las detenciones,
y si existe la tabla de tiempo programado, carga HS PROG por día como HISTORICO.
"""
import sqlite3, sys, datetime as dt, collections
import openpyxl

excel, dbp = sys.argv[1], sys.argv[2]
db = sqlite3.connect(dbp)
NOW = dt.datetime.now(dt.timezone.utc).isoformat(sep=" ")
SEED = "SYSTEM-SEED"

def cols(t):
    return {r[1]: r for r in db.execute(f"PRAGMA table_info({t})")}

def table(*names):
    have = {r[0] for r in db.execute("select name from sqlite_master where type='table'")}
    return next((n for n in names if n in have), None)

def insert(t, data):
    info = cols(t)
    row = dict(data)
    for name, (_, _, typ, notnull, default, pk) in info.items():
        if name in row or pk or not notnull or default is not None:
            continue
        typ = (typ or "").upper()
        if name in ("FechaHoraCreacion", "FechaHoraModificacion"): row[name] = NOW
        elif name in ("CreadoPorUsuarioId", "ModificadoPorUsuarioId"): row[name] = SEED
        elif name == "Activo": row[name] = 1
        elif name == "RowVersion": row[name] = b""
        elif "INT" in typ or "REAL" in typ or "NUMERIC" in typ: row[name] = 0
        elif "BLOB" in typ: row[name] = b""
        else: row[name] = ""
    row = {k: v for k, v in row.items() if k in info}
    q = f"insert into {t} ({','.join(row)}) values ({','.join('?' * len(row))})"
    return db.execute(q, list(row.values())).lastrowid

def get_or_create(t, idcol, where, data):
    clause = " and ".join(f"{k}=?" for k in where)
    r = db.execute(f"select {idcol} from {t} where {clause}", list(where.values())).fetchone()
    return r[0] if r else insert(t, {**where, **data})

linea = get_or_create("Lineas", "LineaId", {"Nombre": "Tango 2"}, {"Codigo": "TGO2", "Orden": 7})
turnos = {r[0]: r[1] for r in db.execute("select Codigo, TurnoId from Turnos")}
turno_manana = turnos.get("T1") or next(iter(turnos.values()))

wb = openpyxl.load_workbook(excel, data_only=True, read_only=True)
horas = collections.defaultdict(float)
n = 0
seq = collections.Counter()
for ws in wb.worksheets:
    for r in list(ws.iter_rows(values_only=True))[1:]:
        fecha, maq, mins, hsprog, tipo, equipo, detalle, desc = (list(r) + [None] * 8)[:8]
        if not isinstance(fecha, dt.datetime):
            continue
        d = fecha.date()
        if d.year < 2026:
            continue  # fechas mal tipeadas en el Excel (1900-01-xx)
        if hsprog:
            horas[d] += float(hsprog)
        if not mins or not tipo:
            continue
        tipo = str(tipo).strip().upper()
        tf = get_or_create("TiposFalla", "TipoFallaId", {"Nombre": tipo}, {"Codigo": "TF-%d" % db.execute("select coalesce(max(TipoFallaId),0)+1 from TiposFalla").fetchone()[0], "Orden": 99})
        eq = None
        if equipo and str(equipo).strip().upper() != "MAQUINA":
            en = str(equipo).strip().upper()
            eq = get_or_create("Equipos", "EquipoId", {"Nombre": en, "LineaId": linea}, {"Codigo": "EQ-%d" % db.execute("select coalesce(max(EquipoId),0)+1 from Equipos").fetchone()[0], "Orden": 99})
        df = None
        if detalle:
            dn = str(detalle).strip().upper()
            dcols = cols("DetallesFalla")
            where = {"Nombre": dn, "TipoFallaId": tf}
            df = get_or_create("DetallesFalla", "DetalleFallaId", where, {"Codigo": "DF-%d" % db.execute("select coalesce(max(DetalleFallaId),0)+1 from DetallesFalla").fetchone()[0], "Orden": 99})
        seq[d] += 1
        hora = dt.datetime.combine(d, dt.time(8, 0))
        insert("Detenciones", {
            "Codigo": f"DET-{d:%Y%m%d}-{n + 1:06d}", "LineaId": linea, "EquipoId": eq, "TipoFallaId": tf,
            "DetalleFallaId": df, "TurnoId": turno_manana, "Fecha": d.isoformat(),
            "FechaHoraParada": hora.isoformat(sep=" ") + "+00:00", "MinutosDeclarados": int(mins),
            "MinutosEfectivos": int(mins), "OrigenTiempo": "DECLARADO",
            "Descripcion": (str(desc).strip() if desc else f"{tipo} {detalle or ''}".strip()),
            "EstadoActual": "REGISTRADA", "FechaHoraCreacion": NOW, "FechaHoraModificacion": NOW,
            "CreadoPorUsuarioId": "IMPORT", "ModificadoPorUsuarioId": "IMPORT",
        })
        n += 1

tp = table("TiemposProgramadosDia", "TiempoProgramadoDia", "TiemposProgramados")
cargados = 0
if tp and horas:
    first, last = min(horas), max(horas)
    d = first.replace(day=1)
    while d <= last:
        if not db.execute(f"select 1 from {tp} where LineaId=? and Fecha=?", (linea, d.isoformat())).fetchone():
            insert(tp, {"LineaId": linea, "Fecha": d.isoformat(), "Horas": horas.get(d, 0.0),
                        "Motivo": "Histórico HS PROG (Excel Tango 2)", "Origen": "HISTORICO"})
            cargados += 1
        d += dt.timedelta(days=1)
db.commit()
print(f"linea Tango 2 id={linea} detenciones={n} dias_tiempo_programado={cargados} tabla_tp={tp}")
print("horas por mes:", {k: v for k, v in sorted(collections.Counter({f'{d:%Y-%m}': 0 for d in horas}).items())} or "", end=" ")
mes = collections.defaultdict(float)
for d, h in horas.items(): mes[f"{d:%Y-%m}"] += h
print(dict(sorted(mes.items())))
