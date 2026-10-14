"""Fixtures da E2E de UI com respostas simuladas (borda nginx + mock do plano de controle)."""

import json
import os
from pathlib import Path

import pytest
from playwright.sync_api import Browser, Page, Playwright

BASE_URL = os.environ.get("DOKPOD_E2E_MOCK_BASE_URL", "http://dokpod-edge:8080/").rstrip("/") + "/"
AXE_PATH = os.environ.get("DOKPOD_E2E_AXE", "/app/axe/axe.min.js")
REPORTS = Path(os.environ.get("DOKPOD_E2E_REPORTS", "/app/reports"))


@pytest.fixture(scope="session")
def browser(playwright: Playwright) -> Browser:
    browser = playwright.chromium.launch()
    yield browser
    browser.close()


@pytest.fixture
def scenario(browser: Browser):
    """Seleciona o cenário do mock; volta ao padrão ao fim do teste."""
    context = browser.new_context()

    def set_scenario(name: str) -> None:
        response = context.request.post(f"{BASE_URL}__mock/scenario", data=json.dumps({"name": name}), headers={"Content-Type": "application/json"})
        assert response.ok, response.status

    set_scenario("default")
    yield set_scenario
    set_scenario("default")
    context.close()


@pytest.fixture
def page(browser: Browser, scenario) -> Page:
    context = browser.new_context(viewport={"width": 1440, "height": 900}, locale="pt-BR", timezone_id="America/Sao_Paulo")
    page = context.new_page()
    errors: list[str] = []
    page.on("pageerror", lambda error: errors.append(str(error)))
    yield page
    context.close()
    assert not errors, f"erros de JavaScript na página: {errors}"


def goto(page: Page, path: str = "") -> None:
    page.goto(BASE_URL + path.lstrip("/"), wait_until="networkidle")


def axe_violations(page: Page, impacts=("serious", "critical"), context: str | None = None) -> list[dict]:
    """Executa axe-core (WCAG 2.x A/AA) e devolve violações dos impactos informados."""
    page.add_script_tag(path=AXE_PATH)
    result = page.evaluate(
        """async (ctx) => {
            const r = await axe.run(ctx || document, { runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa'] } });
            return r.violations.map(v => ({ id: v.id, impact: v.impact, help: v.help, nodes: v.nodes.slice(0, 3).map(n => n.target.join(' ')) }));
        }""",
        context,
    )
    return [v for v in result if v["impact"] in impacts]


def no_horizontal_scroll(page: Page) -> bool:
    return page.evaluate("document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1")


def screenshot(page: Page, name: str) -> None:
    REPORTS.mkdir(parents=True, exist_ok=True)
    page.screenshot(path=str(REPORTS / f"{name}.png"), full_page=True)
