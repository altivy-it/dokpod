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

Spec 016: alertas, regras, silenciamentos e política de justificativa vivem em `ALERTS` e são recriados
a cada troca de cenário; comandos de parar, pausar e remover respeitam a política do estágio.
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
    "container:create", "container:exec", "audit:read", "alert:read", "alert:manage", "volume:delete", "network:delete",
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
    if STATE["scenario"] == "readonly":
        return ["environment:read", "alert:read"]
    return ALL_SCOPES


RULE_IDS = {k: f"01600000-0000-4000-8000-00000000000{k}" for k in range(1, 10)}


def seed_alerts() -> dict:
    """Regras padrão da migration 202610140001 e alertas derivados dos eventos e containers do mock."""
    def rule(k, name, kind, severity, event=None, metric=None, threshold=None, duration=0, occurrences=1):
        return {"id": RULE_IDS[k], "name": name, "description": None, "conditionKind": kind, "eventType": event, "metric": metric,
                "thresholdPercent": threshold, "durationSeconds": duration, "minOccurrences": occurrences, "severity": severity,
                "scope": {"stage": None, "environmentId": None, "containerPattern": None}, "enabled": True, "builtIn": True, "version": 1}
    rules = [
        rule(1, "Container encerrado com erro", "event", "high", "container.died"),
        rule(2, "Container sem memória (OOM)", "event", "critical", "container.oom_killed"),
        rule(3, "Container sem saúde", "event", "medium", "container.unhealthy"),
        rule(4, "Reinícios em sequência", "event", "high", "container.auto_restarted", duration=600, occurrences=3),
        rule(5, "Agente desconectado", "event", "high", "agent.disconnected", duration=120),
        rule(6, "CPU do host acima do limite", "metric", "medium", metric="host_cpu", threshold=80, duration=300),
        rule(7, "Memória do host acima do limite", "metric", "medium", metric="host_memory", threshold=80, duration=300),
        rule(8, "Comando falhou", "event", "medium", "command.failed"),
        rule(9, "Comando indeterminado", "event", "low", "command.indeterminate"),
    ]

    def alert(i, k, n, container, condition, state, minutes, occurrences=1, resolved=None, resolution=None, assignee=None):
        cid = (container.encode().hex() * 4)[:64] if container else None
        r = next(x for x in rules if x["id"] == RULE_IDS[k])
        return {"id": f"0a1e0000-0000-4000-8000-{i:012d}", "ruleId": r["id"], "ruleName": r["name"], "environmentId": env_id(n),
                "containerId": cid, "containerName": container, "severity": r["severity"], "state": state, "condition": condition,
                "openedAt": iso(minutes + 30), "lastOccurredAt": iso(minutes), "occurrences": occurrences, "assignee": assignee,
                "silenceId": None, "resolvedAt": iso(resolved) if resolved is not None else None, "resolution": resolution,
                "timeline": [{"at": iso(minutes + 30), "kind": "opened", "actor": None, "comment": None}]}
    alerts = [
        alert(1, 1, 5, "migracao-catalogo", "Container migracao-catalogo encerrado com erro (código 1)", "open", 4),
        alert(2, 4, 1, "gateway-01", "Container gateway-01 reiniciou 3 ou mais vezes em 10 minutos", "open", 9, occurrences=5),
        alert(3, 5, 7, None, "Agente desconectado", "acknowledged", 95, assignee="u-carlos"),
        alert(4, 6, 2, None, "CPU de srv-prod-02 em 91% (limite 80%)", "open", 2),
        alert(5, 3, 6, "api-pedidos-hml", "Container api-pedidos-hml sem saúde", "open", 12),
        alert(6, 1, 1, "checkout-web", "Container checkout-web encerrado com erro (código 2)", "resolved", 300, resolved=280, resolution="condition_cleared"),
        alert(7, 8, 3, "api-pedidos-7f9c", "Comando restart falhou em api-pedidos-7f9c", "resolved", 900, resolved=860, resolution="manual"),
    ]
    alerts[2]["timeline"].append({"at": iso(80), "kind": "acknowledged", "actor": "u-carlos", "comment": "Investigando a rede do host de dev"})
    for a in alerts:
        if a["state"] == "resolved":
            a["timeline"].append({"at": a["resolvedAt"], "kind": "resolved", "actor": "u-mariana" if a["resolution"] == "manual" else None, "comment": None})
    silences = [{"id": "05110000-0000-4000-8000-000000000001", "scope": {"stage": "staging", "environmentId": None, "containerPattern": "batch-*"},
                 "alertId": None, "startsAt": iso(60), "endsAt": iso(-120), "endedAt": None,
                 "justification": "Janela de carga noturna dos jobs batch", "createdBy": "u-carlos"}]
    intentional = {(env_id(5), ("relatorios-bi".encode().hex() * 4)[:64]): {
        "action": "stop", "actor": "u-carlos", "at": iso(240), "reason": "Relatórios suspensos até o fechamento do mês"}}
    return {"rules": rules, "alerts": alerts, "silences": silences, "policies": {"production": True, "staging": False, "development": False},
            "intentional": intentional, "justifications": []}


ALERTS: dict = seed_alerts()


def seed_audit() -> list[dict]:
    """Trilha de auditoria (spec 017) derivada dos hosts e containers do mock, mais recente primeiro."""
    def cid(name: str) -> str:
        return (name.encode().hex() * 4)[:64]
    corr = "0c0c0000-0000-4000-8000-000000000001"
    cmd = "0c0d0000-0000-4000-8000-000000000001"
    rows = [
        (2, "user", "u-mariana", "Mariana Costa", "container.stop", 1, "succeeded", None, cid("checkout-web"), cmd, corr, {"justification": "Janela de manutenção do checkout"}),
        (2, "agent", "agent:srv-prod-01", None, "container.command.result", 1, "succeeded", None, None, cmd, corr, None),
        (15, "user", "u-carlos", "Carlos Lima", "container.exec.denied", 3, "denied", "scope", cid("api-pedidos-7f9c"), None, None, {"reason": "scope"}),
        (40, "user", "u-mariana", "Mariana Costa", "environment.stage_changed", 6, "succeeded", None, None, None, None, {"previousStage": "development", "stage": "staging"}),
        (80, "user", "u-carlos", "Carlos Lima", "alert.acknowledged", 7, "succeeded", None, "0a1e0000-0000-4000-8000-000000000003", None, None, {"alertId": "0a1e0000-0000-4000-8000-000000000003", "severity": "high"}),
        (300, "user", "u-mariana", "Mariana Costa", "container.logs.read", 2, "succeeded", None, cid("postgres-principal"), None, None, {"tail": "500", "lines": "500"}),
        (60 * 24 * 3, "user", "sub-legado", None, "image.pull", 3, "failed", "registry_unauthorized", None, "0c0d0000-0000-4000-8000-000000000002", None, {"image": "registry.empresa.com/privado/app:1.0"}),
    ]
    return [{
        "id": f"0a0d1000-0000-4000-8000-{i:012d}", "occurredAt": iso(minutes), "correlationId": correlation or f"0c0c0000-0000-4000-8000-{100 + i:012d}",
        "actorKind": kind, "actorId": actor, "actorName": name, "action": action, "environmentId": env_id(n), "outcome": outcome,
        "failureCode": failure, "subjectId": subject, "commandId": command, "details": details,
    } for i, (minutes, kind, actor, name, action, n, outcome, failure, subject, command, correlation, details) in enumerate(rows, start=1)]


AUDIT: list = seed_audit()
AUDIT_CATEGORY = {
    "environments": ["environment.register", "environment.approve", "environment.suspend", "environment.revoke", "environment.stage_changed"],
    "containers": ["container.start", "container.stop", "container.restart", "container.delete", "container.pause", "container.unpause", "container.create.requested"],
    "commands": ["container.command.result"], "images": ["image.pull"], "volumes": ["volume.remove"], "networks": ["network.remove"], "pods": ["pod.start", "pod.stop", "pod.restart", "pod.pause", "pod.unpause"],
    "inspection": ["container.inspected", "container.logs.read", "container.logs.stream_started", "container.logs.stream_ended"],
    "terminal": ["container.exec.requested", "container.exec.started", "container.exec.ended", "container.exec.denied"],
    "governance": ["alert.rule_created", "alert.rule_updated", "alert.rule_deleted", "alert.acknowledged", "alert.resolved", "alert.silenced",
                   "alert.silence_created", "alert.silence_ended", "container.justification_policy_changed", "registry.policy_changed"],
    "audit": ["audit.exported"],
}


def audit_view(e: dict) -> dict:
    category = next((c for c, actions in AUDIT_CATEGORY.items() if e["action"] in actions), None)
    return {**e, "category": category, "environmentName": host_by_n(int(e["environmentId"][-12:]))[1]}


def fold(text: str) -> str:
    """Aproxima a busca textual do PostgreSQL (D-008): minúsculas e sem acentos."""
    return text.lower().translate(str.maketrans("áàâãäéèêëíìîïóòôõöúùûüçñ", "aaaaaeeeeiiiiooooouuuucn"))


