"""Plano de controle simulado para E2E de UI com respostas simuladas.

Serve as rotas do BFF (`/bff/*`) e da API (`/api/v1/*`) com dados derivados dos mockups de
`docs/design/app`. Roda atrás do nginx containerizado (`nginx.conf`), nunca acessado diretamente
pelo navegador. Somente biblioteca padrão; sem credenciais.

Cenários (POST /__mock/scenario {"name": ...}):
- `default`: sessão autenticada, todos os scopes;
- `anonymous`: sessão não autenticada;
- `readonly`: somente `environment:read`;
- `expire-next`: a próxima chamada `/api/*` responde 401;
- `empty`: nenhum host nem container.
"""

from __future__ import annotations

import json
import os
import re
import uuid
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

NOW = datetime.now(timezone.utc).replace(microsecond=0)
ALL_SCOPES = [
    "environment:read", "environment:manage", "container:start", "container:stop", "container:restart",
    "container:delete", "container:pause", "container:inspect", "container:logs", "image:pull",
    "container:create", "container:exec", "audit:read",
]
STATE: dict = {"scenario": "default", "commands": {}, "stages": {}, "registered": {}, "revoked": set()}


def iso(delta_minutes: float = 0) -> str:
    return (NOW - timedelta(minutes=delta_minutes)).isoformat().replace("+00:00", "Z")


def env_id(n: int) -> str:
    return f"00000000-0000-4000-8000-{n:012d}"


HOSTS = [
    # n, nome, estágio, engine, versão, situação, cpus, memória GiB, rootless
    (1, "srv-prod-01", "production", "docker", "27.3.1", "up_to_date", 16, 64, False),
    (2, "srv-prod-02", "production", "docker", "27.3.1", "up_to_date", 16, 64, False),
    (3, "srv-prod-03", "production", "podman", "5.2.3", "up_to_date", 8, 32, True),
    (4, "srv-prod-04", "production", "podman", "5.2.3", "update_required", 8, 32, True),
    (5, "srv-batch-01", "production", "podman", "5.2.3", "stale", 8, 16, True),
    (6, "srv-hml-01", "staging", "docker", "27.2.0", "up_to_date", 8, 16, False),
    (7, "srv-dev-01", "development", "docker", "27.3.1", "disconnected", 4, 8, False),
]

CONTAINERS = [
    # nome, host n, imagem, status, cpu-proxy, restarts, portas
    ("api-pedidos-7f9c", 3, "registry.empresa.com/pedidos/api:3.14.2", "running", 2, [(8081, 80)]),
    ("checkout-web", 1, "registry.empresa.com/loja/checkout:5.2.0", "running", 0, [(8080, 8080)]),
    ("postgres-principal", 2, "docker.io/library/postgres:16.4", "running", 0, [(5432, 5432)]),
    ("redis-sessoes", 2, "docker.io/library/redis:7.4-alpine", "running", 0, [(6379, 6379)]),
    ("gateway-01", 1, "registry.empresa.com/edge/gateway:2.8.1", "restarting", 5, [(443, 8443)]),
    ("prometheus", 4, "quay.io/prometheus/prometheus:v2.54.1", "running", 0, [(9090, 9090)]),
    ("migracao-catalogo", 5, "registry.empresa.com/loja/catalogo-migrate:2.1.0", "failed", 3, []),
    ("relatorios-bi", 5, "registry.empresa.com/bi/relatorios:1.9.0", "stopped", 0, []),
    ("worker-notificacoes", 3, "registry.empresa.com/pedidos/worker:3.14.2", "paused", 0, []),
    ("api-pedidos-hml", 6, "registry.empresa.com/pedidos/api:3.15.0-rc1", "running", 0, [(8081, 80)]),
    ("checkout-dev", 7, "registry.empresa.com/loja/checkout:5.3.0-dev", "stopped", 0, []),
]

STATE_FOR = {"running": "running", "restarting": "restarting", "failed": "exited", "stopped": "exited", "paused": "paused", "starting": "created"}
ACTIONS_FOR = {
    "running": ["stop", "restart", "pause", "delete"],
    "restarting": ["stop", "delete"],
    "failed": ["start", "delete"],
    "stopped": ["start", "delete"],
    "paused": ["unpause", "stop", "delete"],
    "starting": ["stop"],
}


def scopes() -> list[str]:
    return ["environment:read"] if STATE["scenario"] == "readonly" else ALL_SCOPES


