"""Spec 005 T016: comandos concorrentes para os cenários de recuperação.

  python recovery.py submit <base> <saida.json>   um restart por host de carga conectado, em paralelo
  python recovery.py wait   <base> <saida.json>   espera todos terminarem (até 5 min) e resume os estados

Variáveis: KC_USER, KC_PASS, LOAD_ENV_PREFIX (padrão "Carga").
"""
import asyncio, json, os, sys, time, uuid
from playwright.async_api import async_playwright

MODE, BASE, OUT = sys.argv[1], sys.argv[2].rstrip("/"), sys.argv[3]
PREFIX = os.environ.get("LOAD_ENV_PREFIX", "Carga")
TERMINAL = ("succeeded", "failed", "indeterminate", "expired", "rejected")

async def login(p):
    browser = await p.chromium.launch()
    page = await (await browser.new_context(ignore_https_errors=True)).new_page()
    await page.goto(BASE + "/bff/login?returnUrl=%2Fdokpod%2F")
    await page.locator("#username").fill(os.environ["KC_USER"]); await page.locator("#password").fill(os.environ["KC_PASS"]); await page.locator("#kc-login").click()
    await page.wait_for_url("**/dokpod/**")
    return browser, await p.request.new_context(ignore_https_errors=True, storage_state=await page.context.storage_state())

async def submit(ctx):
    envs, cursor = [], None
    while True:
        body = await (await ctx.get(BASE + "/api/v1/environments?limit=100" + (f"&cursor={cursor}" if cursor else ""))).json()
        envs += [e for e in body["environments"] if e["name"].startswith(PREFIX) and (e.get("agent") or {}).get("connected")]
        cursor = body.get("nextCursor")
        if not cursor: break
    targets = []
    for e in envs:
        running = [c for c in (await (await ctx.get(f"{BASE}/api/v1/environments/{e['environmentId']}/containers")).json())["containers"] if c["state"].lower() == "running"]
        if running: targets.append((e["environmentId"], running[0]))
    token = (await (await ctx.get(BASE + "/bff/antiforgery")).json())["requestToken"]
    async def one(eid, c):
        r = await ctx.post(f"{BASE}/api/v1/environments/{eid}/containers/{c['containerId']}/commands", data=json.dumps({
            "action": "restart", "expectedContainerRevision": c["revision"], "justification": "Recuperação da spec 005",
            "deadlineUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(time.time() + 240))}),
            headers={"Content-Type": "application/json", "X-Dokpod-Antiforgery": token, "Origin": BASE.split("/dokpod")[0], "Idempotency-Key": str(uuid.uuid4())})
        return {"environmentId": eid, "containerId": c["containerId"], "status": r.status, "commandId": (await r.json()).get("commandId") if r.status == 202 else None}
    t0 = time.perf_counter()
    sent = await asyncio.gather(*(one(eid, c) for eid, c in targets))
    json.dump({"submittedAt": time.time(), "commands": sent}, open(OUT, "w"))
    print(f"{sum(1 for s in sent if s['status'] == 202)}/{len(sent)} comandos aceitos em {time.perf_counter() - t0:.1f} s", flush=True)

async def wait(ctx):
    data = json.load(open(OUT)); pending = {c["commandId"]: c for c in data["commands"] if c["commandId"]}
    states, end = {}, time.time() + 300
    while pending and time.time() < end:
        for cid, c in list(pending.items()):
            try:
                r = await ctx.get(f"{BASE}/api/v1/environments/{c['environmentId']}/commands/{cid}")
                if r.ok and (s := await r.json())["state"] in TERMINAL:
                    states[cid] = s["state"]; c["state"] = s["state"]; c["failureCode"] = s.get("failureCode"); del pending[cid]
            except Exception:
                pass
        await asyncio.sleep(2)
    for cid in pending: states[cid] = "nao_terminal"
    data["result"] = {s: sum(1 for v in states.values() if v == s) for s in sorted(set(states.values()))}
    data["finishedAt"] = time.time()
    json.dump(data, open(OUT, "w"))
    print(f"estados finais: {data['result']} em {data['finishedAt'] - data['submittedAt']:.0f} s desde o envio", flush=True)

async def main():
    async with async_playwright() as p:
        browser, ctx = await login(p)
        await (submit(ctx) if MODE == "submit" else wait(ctx))
        await browser.close()

asyncio.run(main())
