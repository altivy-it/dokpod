"""Spec 007 (T028): moldura, navegação por teclado, estágio na URL, Em breve, 404, larguras e acessibilidade."""

import re

import pytest
from playwright.sync_api import Page, expect

from conftest import axe_violations, goto, no_horizontal_scroll, screenshot


def test_sign_in_screen_for_anonymous_session(page: Page, scenario) -> None:
    scenario("anonymous")
    goto(page)
    expect(page.get_by_role("heading", name="Docker e Podman sob uma única operação")).to_be_visible()
    expect(page.get_by_role("button", name="Entrar com Keycloak")).to_be_visible()
    assert axe_violations(page) == []
    screenshot(page, "007-entrada-1440")


def test_menu_is_keyboard_navigable_and_marks_current_page(page: Page) -> None:
    goto(page)
    page.keyboard.press("Tab")
    skip = page.get_by_role("link", name="Pular para o conteúdo")
    expect(skip).to_be_focused()
    menu = page.get_by_role("navigation", name="Menu principal")
    expect(menu.get_by_role("link", name="Visão geral", exact=True)).to_have_attribute("aria-current", "page")
    containers = menu.get_by_role("link", name="Containers", exact=True)
    containers.focus()
    page.keyboard.press("Enter")
    expect(page).to_have_url(re.compile(r"/containers"))
    expect(containers).to_have_attribute("aria-current", "page")


def test_stage_shortcut_persists_in_url_across_navigation(page: Page) -> None:
    goto(page)
    page.get_by_role("button", name=re.compile("Produção")).first.click()
    expect(page).to_have_url(re.compile(r"stage=production"))
    page.get_by_role("navigation", name="Menu principal").get_by_role("link", name="Containers", exact=True).click()
    expect(page).to_have_url(re.compile(r"/containers\?.*stage=production"))
    page.reload(wait_until="networkidle")
    expect(page).to_have_url(re.compile(r"stage=production"))


def test_coming_soon_and_not_found_pages(page: Page) -> None:
    goto(page, "em-breve/registros")
    expect(page.get_by_text("Registros em breve")).to_be_visible()
    expect(page.get_by_role("link", name="Voltar à visão geral")).to_be_visible()
    goto(page, "rota-que-nao-existe")
    expect(page.get_by_text("Página não encontrada").first).to_be_visible()
    expect(page.get_by_role("link", name="Voltar à visão geral")).to_be_visible()
    assert axe_violations(page) == []


@pytest.mark.parametrize("width", [1920, 1440, 1100, 390, 320])
def test_no_horizontal_scroll_at_supported_widths(page: Page, width: int) -> None:
    page.set_viewport_size({"width": width, "height": 900})
    for path in ("", "containers", "hosts", "em-breve/pods"):
        goto(page, path)
        assert no_horizontal_scroll(page), f"rolagem horizontal em {path or '/'} a {width}px"
    if width in (1440, 390):
        goto(page)
        screenshot(page, f"007-visao-geral-{width}")


def test_zoom_200_percent_keeps_content_reachable(browser) -> None:
    context = browser.new_context(viewport={"width": 720, "height": 450}, device_scale_factor=2)
    page = context.new_page()
    goto(page)
    expect(page.get_by_role("navigation", name="Menu principal")).to_be_visible()
    assert no_horizontal_scroll(page)
    context.close()


@pytest.mark.parametrize("path", ["", "containers", "hosts", "em-breve/alertas"])
def test_no_serious_or_critical_accessibility_violations(page: Page, path: str) -> None:
    goto(page, path)
    violations = axe_violations(page)
    assert violations == [], violations


def test_expired_session_offers_sign_in_again(page: Page, scenario) -> None:
    goto(page)
    scenario("expire-next")
    page.get_by_role("navigation", name="Menu principal").get_by_role("link", name="Containers", exact=True).click()
    expect(page.get_by_text("Sessão expirada")).to_be_visible()
    expect(page.get_by_role("button", name="Entrar novamente")).to_be_visible()