def host_view(h) -> dict:
    n, name, stage, engine, version, situation, cpus, mem, rootless = h
    connected = situation not in ("disconnected", "disabled")
    return {
        "environmentId": env_id(n),
        "name": name,
        "host": f"{name}.empresa.local",
        "enabled": situation != "disabled",
        "scopes": scopes(),
        "stage": stage,
        "situation": situation,
        "containerCount": sum(1 for c in all_containers() if c[1] == n),
        "agent": {
            "connected": connected,
            "sessionStartedAt": iso(60 * 26) if connected else None,
            "lastSeenAt": iso(0.2 if connected else 95),
            "agentVersion": "0.9.0" if situation != "update_required" else "0.8.2",
            "engine": engine,
            "engineVersion": version,
            "engineApiVersion": "1.47" if engine == "docker" else "5.2.3",
            "engineRootless": rootless,
            "operatingSystem": "Ubuntu",
            "operatingSystemVersion": "24.04",
            "architecture": "x86_64",
            "hostName": name,
            "cpuCount": cpus,
            "memoryBytes": mem * 1024**3,
            "capabilities": ["CONTAINER_LIFECYCLE", "ENGINE_EVENTS", "RESOURCE_METRICS", "CONTAINER_PAUSE", "CONTAINER_UNPAUSE", "CONTAINER_INSPECT", "CONTAINER_LOGS"]
            + ([] if situation == "update_required" else ["ImageInventory", "ImagePull", "ContainerCreate"]),
        },
    }


LARGE_HOSTS = [
    (100 + i, f"srv-lab-{i:02d}", ("production", "staging", "development")[i % 3], "podman" if i % 3 == 0 else "docker",
     "5.2.3" if i % 3 == 0 else "27.3.1", "up_to_date", 8, 32, i % 3 == 0)
    for i in range(56)
]
LARGE_CONTAINERS = [
    (f"lab-{h[0]}-{c:02d}", h[0], "registry.empresa.com/lab/app:1.0.0",
     ("running", "running", "running", "stopped", "failed", "paused")[c % 6], 0, [])
    for h in LARGE_HOSTS for c in range(20)
]


def all_hosts():
    return LARGE_HOSTS if STATE["scenario"] == "large" else HOSTS


def all_containers():
    return LARGE_CONTAINERS if STATE["scenario"] == "large" else CONTAINERS + STATE.get("created", [])


def hosts() -> list[dict]:
    if STATE["scenario"] == "empty":
        return []
    views = [host_view(h) for h in all_hosts()]
    for view in views:
        view["stage"] = STATE["stages"].get(view["environmentId"], view["stage"])
        if view["environmentId"] in STATE["revoked"]:
            view["situation"] = "disconnected"
            view["agent"]["connected"] = False
    return views + list(STATE["registered"].values())


def host_by_n(n: int):
    return next(h for h in all_hosts() if h[0] == n)


def container_item(c) -> dict:
    name, n, image, status, restarts, ports = c
    h = host_by_n(n)
    cid = (name.encode().hex() * 4)[:64]
    connected = h[5] not in ("disconnected", "disabled")
    return {
        "containerId": cid,
        "name": name,
        "imageReference": image,
        "state": STATE_FOR[status],
        "revision": f"rev-{cid[:8]}",
        "observedAt": iso(0.5),
        "displayStatus": status,
        "createdAt": iso(60 * 24 * 3),
        "startedAt": iso(60 * 5) if status in ("running", "paused", "restarting") else None,
        "health": "healthy" if status == "running" else ("unhealthy" if status == "restarting" else "none"),
        "restartCount": restarts,
        "exitCode": 1 if status == "failed" else (0 if status == "stopped" else None),
        "oomKilled": False,
        "ports": [{"hostIp": "0.0.0.0", "hostPort": hp, "containerPort": cp, "protocol": "tcp"} for hp, cp in ports],
        "environmentId": env_id(n),
        "environmentName": h[1],
        "stage": h[2],
        "engine": h[3],
        "engineVersion": h[4],
        "hostConnected": connected,
        "allowedActions": [a for a in ACTIONS_FOR[status] if f"container:{a if a != 'unpause' else 'pause'}" in scopes()] if connected else [],
    }


def stable(text: str, modulo: int) -> int:
    return int.from_bytes(text.encode()[:8].ljust(8, b"\0"), "little") % modulo


def with_resources(item: dict) -> dict:
    """Spec 009: recursos atuais só para containers em execução cujo host publica métricas."""
    if item["displayStatus"] in ("running", "starting", "restarting") and item["hostConnected"] and STATE["scenario"] != "no-metrics":
        item["resources"] = {
            "cpuPercent": float(5 + stable(item["name"], 80)),
            "memoryUsedBytes": (64 + stable(item["name"][::-1], 900)) * 1024 * 1024,
            "memoryLimitBytes": 1024 * 1024 * 1024,
            "sampledAt": iso(0.2),
        }
    return item


def host_cpu(host: dict) -> float | None:
    if not host["agent"] or not host["agent"]["connected"] or STATE["scenario"] == "no-metrics":
        return None
    return float(20 + stable(host["name"], 70))


