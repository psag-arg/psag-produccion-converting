"""Prueba de punta a punta de T-76 contra la API local. Uso: e2e.py <api_url> <db.sqlite>"""
import json, sys, sqlite3, urllib.request, urllib.error, io

API, DB = sys.argv[1].rstrip("/"), sys.argv[2]
ok_count = fail_count = 0

def call(method, path, token=None, body=None, raw=False):
    req = urllib.request.Request(API + path, method=method,
                                 data=json.dumps(body).encode() if body is not None else None)
    req.add_header("Content-Type", "application/json")
    if token: req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as r:
            data = r.read()
            return r.status, (data if raw else (json.loads(data) if data else None))
    except urllib.error.HTTPError as e:
        data = e.read()
        try: return e.code, json.loads(data)
        except Exception: return e.code, data.decode(errors="replace")

def check(name, cond, detail=""):
    global ok_count, fail_count
    if cond: ok_count += 1; print("  OK  ", name)
    else: fail_count += 1; print("  FAIL", name, "->", str(detail)[:300])

def login(u, p):
    s, r = call("POST", "/api/auth/login", body={"usuario": u, "password": p})
    assert s == 200, (u, s, r)
    return r.get("token") or r.get("accessToken")

db = sqlite3.connect(DB)
linea = db.execute("select LineaId from Lineas where Nombre='Tango 2'").fetchone()[0]
fallo_equipo = db.execute("select TipoFallaId from TiposFalla where Nombre='FALLO EQUIPO'").fetchone()[0]
rebob = db.execute("select EquipoId from Equipos where Nombre='REBOBINADORA'").fetchone()[0]

admin = login("admin", "Admin123!")
print("Setup")
s, _ = call("POST", "/api/catalogos/reglas-analisis", admin, {"lineaId": linea, "tipoFallaId": fallo_equipo, "minutosMinimos": 60, "responsableRol": "MANTENIMIENTO", "activo": True})
check("admin crea regla FALLO EQUIPO >= 60 -> Mantenimiento", s in (200, 201), s)
for u, rol in [("mant1", "MANTENIMIENTO"), ("oper1", "OPERARIO")]:
    s, r = call("POST", "/api/usuarios", admin, {"userName": u, "email": f"{u}@psag.local", "nombreCompleto": u.upper(), "password": "Clave123!", "roles": [rol], "lineasAsignadas": [linea]})
    check(f"admin crea usuario {u} ({rol})", s in (200, 201), r)
mant, oper = login("mant1", "Clave123!"), login("oper1", "Clave123!")

print("Operario declara 05:40 a 08:05 (#20, #6)")
det = {"lineaId": linea, "equipoId": rebob, "tipoFallaId": fallo_equipo, "fecha": "2026-10-08",
       "horaInicio": "05:40:00", "horaFin": "08:05:00", "descripcion": "Carro de limpieza rodillo inferior se rompe, carro choca prensa."}
s, r = call("POST", "/api/detenciones", oper, det)
check("operario crea detención", s in (200, 201), r)
did = (r or {}).get("detencionId")
s, d = call("GET", f"/api/detenciones/{did}", oper)
check("minutos calculados = 145", d.get("minutosEfectivos") == 145, d.get("minutosEfectivos"))
check("turno por hora de inicio (05:40 con turnos de prueba 06-14/14-22/22-06 = Noche)", "Noche" in d.get("turnoNombre",""), d.get("turnoNombre"))
check("requiere análisis por la regla", d.get("requiereAnalisis") is True and d.get("analisisPendiente") is True, d)
check("fecha de creación no es 0001", not str(d.get("fechaHoraCreacion", "")).startswith("0001"), d.get("fechaHoraCreacion"))

print("Permisos del análisis (#8)")
s, _ = call("PUT", f"/api/analisis/{did}", oper, {"accionesRealizadas": "x", "causaFalla": "y", "rowVersion": d.get("rowVersion")})
check("operario NO puede editar el análisis (403)", s == 403, s)
s, a = call("GET", f"/api/analisis/{did}", oper)
check("operario SÍ puede leer el análisis", s == 200, s)
s, r = call("POST", f"/api/detenciones/{did}/cerrar", admin, {"resolucionDescripcion": "x"})
check("cierre viejo rechazado si requiere análisis (400)", s == 400, (s, r))