def audit_filter(query: dict) -> list[dict] | None:
    q = lambda key: (query.get(key) or [None])[0]  # noqa: E731
    since, until = q("since"), q("until")
    if since and until and datetime.fromisoformat(until.replace("Z", "+00:00")) - datetime.fromisoformat(since.replace("Z", "+00:00")) > timedelta(days=90):
        return None
    actions = {a for c in query.get("category", []) for a in AUDIT_CATEGORY.get(c, [])}
    when = lambda text: datetime.fromisoformat(text.replace("Z", "+00:00"))  # noqa: E731
    rows = [e for e in AUDIT if (not since or when(e["occurredAt"]) >= when(since)) and (not until or when(e["occurredAt"]) <= when(until))]
    rows = [e for e in rows
            if (not actions or e["action"] in actions)
            and (not query.get("outcome") or e["outcome"] in query["outcome"])
            and (not query.get("environmentId") or e["environmentId"] in query["environmentId"])
            and (not q("stage") or host_stage(e["environmentId"]) == q("stage"))
            and (not q("actor") or e["actorId"] == q("actor") or (e["actorName"] or "").lower().startswith(q("actor").lower()))
            and (not q("q") or q("q") in (e["correlationId"], e["commandId"], e["id"]) or (e["subjectId"] or "").startswith(q("q").lower())
                 or all(word in fold(" ".join([e["action"].replace(".", " "), e["actorId"], e["actorName"] or "", e["subjectId"] or "", e["failureCode"] or "",
                                                 *(e["details"] or {}).values()])) for word in fold(q("q")).split()))]
    return [audit_view(e) for e in rows]


def alert_view(a: dict) -> dict:
    return {**{k: v for k, v in a.items() if k != "timeline"}, "canManage": "alert:manage" in scopes()}


def alert_summary(stage: str | None) -> dict:
    rows = [a for a in ALERTS["alerts"] if a["state"] in ("open", "acknowledged") and (not stage or host_stage(a["environmentId"]) == stage)]
    rows.sort(key=lambda a: a["lastOccurredAt"], reverse=True)
    by = {s: sum(1 for a in rows if a["severity"] == s) for s in ("critical", "high", "medium", "low")}
    return {"open": len(rows), "bySeverity": by, "recent": [alert_view(a) for a in rows[:10]]}


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
            + ([] if situation == "update_required" else ["ImageInventory", "ImagePull", "ContainerCreate", "ContainerExec", "VolumeInventory", "VolumeRemove", "NetworkInventory", "NetworkRemove", "RegistryInventory"] + (["PodInventory", "PodActions"] if engine == "podman" else [])),
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
        **({"intentional": ALERTS["intentional"][(env_id(n), cid)]} if (env_id(n), cid) in ALERTS["intentional"] and status in ("stopped", "paused") else {}),
        "allowedActions": ([a for a in ACTIONS_FOR[status] if f"container:{a if a != 'unpause' else 'pause'}" in scopes()]
                           + (["exec"] if status == "running" and "container:exec" in scopes() and h[5] != "update_required" else [])) if connected else [],
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


def volumes() -> list[dict]:
    """Inventário de volumes (spec 018): todo container monta vol-pedidos-uploads; host 4 (agente 0.8.2) não tem inventário."""
    removed = STATE.get("removed_volumes", set())
    by_host: dict[str, list] = {}
    for c in containers():
        by_host.setdefault(c["environmentId"], []).append(c)
    result = []
    for h in all_hosts():
        n, name, stage, engine, situation = h[0], h[1], h[2], h[3], h[5]
        if situation == "update_required":
            continue
        mounted = [{"containerId": c["containerId"], "containerName": c["name"], "destination": "/srv/app/uploads", "readOnly": False}
                   for c in by_host.get(env_id(n), [])]
        seeds = [("vol-pedidos-uploads", False, mounted, {"com.docker.compose.project": "pedidos"}),
                 (f"dados-{name}", False, [], {"com.empresa.backup": "semanal"}),
                 ("cache-antigo", False, [], {}),
                 (uuid.uuid5(uuid.NAMESPACE_URL, f"anon-{n}").hex * 2, True, [], {})]
        for vname, anonymous, mounts, labels in seeds:
            if (n, vname) in removed or (vname == "vol-pedidos-uploads" and not mounts):
                continue
            connected = situation != "disconnected"
            result.append({
                "environmentId": env_id(n), "environmentName": name, "stage": STATE["stages"].get(env_id(n), stage), "engine": engine,
                "hostConnected": connected, "name": vname, "anonymous": anonymous, "driver": "local", "scope": "local",
                "createdAt": iso(60 * 24 * (2 + stable(vname, 30))), "labels": labels, "mounts": mounts, "inUse": bool(mounts),
                "truncated": False, "observedAt": iso(0.5),
                "canRemove": not mounts and connected and "volume:delete" in scopes(),
            })
    return result


def networks() -> list[dict]:
    """Inventário de redes (spec 019): todo container está em rede-pedidos; host 4 (agente 0.8.2) não tem inventário."""
    removed = STATE.get("removed_networks", set())
    by_host: dict[str, list] = {}
    for c in containers():
        by_host.setdefault(c["environmentId"], []).append(c)
    nid = lambda seed: (uuid.uuid5(uuid.NAMESPACE_URL, seed).hex * 2)[:64]  # noqa: E731
    result = []
    for h in all_hosts():
        n, name, stage, engine, situation = h[0], h[1], h[2], h[3], h[5]
        if situation == "update_required":
            continue
        attached = [{"containerId": c["containerId"], "containerName": c["name"],
                     "ipv4": f"172.20.{n}.{10 + i}" if c["state"] == "running" else None, "ipv6": None}
                    for i, c in enumerate(by_host.get(env_id(n), []))]
        seeds = [("podman" if engine == "podman" else "bridge", "bridge", "local", True, [], f"172.{16 + n}.0.0/16", {}),
                 ("rede-pedidos", "bridge", "local", False, attached, f"172.20.{n}.0/24", {"com.docker.compose.project": "pedidos"}),
                 (f"rede-orfa-{name}", "macvlan" if n == 2 else "bridge", "local", False, [], f"10.89.{70 + n}.0/24", {}),
                 ("cache-net", "bridge", "local", False, [], f"10.90.{n}.0/24", {})]
        if n == 1:
            seeds.append(("cluster-overlay", "overlay", "swarm", False, [], "10.0.9.0/24", {}))
        for net_name, driver, scope, system, conns, subnet, labels in seeds:
            network_id = nid(f"{n}-{net_name}")
            if (n, network_id) in removed or (net_name == "rede-pedidos" and not conns):
                continue
            connected = situation != "disconnected"
            result.append({
                "environmentId": env_id(n), "environmentName": name, "stage": STATE["stages"].get(env_id(n), stage), "engine": engine,
                "hostConnected": connected, "networkId": network_id, "name": net_name, "driver": driver, "scope": scope,
                "internal": False, "ipv6": False, "attachable": driver == "overlay", "system": system,
                "createdAt": None if system else iso(60 * 24 * (2 + stable(net_name, 30))),
                "subnets": [{"subnet": subnet, "gateway": subnet.rsplit(".", 1)[0] + ".1"}], "labels": labels, "containers": conns,
                "inUse": bool(conns), "truncated": False, "observedAt": iso(0.5),
                "canRemove": not conns and not system and scope == "local" and connected and "network:delete" in scopes(),
            })
    return result


REGISTRY_CONFIG = {
    # host n: [(registro, espelhos, espelho de, inseguro, bloqueado, busca, credencial)]
    1: [("docker.io", ["mirror.empresa.com"], None, False, False, True, False), ("mirror.empresa.com", [], "docker.io", False, False, False, False),
        ("registry.empresa.com", [], None, False, False, False, True)],
    2: [("registry.empresa.com", [], None, False, False, False, True)],
    3: [("registry.empresa.com", [], None, False, False, False, True), ("quay.io", [], None, False, False, True, False)],
    6: [("registry.local:5000", [], None, True, False, False, False)],
}
REGISTRY_POLICY_CONFIG = {"production": ["docker.io", "registry.empresa.com"]}


def registry_policy(stage_key: str) -> dict:
    saved = STATE.get("registry_policies", {}).get(stage_key)
    if saved:
        return {"stage": stage_key, "registries": saved["registries"], "source": "console", "updatedBy": saved["by"], "updatedAt": saved["at"]}
    return {"stage": stage_key, "registries": REGISTRY_POLICY_CONFIG.get(stage_key, []), "source": "configuration", "updatedBy": None, "updatedAt": None}


