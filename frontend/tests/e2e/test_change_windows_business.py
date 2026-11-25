"""Spec 026 T038, cenários 1 a 3 do quickstart com a imagem Business, licença com `change-windows` e agentes reais.

Os testes rodam em ordem, pela borda nginx, e mudam o estado do host:
- cenário 1: `admin-026` define a janela de Produção; `operador-026` não edita;
- cenário 2: com a janela fechada, as ações controladas de Produção dão `409 change_window_closed`, as leituras
  continuam e, num lote com os dois hosts, só o item de Produção é recusado;
- cenário 3: `operador-026` não tem a exceção; `plantao-026` para um container de Produção com ela e a auditoria
  liga os dois eventos pelo `commandId`; o lote com exceção só passa no host em que o usuário tem o scope.

No fim, as janelas dos dois estágios são removidas e os containers parados voltam a rodar. Variáveis:
`DOKPOD_E2E_PRD_ENVIRONMENT_ID`, `DOKPOD_E2E_HML_ENVIRONMENT_ID`, `DOKPOD_E2E_CONTAINERS` (três nomes separados por
vírgula, presentes nos dois hosts) e as credenciais `DOKPOD_E2E_USERNAME`/`DOKPOD_E2E_PASSWORD` (administrador, que lê
a auditoria e edita Homologação) e `T038_ADMIN_*`, `T038_OPERADOR_*`, `T038_PLANTAO_*`.
"""

import os
import time
import uuid
from datetime import datetime, timedelta, timezone
from zoneinfo import ZoneInfo

import pytest
from playwright.sync_api import Browser, Page, expect

from conftest import login


BASE_URL = os.environ.get("DOKPOD_E2E_BASE_URL", "https://localhost:7443/dokpod/")
ORIGIN = BASE_URL.split("/dokpod/")[0]
PRD = os.environ.get("DOKPOD_E2E_PRD_ENVIRONMENT_ID")
HML = os.environ.get("DOKPOD_E2E_HML_ENVIRONMENT_ID")
CONTAINERS = [name for name in os.environ.get("DOKPOD_E2E_CONTAINERS", "").split(",") if name]
ZONE = "America/Sao_Paulo"
# O valor depois de "token=" é falso; a auditoria deve mascará-lo (JustificationMasker, spec 016).
MASKING_PROBE = "t038-valor-falso"
JUSTIFICATION = f"T038: incidente simulado INC-026 token={MASKING_PROBE}"
STARTED_AT = datetime.now(timezone.utc)

pytestmark = pytest.mark.skipif(
    not (PRD and HML and len(CONTAINERS) >= 3 and os.environ.get("T038_PLANTAO_PASSWORD")),
    reason="ambientes, containers e usuários da T038 não configurados",
)


def api(path: str) -> str:
    return f"{BASE_URL.rstrip('/')}/api/v1/{path}"


def user_page(browser: Browser, prefix: str) -> Page:
    return login(browser, os.environ[f"{prefix}_USERNAME"], os.environ[f"{prefix}_PASSWORD"])


@pytest.fixture(scope="module")
def admin(browser: Browser) -> Page:
    page = login(browser, os.environ["DOKPOD_E2E_USERNAME"], os.environ["DOKPOD_E2E_PASSWORD"])
    yield page
    cleanup(page)
    page.context.close()


@pytest.fixture(scope="module")
def admin_026(browser: Browser) -> Page:
    page = user_page(browser, "T038_ADMIN")
    yield page
    page.context.close()


@pytest.fixture(scope="module")
def operador(browser: Browser) -> Page:
    page = user_page(browser, "T038_OPERADOR")
    yield page
    page.context.close()


@pytest.fixture(scope="module")
def plantao(browser: Browser) -> Page:
    page = user_page(browser, "T038_PLANTAO")
    yield page
    page.context.close()


def mutation_headers(page: Page) -> dict[str, str]:
    token = page.request.get(f"{BASE_URL.rstrip('/')}/bff/antiforgery").json()["requestToken"]
    return {"Origin": ORIGIN, "X-Dokpod-Antiforgery": token, "Idempotency-Key": str(uuid.uuid4())}


def deadline() -> str:
    return (datetime.now(timezone.utc) + timedelta(minutes=5)).isoformat().replace("+00:00", "Z")


def window(page: Page, stage: str) -> dict:
    response = page.request.get(api("change-windows"))
    expect(response).to_be_ok()
    return next(item for item in response.json()["stages"] if item["stage"] == stage)


def save_window(page: Page, stage: str, ranges: list[dict]):
    version = window(page, stage)["version"]
    headers = mutation_headers(page)
    headers.pop("Idempotency-Key")
    headers |= {"If-None-Match": "*"} if version is None else {"If-Match": f'"{version}"'}
    return page.request.put(api(f"change-windows/{stage}"), headers=headers, data={"timeZone": ZONE, "ranges": ranges})


