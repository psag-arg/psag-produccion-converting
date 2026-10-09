"""Capturas headless de la app PSAG. Uso: capturas.py <web_url> <outdir> <email> <password> ruta[=nombre] ...

Una ruta puede llevar acciones después de '|': click:<selector>, wait:<ms>, fill:<selector>=<valor>
"""
import sys, asyncio, pathlib
from playwright.async_api import async_playwright

web, out, email, pwd, *routes = sys.argv[1:]
out = pathlib.Path(out); out.mkdir(parents=True, exist_ok=True)

async def main():
    async with async_playwright() as p:
        b = await p.chromium.launch()
        pg = await b.new_page(viewport={"width": 1920, "height": 1080})
        errors = []
        pg.on("console", lambda m: errors.append(m.text) if m.type == "error" else None)
        await pg.goto(web + "/login")
        await pg.wait_for_selector("input[type=password]", timeout=60000)
        await pg.fill("#username", email)
        await pg.fill("#password", pwd)
        await pg.click("button[type=submit]")
        await pg.wait_for_url(lambda u: "/login" not in u, timeout=60000)
        await pg.wait_for_timeout(1500)
        for spec in routes:
            route, _, acts = spec.partition("|")
            route, _, name = route.partition("=")
            name = name or route.strip("/").replace("/", "_") or "inicio"
            await pg.goto(web + "/" + route.lstrip("/"))
            await pg.wait_for_timeout(2500)
            for a in filter(None, acts.split(";")):
                kind, _, arg = a.partition(":")
                if kind == "click": await pg.click(arg)
                elif kind == "wait": await pg.wait_for_timeout(int(arg))
                elif kind == "select":
                    sel, _, val = arg.rpartition("=")
                    await pg.locator(sel).first.select_option(val)
                elif kind == "press":
                    await pg.keyboard.press(arg)
                elif kind == "fill":
                    sel, _, val = arg.rpartition("=")
                    await pg.fill(sel, val)
                await pg.wait_for_timeout(800)
            await pg.screenshot(path=str(out / f"{name}.png"))
            print("ok", name, pg.url)
        if errors:
            print("console errors:", *errors[:10], sep="\n  ")
        await b.close()

asyncio.run(main())
