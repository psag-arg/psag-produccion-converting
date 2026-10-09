"""Verificación de los arreglos de F2 (T-76) contra la API local.

Uso: fixes_f2.py <api_url> <db.sqlite> [api_url_utc]
  api_url_utc (opcional): segunda API arrancada con TZ=UTC sobre la misma base, para el punto 6.
Requiere que regresion.py y e2e.py ya hayan corrido sobre la misma base (usa Tango 2, mant1 y oper1).
"""
import json, sys, sqlite3, urllib.request, urllib.error
from datetime import datetime, timedelta, timezone

API, DB = sys.argv[1].rstrip("/"), sys.argv[2]
API_UTC = sys.argv[3].rstrip("/") if len(sys.argv) > 3 else None
ok = fail = 0
FECHA = "2026-10-09"
PASS = "Clave123!"


def call(method, path, token=None, body=None, base=None):
    req = urllib.request.Request((base or API) + path, method=method,
                                 data=json.dumps(body).encode() if body is not None else None)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as r:
            d = r.read()
            try:
                return r.status, json.loads(d) if d else None
            except Exception:
                return r.status, d
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")[:300]


def check(name, cond, detail=""):
    global ok, fail
    if cond:
        ok += 1
        print("  OK  ", name)
    else:
        fail += 1
        print("  FAIL", name, "->", str(detail)[:400])


def login(u, p):
    s, r = call("POST", "/api/auth/login", body={"usuario": u, "password": p})
    assert s == 200, (u, s, r)
    return r.get("token") or r.get("accessToken")


db = sqlite3.connect(DB)
linea = db.execute("select LineaId from Lineas where Nombre='Tango 2'").fetchone()[0]
otros = db.execute("select TipoFallaId from TiposFalla where Nombre='OTROS'").fetchone()[0]
sin_regla = db.execute("select TipoFallaId from TiposFalla where Nombre='Falla Mecanica'").fetchone()[0]

admin = login("admin", "Admin123!")


def usuario_id(nombre):
    s, lista = call("GET", "/api/usuarios", admin)
    return next((u["id"] for u in lista if u["userName"] == nombre), None)


def asegurar_usuario(nombre, roles, lineas):
    if usuario_id(nombre) is None:
        s, r = call("POST", "/api/usuarios", admin, {"userName": nombre, "email": f"{nombre}@psag.local",
                    "nombreCompleto": nombre.upper(), "password": PASS, "roles": roles, "lineasAsignadas": lineas})
        assert s in (200, 201), (nombre, s, r)


asegurar_usuario("oper1", ["OPERARIO"], [linea])
asegurar_usuario("ger1", ["GERENCIA"], [])
asegurar_usuario("ger2", ["GERENCIA"], [])
oper = login("oper1", PASS)
ger1 = login("ger1", PASS)
ger2 = login("ger2", PASS)
ger1_id = usuario_id("ger1")


def regla(body):
    s, r = call("POST", "/api/catalogos/reglas-analisis", admin, body)
    assert s in (200, 201), (s, r)
    return r["reglaAnalisisId"]


def desactivar(rid, cuerpo):
    s, r = call("PUT", f"/api/catalogos/reglas-analisis/{rid}", admin,
                {**cuerpo, "activo": False})
    assert s == 200, (s, r)


def detencion(id_, token=None):
    return call("GET", f"/api/detenciones/{id_}", token or admin)[1]


def crear(token, hora_inicio, hora_fin, tipo, descripcion="Prueba F2", **extra):
    body = {"lineaId": linea, "tipoFallaId": tipo, "fecha": FECHA, "descripcion": descripcion,
            "horaInicio": hora_inicio, "horaFin": hora_fin, **extra}
    s, r = call("POST", "/api/detenciones", token, body)
    assert s in (200, 201), (s, r)
    return r["detencionId"]


def pendientes(token):
    return call("GET", "/api/analisis/pendientes", token)[1]


def codigos(lista):
    return {x["detencionId"] for x in lista}


# ---------- Reglas de prueba ----------
R_OP_BODY = {"lineaId": linea, "tipoFallaId": otros, "equipoId": None, "minutosMinimos": 45,
             "responsableRol": "OPERARIO", "responsableUsuarioId": None}
