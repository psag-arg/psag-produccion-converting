"""Regresión de lo que existía antes de T-76. Uso: regresion.py <api_url> <db.sqlite>"""
import json, sys, sqlite3, urllib.request, urllib.error

API, DB = sys.argv[1].rstrip("/"), sys.argv[2]
ok = fail = 0

def call(method, path, token=None, body=None):
    req = urllib.request.Request(API + path, method=method, data=json.dumps(body).encode() if body is not None else None)
    req.add_header("Content-Type", "application/json")
    if token: req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as r:
            d = r.read()
            try: return r.status, json.loads(d) if d else None
            except Exception: return r.status, d
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")[:300]

def check(name, cond, detail=""):
    global ok, fail
    if cond: ok += 1; print("  OK  ", name)
    else: fail += 1; print("  FAIL", name, "->", str(detail)[:300])

db = sqlite3.connect(DB)
q = lambda s: db.execute(s).fetchone()[0]
admin = call("POST", "/api/auth/login", body={"usuario": "admin", "password": "Admin123!"})[1]["accessToken"]
linea = q("select LineaId from Lineas where Nombre='Tango 2'")
tipo = q("select TipoFallaId from TiposFalla where Nombre='OTROS'")

print("Endpoints que ya existían")
for path in ["/api/dashboard", "/api/dashboard/resumen", "/api/dashboard/detenciones-por-linea", "/api/dashboard/detenciones-por-tipo-falla",
             "/api/dashboard/top-detalles-falla", "/api/dashboard/tendencia", f"/api/dashboard/indisponibilidad?lineaId={linea}&anio=2026",
             "/api/registrosproceso", "/api/inspecciones/planes?lineaId=1&puesto=MAQUINISTA", "/api/inspecciones/pendientes?lineaId=1&puesto=MAQUINISTA", "/api/inspecciones/ejecuciones",
             "/api/inspecciones/no-ok", "/api/catalogos/lineas", "/api/catalogos/equipos", "/api/catalogos/tipos-falla",
             "/api/catalogos/detalles-falla", "/api/catalogos/turnos", "/api/catalogos/turnos/actual", "/api/usuarios", "/api/usuarios/roles",
             "/api/detenciones?page=1&pageSize=10", "/api/detenciones/abiertas", "/api/detenciones/export/csv?fechaDesde=2026-03-01&fechaHasta=2026-03-31"]:
    s, _ = call("GET", path, admin)
    check(f"GET {path}", s == 200, s)

print("Carga de siempre: minutos declarados, sin horas")
s, r = call("POST", "/api/detenciones", admin, {"lineaId": linea, "tipoFallaId": tipo, "fecha": "2026-10-07", "minutosDeclarados": 25, "descripcion": "Prueba de regresión sin horas"})
check("crea con minutos declarados", s == 201, (s, r))
did = r.get("detencionId") if isinstance(r, dict) else None
s, d = call("GET", f"/api/detenciones/{did}", admin)
check("minutos = 25, origen DECLARADO, sin análisis", d.get("minutosEfectivos") == 25 and d.get("origenTiempo") == "DECLARADO" and not d.get("requiereAnalisis"), d)
check("turno asignado", bool(d.get("turnoNombre")), d.get("turnoNombre"))
s, r = call("PUT", f"/api/detenciones/{did}", admin, {"lineaId": linea, "tipoFallaId": tipo, "fecha": "2026-10-07", "minutosDeclarados": 40, "descripcion": "Prueba editada", "rowVersion": d.get("rowVersion")})
check("edita minutos declarados", s == 200, (s, r))
s, d = call("GET", f"/api/detenciones/{did}", admin)
check("minutos editados = 40", d.get("minutosEfectivos") == 40, d.get("minutosEfectivos"))
n = q("select count(*) from Detenciones where Descripcion like 'Prueba%'")
check("editar no duplica la detención", n == 1, n)
s, r = call("POST", f"/api/detenciones/{did}/cerrar", admin, {"accionesRealizadas": "Ajuste", "resolucionDescripcion": "Resuelto"})
check("cierre de siempre sigue andando sin análisis", s in (200, 204), (s, r))
s, n = call("POST", f"/api/detenciones/{did}/notas", admin, {"texto": "nota de prueba"})
check("agregar nota", s in (200, 201), (s, n))

print("Editar no borra un análisis en curso")
fe = q("select TipoFallaId from TiposFalla where Nombre='FALLO EQUIPO'")
s, _ = call("POST", "/api/catalogos/reglas-analisis", admin, {"lineaId": linea, "tipoFallaId": fe, "minutosMinimos": 60, "responsableRol": "MANTENIMIENTO", "activo": True})
s, r = call("POST", "/api/detenciones", admin, {"lineaId": linea, "tipoFallaId": fe, "fecha": "2026-10-06", "horaInicio": "08:00:00", "horaFin": "09:30:00", "descripcion": "Regresión análisis"})
aid = r.get("detencionId") if isinstance(r, dict) else None
s, a = call("GET", f"/api/analisis/{aid}", admin)
s, _ = call("PUT", f"/api/analisis/{aid}", admin, {"accionesRealizadas": "Solución en borrador", "rowVersion": a.get("rowVersion")})
check("borrador guardado", s == 200, s)
s, d = call("GET", f"/api/detenciones/{aid}", admin)
s, r = call("PUT", f"/api/detenciones/{aid}", admin, {"lineaId": linea, "tipoFallaId": fe, "fecha": "2026-10-06", "descripcion": "Regresión análisis (editada)", "rowVersion": d.get("rowVersion")})
check("edita la descripción", s == 200, (s, r))
s, d = call("GET", f"/api/detenciones/{aid}", admin)
check("la solución sigue", d.get("accionesRealizadas") == "Solución en borrador", d.get("accionesRealizadas"))
check("horas, minutos y origen sin cambios", d.get("minutosEfectivos") == 90 and d.get("origenTiempo") == "CALCULADO", (d.get("minutosEfectivos"), d.get("origenTiempo")))
check("sigue En análisis", d.get("estadoActual") == "EN_SEGUIMIENTO" and d.get("requiereAnalisis"), (d.get("estadoActual"), d.get("requiereAnalisis")))

print(f"\nRESULTADO: {ok} OK, {fail} FAIL")
sys.exit(1 if fail else 0)