def stage_metrics(stage: str | None) -> dict:
    hs = [h for h in hosts() if h["enabled"] and (not stage or h["stage"] == stage)]
    sampled = [h for h in hs if host_cpu(h) is not None]
    cores = sum(h["agent"]["cpuCount"] for h in sampled) or 1
    cpu = round(sum(host_cpu(h) * h["agent"]["cpuCount"] for h in sampled) / cores, 1) if sampled else None
    memory = 61.2 if sampled else None
    x = [(NOW.replace(minute=0, second=0) - timedelta(hours=11 - i)).isoformat().replace("+00:00", "Z") for i in range(12)]
    cpu_series = [None if (i == 4 or not sampled) else round(30 + i * 1.6, 1) for i in range(12)]
    mem_series = [None if (i == 4 or not sampled) else round(55 + i * 0.6, 1) for i in range(12)]
    return {
        "current": {"cpuPercent": cpu, "memoryPercent": memory, "hostsSampled": len(sampled), "hostsTotal": len(hs), "sampledAt": iso(0.2) if sampled else None},
        "series": {"x": x, "cpu": cpu_series, "memory": mem_series},
        "operationalLimitPercent": 80,
    }


def containers() -> list[dict]:
    return [] if STATE["scenario"] == "empty" else [with_resources(container_item(c)) for c in all_containers()]


def find_container(environment_id: str, container_id: str):
    return next((c for c in containers() if c["environmentId"] == environment_id and c["containerId"] == container_id), None)


def count_map(values) -> dict:
    out: dict = {}
    for v in values:
        out[v] = out.get(v, 0) + 1
    return out


def events() -> list[dict]:
    if STATE["scenario"] in ("empty", "large"):
        return []
    rows = [
        ("container.died", "error", 4, "migracao-catalogo", 5, {"exitCode": "1"}),
        ("container.auto_restarted", "warning", 9, "gateway-01", 1, {"restartCount": "5"}),
        ("engine.version_outdated", "warning", 22, None, 6, {"engine": "docker", "version": "27.2.0"}),
        ("command.succeeded", "success", 31, "api-pedidos-7f9c", 3, {"action": "restart"}),
        ("image.pulled", "success", 48, None, 3, {"image": "registry.empresa.com/pedidos/api:3.14.2"}),
        ("agent.disconnected", "warning", 95, None, 7, {}),
    ]
    return [
        {
            "eventId": str(uuid.UUID(int=i + 1)),
            "environmentId": env_id(n),
            "environmentName": host_by_n(n)[1],
            "type": t,
            "severity": sev,
            "occurredAt": iso(minutes),
            "receivedAt": iso(minutes),
            "clockSkewDetected": False,
            "subjectKind": "container" if t.startswith(("container.", "command.")) else None,
            "subjectId": subject,
            "subjectName": subject,
            "parameters": params,
            "aggregatedCount": 1,
        }
        for i, (t, sev, minutes, subject, n, params) in enumerate(rows)
    ]


def overview(stage: str | None) -> dict:
    hs = [h for h in hosts() if not stage or h["stage"] == stage]
    cs = [c for c in containers() if not stage or c["stage"] == stage]
    engines = []
    for engine in ("docker", "podman"):
        eh = [h for h in hs if h["agent"]["engine"] == engine]
        if not eh:
            continue
        versions = count_map(h["agent"]["engineVersion"] for h in eh)
        engines.append({
            "engine": engine,
            "hosts": len(eh),
            "containers": sum(1 for c in cs if c["engine"] == engine),
            "versions": [{"version": v, "hosts": k} for v, k in sorted(versions.items())],
        })
    return {
        "stage": stage,
        "observedAt": iso(0.3),
        "hosts": {"total": len(hs), "bySituation": count_map(h["situation"] for h in hs)},
        "containers": {"total": len(cs), "byDisplayStatus": count_map(c["displayStatus"] for c in cs)},
        "engines": engines,
        "attentionEvents24h": sum(1 for e in events() if e["severity"] in ("error", "warning") and (not stage or host_stage(e["environmentId"]) == stage)),
        "images": (lambda rows: {k: v for k, v in images_summary(rows, {h["environmentId"] for h in hs}).items() if k != "totalSizeBytes"})(
            [i for i in images() if not stage or i["stage"] == stage]),
    }


EXTRA_IMAGES = {
    # imagens sem uso por host (spec 012)
    1: [("registry.empresa.com/loja/checkout:5.1.9", 310), ("docker.io/library/nginx:1.27", 188)],
    2: [("docker.io/library/postgres:15.8", 412)],
    3: [("registry.empresa.com/pedidos/api:3.14.1", 240), ("<none>", 96)],
    6: [("docker.io/library/nginx:1.27", 188)],
}