print("Mis pendientes y análisis (#9, #10)")
s, p = call("GET", "/api/analisis/pendientes", mant)
codigos = json.dumps(p)
check("aparece en Mis pendientes de mantenimiento", d.get("codigo") in codigos, s)
s, p2 = call("GET", "/api/analisis/pendientes", oper)
check("no aparece como 'para analizar' del operario", d.get("codigo") not in json.dumps((p2 or {}).get("paraAnalizar", [])), p2)
s, a = call("GET", f"/api/analisis/{did}", mant)
s, r = call("PUT", f"/api/analisis/{did}", mant, {"accionesRealizadas": "Se cambia cadena del carro y se regula sensor.", "causaFalla": None, "planAccion": None, "rowVersion": a.get("rowVersion")})
check("mantenimiento guarda borrador", s == 200, (s, r))
s, d = call("GET", f"/api/detenciones/{did}", mant)
check("estado pasa a En análisis", d.get("estadoActual") == "EN_SEGUIMIENTO", d.get("estadoActual"))
s, a = call("GET", f"/api/analisis/{did}", mant)
s, r = call("POST", f"/api/analisis/{did}/cerrar", mant, {"accionesRealizadas": a.get("accionesRealizadas"), "causaFalla": "", "rowVersion": a.get("rowVersion")})
check("cerrar sin causa rechazado (400)", s == 400, (s, r))
s, r = call("POST", f"/api/analisis/{did}/cerrar", mant, {"accionesRealizadas": a.get("accionesRealizadas"), "causaFalla": "Cadena estirada.", "planAccion": "Revisar cadena cada 15 días.", "rowVersion": a.get("rowVersion")})
check("cerrar con solución y causa", s == 200, (s, r))
s, d = call("GET", f"/api/detenciones/{did}", mant)
check("estado Cerrada y firma", d.get("estadoActual") == "CERRADA" and d.get("analisisPorUsuarioId"), d)
hist = [h.get("estadoNuevoDescripcion") for h in d.get("historialEstados", [])]
check("historial con textos (no códigos)", "En análisis" in hist, hist)
s, sol = call("GET", "/api/analisis/soluciones?texto=carro", oper)
check("base de soluciones encuentra el caso (#11)", d.get("codigo") in json.dumps(sol), s)

print("Borrado lógico (#1)")
otra = db.execute("select DetencionId, MinutosEfectivos from Detenciones where Fecha='2026-03-02' order by DetencionId limit 1").fetchone()
s, ind0 = call("GET", f"/api/dashboard/indisponibilidad?lineaId={linea}&anio=2026", admin)
mar0 = next(m for m in ind0["meses"] if m["mes"] == 3)
s, _ = call("POST", f"/api/detenciones/{otra[0]}/eliminar", oper, {"motivo": "prueba"})
check("operario no puede eliminar (403)", s == 403, s)
s, r = call("POST", f"/api/detenciones/{otra[0]}/eliminar", admin, {"motivo": "Detención de prueba"})
check("admin elimina con motivo", s in (200, 204), (s, r))
s, _ = call("GET", f"/api/detenciones/{otra[0]}", admin)
check("eliminada no aparece en el detalle normal (404)", s == 404, s)
s, ind1 = call("GET", f"/api/dashboard/indisponibilidad?lineaId={linea}&anio=2026", admin)
mar1 = next(m for m in ind1["meses"] if m["mes"] == 3)
check("sale de los reportes", mar1["minutosTotalesParada"] == mar0["minutosTotalesParada"] - otra[1], (mar0["minutosTotalesParada"], mar1["minutosTotalesParada"], otra[1]))
fila = db.execute("select Eliminada, MotivoEliminacion from Detenciones where DetencionId=?", (otra[0],)).fetchone()
check("sigue en la base, marcada", fila == (1, "Detención de prueba"), fila)
s, _ = call("POST", f"/api/detenciones/{otra[0]}/restaurar", admin, {})
check("admin restaura", s in (200, 204), s)

print("Indisponibilidad (#14) y Excel (#13)")
esperado = {3: 12.9, 4: 10.3, 5: 7.6, 6: 10.5, 7: 10.7, 8: 6.7}
reales = {m["mes"]: m["indisponibilidadTotal"] for m in ind0["meses"] if m["mes"] in esperado}
check("indisponibilidad mar-ago con horas programadas", reales == esperado, reales)
s, xlsx = call("GET", "/api/detenciones/export/xlsx?fechaDesde=2026-03-01&fechaHasta=2026-03-31", admin, raw=True)
import openpyxl
filas = openpyxl.load_workbook(io.BytesIO(xlsx)).active.max_row - 1 if s == 200 else -1
marzo = db.execute("select count(*) from Detenciones where Fecha between '2026-03-01' and '2026-03-31' and Eliminada=0").fetchone()[0]
check("Excel de marzo con las mismas filas que la base", filas == marzo, (s, filas, marzo))

print(f"\nRESULTADO: {ok_count} OK, {fail_count} FAIL")
sys.exit(1 if fail_count else 0)