def registry_entries() -> list[dict]:
    """Spec 020: por host e registro — imagens (pela primeira tag), configuração do engine e credencial; host 4 sem configuração."""
    entries: dict[tuple, dict] = {}
    hosts_by_env = {h["environmentId"]: h for h in hosts()}
    def entry(env: str, registry: str) -> dict:
        key = (env, registry)
        if key not in entries:
            h = hosts_by_env[env]
            entries[key] = {"environmentId": env, "environmentName": h["name"], "stage": h["stage"], "hostConnected": h["agent"]["connected"] if h.get("agent") else False,
                            "configAvailable": "RegistryInventory" in ((h.get("agent") or {}).get("capabilities") or []), "origins": [], "credential": False,
                            "insecure": False, "blocked": False, "search": False, "mirrors": [], "mirrorOf": None, "images": 0, "sizeBytes": 0}
        return entries[key]
    for image in images():
        if not image["repoTags"]:
            continue
        e = entry(image["environmentId"], image["repoTags"][0].split("/")[0])
        e["images"] += 1
        e["sizeBytes"] += image["sizeBytes"]
    for n, configs in REGISTRY_CONFIG.items():
        env = env_id(n)
        if env not in hosts_by_env:
            continue
        for registry, mirrors, mirror_of, insecure, blocked, search, credential in configs:
            e = entry(env, registry)
            e.update({"mirrors": mirrors, "mirrorOf": mirror_of, "insecure": insecure, "blocked": blocked, "search": search, "credential": credential})
    for e in entries.values():
        e["origins"] = [o for o, on in (("credential", e["credential"]), ("images", e["images"] > 0), ("insecure", e["insecure"]), ("mirror", e["mirrorOf"] is not None),
                                         ("search", e["search"]), ("blocked", e["blocked"])) if on]
        e["registry"] = e_registry(e, entries)
        allowed = registry_policy(e["stage"] or "none")["registries"]
        e["allowed"] = not allowed or e["registry"] in allowed
    return list(entries.values())


def e_registry(e: dict, entries: dict) -> str:
    return next(k[1] for k, v in entries.items() if v is e)


def e_name(e: dict) -> str:
    return e["registry"]


POD_SEEDS = [
    # host n, nome, containers (do mock), pod do agente
    (3, "pod-pedidos", ["api-pedidos-7f9c", "worker-notificacoes"], False),
    (3, "pod_dokpod", [], True),
    (5, "pod-batch", ["migracao-catalogo", "relatorios-bi"], False),
]
POD_ACTION_SCOPE = {"start": "container:start", "stop": "container:stop", "restart": "container:restart", "pause": "container:pause", "unpause": "container:pause"}


def pods() -> list[dict]:
    """Spec 021: pods dos hosts Podman; o pod do agente contém o container do agente; host 4 sem inventário de pods."""
    by_name = {(c["environmentId"], c["name"]): c for c in containers()}
    hosts_by_n = {h[0]: h for h in all_hosts()}
    result = []
    for n, name, members, agent in POD_SEEDS:
        env = env_id(n)
        h = hosts_by_n.get(n)
        if not h or h[3] != "podman" or h[5] == "update_required":
            continue
        pod_id = (uuid.uuid5(uuid.NAMESPACE_URL, f"pod-{n}-{name}").hex * 2)[:64]
        override = STATE.get("pod_status", {}).get(pod_id)
        conts = []
        for member in members:
            c = by_name.get((env, member))
            if not c:
                continue
            state = c["state"] if not override else ("running" if override in ("running", "degraded") and member != "migracao-catalogo" else "paused" if override == "paused" else "exited")
            conts.append({"containerId": c["containerId"], "name": member, "status": state, "restarts": c.get("restartCount") or 0, "infra": False})
        if agent:
            conts.append({"containerId": (uuid.uuid5(uuid.NAMESPACE_URL, f"agent-{n}").hex * 2)[:64], "name": "dokpod-agent", "status": "running", "restarts": 0, "infra": False})
        running = sum(c["status"] == "running" for c in conts)
        paused = sum(c["status"] == "paused" for c in conts)
        status = override or ("running" if conts and running == len(conts) else "paused" if conts and paused == len(conts) else "exited" if running == 0 and paused == 0 else "degraded")
        connected = h[5] != "disconnected"
        allowed = []
        if connected:
            candidates = []
            if status not in ("running", "paused"): candidates.append("start")
            if status in ("running", "degraded", "paused"): candidates.append("stop")
            if status not in ("paused", "created"): candidates.append("restart")
            if status in ("running", "degraded"): candidates.append("pause")
            if status == "paused": candidates.append("unpause")
            allowed = [a for a in candidates if not (agent and a in ("stop", "pause", "restart")) and POD_ACTION_SCOPE[a] in scopes()]
        result.append({"environmentId": env, "environmentName": h[1], "stage": STATE["stages"].get(env, h[2]), "hostConnected": connected, "podId": pod_id,
                       "name": name, "status": status, "createdAt": iso(60 * 24 * 3), "labels": {"io.podman.compose.project": name}, "networks": [f"{name}_default"],
                       "infraId": None, "containers": conts, "runningContainers": running, "agentPod": agent, "truncated": False, "observedAt": iso(0.5),
                       "allowedActions": allowed})
    return result


def host_stage(environment_id: str) -> str | None:
    return next((h["stage"] for h in hosts() if h["environmentId"] == environment_id), None)


def problem(status: int, title: str, code: str | None = None) -> dict:
    return {"type": "about:blank", "title": title, "status": status, "detail": title, "code": code}



# Spec 031 (T029): arquivo de referência aceito pelo mock; o conteúdo não é uma licença assinada de verdade.
MOCK_VALID_LICENSE = "mock-license.e2e-fixture.not-signed"


MOCK_LICENSING_HOSTS = [
    {"hostId": "a1000000-0000-4000-8000-000000000001", "label": "srv-prod-01", "removed": False,
     "environmentIds": ["e1000000-0000-4000-8000-000000000001"], "vcpus": 64, "source": "engine", "nodeEquivalents": 2},
    {"hostId": "a1000000-0000-4000-8000-000000000002", "label": "srv-legado-n1", "removed": False,
     "environmentIds": ["e1000000-0000-4000-8000-000000000002"], "vcpus": None, "source": "unknown", "nodeEquivalents": None},
]


def licensing_status(state: dict) -> dict:
    if not state["license"]:
        return {"entitlement": "ce_only", "authenticity": "absent", "validity": None, "metering": "regular", "reasons": [],
                "license": None, "installation": {"slotId": state["slot"], "kind": None, "quota": None},
                "consumption": {"vcpuBand": None, "current": None, "windowPeak": None, "windowPeakAt": None, "unknownHosts": 0},
                "grace": None}
    restricted = state.get("entitlement") == "restricted"
    return {"entitlement": state.get("entitlement", "granted"), "authenticity": "valid", "validity": "expired" if restricted else "active",
            "metering": "regular", "reasons": ["expired"] if restricted else [],
            "license": {"licenseId": "lic_e2e", "organizationId": "org_e2e", "revision": 1, "plan": "business",
                        "notBefore": "2026-11-01T00:00:00Z", "expiresAt": "2027-11-01T00:00:00Z",
                        "capabilities": state.get("capabilities", ["change-windows"]),
                        "capacity": {"mode": "limited", "nodeEquivalents": 10}},
            "installation": {"slotId": state["slot"], "kind": "production", "quota": 8},
            "consumption": {"vcpuBand": 32, "current": 3, "windowPeak": 4, "windowPeakAt": "2026-11-20T15:00:00Z", "unknownHosts": 1},
            "grace": None}


def is_business() -> bool:
    """Spec 031: cenários "business*" simulam a imagem Business; os "business-siem*" já têm licença com a capability
    da exportação ao SIEM (spec 027), em vigor ou restrita."""
    return STATE["scenario"].startswith("business")


AUDIT_EXPORT_CATEGORIES = {"environments", "containers", "commands", "images", "volumes", "networks", "pods", "inspection",
                           "terminal", "governance", "audit", "audit_export"}
AUDIT_EXPORT_FIELDS = {"name", "protocol", "host", "port", "path", "secretRef", "categories", "active"}


def audit_export_destination(item: dict) -> dict:
    """Situação simulada; "health" (só no mock) sobrepõe estado, fila, erro e lacuna de um destino indisponível."""
    view = {key: value for key, value in item.items() if key != "health"}
    return {**view, "secretFiles": {"ca": bool(item.get("secretRef")), "clientCertificate": False, "token": False},
            "state": "ok" if item["active"] else "disabled", "lastSentAt": None, "lastError": None,
            "pending": {"count": 0, "capped": False, "oldestAt": None}, "lastGapAt": None,
            **(item.get("health") or {})}


