"""Verificación en pantalla (headless) de la edición de detenciones, punto 5 de F2.

Uso: web_f2.py <web_url> <api_url> <db.sqlite> <outdir>
Requiere fixes_f2.py corrido sobre la misma base (usa "Solo descripcion F2" y "PLC F2 editada").
"""
import sys, json, sqlite3, asyncio, pathlib, urllib.request, urllib.error
from playwright.async_api import async_playwright

WEB, API, DB, OUT = sys.argv[1].rstrip("/"), sys.argv[2].rstrip("/"), sys.argv[3], pathlib.Path(sys.argv[4])
OUT.mkdir(parents=True, exist_ok=True)
ok = fail = 0


def check(name, cond, detail=""):
    global ok, fail
    if cond:
        ok += 1
        print("  OK  ", name)
    else:
        fail += 1
        print("  FAIL", name, "->", str(detail)[:400])


def api(method, path, token=None, body=None):
    req = urllib.request.Request(API + path, method=method,
                                 data=json.dumps(body).encode() if body is not None else None)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as r:
            d = r.read()
            return r.status, json.loads(d) if d else None
    except urllib.error.HTTPError as e:
        return e.code, e.read().decode(errors="replace")[:300]


_, login = api("POST", "/api/auth/login", body={"usuario": "admin", "password": "Admin123!"})
TOKEN = login.get("token") or login.get("accessToken")

db = sqlite3.connect(DB)
# Las descripciones cambian en cada corrida: se busca por cualquiera de las que pudo tener.
c1_codigo, c1_id = db.execute("select Codigo, DetencionId from Detenciones where Descripcion in ('Horas reales F2', 'Solo descripcion F2', 'Editado desde la pantalla F2') order by DetencionId desc").fetchone()
c3_codigo, c3_id = db.execute("select Codigo, DetencionId from Detenciones where Descripcion in ('PLC F2 editada', 'PLC F2 editada desde la pantalla') order by DetencionId desc").fetchone()
t15 = db.execute("select TurnoId from Detenciones where Descripcion='Control turno tarde F2'").fetchone()[0]
t06 = db.execute("select TurnoId from Detenciones where Descripcion='Carro trabado F2 uno editado'").fetchone()[0]


def detalle(i):
    return api("GET", f"/api/detenciones/{i}", TOKEN)[1]


async def abrir(pg, codigo):
    # Se toma la fila de ESTE código (no la primera de la lista), así no se edita otra detención.
    await pg.goto(WEB + "/detenciones")
    await pg.wait_for_selector("input[placeholder^='Codigo']", timeout=60000)
    await pg.fill("input[placeholder^='Codigo']", codigo)
    fila = pg.locator("tr", has_text=codigo).filter(has=pg.locator("button[title=Editar]")).first
    await fila.wait_for(timeout=20000)
    await fila.locator("button[title=Editar]").click()
    await pg.wait_for_selector(".modal.show", timeout=30000)
    await pg.wait_for_timeout(800)


async def guardar(pg):
    await pg.locator(".modal.show .modal-footer button.btn-primary").click()
    await pg.wait_for_timeout(2500)


