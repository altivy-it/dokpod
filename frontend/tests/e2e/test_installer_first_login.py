"""Spec 033 T034 (SC-001, SC-002, FR-014): primeiro acesso a uma instalação feita pelo instalador.

Entra pela borda com o administrador inicial e a senha temporária, troca a senha como o Keycloak exige
(UPDATE_PASSWORD) e confere a sessão do BFF, o ambiente local com o agente conectado e o inventário de containers.

Variáveis, só em runtime: DOKPOD_E2E_BASE_URL, DOKPOD_E2E_USERNAME, DOKPOD_E2E_TEMPORARY_PASSWORD,
DOKPOD_E2E_NEW_PASSWORD e DOKPOD_E2E_ENVIRONMENT_ID. Sem elas, o teste é pulado.
"""

import os
import re

import pytest
from playwright.sync_api import Browser, expect


BASE_URL = os.environ.get("DOKPOD_E2E_BASE_URL", "https://localhost:7443/dokpod/")
USERNAME = os.environ.get("DOKPOD_E2E_USERNAME")
TEMPORARY_PASSWORD = os.environ.get("DOKPOD_E2E_TEMPORARY_PASSWORD")
NEW_PASSWORD = os.environ.get("DOKPOD_E2E_NEW_PASSWORD")
ENVIRONMENT_ID = os.environ.get("DOKPOD_E2E_ENVIRONMENT_ID")
REPORT_DIRECTORY = os.environ.get("DOKPOD_E2E_REPORT_DIRECTORY")


def test_first_login_changes_password_and_shows_local_host(browser: Browser) -> None:
    if not (USERNAME and TEMPORARY_PASSWORD and NEW_PASSWORD and ENVIRONMENT_ID):
        pytest.skip("variáveis do primeiro acesso não configuradas")

    # A CA local da instalação não está no navegador do container; a cadeia é conferida pelo instalador e pelos testes Pester.
    context = browser.new_context(ignore_https_errors=True)
    page = context.new_page()
    try:
        page.goto(BASE_URL, wait_until="domcontentloaded")
        if page.get_by_role("button", name="Entrar com Keycloak").is_visible():
            page.get_by_role("button", name="Entrar com Keycloak").click()

        # Ids dos formulários do Keycloak: os rótulos seguem o idioma do realm (pt-BR na instalação).
        page.locator("#username").fill(USERNAME)
        page.locator("#password").fill(TEMPORARY_PASSWORD)
        page.locator("#kc-login").click()

        # A senha temporária obriga a troca antes de voltar ao console.
        page.locator("#password-new").fill(NEW_PASSWORD)
        page.locator("#password-confirm").fill(NEW_PASSWORD)
        page.locator("#kc-passwd-update-form [type=submit]").click()

        page.wait_for_load_state("networkidle")
        expect(page).to_have_url(re.compile(r"/dokpod/"))
        expect(page.get_by_role("main")).to_be_visible()

        session = page.request.get(f"{BASE_URL.rstrip('/')}/bff/session")
        expect(session).to_be_ok()
        assert session.json().get("authenticated") is True

        environment = page.request.get(f"{BASE_URL.rstrip('/')}/api/v1/environments/{ENVIRONMENT_ID}")
        expect(environment).to_be_ok()
        payload = environment.json()
        assert payload["environmentId"].lower() == ENVIRONMENT_ID.lower()
        assert payload["agent"]["connected"] is True

        containers = page.request.get(f"{BASE_URL.rstrip('/')}/api/v1/environments/{ENVIRONMENT_ID}/containers")
        expect(containers).to_be_ok()
        assert containers.json()["containers"]
    finally:
        if REPORT_DIRECTORY:
            page.screenshot(path=os.path.join(REPORT_DIRECTORY, "installer-first-login.png"), full_page=True)
        context.close()