def closed_ranges() -> tuple[list[dict], datetime]:
    """Uma faixa diária que começa duas horas depois da hora local atual e dura uma hora: a janela está fechada agora."""
    local = datetime.now(ZoneInfo(ZONE))
    start = (local.hour + 2) % 24
    days = ["mon", "tue", "wed", "thu", "fri", "sat", "sun"]
    opens = local.replace(minute=0, second=0, microsecond=0) + timedelta(hours=2)
    return [{"days": days, "start": f"{start:02d}:00", "end": f"{(start + 1) % 24:02d}:00"}], opens.astimezone(timezone.utc)


def container(page: Page, environment: str, name: str) -> dict:
    response = page.request.get(api(f"environments/{environment}/containers"))
    expect(response).to_be_ok()
    return next(item for item in response.json()["containers"] if item["name"].lstrip("/") == name)


def command(page: Page, environment: str, name: str, action: str, **extra):
    item = container(page, environment, name)
    return page.request.post(
        api(f"environments/{environment}/containers/{item['containerId']}/commands"),
        headers=mutation_headers(page),
        data={"action": action, "expectedContainerRevision": item["revision"], "deadlineUtc": deadline(), **extra},
    )


def wait_command(page: Page, environment: str, command_id: str) -> str:
    state = None
    for _ in range(60):
        response = page.request.get(api(f"environments/{environment}/commands/{command_id}"))
        expect(response).to_be_ok()
        state = response.json()["state"]
        if state in ("succeeded", "failed", "indeterminate"):
            break
        time.sleep(2)
    return state


def audit(page: Page, action: str, environment: str) -> list[dict]:
    since = STARTED_AT.isoformat().replace("+00:00", "Z")
    response = page.request.get(api("audit/events"), params={"action": action, "environmentId": environment, "since": since, "limit": "100"})
    expect(response).to_be_ok()
    return response.json()["events"]


def assert_closed(response, expected_opening: datetime) -> None:
    assert response.status == 409, response.text()
    body = response.json()
    assert body["code"] == "change_window_closed", body
    assert body["timeZone"] == ZONE
    assert datetime.fromisoformat(body["nextWindowStartsAt"].replace("Z", "+00:00")) == expected_opening


def cleanup(page: Page) -> None:
    for stage in ("production", "staging"):
        version = window(page, stage)["version"]
        if version is not None:
            headers = mutation_headers(page)
            headers.pop("Idempotency-Key")
            page.request.delete(api(f"change-windows/{stage}"), headers=headers | {"If-Match": f'"{version}"'})
    for name in CONTAINERS:
        if container(page, HML, name)["state"] != "running":
            response = command(page, HML, name, "start")
            if response.status == 202:
                wait_command(page, HML, response.json()["commandId"])


# Cenário 1: definir a janela.


def test_1_admin_defines_the_production_window(admin_026: Page, admin: Page) -> None:
    ranges = [{"days": ["mon", "tue", "wed", "thu", "fri"], "start": "22:00", "end": "06:00"}]
    response = save_window(admin_026, "production", ranges)
    assert response.status == 200, response.text()
    assert response.headers["etag"].startswith('"')

    item = window(admin_026, "production")
    assert item["editable"] is True
    assert item["timeZone"] == ZONE
    assert item["ranges"] == ranges
    assert item["updatedBy"] and item["updatedAt"]
    assert item["status"]["state"] in ("open", "closed")
    # D-007: sem nenhum host legível em Homologação, a regra usa todos os hosts legíveis (só o de Produção, que
    # admin-026 gerencia); por isso Homologação também é editável para ele.
    assert window(admin_026, "staging")["editable"] is True
    assert audit(admin, "change_window.updated", PRD), "sem change_window.updated no host de Produção"


def test_1_operator_cannot_edit_the_window(operador: Page) -> None:
    # Sem environment:manage em nenhum host, a vista de Políticas não abre e a edição é recusada.
    assert operador.request.get(api("change-windows")).status == 403
    headers = mutation_headers(operador)
    headers.pop("Idempotency-Key")
    response = operador.request.put(
        api("change-windows/production"), headers=headers | {"If-Match": '"1"'}, data={"timeZone": ZONE, "ranges": []}
    )
    assert response.status == 403, response.text()


# Cenário 2: recusa fora da janela.