def images() -> list[dict]:
    """Inventário de imagens: uma por imagem de container (em uso) mais as sem uso; host 4 (agente 0.8.2) não tem inventário."""
    rows: dict[tuple, dict] = {}
    by_n = {h[0]: h for h in all_hosts()}
    for c in all_containers():
        n, ref = c[1], c[2]
        if by_n[n][5] == "update_required":
            continue
        key = (n, ref)
        if key in rows:
            rows[key]["containers"] += 1
            continue
        rows[key] = {"n": n, "ref": ref, "size": 120 + stable(ref, 400), "containers": 1}
    if STATE["scenario"] != "large":
        for n, extra in EXTRA_IMAGES.items():
            for ref, size in extra:
                rows.setdefault((n, ref), {"n": n, "ref": ref, "size": size, "containers": 0})
    for n, ref in STATE.get("pulled", []):
        rows.setdefault((n, ref), {"n": n, "ref": ref, "size": 150, "containers": 0})
    result = []
    for (n, ref), r in rows.items():
        h = by_n[n]
        digest_seed = f"{n}-{ref}"
        image_id = "sha256:" + (uuid.uuid5(uuid.NAMESPACE_URL, digest_seed).hex * 2)
        canonical = ref if ref == "<none>" else canonical_reference(ref)
        tags = [] if ref == "<none>" else [canonical]
        repository, _, tag = canonical.rpartition(":") if tags else (None, None, None)
        result.append({
            "environmentId": env_id(n), "environmentName": h[1], "stage": STATE["stages"].get(env_id(n), h[2]), "engine": h[3],
            "imageId": image_id, "repository": repository, "tag": tag, "repoTags": tags, "repoDigests": [],
            "sizeBytes": r["size"] * 1024**2, "createdAt": iso(60 * 24 * (3 + stable(ref, 40))), "architecture": "amd64", "os": "linux",
            "truncated": False, "containers": r["containers"], "inUse": r["containers"] > 0, "observedAt": iso(0.5),
            "canPull": "image:pull" in scopes() and h[5] not in ("disconnected", "update_required"),
        })
    return result


def canonical_reference(ref: str) -> str:
    name, tag = (ref.rsplit(":", 1) + ["latest"])[:2] if ":" in ref.split("/")[-1] else (ref, "latest")
    parts = name.split("/")
    if len(parts) == 1 or not ("." in parts[0] or ":" in parts[0] or parts[0] == "localhost"):
        parts = ["docker.io"] + (["library"] + parts if len(parts) == 1 else parts)
    return "/".join(parts) + ":" + tag


REFERENCE = re.compile(r"^(?:[a-z0-9.-]+(?::[0-9]+)?/)?[a-z0-9]+(?:[._-]+[a-z0-9]+)*(?:/[a-z0-9]+(?:[._-]+[a-z0-9]+)*)*(?::[\w][\w.-]{0,127})?(?:@sha256:[a-f0-9]{64})?$")


def images_summary(rows: list[dict], host_ids: set[str]) -> dict:
    partial = any(h[5] == "update_required" for h in all_hosts() if env_id(h[0]) in host_ids)
    return {"total": len(rows), "unused": sum(1 for r in rows if not r["inUse"]), "totalSizeBytes": sum(r["sizeBytes"] for r in rows), "partial": partial}


def host_stage(environment_id: str) -> str | None:
    return next((h["stage"] for h in hosts() if h["environmentId"] == environment_id), None)


def problem(status: int, title: str, code: str | None = None) -> dict:
    return {"type": "about:blank", "title": title, "status": status, "detail": title, "code": code}


