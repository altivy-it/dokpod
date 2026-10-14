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
STATE: dict = {"scenario": "default", "commands": {}}


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
        "containerCount": sum(1 for c in CONTAINERS if c[1] == n),
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
            "capabilities": ["CONTAINER_LIFECYCLE", "ENGINE_EVENTS", "RESOURCE_METRICS", "CONTAINER_PAUSE", "CONTAINER_UNPAUSE", "CONTAINER_INSPECT", "CONTAINER_LOGS"],
        },
    }


def hosts() -> list[dict]:
    return [] if STATE["scenario"] == "empty" else [host_view(h) for h in HOSTS]


def host_by_n(n: int):
    return next(h for h in HOSTS if h[0] == n)


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


def containers() -> list[dict]:
    return [] if STATE["scenario"] == "empty" else [container_item(c) for c in CONTAINERS]


def find_container(environment_id: str, container_id: str):
    return next((c for c in containers() if c["environmentId"] == environment_id and c["containerId"] == container_id), None)


def count_map(values) -> dict:
    out: dict = {}
    for v in values:
        out[v] = out.get(v, 0) + 1
    return out


def events() -> list[dict]:
    if STATE["scenario"] == "empty":
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
    }


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
            return self.send_json(200, {"scenario": STATE["scenario"]})
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
        if path == "/api/v1/events":
            rows = [e for e in events() if (not q("stage") or host_stage(e["environmentId"]) == q("stage")) and (not q("environmentId") or e["environmentId"] == q("environmentId")) and (not q("severity") or e["severity"] == q("severity"))]
            return self.send_json(200, {"events": rows[: int(q("limit") or 50)], "nextCursor": None})
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
            host["stage"] = self.read_body().get("stage")
            return self.send_json(200, host)
        if rest == "/containers":
            rows = [c for c in containers() if c["environmentId"] == environment_id]
            return self.send_json(200, {"environmentId": environment_id, "revision": 7, "observedAt": iso(0.5), "ageSeconds": 30, "containers": rows, "nextCursor": None})
        cm = re.fullmatch(r"/commands/([^/]+)", rest)
        if cm:
            command = STATE["commands"].get(cm.group(1))
            if not command:
                return self.send_json(404, problem(404, "Comando não encontrado"))
            command.update({"state": "succeeded", "updatedAtUtc": iso(0), "completedAtUtc": iso(0)})
            return self.send_json(200, command)
        cm = re.fullmatch(r"/containers/([^/]+)(/.*)?", rest)
        if not cm:
            return self.send_json(404, problem(404, "Rota inexistente"))
        container = find_container(environment_id, cm.group(1))
        if container is None:
            return self.send_json(404, problem(404, "Container não encontrado"))
        sub = cm.group(2) or ""
        if sub == "":
            return self.send_json(200, {**container, "restartsLast24h": container["restartCount"] or 0, "hostName": host["name"], "capabilities": host["agent"]["capabilities"]})
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
            lines = [
                {"timestamp": iso(3 - i * 0.1), "stream": "stderr" if lvl == "ERROR" else "stdout", "level": lvl, "text": text}
                for i, (lvl, text) in enumerate([
                    ("INFO", "Servidor iniciado na porta 80"), ("INFO", "GET /healthz 200 2ms"), ("WARN", "Pool de conexões em 80%"),
                    ("INFO", "POST /pedidos 201 48ms"), ("ERROR", "Timeout ao consultar postgres-principal:5432"), ("INFO", "GET /pedidos/42 200 12ms"),
                ])
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
            return self.send_json(202, STATE["commands"][command_id])
        return self.send_json(404, problem(404, "Rota inexistente"))


def main() -> None:
    port = int(os.environ.get("DOKPOD_MOCK_PORT", "8090"))
    ThreadingHTTPServer(("0.0.0.0", port), Handler).serve_forever()


if __name__ == "__main__":
    main()
