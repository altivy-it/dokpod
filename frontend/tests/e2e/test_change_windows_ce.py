"""Spec 026 T038, cenário 5 do quickstart: com a imagem CE, as janelas de mudança não existem.

As rotas /api/v1/change-windows respondem 404, todo host traz changeWindow.state = none e uma ação que muda o host
(baixar uma imagem pequena) é aceita e concluída. Roda contra a stack real pela borda nginx.
"""

import os
import time
import uuid
from datetime import datetime, timedelta, timezone

import pytest
from playwright.sync_api import Page, expect


BASE_URL = os.environ.get("DOKPOD_E2E_BASE_URL", "https://localhost:7443/dokpod/")
ORIGIN = BASE_URL.split("/dokpod/")[0]
ENVIRONMENT_ID = os.environ.get("DOKPOD_E2E_ENVIRONMENT_ID")
PULL_REFERENCE = os.environ.get("DOKPOD_E2E_PULL_REFERENCE", "docker.io/library/busybox:1.37.0")


def api(path: str) -> str:
    return f"{BASE_URL.rstrip('/')}/api/v1/{path}"


def test_ce_has_no_change_window_routes(authenticated_page: Page) -> None:
    request = authenticated_page.request
    assert request.get(api("change-windows")).status == 404
    assert request.get(api("change-windows/production")).status == 404


def test_ce_reports_no_window_on_every_host(authenticated_page: Page) -> None:
    response = authenticated_page.request.get(api("environments"))
    expect(response).to_be_ok()
    environments = response.json()["environments"]
    assert environments, "nenhum ambiente visível para o usuário"
    for environment in environments:
        assert environment["changeWindow"]["state"] == "none", environment["environmentId"]


def test_ce_accepts_a_host_changing_action(authenticated_page: Page) -> None:
    if not ENVIRONMENT_ID:
        pytest.skip("DOKPOD_E2E_ENVIRONMENT_ID não configurado")
    request = authenticated_page.request
    token = request.get(f"{BASE_URL.rstrip('/')}/bff/antiforgery").json()["requestToken"]
    deadline = (datetime.now(timezone.utc) + timedelta(minutes=10)).isoformat().replace("+00:00", "Z")
    accepted = request.post(
        api(f"environments/{ENVIRONMENT_ID}/images/pull"),
        headers={"Origin": ORIGIN, "X-Dokpod-Antiforgery": token, "Idempotency-Key": str(uuid.uuid4())},
        data={"reference": PULL_REFERENCE, "deadlineUtc": deadline},
    )
    assert accepted.status == 202, accepted.text()
    command_id = accepted.json()["commandId"]

    state = None
    for _ in range(60):
        status = request.get(api(f"environments/{ENVIRONMENT_ID}/commands/{command_id}"))
        expect(status).to_be_ok()
        state = status.json()["state"]
        if state in ("succeeded", "failed", "indeterminate"):
            break
        time.sleep(2)
    assert state == "succeeded", state
