"""Spec 005 T013: usuários virtuais do console pela borda (nginx + BFF). Mede latência por rota, erros, comandos
(aceito → concluído) e convergência do inventário depois do comando.

Uso (imagem Playwright do projeto): python vusers.py <base> <usuários> <minutos> <saída.json>
Variáveis: KC_USER, KC_PASS (login real no Keycloak); LOAD_ENV_PREFIX (prefixo do nome dos hosts de carga, padrão "Carga").
"""
import asyncio, json, os, random, statistics, sys, time, uuid
from playwright.async_api import async_playwright

BASE, USERS, MINUTES, OUT = sys.argv[1].rstrip("/"), int(sys.argv[2]), float(sys.argv[3]), sys.argv[4]
PREFIX = os.environ.get("LOAD_ENV_PREFIX", "Carga")
READS = [  # (rota, peso): telas mais usadas pesam mais
    ("/api/v1/overview", 6), ("/api/v1/containers?limit=50", 6), ("/api/v1/environments", 4), ("/api/v1/events?limit=50", 3),
    ("/api/v1/metrics/overview?window=1h", 3), ("/api/v1/metrics/hosts", 2), ("/api/v1/alerts/summary", 3), ("/api/v1/images?limit=50", 1),
    ("/api/v1/volumes?limit=50", 1), ("/api/v1/networks?limit=50", 1), ("{detail}", 3), ("{summary}", 2),
]
latency, errors, commands, convergence = {}, {}, [], []

def pick():
    return random.choices([r for r, _ in READS], [w for _, w in READS])[0]

async def user(ctx, containers, deadline, index):
    rnd = random.Random(index)
    next_command = time.monotonic() + rnd.uniform(20, 60)
    while time.monotonic() < deadline:
        route = pick()
        eid, c = rnd.choice(containers)
        path = route.replace("{detail}", f"/api/v1/environments/{eid}/containers/{c}").replace("{summary}", f"/api/v1/environments/{eid}/containers/{c}/summary")
        key = route if "{" not in route else route.strip("{}")
        t0 = time.perf_counter()
        try:
            r = await ctx.get(BASE + path, timeout=30000)
            ok = r.ok
        except Exception:
            ok = False
        latency.setdefault(key, []).append((time.perf_counter() - t0) * 1000)
        if not ok: errors[key] = errors.get(key, 0) + 1
        if time.monotonic() >= next_command:
            next_command = time.monotonic() + rnd.uniform(45, 75)
            await command(ctx, rnd.choice(containers))
        await asyncio.sleep(rnd.uniform(1.0, 3.0))

async def command(ctx, target):
    eid, cid = target
    item = await (await ctx.get(f"{BASE}/api/v1/environments/{eid}/containers/{cid}")).json()
    state = (item.get("state") or "").lower()
    action = "start" if state in ("exited", "created") else "unpause" if state == "paused" else "restart"
    token = (await (await ctx.get(BASE + "/bff/antiforgery")).json())["requestToken"]
    t0 = time.perf_counter()
    r = await ctx.post(f"{BASE}/api/v1/environments/{eid}/containers/{cid}/commands", data=json.dumps({
        "action": action, "expectedContainerRevision": item["revision"], "justification": "Teste de carga da spec 005",
        "deadlineUtc": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(time.time() + 110))}),
        headers={"Content-Type": "application/json", "X-Dokpod-Antiforgery": token, "Origin": BASE.split("/dokpod")[0], "Idempotency-Key": str(uuid.uuid4())})
    if r.status != 202:
        errors["command_submit"] = errors.get("command_submit", 0) + 1
        return
    command_id = (await r.json())["commandId"]
    state_out = "timeout"
    while time.perf_counter() - t0 < 60:
        s = await (await ctx.get(f"{BASE}/api/v1/environments/{eid}/commands/{command_id}")).json()
        if s["state"] in ("succeeded", "failed", "indeterminate", "expired", "rejected"):
            state_out = s["state"]; break
        await asyncio.sleep(0.25)
    done = time.perf_counter()
    commands.append({"action": action, "state": state_out, "ms": (done - t0) * 1000})
    if state_out != "succeeded":
        errors["command_result"] = errors.get("command_result", 0) + 1
        return
    expected = "paused" if action == "pause" else "running"
    while time.perf_counter() - done < 60:
        item = await (await ctx.get(f"{BASE}/api/v1/environments/{eid}/containers/{cid}")).json()
        if (item.get("state") or "").lower() == expected and item.get("revision") != s.get("observedContainerRevision", "x"):
            convergence.append((time.perf_counter() - done) * 1000); return
        await asyncio.sleep(0.5)
    convergence.append(60000.0)