def test_2_controlled_actions_are_refused_outside_the_window(admin_026: Page, operador: Page, admin: Page) -> None:
    ranges, opening = closed_ranges()
    assert save_window(admin_026, "production", ranges).status == 200
    host = operador.request.get(api(f"environments/{PRD}"))
    expect(host).to_be_ok()
    assert host.json()["changeWindow"]["state"] == "closed"

    name = CONTAINERS[0]
    attempts = {
        "stop": command(operador, PRD, name, "stop"),
        "restart": command(operador, PRD, name, "restart"),
        "create": operador.request.post(
            api(f"environments/{PRD}/containers"),
            headers=mutation_headers(operador),
            data={"image": "docker.io/library/busybox:1.37.0", "name": "t038-novo", "deadlineUtc": deadline()},
        ),
        "pull": operador.request.post(
            api(f"environments/{PRD}/images/pull"),
            headers=mutation_headers(operador),
            data={"reference": "docker.io/library/busybox:1.37.0", "deadlineUtc": deadline()},
        ),
        "volume": operador.request.post(
            api(f"environments/{PRD}/volumes/t038-volume/remove"), headers=mutation_headers(operador), data={"deadlineUtc": deadline()}
        ),
        "network": operador.request.post(
            api(f"environments/{PRD}/networks/{'b' * 64}/remove"), headers=mutation_headers(operador), data={"deadlineUtc": deadline()}
        ),
        "pod": operador.request.post(
            api(f"environments/{PRD}/pods/{'c' * 64}/actions"),
            headers=mutation_headers(operador),
            data={"action": "stop", "deadlineUtc": deadline()},
        ),
    }
    for kind, response in attempts.items():
        print(kind, response.status, response.text()[:200])
    for response in attempts.values():
        assert_closed(response, opening)
    assert len(audit(admin, "change_window.denied", PRD)) >= len(attempts)

    # SC-003: leituras do mesmo host continuam.
    item = container(operador, PRD, name)
    base = f"environments/{PRD}/containers/{item['containerId']}"
    for path in (f"{base}/inspect", f"{base}/logs?tail=10", "alerts", f"audit/events?environmentId={PRD}&limit=5"):
        response = operador.request.get(api(path))
        assert response.status == 200, f"{path}: {response.status}"


def test_2_console_shows_the_closed_window(operador: Page) -> None:
    item = container(operador, PRD, CONTAINERS[0])
    operador.goto(f"{BASE_URL.rstrip('/')}/containers/{PRD}/{item['containerId']}", wait_until="networkidle")
    expect(operador.get_by_text("Fora da janela de mudança").first).to_be_visible()


def test_2_batch_refuses_only_the_production_item(operador: Page) -> None:
    production = command(operador, PRD, CONTAINERS[0], "stop")
    staging = command(operador, HML, CONTAINERS[2], "stop")
    assert production.status == 409 and production.json()["code"] == "change_window_closed", production.text()
    assert staging.status == 202, staging.text()
    assert wait_command(operador, HML, staging.json()["commandId"]) == "succeeded"


# Cenário 3: exceção de emergência.


def test_3_operator_has_no_override(operador: Page) -> None:
    response = command(operador, PRD, CONTAINERS[0], "stop", changeWindowOverride=True, justification=JUSTIFICATION)
    assert response.status == 403, response.text()
    assert response.json()["code"] == "change_override_denied"


def test_3_on_call_stops_with_override_and_audit_links_the_command(plantao: Page, admin: Page) -> None:
    response = command(plantao, PRD, CONTAINERS[1], "stop", changeWindowOverride=True, justification=JUSTIFICATION)
    assert response.status == 202, response.text()
    command_id = response.json()["commandId"]
    assert wait_command(plantao, PRD, command_id) == "succeeded"

    overrides = [event for event in audit(admin, "change_window.override", PRD) if event.get("commandId") == command_id]
    stops = [event for event in audit(admin, "container.stop", PRD) if event.get("commandId") == command_id]
    print("override:", overrides)
    print("stop:", stops)
    assert len(overrides) == 1 and len(stops) == 1
    assert overrides[0]["actorId"] == stops[0]["actorId"]
    # A justificativa fica na auditoria com o segredo mascarado.
    recorded = overrides[0]["details"]["justification"]
    assert recorded.startswith("T038: incidente simulado INC-026") and "token=***" in recorded
    assert MASKING_PROBE not in str(overrides) + str(stops)

    # Não existe período de exceção: a próxima ação no host fechado pede outra.
    again = command(plantao, PRD, CONTAINERS[0], "stop")
    assert again.status == 409 and again.json()["code"] == "change_window_closed", again.text()


def test_3_batch_override_only_passes_where_the_scope_exists(plantao: Page, admin: Page) -> None:
    ranges, _ = closed_ranges()
    assert save_window(admin, "staging", ranges).status == 200
    production = command(plantao, PRD, CONTAINERS[1], "start", changeWindowOverride=True, justification=JUSTIFICATION)
    staging = command(plantao, HML, CONTAINERS[2], "start", changeWindowOverride=True, justification=JUSTIFICATION)
    assert production.status == 202, production.text()
    assert wait_command(plantao, PRD, production.json()["commandId"]) == "succeeded"
    assert staging.status == 403 and staging.json()["code"] == "change_override_denied", staging.text()