class Handler(BaseHTTPRequestHandler):
    server_version = "DokpodMock/1"

    def log_message(self, fmt: str, *args) -> None:  # silencioso salvo DOKPOD_MOCK_VERBOSE
        if os.environ.get("DOKPOD_MOCK_VERBOSE"):
            super().log_message(fmt, *args)

    def send_json(self, status: int, payload, content_type: str = "application/json", headers: dict | None = None) -> None:
        body = b"" if payload is None else json.dumps(payload).encode()
        self.send_response(status)
        self.send_header("Content-Type", content_type)
        self.send_header("Cache-Control", "no-store")
        for name, value in (headers or {}).items():
            self.send_header(name, value)
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

    def do_DELETE(self) -> None:
        self.route("DELETE")

    def licensing(self, method: str, path: str, q) -> None:
        """Spec 031 (T029): licenciamento da edição Business. A assinatura é verificada pela API real (T021); aqui só
        o arquivo de referência é aceito, e qualquer outro conteúdo é recusado como license_invalid."""
        if not is_business():
            return self.send_json(404, problem(404, "Não encontrado"), "application/problem+json")
        state = STATE.setdefault("licensing", {"version": 0, "license": None, "slot": None})
        if path == "/api/v1/licensing/status" and method == "GET":
            return self.send_json(200, licensing_status(state), headers={"ETag": f'"{state["version"]}"'})
        if path == "/api/v1/licensing/hosts" and method == "GET":
            return self.send_json(200, {"hosts": MOCK_LICENSING_HOSTS})
        if path == "/api/v1/licensing/license" and method == "PUT":
            length = int(self.headers.get("Content-Length") or 0)
            body = self.rfile.read(length).decode("ascii", errors="replace") if length else ""
            if self.headers.get("X-Dokpod-Antiforgery") != "mock-antiforgery":
                return self.send_json(400, problem(400, "Token antiforgery ausente ou inválido"), "application/problem+json")
            if self.headers.get("If-Match") != f'"{state["version"]}"':
                return self.send_json(412, {**problem(412, "Estado da licença mudou"), "code": "license_revision_conflict"}, "application/problem+json")
            if body.strip() != MOCK_VALID_LICENSE:
                STATE.setdefault("submitted", []).append(("license_import", q("slotId"), "rejected"))
                return self.send_json(422, {**problem(422, "Arquivo de licença recusado"), "code": "license_invalid"}, "application/problem+json")
            state.update(version=state["version"] + 1, license=True, slot=q("slotId"))
            STATE.setdefault("submitted", []).append(("license_import", q("slotId"), "accepted"))
            return self.send_json(200, licensing_status(state), headers={"ETag": f'"{state["version"]}"'})
        return self.send_json(404, problem(404, "Não encontrado"), "application/problem+json")

    def audit_export(self, method: str, path: str) -> None:
        """Spec 027 (T023): destinos de exportação conforme contracts/api.md. O envio real (TLS, cursor, licença) é
        coberto pelos testes da API com PostgreSQL e servidor syslog (T015–T017); aqui fica a jornada pela borda."""
        if not is_business():
            return self.send_json(404, problem(404, "Não encontrado"), "application/problem+json")
        items: list = STATE.setdefault("audit_export", [])
        restricted = STATE.get("licensing", {}).get("entitlement") == "restricted"
        denied = lambda code, status=403: self.send_json(status, problem(status, "Recusado", code), "application/problem+json")  # noqa: E731
        base = "/api/v1/settings/audit-export/destinations"
        if path == base and method == "GET":
            return self.send_json(200, [audit_export_destination(item) for item in items])
        if method != "GET" and self.headers.get("X-Dokpod-Antiforgery") != "mock-antiforgery":
            return self.send_json(400, problem(400, "Token antiforgery ausente ou inválido"), "application/problem+json")
        parts = path[len(base):].strip("/").split("/") if path.startswith(base) else []
        current = next((item for item in items if parts and item["destinationId"] == parts[0]), None)
        if method in ("POST", "PUT") and parts[-1:] != ["test"]:
            body = self.read_body()
            if set(body) - AUDIT_EXPORT_FIELDS or not set(body.get("categories", [])) <= AUDIT_EXPORT_CATEGORIES:
                return denied("audit_export_invalid_destination", 400)
            body["categories"] = sorted(set(body["categories"]) | {"audit_export"})
        if path == base and method == "POST":
            if restricted:
                return denied("license_expired")
            if len(items) >= 5:
                return denied("audit_export_limit_reached", 409)
            if any(item["name"].lower() == body["name"].lower() for item in items):
                return denied("audit_export_name_taken", 409)
            item = {"destinationId": f"d0000000-0000-4000-8000-{len(STATE.setdefault('submitted', [])) + 1:012d}",
                    **{key: body.get(key) for key in AUDIT_EXPORT_FIELDS}, "version": 1}
            items.append(item)
            STATE["submitted"].append(("audit_export_create", item["name"], self.headers.get("Idempotency-Key") is not None))
            return self.send_json(201, audit_export_destination(item), headers={"ETag": '"1"'})
        if current is None:
            return denied("audit_export_not_found", 404)
        if method == "POST" and parts[-1:] == ["test"]:
            if restricted:
                return denied("license_expired")
            delivered = current["host"] != "siem-fora.interno"
            STATE.setdefault("submitted", []).append(("audit_export_test", current["name"], delivered))
            return self.send_json(200, {"delivered": delivered, "errorCode": None if delivered else "connection_refused"})
        if self.headers.get("If-Match") != f'"{current["version"]}"':
            return denied("audit_export_version_conflict", 412)
        if method == "DELETE":
            items.remove(current)
            STATE.setdefault("submitted", []).append(("audit_export_delete", current["name"]))
            return self.send_json(204, None)
        if method == "PUT":
            only_deactivate = not body["active"] and all(body.get(key) == current.get(key) for key in AUDIT_EXPORT_FIELDS - {"active"})
            if restricted and not only_deactivate:
                return denied("license_expired")
            current.update({key: body.get(key) for key in AUDIT_EXPORT_FIELDS}, version=current["version"] + 1)
            STATE.setdefault("submitted", []).append(("audit_export_update", current["name"], current["active"]))
            return self.send_json(200, audit_export_destination(current), headers={"ETag": f'"{current["version"]}"'})
        return self.send_json(404, problem(404, "Não encontrado"), "application/problem+json")

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
            STATE["removed_volumes"] = set()
            STATE["removed_networks"] = set()
            STATE["registry_policies"] = {}
            STATE["pod_status"] = {}
            STATE["licensing"] = {"version": 0, "license": None, "slot": None}
            if STATE["scenario"].startswith("business-siem"):
                STATE["licensing"].update(license=True, slot="prd", capabilities=["change-windows", "audit-siem-export"],
                                          entitlement="restricted" if STATE["scenario"].endswith("restricted") else "granted")
            STATE["audit_export"] = []
            if STATE["scenario"] == "business-siem-gap":
                STATE["audit_export"].append({
                    "destinationId": "d0000000-0000-4000-8000-000000000098", "name": "siem-soc", "protocol": "syslog",
                    "host": "siem.exemplo.interno", "port": 6514, "path": None, "secretRef": "siem-soc",
                    "categories": ["audit_export", "containers"], "active": True, "version": 4,
                    "health": {"state": "gap", "lastSentAt": iso(4400), "lastError": {"code": "connection_refused", "at": iso(1)},
                               "pending": {"count": 10000, "capped": True, "oldestAt": iso(4300)}, "lastGapAt": iso(5)}})
            if STATE["scenario"] == "business-siem-restricted":
                STATE["audit_export"].append({
                    "destinationId": "d0000000-0000-4000-8000-000000000099", "name": "siem-soc", "protocol": "syslog",
                    "host": "siem.exemplo.interno", "port": 6514, "path": None, "secretRef": "siem-soc",
                    "categories": ["audit_export", "containers"], "active": True, "version": 1})
            ALERTS.clear()
            ALERTS.update(seed_alerts())
            AUDIT[:] = seed_audit()
            return self.send_json(200, {"scenario": STATE["scenario"]})
        if path == "/__mock/submitted":
            return self.send_json(200, STATE.get("submitted", []))
        if path == "/__mock/health":
            return self.send_json(200, {"ok": True})

        if path == "/bff/session":
            if STATE["scenario"] == "anonymous":
                return self.send_json(200, {"authenticated": False})
            return self.send_json(200, {"authenticated": True, "subject": "u-mariana", "name": "Mariana Costa", "displayGroup": "Plataforma · SRE"})
        if path == "/bff/version":
            return self.send_json(200, {"version": "0.1.0"})
        if path == "/bff/antiforgery":
            return self.send_json(200, {"requestToken": "mock-antiforgery"})
        if path.startswith("/api/v1/licensing/"):
            return self.licensing(method, path, q)
        if path.startswith("/api/v1/settings/audit-export/"):
            return self.audit_export(method, path)
        if path == "/bff/logout" and method == "POST":
            # Como o BFF real: POST de formulário com o token antiforgery no campo padrão do ASP.NET Core e
            # redirecionamento de página (o encerramento no Keycloak volta ao console). Sem o token, 400.
            length = int(self.headers.get("Content-Length") or 0)
            form = parse_qs(self.rfile.read(length).decode("utf-8")) if length else {}
            if "application/x-www-form-urlencoded" not in (self.headers.get("Content-Type") or "") or form.get("__RequestVerificationToken") != ["mock-antiforgery"]:
                return self.send_json(400, problem(400, "Token antiforgery ausente ou inválido"), "application/problem+json")
            STATE["scenario"] = "anonymous"
            STATE.setdefault("submitted", []).append(("logout",))
            self.send_response(302)
            self.send_header("Location", "/")
            self.send_header("Content-Length", "0")
            self.end_headers()
            return None
        if path.startswith("/hubs/"):
            return self.send_json(404, problem(404, "Tempo real indisponível no mock"))

        if not path.startswith("/api/v1/"):
            return self.send_json(404, problem(404, "Rota inexistente"))
        if STATE["scenario"] == "expire-next":
            STATE["scenario"] = "default"
            return self.send_json(401, problem(401, "Sessão expirada"), "application/problem+json")

        if path.startswith("/api/v1/audit/"):
            return self.audit_routes(path, q, query)
        if path.startswith("/api/v1/settings/"):
            return self.settings_routes(path)
        handled = self.alert_routes(method, path, q, query)
        if handled is not None:
            return handled
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
            if key in ("cpu", "memory"):
                # Spec 015: consumo atual; sem amostra no fim nas duas direções, por nome.
                field = "cpuPercent" if key == "cpu" else "memoryUsedBytes"
                sampled = sorted((c for c in rows if c.get("resources")), key=lambda c: c["resources"][field], reverse=reverse)
                rows = sampled + sorted((c for c in rows if not c.get("resources")), key=lambda c: c["name"])
            else:
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

        if path == "/api/v1/volumes":
            rows = [v for v in volumes() if not q("stage") or v["stage"] == q("stage")]
            visible = [h for h in hosts() if (not q("stage") or h["stage"] == q("stage")) and (not query.get("environmentId") or h["environmentId"] in query["environmentId"])]
            for key in ("environmentId", "engine"):
                if query.get(key):
                    rows = [v for v in rows if v[key] in query[key]]
            if q("anonymous") in ("true", "false"):
                rows = [v for v in rows if v["anonymous"] == (q("anonymous") == "true")]
            if q("q"):
                needle = q("q").lower()
                rows = [v for v in rows if needle in json.dumps([v["name"], v["labels"], v["mounts"]]).lower()]
            summary = {"total": len(rows), "unused": sum(1 for v in rows if not v["inUse"]),
                       "unavailableHosts": [{"environmentId": h["environmentId"], "environmentName": h["name"]} for h in visible
                                            if "VolumeInventory" not in ((h.get("agent") or {}).get("capabilities") or [])]}
            if q("unused") == "true":
                rows = [v for v in rows if not v["inUse"]]
            sort = q("sort") or "name"
            key = {"name": lambda v: v["name"], "createdAt": lambda v: v["createdAt"], "environment": lambda v: v["environmentName"]}[sort.lstrip("-")]
            rows.sort(key=key, reverse=sort.startswith("-"))
            return self.send_json(200, {"volumes": rows, "summary": summary, "nextCursor": None})

        if path == "/api/v1/pods":
            rows = [p for p in pods() if (not q("stage") or p["stage"] == q("stage")) and (not query.get("environmentId") or p["environmentId"] in query["environmentId"])]
            if q("containerId"):
                rows = [p for p in rows if any(c["containerId"] == q("containerId") for c in p["containers"])]
            if q("q"):
                rows = [p for p in rows if q("q").lower() in json.dumps([p["name"], p["labels"], [c["name"] for c in p["containers"]]]).lower()]
            by_status: dict[str, int] = {}
            for p in rows:
                by_status[p["status"]] = by_status.get(p["status"], 0) + 1
            if query.get("status"):
                rows = [p for p in rows if p["status"] in query["status"]]
            visible = [h for h in hosts() if (not q("stage") or h["stage"] == q("stage")) and (not query.get("environmentId") or h["environmentId"] in query["environmentId"])]
            ref = lambda h: {"environmentId": h["environmentId"], "environmentName": h["name"]}  # noqa: E731
            caps = lambda h: (h.get("agent") or {}).get("capabilities") or []  # noqa: E731
            summary = {"total": sum(by_status.values()), "byStatus": by_status,
                       "withoutPodsHosts": [ref(h) for h in visible if h.get("agent") and h["agent"].get("engine") == "docker"],
                       "unavailableHosts": [ref(h) for h in visible if h.get("agent") and h["agent"].get("engine") == "podman" and "PodInventory" not in caps(h)]}
            rows.sort(key=lambda p: p["name"], reverse=(q("sort") or "name").startswith("-"))
            return self.send_json(200, {"pods": rows, "summary": summary, "nextCursor": None})

        if path == "/api/v1/networks":
            rows = [v for v in networks() if not q("stage") or v["stage"] == q("stage")]
            visible = [h for h in hosts() if (not q("stage") or h["stage"] == q("stage")) and (not query.get("environmentId") or h["environmentId"] in query["environmentId"])]
            for key in ("environmentId", "engine", "driver"):
                if query.get(key):
                    rows = [v for v in rows if v[key] in query[key]]
            if q("system") in ("true", "false"):
                rows = [v for v in rows if v["system"] == (q("system") == "true")]
            if q("q"):
                needle = q("q").lower()
                rows = [v for v in rows if needle in json.dumps([v["name"], v["subnets"], v["labels"], v["containers"]]).lower() or v["networkId"].startswith(needle)]
            summary = {"total": len(rows), "unused": sum(1 for v in rows if not v["inUse"] and not v["system"]),
                       "unavailableHosts": [{"environmentId": h["environmentId"], "environmentName": h["name"]} for h in visible
                                            if "NetworkInventory" not in ((h.get("agent") or {}).get("capabilities") or [])]}
            if q("unused") == "true":
                rows = [v for v in rows if not v["inUse"] and not v["system"]]
            sort = q("sort") or "name"
            key = {"name": lambda v: v["name"], "createdAt": lambda v: v["createdAt"] or "", "environment": lambda v: v["environmentName"]}[sort.lstrip("-")]
            rows.sort(key=key, reverse=sort.startswith("-"))
            return self.send_json(200, {"networks": rows, "summary": summary, "nextCursor": None})

        if path == "/api/v1/registries":
            entries = [e for e in registry_entries() if (not q("stage") or e["stage"] == q("stage")) and (not query.get("environmentId") or e["environmentId"] in query["environmentId"])]
            by_registry: dict[str, list] = {}
            for e in entries:
                by_registry.setdefault(e_name(e), []).append(e)
            rows = [{"registry": r, "hosts": len(es), "origins": sorted({o for e in es for o in e["origins"]}), "images": sum(e["images"] for e in es),
                     "sizeBytes": sum(e["sizeBytes"] for e in es), "insecureHosts": sum(e["insecure"] for e in es), "credentialHosts": sum(e["credential"] for e in es),
                     "blockedHosts": sum(e["blocked"] for e in es), "outsidePolicyHosts": sum(not e["allowed"] for e in es),
                     "mirrorOf": next((e["mirrorOf"] for e in es if e["mirrorOf"]), None)} for r, es in by_registry.items()]
            if query.get("origin"):
                rows = [r for r in rows if set(r["origins"]) & set(query["origin"])]
            if q("q"):
                rows = [r for r in rows if q("q").lower() in r["registry"]]
            visible = [h for h in hosts() if (not q("stage") or h["stage"] == q("stage"))]
            summary = {"total": len(rows), "insecure": sum(r["insecureHosts"] > 0 for r in rows), "outsidePolicy": sum(r["outsidePolicyHosts"] > 0 for r in rows),
                       "unavailableHosts": [{"environmentId": h["environmentId"], "environmentName": h["name"]} for h in visible
                                            if "RegistryInventory" not in ((h.get("agent") or {}).get("capabilities") or [])]}
            if q("insecure") == "true":
                rows = [r for r in rows if r["insecureHosts"] > 0]
            if q("outsidePolicy") == "true":
                rows = [r for r in rows if r["outsidePolicyHosts"] > 0]
            sort = q("sort") or "registry"
            rows.sort(key={"registry": lambda r: r["registry"], "images": lambda r: r["images"], "hosts": lambda r: r["hosts"]}[sort.lstrip("-")], reverse=sort.startswith("-"))
            return self.send_json(200, {"registries": rows, "summary": summary, "nextCursor": None})
        rm = re.fullmatch(r"/api/v1/registries/([^/]+)", path)
        if rm:
            es = [dict(e) for e in registry_entries() if e_name(e) == rm.group(1)]
            if not es:
                return self.send_json(404, problem(404, "Registro não encontrado", "not_found"), "application/problem+json")
            return self.send_json(200, {"registry": rm.group(1), "hosts": sorted(es, key=lambda e: e["environmentName"])})
        if path == "/api/v1/registry-policies" and method == "GET":
            can_edit = "environment:manage" in scopes()
            return self.send_json(200, {"policies": [dict(registry_policy(k), canEdit=can_edit) for k in ("production", "staging", "development", "none")]})
        pm = re.fullmatch(r"/api/v1/registry-policies/([a-z]+)(/impact)?", path)
        if pm:
            stage_key = pm.group(1)
            if pm.group(2):
                proposed = query.get("registries") or []
                outside = [e for e in registry_entries() if (e["stage"] or "none") == stage_key and e["images"] > 0 and proposed and e_name(e) not in proposed]
                return self.send_json(200, {"images": sum(e["images"] for e in outside), "hosts": len({e["environmentId"] for e in outside})})
            if method == "PUT":
                if "environment:manage" not in scopes():
                    return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
                registries = sorted({r.strip().lower() for r in self.read_body().get("registries") or []})
                if any(("/" in r) or ("@" in r) or not r for r in registries):
                    return self.send_json(400, problem(400, "Registro inválido", "registry_invalid"), "application/problem+json")
                STATE.setdefault("registry_policies", {})[stage_key] = {"registries": registries, "by": "Mariana Costa", "at": iso(0)}
                STATE.setdefault("submitted", []).append(("registry_policy", stage_key, ",".join(registries) or "*"))
                return self.send_json(204, None)

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
        pm = re.fullmatch(r"/pods/([^/]+)(/actions)?", rest)
        if pm:
            key, sub = pm.group(1), pm.group(2) or ""
            pod = next((p for p in pods() if p["environmentId"] == environment_id and (p["podId"] == key or p["name"] == key)), None)
            if pod is None:
                return self.send_json(404, problem(404, "Pod não encontrado", "pod_not_found"), "application/problem+json")
            if sub == "":
                return self.send_json(200, pod)
            body = self.read_body()
            action = body.get("action")
            justification = (body.get("justification") or "").strip()
            if POD_ACTION_SCOPE.get(action) not in scopes():
                return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
            if pod["agentPod"] and action in ("stop", "pause", "restart"):
                return self.send_json(409, problem(409, "O pod do agente é protegido.", "agent_pod"), "application/problem+json")
            if action in ("stop", "pause") and not justification and ALERTS["policies"].get(host["stage"]):
                return self.send_json(422, problem(422, "Informe a justificativa para esta ação (política do estágio).", "justification_required"), "application/problem+json")
            idem = self.headers.get("Idempotency-Key")
            existing = next((c for c in STATE["commands"].values() if c.get("idempotencyKey") == idem), None)
            if existing:
                return self.send_json(202, {"commandId": existing["commandId"]})
            command_id = str(uuid.uuid4())
            partial = pod["name"] == "pod-batch" and action in ("start", "restart")
            STATE["commands"][command_id] = {
                "environmentId": environment_id, "commandId": command_id, "action": f"pod_{action}", "kind": "pod", "containerId": None,
                "expectedContainerRevision": None, "podName": pod["name"], "podId": pod["podId"], "podAction": action, "state": "pending", "failureCode": None,
                "result": None, "observedContainerRevision": None, "deadlineUtc": body.get("deadlineUtc"), "createdAtUtc": iso(0), "updatedAtUtc": iso(0),
                "completedAtUtc": None, "idempotencyKey": idem, "polls": 0, "failure": "pod_partial_failure" if partial else None,
            }
            STATE.setdefault("submitted", []).append(("pod_" + action, host["name"], pod["name"]) + ((justification,) if justification else ()))
            return self.send_json(202, {"commandId": command_id})
        nm = re.fullmatch(r"/networks/([^/]+)(/remove)?", rest)
        if nm:
            key, sub = nm.group(1), nm.group(2) or ""
            net = next((v for v in networks() if v["environmentId"] == environment_id and (v["networkId"] == key or v["name"] == key)), None)
            if net is None:
                return self.send_json(404, problem(404, "Rede não encontrada", "network_not_found"), "application/problem+json")
            if sub == "":
                return self.send_json(200, net)
            if method != "POST":
                return self.send_json(405, problem(405, "Método não permitido"))
            body = self.read_body()
            justification = (body.get("justification") or "").strip()
            if "network:delete" not in scopes():
                return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
            for refused, code in ((net["system"], "network_system"), (net["scope"] != "local", "network_not_local"), (net["inUse"], "network_in_use"),
                                  (not host["agent"]["connected"], "agent_disconnected")):
                if refused:
                    return self.send_json(409, problem(409, "Remoção recusada", code), "application/problem+json")
            if not justification and ALERTS["policies"].get(host["stage"]):
                return self.send_json(422, problem(422, "Informe a justificativa para remover a rede (política do estágio).", "justification_required"), "application/problem+json")
            if justification and not 10 <= len(justification) <= 500:
                return self.send_json(400, problem(400, "Justificativa inválida", "justification_invalid"), "application/problem+json")
            idem = self.headers.get("Idempotency-Key")
            existing = next((c for c in STATE["commands"].values() if c.get("idempotencyKey") == idem), None)
            if existing:
                return self.send_json(202, {"commandId": existing["commandId"]})
            command_id = str(uuid.uuid4())
            STATE["commands"][command_id] = {
                "environmentId": environment_id, "commandId": command_id, "action": "remove_network", "kind": "network", "containerId": None,
                "expectedContainerRevision": None, "networkId": net["networkId"], "state": "pending", "failureCode": None, "result": None,
                "observedContainerRevision": None, "deadlineUtc": body.get("deadlineUtc"), "createdAtUtc": iso(0), "updatedAtUtc": iso(0),
                "completedAtUtc": None, "idempotencyKey": idem, "polls": 0, "failure": "network_remove_failed" if net["name"].startswith("falha") else None,
            }
            STATE.setdefault("submitted", []).append(("remove_network", host["name"], net["name"]) + ((justification,) if justification else ()))
            return self.send_json(202, {"commandId": command_id})
        vm = re.fullmatch(r"/volumes/([^/]+)(/usage|/remove)?", rest)
        if vm:
            name, sub = vm.group(1), vm.group(2) or ""
            volume = next((v for v in volumes() if v["environmentId"] == environment_id and v["name"] == name), None)
            if volume is None:
                return self.send_json(404, problem(404, "Volume não encontrado", "volume_not_found"), "application/problem+json")
            if sub == "":
                return self.send_json(200, volume)
            if sub == "/usage":
                if not host["agent"]["connected"]:
                    return self.send_json(409, problem(409, "Agente desconectado", "agent_disconnected"), "application/problem+json")
                return self.send_json(200, {"sizeBytes": (1 + stable(name, 900)) * 1024**2, "refCount": len(volume["mounts"])})
            if method != "POST":
                return self.send_json(405, problem(405, "Método não permitido"))
            body = self.read_body()
            justification = (body.get("justification") or "").strip()
            if "volume:delete" not in scopes():
                return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
            if volume["inUse"]:
                return self.send_json(409, problem(409, "O volume está em uso por um container.", "volume_in_use"), "application/problem+json")
            if not host["agent"]["connected"]:
                return self.send_json(409, problem(409, "Agente desconectado", "agent_disconnected"), "application/problem+json")
            if not justification and ALERTS["policies"].get(host["stage"]):
                return self.send_json(422, problem(422, "Informe a justificativa para remover o volume (política do estágio).", "justification_required"), "application/problem+json")
            if justification and not 10 <= len(justification) <= 500:
                return self.send_json(400, problem(400, "Justificativa inválida", "justification_invalid"), "application/problem+json")
            key = self.headers.get("Idempotency-Key")
            existing = next((c for c in STATE["commands"].values() if c.get("idempotencyKey") == key), None)
            if existing:
                return self.send_json(202, {"commandId": existing["commandId"]})
            command_id = str(uuid.uuid4())
            STATE["commands"][command_id] = {
                "environmentId": environment_id, "commandId": command_id, "action": "remove_volume", "kind": "volume", "containerId": None,
                "expectedContainerRevision": None, "volumeName": name, "state": "pending", "failureCode": None, "result": None,
                "observedContainerRevision": None, "deadlineUtc": body.get("deadlineUtc"), "createdAtUtc": iso(0), "updatedAtUtc": iso(0),
                "completedAtUtc": None, "idempotencyKey": key, "polls": 0, "failure": "volume_remove_failed" if name.startswith("falha") else None,
            }
            STATE.setdefault("submitted", []).append(("remove_volume", host["name"], name) + ((justification,) if justification else ()))
            return self.send_json(202, {"commandId": command_id})
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
            if command["action"].startswith("pod_"):
                command["polls"] += 1
                if command["polls"] == 1:
                    command["state"] = "accepted"
                elif command["state"] not in ("succeeded", "failed"):
                    failure = command["failure"]
                    target = {"start": "running", "restart": "running", "unpause": "running", "stop": "exited", "pause": "paused"}[command["podAction"]]
                    STATE.setdefault("pod_status", {})[command["podId"]] = "degraded" if failure else target
                    command.update({"state": "failed" if failure else "succeeded", "failureCode": failure, "completedAtUtc": iso(0),
                                    "result": {"failedContainers": ["migracao-catalogo"]} if failure else None})
                return self.send_json(200, {k: v for k, v in command.items() if k not in ("idempotencyKey", "polls", "failure", "podId", "podAction")})
            if command["action"] == "remove_network":
                command["polls"] += 1
                if command["polls"] == 1:
                    command["state"] = "accepted"
                elif command["state"] not in ("succeeded", "failed"):
                    failure = command["failure"]
                    command.update({"state": "failed" if failure else "succeeded", "failureCode": failure, "completedAtUtc": iso(0)})
                    if not failure:
                        n = next(h[0] for h in all_hosts() if env_id(h[0]) == environment_id)
                        STATE.setdefault("removed_networks", set()).add((n, command["networkId"]))
                return self.send_json(200, {k: v for k, v in command.items() if k not in ("idempotencyKey", "polls", "failure", "networkId")})
            if command["action"] == "remove_volume":
                command["polls"] += 1
                if command["polls"] == 1:
                    command["state"] = "accepted"
                elif command["state"] not in ("succeeded", "failed"):
                    failure = command["failure"]
                    command.update({"state": "failed" if failure else "succeeded", "failureCode": failure, "completedAtUtc": iso(0)})
                    if not failure:
                        n = next(h[0] for h in all_hosts() if env_id(h[0]) == environment_id)
                        STATE.setdefault("removed_volumes", set()).add((n, command["volumeName"]))
                return self.send_json(200, {k: v for k, v in command.items() if k not in ("idempotencyKey", "polls", "failure", "volumeName")})
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
        tm = re.fullmatch(r"/containers/([^/]+)/terminal-policy", rest)
        if tm:
            container = find_container(environment_id, tm.group(1))
            reason = None
            if "container:exec" not in scopes():
                reason = "scope"
            elif not host["agent"]["connected"]:
                reason = "agent_disconnected"
            elif "ContainerExec" not in host["agent"]["capabilities"]:
                reason = "not_supported"
            elif container is None:
                reason = "not_found"
            elif container["displayStatus"] != "running":
                reason = "not_running"
            return self.send_json(200, {
                "allowed": reason is None, "reason": reason, "requiresJustification": host["stage"] == "production", "requiresRecentMfa": False,
                "limits": {"idleTimeoutSeconds": 600, "maxDurationSeconds": 1800, "maxPerUser": 2, "maxPerUserPerContainer": 1, "maxPerAgent": 4,
                           "justificationMinLength": 10, "justificationMaxLength": 500},
            })
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
            justification = (body.get("justification") or "").strip()
            if body.get("action") in ("stop", "pause", "delete"):
                if not justification and ALERTS["policies"].get(host["stage"]):
                    return self.send_json(422, problem(422, "Informe a justificativa para esta ação (política do estágio).", "justification_required"), "application/problem+json")
                if justification and not 10 <= len(justification) <= 500:
                    return self.send_json(400, problem(400, "Justificativa inválida", "justification_invalid"), "application/problem+json")
            command_id = str(uuid.uuid4())
            STATE["commands"][command_id] = {
                "environmentId": environment_id, "commandId": command_id, "action": body.get("action"), "containerId": container["containerId"],
                "expectedContainerRevision": body.get("expectedContainerRevision"), "state": "accepted", "failureCode": None,
                "observedContainerRevision": None, "deadlineUtc": body.get("deadlineUtc"), "createdAtUtc": iso(0), "updatedAtUtc": iso(0), "completedAtUtc": None,
            }
            STATE["commands"][command_id]["containerName"] = container["name"]
            STATE.setdefault("submitted", []).append((body.get("action"), container["name"]) + ((justification,) if justification else ()))
            if justification:
                ALERTS["justifications"].append({"action": body.get("action"), "container": container["name"], "reason": justification})
            return self.send_json(202, STATE["commands"][command_id])
        return self.send_json(404, problem(404, "Rota inexistente"))