R_GER_BODY = {"lineaId": linea, "tipoFallaId": otros, "equipoId": None, "minutosMinimos": 75,
              "responsableRol": None, "responsableUsuarioId": ger1_id}
r_op = regla(R_OP_BODY)
r_ger = regla(R_GER_BODY)

print("Mis pendientes por rol y asignación (puntos 1, 2 y 3)")
d1 = crear(admin, "06:00:00", "07:00:00", otros, "Carro trabado F2 uno")   # 60 min -> OPERARIO
d2 = crear(admin, "06:00:00", "07:00:00", otros, "Carro trabado F2 dos")   # 60 min -> OPERARIO
d3 = crear(admin, "09:00:00", "10:20:00", otros, "Motor F2 asignado a gerente")  # 80 min -> usuario ger1
d4 = crear(admin, "06:00:00", "07:00:00", otros, "Justificar F2")          # 60 min -> OPERARIO (luego PENDIENTE)
x1, x2, x3, x4 = detencion(d1), detencion(d2), detencion(d3), detencion(d4)
check("60 min con regla OPERARIO queda con responsable por rol", x1["requiereAnalisis"] and x1.get("responsableRol") == "OPERARIO", x1)
check("80 min gana la regla de usuario (mayor minimo)", x3.get("responsableUsuarioId") == ger1_id and x3.get("responsableRol") is None, x3)

po = pendientes(oper)
check("OPERARIO responsable ve las pestañas de análisis (punto 1)", po["veAnalisis"] is True, po.get("veAnalisis"))
check("OPERARIO ve sus análisis en Para analizar", {d1, d2, d4} <= codigos(po["paraAnalizar"]), codigos(po["paraAnalizar"]))
check("OPERARIO no ve el análisis de otro responsable", d3 not in codigos(po["paraAnalizar"]) and d3 not in codigos(po["enAnalisis"]))

pg1 = pendientes(ger1)
check("gerente con análisis asignado por usuario ve las pestañas (punto 1)", pg1["veAnalisis"] is True, pg1.get("veAnalisis"))
check("gerente asignado ve sólo su análisis", codigos(pg1["paraAnalizar"]) == {d3}, codigos(pg1["paraAnalizar"]))

pg2 = pendientes(ger2)
check("gerente sin análisis asignado NO ve las pestañas (punto 1)", pg2["veAnalisis"] is False, pg2.get("veAnalisis"))
check("gerente sin asignación no recibe análisis ajenos (punto 2)",
      not pg2["paraAnalizar"] and not pg2["enAnalisis"], (pg2["paraAnalizar"], pg2["enAnalisis"]))

pa = pendientes(admin)
check("administrador ve todos los abiertos", {d1, d2, d3, d4} <= codigos(pa["paraAnalizar"]))

s, c_op = call("GET", "/api/analisis/pendientes/conteo", oper)
n_op = len(po["paraAnalizar"]) + len(po["enAnalisis"]) + len(po["paraDeclarar"])
check("conteo de OPERARIO = lo que muestran las listas (punto 2)", c_op["total"] == n_op, (c_op, n_op))
s, c_g1 = call("GET", "/api/analisis/pendientes/conteo", ger1)
check("conteo de gerente asignado = su lista (punto 2)", c_g1["total"] == len(pg1["paraAnalizar"]) + len(pg1["enAnalisis"]), (c_g1, len(pg1["paraAnalizar"])))

print("Borrador en curso sobrevive a desactivar la regla (punto 4)")
a1 = call("GET", f"/api/analisis/{d1}", oper)[1]
s, r = call("PUT", f"/api/analisis/{d1}", oper, {"accionesRealizadas": "Se ajusta guia de cadena", "causaFalla": None,
            "planAccion": None, "rowVersion": a1["rowVersion"]})
check("operario responsable guarda borrador", s == 200, (s, r))
check("borrador pasa a En análisis", detencion(d1)["estadoActual"] == "EN_SEGUIMIENTO", detencion(d1)["estadoActual"])
desactivar(r_op, R_OP_BODY)

actual = detencion(d1)
s, r = call("PUT", f"/api/detenciones/{d1}", admin, {"fecha": FECHA, "tipoFallaId": otros, "descripcion": "Carro trabado F2 uno editado",
            "minutosDeclarados": 60, "origenTiempo": "CALCULADO", "accionesRealizadas": actual.get("accionesRealizadas"),
            "rowVersion": actual["rowVersion"]})