def summary(values):
    if not values: return None
    v = sorted(values)
    q = lambda p: v[min(len(v) - 1, int(round(p * (len(v) - 1))))]
    return {"n": len(v), "p50": round(q(0.50), 1), "p95": round(q(0.95), 1), "p99": round(q(0.99), 1), "max": round(v[-1], 1)}

async def main():
    async with async_playwright() as p:
        browser = await p.chromium.launch()
        page = await (await browser.new_context(ignore_https_errors=True)).new_page()
        await page.goto(BASE + "/bff/login?returnUrl=%2Fdokpod%2F")
        await page.locator("#username").fill(os.environ["KC_USER"]); await page.locator("#password").fill(os.environ["KC_PASS"]); await page.locator("#kc-login").click()
        await page.wait_for_url("**/dokpod/**")
        state = await page.context.storage_state()
        setup = await p.request.new_context(ignore_https_errors=True, storage_state=state)
        envs, cursor = [], None
        while True:
            body = await (await setup.get(BASE + "/api/v1/environments?limit=100" + (f"&cursor={cursor}" if cursor else ""))).json()
            envs += [e for e in body["environments"] if e["name"].startswith(PREFIX) and (e.get("agent") or {}).get("connected")]
            cursor = body.get("nextCursor")
            if not cursor: break
        containers = []
        for e in envs:
            for attempt in range(5):
                r = await setup.get(f"{BASE}/api/v1/environments/{e['environmentId']}/containers")
                if r.ok: break
                print(f"preparo: {e['name']} respondeu {r.status} ({(await r.text())[:160]}); nova tentativa", flush=True)
                await asyncio.sleep(2)
            containers += [(e["environmentId"], c["containerId"]) for c in (await r.json()).get("containers", [])]
        print(f"{len(envs)} hosts de carga, {len(containers)} containers; {USERS} usuários por {MINUTES} min", flush=True)
        contexts = [await p.request.new_context(ignore_https_errors=True, storage_state=state) for _ in range(USERS)]
        deadline = time.monotonic() + MINUTES * 60
        started = time.time()
        await asyncio.gather(*(user(contexts[i], containers, deadline, i) for i in range(USERS)))
        total = sum(len(v) for v in latency.values())
        all_reads = [x for v in latency.values() for x in v]
        result = {
            "startedAt": time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(started)), "minutes": MINUTES, "users": USERS, "hosts": len(envs), "containers": len(containers),
            "reads": summary(all_reads), "requests": total, "errors": errors, "errorRate": round(sum(errors.values()) / max(1, total + len(commands)), 5),
            "routes": {k: summary(v) for k, v in sorted(latency.items())},
            "commands": summary([c["ms"] for c in commands]), "commandStates": {s: sum(1 for c in commands if c["state"] == s) for s in {c["state"] for c in commands}},
            "convergence": summary(convergence),
        }
        json.dump(result, open(OUT, "w"), indent=1)
        print(json.dumps({k: result[k] for k in ("reads", "errorRate", "commands", "commandStates", "convergence")}, ensure_ascii=False), flush=True)
        await browser.close()

asyncio.run(main())