async def main():
    global ok, fail
    antes_c1 = detalle(c1_id)
    async with async_playwright() as p:
        b = await p.chromium.launch()
        # El operador opera desde Argentina: el navegador muestra y lee las horas en esa zona.
        ctx = await b.new_context(viewport={"width": 1440, "height": 900}, timezone_id="America/Argentina/Buenos_Aires")
        pg = await ctx.new_page()
        await pg.goto(WEB + "/login")
        await pg.wait_for_selector("input[type=password]", timeout=60000)
        await pg.fill("#username", "admin")
        await pg.fill("#password", "Admin123!")
        await pg.click("button[type=submit]")
        await pg.wait_for_url(lambda u: "/login" not in u, timeout=60000)

        print("Edición de una detención con horas reales (punto 5, pantalla)")
        await abrir(pg, c1_codigo)
        horas = pg.locator(".modal.show input[type=time]")
        check("el modal muestra la hora de inicio cargada (15:00)", await horas.nth(0).input_value() == "15:00",
              await horas.nth(0).input_value())
        check("el modal muestra la hora de fin cargada (15:30)", await horas.nth(1).input_value() == "15:30",
              await horas.nth(1).input_value())
        await pg.screenshot(path=str(OUT / "editar-calculada.png"), full_page=True)
        await pg.locator(".modal.show textarea").fill("Editado desde la pantalla F2")
        await guardar(pg)
        check("guardar sólo la descripción cierra el modal", await pg.locator(".modal.show").count() == 0)
        despues = detalle(c1_id)
        check("tras guardar la descripción, horas, minutos y turno quedan iguales",
              despues["fechaHoraParada"] == antes_c1["fechaHoraParada"]
              and despues["fechaHoraArranque"] == antes_c1["fechaHoraArranque"]
              and despues["minutosEfectivos"] == antes_c1["minutosEfectivos"]
              and despues["turnoId"] == antes_c1["turnoId"] and despues["origenTiempo"] == "CALCULADO",
              (antes_c1, despues))
        check("la descripción sí se guardó", despues["descripcion"] == "Editado desde la pantalla F2", despues["descripcion"])

        print("Cambiar la hora desde la pantalla recalcula y re-deriva el turno")
        await abrir(pg, c1_codigo)
        horas = pg.locator(".modal.show input[type=time]")
        await horas.nth(0).fill("06:00")
        await horas.nth(1).fill("06:45")
        await pg.wait_for_timeout(800)
        await guardar(pg)
        cambiada = detalle(c1_id)
        check("horas cambiadas: 45 minutos y turno de la nueva hora de inicio (06:00)",
              cambiada["minutosEfectivos"] == 45 and cambiada["turnoId"] == t06, (cambiada["minutosEfectivos"], cambiada["turnoId"], t06))

        print("No se pueden quitar las horas de una detención cargada con horas")
        await abrir(pg, c1_codigo)
        horas = pg.locator(".modal.show input[type=time]")
        await horas.nth(0).fill("")
        await horas.nth(1).fill("")
        await pg.wait_for_timeout(500)
        await guardar(pg)
        mensaje = await pg.locator(".modal.show .alert-danger").count()
        check("muestra el error y el modal sigue abierto",
              mensaje == 1 and await pg.locator(".modal.show").count() == 1)
        check("la detención no cambió", detalle(c1_id)["minutosEfectivos"] == 45)
        await pg.locator(".modal.show button:has-text('Cancelar')").click()
        await pg.wait_for_timeout(500)

        print("Detención del PLC: horas y minutos de solo lectura")
        await abrir(pg, c3_codigo)
        horas = pg.locator(".modal.show input[type=time]")
        check("hora de inicio del PLC deshabilitada", await horas.nth(0).is_disabled())
        check("hora de fin del PLC deshabilitada", await horas.nth(1).is_disabled())
        texto = await pg.locator(".modal.show").inner_text()
        check("minutos del PLC se muestran como dato del PLC", "del PLC" in texto, texto[:200])
        await pg.screenshot(path=str(OUT / "editar-plc.png"), full_page=True)
        await pg.locator(".modal.show textarea").fill("PLC F2 editada desde la pantalla")
        await guardar(pg)
        plc = detalle(c3_id)
        check("guardar el PLC conserva origen, horas y minutos",
              plc["origenTiempo"] == "PLC" and plc["minutosEfectivos"] == 145
              and plc["descripcion"] == "PLC F2 editada desde la pantalla", plc)

        await b.close()

    print(f"\nRESULTADO: {ok} OK, {fail} FAIL")
    raise SystemExit(1 if fail else 0)


asyncio.run(main())