def _alert_routes(self, method: str, path: str, q, query) -> object | None:
    """Rotas da spec 016 (contracts/alerts-api.md); devolve None quando a rota não é de alertas."""
    if not path.startswith(("/api/v1/alert", "/api/v1/action-justification-policies")):
        return None
    if "alert:read" not in scopes():
        return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
    manage = "alert:manage" in scopes()
    denied = lambda: self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")  # noqa: E731
    invalid = lambda code: self.send_json(400, problem(400, "Dados inválidos", code), "application/problem+json")  # noqa: E731
    if path == "/api/v1/alerts/summary":
        return self.send_json(200, alert_summary(q("stage")))
    if path == "/api/v1/alerts":
        history = q("view") == "history"
        rows = [a for a in ALERTS["alerts"] if (a["state"] == "resolved") == history]
        stage, text = q("stage"), (q("q") or "").lower()
        rows = [a for a in rows if (not stage or host_stage(a["environmentId"]) == stage)
                and (not query.get("severity") or a["severity"] in query["severity"])
                and (not query.get("state") or a["state"] in query["state"])
                and (not query.get("environmentId") or a["environmentId"] in query["environmentId"])
                and (not q("ruleId") or a["ruleId"] == q("ruleId"))
                and (not text or text in (a["condition"] + a["ruleName"] + (a["containerName"] or "")).lower())]
        rows.sort(key=lambda a: a["resolvedAt"] if history else a["lastOccurredAt"], reverse=True)
        return self.send_json(200, {"alerts": [alert_view(a) for a in rows], "nextCursor": None})
    m = re.fullmatch(r"/api/v1/alerts/([^/]+)(/[a-z]+)?", path)
    if m:
        a = next((x for x in ALERTS["alerts"] if x["id"] == m.group(1)), None)
        if a is None:
            return self.send_json(404, problem(404, "Alerta não encontrado", "not_found"), "application/problem+json")
        action = m.group(2)
        if not action:
            r = next((x for x in ALERTS["rules"] if x["id"] == a["ruleId"]), None)
            return self.send_json(200, {"alert": alert_view(a), "rule": r, "timeline": a["timeline"]})
        if method != "POST":
            return self.send_json(405, problem(405, "Método não permitido"))
        if not manage:
            return denied()
        body = self.read_body()
        text = (body.get("comment") or body.get("justification") or "").strip()
        if not 10 <= len(text) <= 500:
            return invalid("comment")
        if a["state"] == "resolved":
            return self.send_json(409, problem(409, "Alerta resolvido", "alert_resolved"), "application/problem+json")
        if action == "/acknowledge":
            a.update({"state": "acknowledged", "assignee": "u-mariana"})
            a["timeline"].append({"at": iso(0), "kind": "acknowledged", "actor": "u-mariana", "comment": text})
        elif action == "/resolve":
            a.update({"state": "resolved", "resolvedAt": iso(0), "resolution": "manual"})
            a["timeline"].append({"at": iso(0), "kind": "resolved", "actor": "u-mariana", "comment": text})
        elif action == "/silence":
            sid = str(uuid.uuid4())
            ALERTS["silences"].append({"id": sid, "scope": {"stage": None, "environmentId": None, "containerPattern": None}, "alertId": a["id"],
                                       "startsAt": iso(0), "endsAt": body.get("until"), "endedAt": None, "justification": text, "createdBy": "u-mariana"})
            a.update({"state": "silenced", "silenceId": sid})
            a["timeline"].append({"at": iso(0), "kind": "silenced", "actor": "u-mariana", "comment": text})
        return self.send_json(200, {"id": a["id"]})
    if path == "/api/v1/alert-rules" and method == "GET":
        return self.send_json(200, {"rules": ALERTS["rules"], "canManage": manage})
    m = re.fullmatch(r"/api/v1/alert-rules(?:/([^/]+))?", path)
    if m:
        if not manage:
            return denied()
        rid = m.group(1)
        existing = next((x for x in ALERTS["rules"] if x["id"] == rid), None) if rid else None
        if rid and existing is None:
            return self.send_json(404, problem(404, "Regra não encontrada", "not_found"), "application/problem+json")
        if method == "DELETE":
            if existing["builtIn"]:
                return self.send_json(409, problem(409, "Regra padrão", "built_in_rule"), "application/problem+json")
            ALERTS["rules"].remove(existing)
            for a in ALERTS["alerts"]:
                if a["ruleId"] == rid and a["state"] != "resolved":
                    a.update({"state": "resolved", "resolvedAt": iso(0), "resolution": "rule_removed"})
            return self.send_json(200, {"id": rid})
        body = self.read_body()
        if not (body.get("name") or "").strip():
            return invalid("name")
        if existing and body.get("version") != existing["version"]:
            return self.send_json(409, problem(409, "Versão desatualizada", "version_conflict"), "application/problem+json")
        rule = {**(existing or {"id": str(uuid.uuid4()), "builtIn": False, "version": 0}),
                **{k: body.get(k) for k in ("name", "description", "conditionKind", "eventType", "metric", "thresholdPercent", "durationSeconds", "severity", "enabled")},
                "minOccurrences": body.get("minOccurrences") or 1,
                "scope": body.get("scope") or {"stage": None, "environmentId": None, "containerPattern": None}}
        rule["version"] += 1
        if existing:
            ALERTS["rules"][ALERTS["rules"].index(existing)] = rule
        else:
            ALERTS["rules"].append(rule)
        STATE.setdefault("submitted", []).append(("rule", rule["name"], rule["enabled"]))
        return self.send_json(200, {"id": rule["id"]})
    if path == "/api/v1/alert-silences" and method == "GET":
        active = q("active") != "false"
        rows = [x for x in ALERTS["silences"] if not active or x["endedAt"] is None]
        return self.send_json(200, {"silences": rows, "canManage": manage})
    if path == "/api/v1/alert-silences" and method == "POST":
        if not manage:
            return denied()
        body = self.read_body()
        if not 10 <= len((body.get("justification") or "").strip()) <= 500:
            return invalid("justification")
        sid = str(uuid.uuid4())
        ALERTS["silences"].append({"id": sid, "scope": body.get("scope") or {"stage": None, "environmentId": None, "containerPattern": None},
                                   "alertId": None, "startsAt": body.get("startsAt") or iso(0), "endsAt": body["endsAt"], "endedAt": None,
                                   "justification": body["justification"].strip(), "createdBy": "u-mariana"})
        return self.send_json(200, {"id": sid})
    m = re.fullmatch(r"/api/v1/alert-silences/([^/]+)/end", path)
    if m and method == "POST":
        if not manage:
            return denied()
        silence = next((x for x in ALERTS["silences"] if x["id"] == m.group(1)), None)
        if silence is None:
            return self.send_json(404, problem(404, "Silenciamento não encontrado", "not_found"), "application/problem+json")
        silence["endedAt"] = iso(0)
        return self.send_json(200, {"id": silence["id"]})
    if path == "/api/v1/action-justification-policies":
        # Spec 022: autor e horário da última alteração e se o usuário pode editar (a regra real é por estágio).
        can_edit = "environment:manage" in scopes()
        meta = ALERTS.setdefault("policy_meta", {})
        return self.send_json(200, {"policies": [{"stage": k, "required": v, "actions": ["stop", "pause", "delete"],
                                                  "updatedBy": meta.get(k, {}).get("by"), "updatedAt": meta.get(k, {}).get("at"), "canEdit": can_edit}
                                                 for k, v in ALERTS["policies"].items()]})
    m = re.fullmatch(r"/api/v1/action-justification-policies/([a-z]+)", path)
    if m and method == "PUT":
        if "environment:manage" not in scopes():
            return denied()
        ALERTS["policies"][m.group(1)] = bool(self.read_body().get("required"))
        ALERTS.setdefault("policy_meta", {})[m.group(1)] = {"by": "Mariana Costa", "at": iso(0)}
        STATE.setdefault("submitted", []).append(("justification_policy", m.group(1), ALERTS["policies"][m.group(1)]))
        return self.send_json(204, None)
    return self.send_json(404, problem(404, "Rota inexistente"))