check("edición con la regla desactivada responde 200", s == 200, (s, r))
e1 = detencion(d1)
check("con borrador en curso la marca y el responsable se conservan",
      e1["requiereAnalisis"] is True and e1.get("responsableRol") == "OPERARIO" and e1["estadoActual"] == "EN_SEGUIMIENTO", e1)
check("el texto del borrador no se pierde", e1.get("accionesRealizadas") == "Se ajusta guia de cadena", e1.get("accionesRealizadas"))

actual2 = detencion(d2)
s, r = call("PUT", f"/api/detenciones/{d2}", admin, {"fecha": FECHA, "tipoFallaId": otros, "descripcion": "Carro trabado F2 dos editado",
            "minutosDeclarados": 60, "origenTiempo": "CALCULADO", "rowVersion": actual2["rowVersion"]})
e2 = detencion(d2)
check("sin análisis iniciado, la regla que ya no aplica quita la marca",
      s == 200 and e2["requiereAnalisis"] is False and e2.get("responsableRol") is None, (s, e2))

print("Análisis abierto excluye PENDIENTE_JUSTIFICACION (punto 3)")
db.execute("update Detenciones set EstadoActual='PENDIENTE_JUSTIFICACION' where DetencionId=?", (d4,))
db.commit()
pa = pendientes(admin)
check("PENDIENTE_JUSTIFICACION no figura en Para analizar del administrador",
      d4 not in codigos(pa["paraAnalizar"]) and d4 not in codigos(pa["enAnalisis"]))
s, c_admin = call("GET", "/api/analisis/pendientes/conteo", admin)
check("conteo del administrador no cuenta la pendiente de justificación",
      c_admin["total"] == len(pa["paraAnalizar"]) + len(pa["enAnalisis"]), (c_admin, len(pa["paraAnalizar"]), len(pa["enAnalisis"])))
po = pendientes(oper)
check("la pendiente de justificación aparece en Para declarar del operario", d4 in codigos(po["paraDeclarar"]), codigos(po["paraDeclarar"]))

print("Horas de una detención existente (punto 5, API)")
c_tarde = crear(admin, "15:00:00", "15:30:00", sin_regla, "Control turno tarde F2")
t15 = detencion(c_tarde)["turnoId"]
c1 = crear(admin, "05:40:00", "08:05:00", sin_regla, "Horas reales F2")
b = detencion(c1)
check("alta con horas: CALCULADO y 145 min", b["origenTiempo"] == "CALCULADO" and b["minutosEfectivos"] == 145, b)
s, r = call("PUT", f"/api/detenciones/{c1}", admin, {"fecha": FECHA, "tipoFallaId": sin_regla, "descripcion": "Solo descripcion F2",
            "origenTiempo": "CALCULADO", "accionesRealizadas": b.get("accionesRealizadas"), "rowVersion": b["rowVersion"]})
a = detencion(c1)
check("editar sólo la descripción responde 200", s == 200, (s, r))
check("sólo la descripción cambió: origen, horas, minutos y turno iguales",
      a["descripcion"] == "Solo descripcion F2" and a["origenTiempo"] == "CALCULADO"
      and a.get("fechaHoraParada") == b.get("fechaHoraParada") and a.get("fechaHoraArranque") == b.get("fechaHoraArranque")
      and a["minutosEfectivos"] == 145 and a["turnoId"] == b["turnoId"], (b, a))
s, r = call("PUT", f"/api/detenciones/{c1}", admin, {"fecha": FECHA, "tipoFallaId": sin_regla, "descripcion": "Solo descripcion F2",
            "origenTiempo": "CALCULADO", "horaInicio": "05:40:00", "horaFin": "08:05:00",
            "accionesRealizadas": a.get("accionesRealizadas"), "rowVersion": a["rowVersion"]})
a2 = detencion(c1)
check("mismas horas no re-derivan el turno", s == 200 and a2["turnoId"] == b["turnoId"] and a2["minutosEfectivos"] == 145, a2)
s, r = call("PUT", f"/api/detenciones/{c1}", admin, {"fecha": FECHA, "tipoFallaId": sin_regla, "descripcion": "Solo descripcion F2",
            "origenTiempo": "CALCULADO", "horaInicio": "15:00:00", "horaFin": "15:30:00",
            "accionesRealizadas": a2.get("accionesRealizadas"), "rowVersion": a2["rowVersion"]})