class Handler(BaseHTTPRequestHandler):
    server_version = "DokpodMock/1"

    def log_message(self, fmt: str, *args) -> None:  # silencioso salvo DOKPOD_MOCK_VERBOSE
        if os.environ.get("DOKPOD_MOCK_VERBOSE"):
            super().log_message(fmt, *args)

    def send_json(self, status: int, payload, content_type: str = "application/json") -> None:
        body = b"" if payload is None else json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Cache-Control", "no-store")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def read_body(self) -> dict:
        length = int(self.headers.get("Content-Length") or 0)
        if not length:
            return {}
        try:
            return json.loads(self.rfile.read(length) or b"{}")
        except json.JSONDecodeError:
            return {}

    def do_GET(self) -> None:
        self.route("GET")

    def do_POST(self) -> None:
        self.route("POST")

    def do_PUT(self) -> None:
        self.route("PUT")

    def route(self, method: str) -> None:
        url = urlparse(self.path)
        path = url.path
        query = parse_qs(url.query)
        q = lambda key: (query.get(key) or [None])[0]  # noqa: E731

        if path == "/__mock/scenario" and method == "POST":
            STATE["scenario"] = self.read_body().get("name", "default")
            STATE["commands"].clear()
            STATE["stages"].clear()
            STATE["registered"].clear()
            STATE["revoked"].clear()
            STATE["submitted"] = []
            STATE["pulled"] = []
            STATE["created"] = []
            return self.send_json(200, {"scenario": STATE["scenario"]})
        if path == "/__mock/submitted":
            return self.send_json(200, STATE.get("submitted", []))
        if path == "/__mock/health":
            return self.send_json(200, {"ok": True})

        if path == "/bff/session":
            if STATE["scenario"] == "anonymous":
                return self.send_json(200, {"authenticated": False})
            return self.send_json(200, {"authenticated": True, "subject": "u-mariana", "name": "Mariana Costa", "displayGroup": "Plataforma · SRE"})
        if path == "/bff/antiforgery":
            return self.send_json(200, {"requestToken": "mock-antiforgery"})
        if path == "/bff/logout" and method == "POST":
            STATE["scenario"] = "anonymous"
            return self.send_json(204, None)
        if path.startswith("/hubs/"):
            return self.send_json(404, problem(404, "Tempo real indisponível no mock"))

        if not path.startswith("/api/v1/"):
            return self.send_json(404, problem(404, "Rota inexistente"))
        if STATE["scenario"] == "expire-next":
            STATE["scenario"] = "default"
            return self.send_json(401, problem(401, "Sessão expirada"), "application/problem+json")

        if path == "/api/v1/overview":
            return self.send_json(200, overview(q("stage")))
        if path == "/api/v1/metrics/overview":
            if q("window") not in (None, "30m", "1h", "6h", "12h", "24h"):
                return self.send_json(400, problem(400, "Janela inválida", "invalid_metrics_query"), "application/problem+json")
            return self.send_json(200, stage_metrics(q("stage")))
        if path == "/api/v1/metrics/hosts":
            rows = [h for h in hosts() if not q("stage") or h["stage"] == q("stage")]
            return self.send_json(200, {"hosts": [{
                "environmentId": h["environmentId"], "cpuPercent": host_cpu(h),
                "memoryUsedBytes": None if host_cpu(h) is None else int(h["agent"]["memoryBytes"] * 0.6),
                "memoryTotalBytes": None if host_cpu(h) is None else h["agent"]["memoryBytes"],
                "sampledAt": None if host_cpu(h) is None else iso(0.2)} for h in rows], "operationalLimitPercent": 80})
        if path == "/api/v1/events":
            rows = [e for e in events() if (not q("stage") or host_stage(e["environmentId"]) == q("stage")) and (not q("environmentId") or e["environmentId"] == q("environmentId")) and (not q("severity") or e["severity"] == q("severity"))]
            return self.send_json(200, {"events": rows[: int(q("limit") or 50)], "nextCursor": None})
        if path == "/api/v1/environments" and method == "POST":
            if STATE["scenario"] == "readonly":
                return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
            body = self.read_body()
            environment_id = body.get("environmentId") or str(uuid.uuid4())
            if any(h["environmentId"] == environment_id for h in hosts()):
                return self.send_json(409, problem(409, "Ambiente já cadastrado", "environment_conflict"), "application/problem+json")
            view = {"environmentId": environment_id, "name": body.get("name"), "host": body.get("host"), "enabled": body.get("enabled", True),
                    "scopes": scopes(), "stage": body.get("stage"), "situation": "disconnected", "containerCount": 0, "agent": None}
            STATE["registered"][environment_id] = view
            return self.send_json(201, view)
        if path == "/api/v1/environments":
            rows = [h for h in hosts() if (not q("stage") or h["stage"] == q("stage")) and (not q("engine") or h["agent"]["engine"] == q("engine")) and (not q("situation") or h["situation"] == q("situation")) and (not q("q") or q("q").lower() in h["name"])]
            return self.send_json(200, {"environments": rows, "nextCursor": None})
        if path == "/api/v1/containers":
            rows = containers()
            if q("stage"):
                rows = [c for c in rows if c["stage"] == q("stage")]
            for key, field in (("environmentId", "environmentId"), ("engine", "engine"), ("displayStatus", "displayStatus")):
                if query.get(key):
                    rows = [c for c in rows if c[field] in query[key]]
            if q("q"):
                rows = [c for c in rows if q("q").lower() in c["name"].lower() or q("q").lower() in c["imageReference"].lower()]
            sort = q("sort") or "name"
            reverse = sort.startswith("-")
            key = sort.lstrip("-")
            rows.sort(key=lambda c: (c["environmentName"] if key == "environment" else c.get(key) or ""), reverse=reverse)
            return self.send_json(200, {"containers": rows, "summary": {"total": len(rows), "byDisplayStatus": count_map(c["displayStatus"] for c in rows)}, "observedAt": iso(0.5), "nextCursor": None})

        if path == "/api/v1/images":
            rows = [i for i in images() if not q("stage") or i["stage"] == q("stage")]
            visible_hosts = {h["environmentId"] for h in hosts() if not q("stage") or h["stage"] == q("stage")}
            for key in ("environmentId", "engine"):
                if query.get(key):
                    rows = [i for i in rows if i[key] in query[key]]
                    if key == "environmentId":
                        visible_hosts &= set(query[key])
            summary = images_summary(rows, visible_hosts)
            if q("unused") == "true":
                rows = [i for i in rows if not i["inUse"]]
            if q("q"):
                rows = [i for i in rows if q("q").lower() in " ".join(i["repoTags"] + [i["imageId"]]).lower()]
            sort = q("sort") or "name"
            key = {"name": lambda i: (i["repoTags"] or ["~" + i["imageId"]])[0], "size": lambda i: i["sizeBytes"], "createdAt": lambda i: i["createdAt"]}[sort.lstrip("-")]
            rows.sort(key=key, reverse=sort.startswith("-"))
            return self.send_json(200, {"images": rows, "summary": summary, "nextCursor": None})

        m = re.fullmatch(r"/api/v1/environments/([^/]+)(/.*)?", path)
        if not m:
            return self.send_json(404, problem(404, "Rota inexistente"))
        environment_id, rest = m.group(1), m.group(2) or ""
        host = next((h for h in hosts() if h["environmentId"] == environment_id), None)
        if host is None:
            return self.send_json(404, problem(404, "Ambiente não encontrado"))
        if rest == "":
            return self.send_json(200, host)
        if rest == "/stage" and method == "PUT":
            if "environment:manage" not in scopes():
                return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
            STATE["stages"][environment_id] = self.read_body().get("stage")
            host["stage"] = STATE["stages"][environment_id]
            return self.send_json(200, host)
        if rest == "/agent-identity/revoke" and method == "POST":
            if "environment:manage" not in scopes():
                return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
            STATE["revoked"].add(environment_id)
            return self.send_json(204, None)
        if rest == "/containers" and method == "GET":
            rows = [c for c in containers() if c["environmentId"] == environment_id]
            return self.send_json(200, {"environmentId": environment_id, "revision": 7, "observedAt": iso(0.5), "ageSeconds": 30, "containers": rows, "nextCursor": None})
        if rest == "/create-options":
            if "container:create" not in scopes():
                return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
            if not host["agent"]["connected"]:
                return self.send_json(409, problem(409, "Agente desconectado", "agent_disconnected"), "application/problem+json")
            podman = host["agent"]["engine"] == "podman"
            return self.send_json(200, {
                "engine": host["agent"]["engine"], "engineVersion": host["agent"]["engineVersion"], "rootless": host["agent"]["engineRootless"],
                "secrets": ([{"kind": "podman_secret", "name": "jwt-pedidos"}] if podman else []) + [{"kind": "file", "name": "token-app"}],
                "networks": [{"name": "rede-pedidos", "driver": "bridge"}], "volumes": [{"name": "vol-pedidos-uploads"}],
                "allowedRegistries": [], "bindMountsAllowed": False, "partial": False,
                "limits": {"ports": 32, "environment": 100, "volumes": 10, "valueBytes": 32768},
            })
        if rest == "/containers" and method == "POST":
            if "container:create" not in scopes():
                return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
            body = self.read_body()
            allowed = {"image", "name", "ports", "environment", "volumes", "network", "cpuLimitCores", "memoryLimitBytes", "restartPolicy", "healthcheck", "autoRemove", "deadlineUtc"}
            extra = sorted(set(body) - allowed)
            if extra:
                return self.send_json(400, {**problem(400, "Especificação inválida", "invalid_container_spec"), "errors": {extra[0]: ["not_allowed"]}}, "application/problem+json")
            if not host["agent"]["connected"]:
                return self.send_json(409, problem(409, "Agente desconectado", "agent_disconnected"), "application/problem+json")
            key = self.headers.get("Idempotency-Key")
            existing = next((c for c in STATE["commands"].values() if c.get("idempotencyKey") == key), None)
            if existing:
                return self.send_json(202, {"commandId": existing["commandId"], "equivalentCommand": None})
            command_id = str(uuid.uuid4())
            name = body.get("name") or ""
            port = (body.get("ports") or [{}])[0].get("hostPort")
            failure = "port_conflict" if port == 9999 else "start_failed" if name.startswith("falha") else None
            STATE["commands"][command_id] = {
                "environmentId": environment_id, "commandId": command_id, "action": "create", "kind": "container", "containerId": None,
                "expectedContainerRevision": None, "reference": body.get("image"), "containerName": name, "state": "pending", "failureCode": None,
                "result": None, "observedContainerRevision": None, "deadlineUtc": iso(-17), "createdAtUtc": iso(0), "updatedAtUtc": iso(0),
                "completedAtUtc": None, "idempotencyKey": key, "polls": 0, "failure": failure, "body": body,
            }
            STATE.setdefault("submitted", []).append(("create", host["name"], name, json.dumps(body, sort_keys=True)))
            return self.send_json(202, {"commandId": command_id, "equivalentCommand": None})
        if rest == "/images/lookup":
            ref = q("reference") or ""
            if not REFERENCE.fullmatch(ref):
                return self.send_json(400, problem(400, "Referência inválida", "invalid_reference"), "application/problem+json")
            canonical = canonical_reference(ref)
            found = next((i for i in images() if i["environmentId"] == environment_id and canonical in i["repoTags"]), None)
            return self.send_json(200, {"reference": canonical, "mutableTag": "@" not in ref, "present": found is not None,
                                        "imageId": found and found["imageId"], "sizeBytes": found and found["sizeBytes"],
                                        "createdAt": found and found["createdAt"], "digest": None})
        if rest == "/images/pull" and method == "POST":
            body = self.read_body()
            ref = body.get("reference") or ""
            if not REFERENCE.fullmatch(ref):
                return self.send_json(400, problem(400, "Referência inválida", "invalid_reference"), "application/problem+json")
            if "image:pull" not in scopes():
                return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
            if not host["agent"]["connected"]:
                return self.send_json(409, problem(409, "Agente desconectado", "agent_disconnected"), "application/problem+json")
            if "ImagePull" not in host["agent"]["capabilities"]:
                return self.send_json(422, problem(422, "Agente sem suporte", "not_supported"), "application/problem+json")
            key = self.headers.get("Idempotency-Key")
            existing = next((c for c in STATE["commands"].values() if c.get("idempotencyKey") == key), None)
            if existing:
                return self.send_json(202, {"commandId": existing["commandId"], "reference": existing["reference"]})
            command_id = str(uuid.uuid4())
            canonical = canonical_reference(ref)
            failure = "image_not_found" if "inexistente" in ref else "registry_unauthorized" if "privado" in ref else None
            STATE["commands"][command_id] = {
                "environmentId": environment_id, "commandId": command_id, "action": "pull", "kind": "image", "containerId": None,
                "expectedContainerRevision": None, "reference": canonical, "state": "pending", "failureCode": None, "result": None,
                "observedContainerRevision": None, "deadlineUtc": body.get("deadlineUtc"), "createdAtUtc": iso(0), "updatedAtUtc": iso(0),
                "completedAtUtc": None, "idempotencyKey": key, "polls": 0, "failure": failure,
            }
            STATE.setdefault("submitted", []).append(("pull", host["name"], canonical))
            return self.send_json(202, {"commandId": command_id, "reference": canonical})
        cm = re.fullmatch(r"/commands/([^/]+)", rest)
        if cm:
            command = STATE["commands"].get(cm.group(1))
            if not command:
                return self.send_json(404, problem(404, "Comando não encontrado"))
            if command["action"] == "create":
                command["polls"] += 1
                if command["polls"] == 1:
                    command["state"] = "accepted"
                elif command["state"] not in ("succeeded", "failed"):
                    failure = command["failure"]
                    n = next(h[0] for h in all_hosts() if env_id(h[0]) == environment_id)
                    created = None
                    if failure != "port_conflict":
                        body = command["body"]
                        created = (body["name"], n, body["image"], "stopped" if failure else "running", 0,
                                   [(p.get("hostPort"), p["containerPort"]) for p in body.get("ports") or [] if p.get("hostPort")])
                        STATE.setdefault("created", []).append(created)
                    cid = None
                    if created:
                        cid = next((c["containerId"] for c in containers() if c["name"] == created[0] and c["environmentId"] == environment_id), None)
                    command.update({"state": "failed" if failure else "succeeded", "failureCode": failure, "completedAtUtc": iso(0),
                                    "result": {"createdContainerId": cid} if cid else None})
                return self.send_json(200, {k: v for k, v in command.items() if k not in ("idempotencyKey", "polls", "failure", "body")})
            if command["action"] == "pull":
                command["polls"] += 1
                if command["polls"] == 1:
                    command["state"] = "accepted"
                elif command["state"] not in ("succeeded", "failed"):
                    failure = command["failure"]
                    command.update({"state": "failed" if failure else "succeeded", "failureCode": failure, "completedAtUtc": iso(0),
                                    "result": None if failure else {"resolvedImageDigest": "sha256:" + "ab" * 32, "imageSizeBytes": 150 * 1024**2}})
                    if not failure:
                        n = next(h[0] for h in all_hosts() if env_id(h[0]) == environment_id)
                        STATE.setdefault("pulled", []).append((n, command["reference"]))
                return self.send_json(200, {k: v for k, v in command.items() if k not in ("idempotencyKey", "polls", "failure")})
            failed = command["action"] == "start" and command.get("containerName", "").startswith("migracao")
            command.update({"state": "failed" if failed else "succeeded", "failureCode": "start_failed" if failed else None,
                            "updatedAtUtc": iso(0), "completedAtUtc": iso(0)})
            return self.send_json(200, {k: v for k, v in command.items() if k != "containerName"})
        cm = re.fullmatch(r"/containers/([^/]+)(/.*)?", rest)
        if not cm:
            return self.send_json(404, problem(404, "Rota inexistente"))
        container = find_container(environment_id, cm.group(1))
        if container is None:
            return self.send_json(404, problem(404, "Container não encontrado"))
        sub = cm.group(2) or ""
        if sub == "":
            return self.send_json(200, {**container, "restartsLast24h": container["restartCount"] or 0, "hostName": host["name"], "capabilities": host["agent"]["capabilities"]})
        if sub == "/metrics":
            if q("window") not in (None, "30m"):
                return self.send_json(400, problem(400, "Janela acima de 30 minutos", "invalid_metrics_query"), "application/problem+json")
            resources = container.get("resources")
            x = [(NOW.replace(second=0) - timedelta(minutes=29 - i)).isoformat().replace("+00:00", "Z") for i in range(30)]
            current = None if not resources else {
                "cpuPercent": resources["cpuPercent"], "cpuPercentOfLimit": resources["cpuPercent"], "cpuPercentOfHost": resources["cpuPercent"] / 4,
                "cpuLimitCores": 2, "memoryUsedBytes": resources["memoryUsedBytes"], "memoryLimitBytes": resources["memoryLimitBytes"],
                "netRxBps": 18432, "netTxBps": 6144, "blkReadBps": 0, "blkWriteBps": 4096, "sampledAt": resources["sampledAt"]}
            series = {"x": x,
                      "cpu": [None if (not resources or i < 3) else round(resources["cpuPercent"] * (0.8 + (i % 5) / 20), 1) for i in range(30)],
                      "memory": [None if (not resources or i < 3) else round(resources["memoryUsedBytes"] / resources["memoryLimitBytes"] * 100, 1) for i in range(30)]}
            return self.send_json(200, {"current": current, "series": series, "operationalLimitPercent": 80})
        if sub in ("/summary", "/inspect") and "container:inspect" not in scopes():
            return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
        if sub == "/logs" and "container:logs" not in scopes():
            return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
        if sub in ("/summary", "/inspect", "/logs") and not container["hostConnected"]:
            return self.send_json(409, problem(409, "Agente desconectado", "agent_disconnected"), "application/problem+json")
        if sub == "/summary":
            return self.send_json(200, {
                "command": "node dist/server.js", "user": "app", "workingDir": "/srv/app",
                "healthcheck": {"test": "curl -fsS http://localhost/healthz", "intervalSeconds": 15, "timeoutSeconds": 5, "retries": 3},
                "labels": {"com.empresa.app": container["name"], "dokpod.created-by": "u-mariana"},
                "logging": {"driver": "json-file", "options": {"max-size": "10m"}},
                "networks": [{"name": "rede-pedidos", "ipAddress": "172.20.0.14"}],
                "volumes": [{"type": "volume", "name": "vol-pedidos-uploads", "destination": "/srv/app/uploads", "readOnly": False}],
                "restartPolicy": {"name": "unless-stopped", "maximumRetryCount": 0},
                "cpuLimitCores": 2, "memoryLimitBytes": 512 * 1024**2, "environmentVariableCount": 14, "secretCount": 3,
            })
        if sub == "/inspect":
            return self.send_json(200, {"json": {"Id": container["containerId"], "Name": "/" + container["name"], "Config": {"Env": "<oculto>", "Image": container["imageReference"]}, "State": {"Status": container["state"]}}, "truncated": False, "redactedPaths": ["Config.Env"]})
        if sub == "/logs":
            contains = (q("contains") or "").lower()
            tail = min(int(q("tail") or 500), 5000)
            sample = [
                ("INFO", "Servidor iniciado na porta 80"), ("INFO", "GET /healthz 200 2ms"), ("WARN", "Pool de conexões em 80%"),
                ("INFO", "POST /pedidos 201 48ms"), ("ERROR", "Timeout ao consultar postgres-principal:5432"),
                ("INFO", "Authorization: Bearer ***"), ("INFO", "GET /pedidos/42 200 12ms"),
            ]
            texts = [sample[i % len(sample)] for i in range(tail)]
            lines = [
                {"timestamp": iso((tail - i) / 600), "stream": "stderr" if lvl == "ERROR" else "stdout", "level": lvl, "text": text}
                for i, (lvl, text) in enumerate(texts)
                if not contains or contains in text.lower()
            ]
            return self.send_json(200, {"lines": lines, "truncated": False, "omittedLines": 0})
        if sub == "/commands" and method == "POST":
            body = self.read_body()
            command_id = str(uuid.uuid4())
            STATE["commands"][command_id] = {
                "environmentId": environment_id, "commandId": command_id, "action": body.get("action"), "containerId": container["containerId"],
                "expectedContainerRevision": body.get("expectedContainerRevision"), "state": "accepted", "failureCode": None,
                "observedContainerRevision": None, "deadlineUtc": body.get("deadlineUtc"), "createdAtUtc": iso(0), "updatedAtUtc": iso(0), "completedAtUtc": None,
            }
            STATE["commands"][command_id]["containerName"] = container["name"]
            STATE.setdefault("submitted", []).append((body.get("action"), container["name"]))
            return self.send_json(202, STATE["commands"][command_id])
        return self.send_json(404, problem(404, "Rota inexistente"))


def main() -> None:
    port = int(os.environ.get("DOKPOD_MOCK_PORT", "8090"))
    ThreadingHTTPServer(("0.0.0.0", port), Handler).serve_forever()


if __name__ == "__main__":
    main()