Handler.alert_routes = _alert_routes


SETTINGS_PARAMETERS = [
    ("retention", "Commands:Retention", "Comandos concluídos e seus parâmetros", "90 dias", "default"),
    ("retention", "EventRetention:Period", "Eventos operacionais", "30 dias", "default"),
    ("retention", "Alerts:ResolvedRetention", "Alertas resolvidos", "90 dias", "default"),
    ("retention", "Metrics:HostRetention", "Métricas por host", "2 dias", "installation"),
    ("retention", "Metrics:LatestExpiry", "Validade da última amostra de métrica", "45 s", "default"),
    ("retention", None, "Auditoria", "sem remoção automática (partições mensais)", "default"),
    ("limits", None, "Ações e remoções em lote", "50 itens por lote", "default"),
    ("limits", None, "Exportação da auditoria", "até 10.000 eventos por arquivo", "default"),
    ("limits", None, "Período de consulta da auditoria", "até 90 dias", "default"),
    ("terminal", "Terminal:IdleTimeout", "Encerramento por ociosidade", "10 min", "default"),
    ("terminal", "Terminal:MaxDuration", "Duração máxima da sessão", "30 min", "default"),
    ("terminal", "Terminal:MaxPerUser", "Sessões simultâneas por usuário", "2", "default"),
    ("terminal", "Terminal:RequireJustificationStages", "Estágios que exigem justificativa", "Produção", "default"),
]


