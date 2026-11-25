import os
import re

import pytest
from playwright.sync_api import Browser, Page, Playwright, expect


BASE_URL = os.environ.get("DOKPOD_E2E_BASE_URL", "https://localhost:7443/dokpod/")
USERNAME = os.environ.get("DOKPOD_E2E_USERNAME")
PASSWORD = os.environ.get("DOKPOD_E2E_PASSWORD")
ENVIRONMENT_ID = os.environ.get("DOKPOD_E2E_ENVIRONMENT_ID")


def require_credentials() -> tuple[str, str]:
    if not USERNAME or not PASSWORD:
        pytest.skip("DOKPOD_E2E_USERNAME e DOKPOD_E2E_PASSWORD não configurados")
    return USERNAME, PASSWORD


@pytest.fixture(scope="session")
def browser(playwright: Playwright) -> Browser:
    return playwright.chromium.launch()


@pytest.fixture
def authenticated_page(browser: Browser) -> Page:
    username, password = require_credentials()
    page = login(browser, username, password)
    yield page
    page.context.close()


def login(browser: Browser, username: str, password: str) -> Page:
    """Abre um contexto novo e entra pelo BFF e pelo Keycloak; quem chama fecha o contexto."""
    context = browser.new_context(ignore_https_errors=True)
    page = context.new_page()
    page.goto(BASE_URL, wait_until="domcontentloaded")

    if page.get_by_role("button", name="Entrar com Keycloak").is_visible():
        page.get_by_role("button", name="Entrar com Keycloak").click()

    # Pelos nomes dos campos, para valer com o Keycloak em qualquer idioma (o quality está em pt-BR).
    if "/realms/" in page.url or "protocol/openid-connect" in page.url:
        page.locator("input[name=username]").fill(username)
        page.locator("input[name=password]").fill(password)
        page.locator("#kc-login, button[type=submit]").first.click()

    page.wait_for_load_state("networkidle")
    expect(page).not_to_have_url(re.compile(r"/bff/login"))
    return page