"""Spec 014 (T013/T015): aba Terminal com justificativa em Produção, estados de bloqueio, ações e acessibilidade.

O mock não tem hub SignalR: a sessão ao vivo é coberta pelo teste .NET ponta a ponta (API + agente + RPC `Exec`)
e pelos testes em Podman real; aqui a abertura termina em "Tempo real indisponível".
"""

import re

from playwright.sync_api import Page, expect

from conftest import axe_violations, goto, no_horizontal_scroll, screenshot


def env(n: int) -> str:
    return f"00000000-0000-4000-8000-{n:012d}"


def cid(name: str) -> str:
    return (name.encode().hex() * 4)[:64]


PROD = f"containers/{env(3)}/{cid('api-pedidos-7f9c')}"
STAGING = f"containers/{env(6)}/{cid('api-pedidos-hml')}"


def test_production_requires_justification_before_opening(page: Page) -> None:
    goto(page, f"{PROD}?aba=resumo")
    page.get_by_role("button", name="Abrir terminal").click()
    expect(page).to_have_url(re.compile(r"aba=terminal"))
    panel = page.get_by_role("tabpanel")
    field = panel.get_by_label("Justificativa (obrigatória em Produção)")
    expect(field).to_be_visible()
    submit = panel.get_by_role("button", name="Abrir terminal")
    field.fill("curta")
    expect(submit).to_be_disabled()
    field.fill("Investigar incidente INC-4242 no checkout")
    expect(submit).to_be_enabled()
    assert axe_violations(page) == []
    screenshot(page, "014-justificativa-1440")
    submit.click()
    expect(panel.get_by_text("Tempo real indisponível", exact=False)).to_be_visible(timeout=10_000)
    expect(panel.get_by_role("button", name="Abrir de novo")).to_be_visible()


def test_staging_opens_without_justification_and_shows_the_dark_panel(page: Page) -> None:
    goto(page, f"{STAGING}?aba=terminal")
    panel = page.get_by_role("tabpanel")
    expect(panel.get_by_role("group", name=re.compile("Terminal do container"))).to_be_visible()
    expect(panel.get_by_label("Justificativa (obrigatória em Produção)")).to_have_count(0)
    expect(panel.get_by_text("Ctrl+] sai do foco do terminal", exact=False).first).to_be_visible()
    box = panel.locator(".tt-term").bounding_box()
    assert box and box["height"] >= 360
    expect(panel.get_by_role("checkbox", name="Modo leitor de tela")).to_be_visible()
    screenshot(page, "014-terminal-1440")
    assert axe_violations(page) == []


def test_blocked_states_explain_why(page: Page, scenario) -> None:
    goto(page, f"containers/{env(4)}/{cid('prometheus')}?aba=terminal")
    expect(page.get_by_text("Terminal não suportado pelo agente deste host").first).to_be_visible()
    goto(page, f"containers/{env(5)}/{cid('relatorios-bi')}?aba=terminal")
    expect(page.get_by_text("Disponível somente com o container em execução").first).to_be_visible()
    scenario("readonly")
    goto(page, f"{STAGING}?aba=terminal")
    expect(page.get_by_role("button", name="Abrir terminal")).to_be_disabled()
    expect(page.get_by_text("Sem permissão para abrir terminal").first).to_be_visible()


def test_row_menu_offers_terminal_only_when_allowed(page: Page) -> None:
    goto(page, "containers")
    page.get_by_role("button", name="Ações de checkout-web em srv-prod-01").click()
    expect(page.get_by_role("menuitem", name="Abrir terminal")).to_be_visible()
    page.get_by_role("button", name="Ações de checkout-web em srv-prod-01").click()
    page.get_by_role("button", name="Ações de relatorios-bi em srv-batch-01").click()
    expect(page.get_by_role("menuitem", name="Abrir terminal")).to_have_count(0)


def test_terminal_tab_mobile_has_no_horizontal_scroll(page: Page) -> None:
    page.set_viewport_size({"width": 390, "height": 900})
    goto(page, f"{STAGING}?aba=terminal")
    expect(page.get_by_role("tabpanel").get_by_role("group", name=re.compile("Terminal do container"))).to_be_visible()
    assert no_horizontal_scroll(page)
    screenshot(page, "014-terminal-390")