def _settings_routes(self, path: str) -> object:
    """Rotas da spec 022 (contracts/settings-api.md): só com environment:manage em algum host."""
    allowed = "environment:manage" in scopes()
    if path == "/api/v1/settings/access":
        return self.send_json(200, {"allowed": allowed})
    if not allowed:
        return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
    if path == "/api/v1/settings/installation":
        rows = hosts()
        known = [h for h in rows if h.get("agent")]
        by_version: dict = {}
        by_engine: dict = {}
        for h in known:
            by_version[h["agent"]["agentVersion"]] = by_version.get(h["agent"]["agentVersion"], 0) + 1
            key = (h["agent"]["engine"], h["agent"]["engineVersion"])
            by_engine[key] = by_engine.get(key, 0) + 1
        keycloak_down = STATE["scenario"] == "keycloak-down"
        return self.send_json(200, {
            # Spec 031: edição fixada no build da API; o cenário "business" simula a imagem Business.
            "controlPlane": {"version": "0.1.0", "edition": "business" if is_business() else "community"},
            "protocolVersion": "1",
            "database": {"configured": True, "lastMigration": "202610160001_PodInventory", "pendingMigrations": 0},
            "dependencies": [
                {"name": "controlplane-api", "status": "healthy", "description": None, "durationMs": 0},
                {"name": "keycloak", "status": "unhealthy" if keycloak_down else "healthy",
                 "description": "Keycloak indisponível" if keycloak_down else None, "durationMs": 1200 if keycloak_down else 18},
                {"name": "postgresql", "status": "healthy", "description": None, "durationMs": 3},
            ],
            "checkedAt": iso(0),
            "agents": {
                "total": len(rows), "connected": sum(1 for h in known if h["agent"]["connected"]),
                "updateRequired": sum(1 for h in rows if h["situation"] == "update_required"), "unknown": len(rows) - len(known),
                "byAgentVersion": [{"version": k, "count": v} for k, v in sorted(by_version.items())],
                "byEngine": [{"engine": k[0], "version": k[1], "count": v} for k, v in sorted(by_engine.items())],
            },
        })
    if path == "/api/v1/settings/parameters":
        return self.send_json(200, {"parameters": [{"group": g, "key": k, "label": label, "value": v, "source": src}
                                                   for g, k, label, v, src in SETTINGS_PARAMETERS]})
    return self.send_json(404, problem(404, "Rota inexistente"))