a3 = detencion(c1)
check("horas nuevas recalculan y re-derivan el turno", s == 200 and a3["minutosEfectivos"] == 30 and a3["turnoId"] == t15, (s, a3))

c3 = crear(admin, "05:40:00", "08:05:00", sin_regla, "PLC F2")
db.execute("update Detenciones set OrigenTiempo='PLC' where DetencionId=?", (c3,))
db.commit()
p0 = detencion(c3)
s, r = call("PUT", f"/api/detenciones/{c3}", admin, {"fecha": FECHA, "tipoFallaId": sin_regla, "descripcion": "PLC F2 editada",
            "origenTiempo": "PLC", "accionesRealizadas": p0.get("accionesRealizadas"), "rowVersion": p0["rowVersion"]})
p1 = detencion(c3)
check("PLC sin horas nuevas conserva origen, horas, minutos y turno",
      s == 200 and p1["origenTiempo"] == "PLC" and p1.get("fechaHoraParada") == p0.get("fechaHoraParada")
      and p1["minutosEfectivos"] == 145 and p1["turnoId"] == p0["turnoId"] and p1["descripcion"] == "PLC F2 editada", (s, p1))

print("Horas en la zona de la planta (punto 6)")
if API_UTC:
    s, r = call("POST", "/api/detenciones", admin, {"lineaId": linea, "tipoFallaId": sin_regla, "fecha": FECHA,
                "descripcion": "Zona F2 servidor UTC", "horaInicio": "05:40:00", "horaFin": "08:05:00"}, base=API_UTC)
    check("API con TZ=UTC acepta la carga manual", s in (200, 201), (s, r))
    z = detencion(r["detencionId"])
    # La API devuelve las fechas en UTC: se convierte a hora de Argentina para comparar la hora de pared.
    parada = datetime.fromisoformat(z.get("fechaHoraParada")).astimezone(timezone(timedelta(hours=-3)))
    check("hora de inicio 05:40 es 05:40 en Argentina (el servidor corre en UTC)",
          parada.hour == 5 and parada.minute == 40, (z.get("fechaHoraParada"), str(parada)))
    check("minutos de la carga en zona UTC del servidor siguen en 145", z["minutosEfectivos"] == 145, z["minutosEfectivos"])
else:
    print("  (omitido: falta la API con TZ=UTC)")

print("Previsualización igual que el alta (punto 7)")
r_prev = regla({"lineaId": linea, "tipoFallaId": otros, "equipoId": None, "minutosMinimos": 50,
                "responsableRol": "OPERARIO", "responsableUsuarioId": None})
prev60 = {"lineaId": linea, "tipoFallaId": otros, "fecha": FECHA, "descripcion": "Preview F2",
          "origenTiempo": "CALCULADO", "fechaHoraParada": "2026-10-09T10:00:00-03:00",
          "fechaHoraArranque": "2026-10-09T11:00:00-03:00"}
s, pv = call("POST", "/api/detenciones/previsualizar-analisis", admin, prev60)
check("previsualización con CALCULADO y fechas (60 min) avisa análisis", s == 200 and pv["requiereAnalisis"] is True, (s, pv))
s, pv30 = call("POST", "/api/detenciones/previsualizar-analisis", admin,
               {**prev60, "fechaHoraArranque": "2026-10-09T10:30:00-03:00"})
check("previsualización con 30 min no avisa", s == 200 and pv30["requiereAnalisis"] is False, (s, pv30))
s, creada = call("POST", "/api/detenciones", admin, prev60)
check("el alta con las mismas fechas responde 201", s in (200, 201), (s, creada))
cr = detencion(creada["detencionId"])
check("el alta coincide con la previsualización (60 min y análisis)",
      cr["minutosEfectivos"] == 60 and cr["requiereAnalisis"] is True, cr)
desactivar(r_prev, {"lineaId": linea, "tipoFallaId": otros, "equipoId": None, "minutosMinimos": 50,
                    "responsableRol": "OPERARIO", "responsableUsuarioId": None})

print("Limpieza de reglas de prueba")
desactivar(r_ger, R_GER_BODY)
print("  reglas de prueba desactivadas")

print(f"\nRESULTADO: {ok} OK, {fail} FAIL")
sys.exit(1 if fail else 0)