Handler.settings_routes = _settings_routes


def _audit_routes(self, path: str, q, query) -> object:
    """Rotas da spec 017 (contracts/audit-api.md)."""
    readable = "audit:read" in scopes()
    if path == "/api/v1/audit/summary":
        return self.send_json(200, {"readableHosts": len(hosts()) if readable else 0, "oldestEventAt": AUDIT[-1]["occurredAt"] if readable else None})
    if not readable:
        return self.send_json(403, problem(403, "Acesso negado", "authorization_denied"), "application/problem+json")
    if path == "/api/v1/audit/events":
        rows = audit_filter(query)
        if rows is None:
            return self.send_json(400, problem(400, "Período longo", "period_too_long"), "application/problem+json")
        return self.send_json(200, {"events": rows, "nextCursor": None})
    m = re.fullmatch(r"/api/v1/audit/events/([^/]+)", path)
    if m:
        e = next((x for x in AUDIT if x["id"] == m.group(1)), None)
        if e is None:
            return self.send_json(404, problem(404, "Evento não encontrado", "not_found"), "application/problem+json")
        related = [audit_view(x) for x in AUDIT if x["id"] != e["id"] and (x["correlationId"] == e["correlationId"] or (e["commandId"] and x["commandId"] == e["commandId"]))]
        return self.send_json(200, {"event": audit_view(e), "related": related})
    if path == "/api/v1/audit/export":
        rows = audit_filter(query) or []
        lines = ["Horário (UTC);Ator;Identificador do ator;Tipo de ator;Ação;Categoria;Host;Alvo;Resultado;Código de falha;Correlação;Comando;Detalhes"]
        lines += [";".join([e["occurredAt"], e["actorName"] or "", e["actorId"], e["actorKind"], e["action"], e["category"] or "", e["environmentName"],
                            e["subjectId"] or "", e["outcome"], e["failureCode"] or "", e["correlationId"], e["commandId"] or "", ""]) for e in rows]
        AUDIT.insert(0, {"id": str(uuid.uuid4()), "occurredAt": iso(0), "correlationId": str(uuid.uuid4()), "actorKind": "user", "actorId": "u-mariana",
                         "actorName": "Mariana Costa", "action": "audit.exported", "environmentId": env_id(1), "outcome": "succeeded", "failureCode": None,
                         "subjectId": "audit", "commandId": None, "details": {"count": str(len(rows))}})
        body = ("\ufeff" + "\n".join(lines) + "\n").encode()
        self.send_response(200)
        self.send_header("Content-Type", "text/csv; charset=utf-8")
        self.send_header("Content-Disposition", 'attachment; filename="auditoria-mock.csv"')
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)
        return None
    return self.send_json(404, problem(404, "Rota inexistente"))


Handler.audit_routes = _audit_routes


def main() -> None:
    port = int(os.environ.get("DOKPOD_MOCK_PORT", "8090"))
    ThreadingHTTPServer(("0.0.0.0", port), Handler).serve_forever()


if __name__ == "__main__":
    main()
