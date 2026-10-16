"""Spec 022 (T005): Configurações — políticas reunidas e iguais às telas de origem, instalação com versões, saúde e
agentes, retenções e limites somente leitura, item de menu só para administradores, teclado e acessibilidade."""

import re

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, no_horizontal_scroll, screenshot


def submitted(page: Page) -> list:
    return page.request.get(f"{BASE_URL}__mock/submitted").json()


def test_policies_match_the_original_screens_and_changes_show_up_in_both(page: Page) -> None:
    goto(page, "configuracoes")
    expect(page.locator("#dok-main").get_by_role("heading", name="Configurações", exact=True)).to_be_visible()
    expect(page.get_by_role("navigation", name="Menu principal").get_by_role("link", name="Configurações")).to_have_attribute("aria-current", "page")
    production = page.get_by_role("region", name="Produção")
    expect(production.get_by_role("checkbox", name="Obrigatória em Produção")).to_be_checked()
    staging = page.get_by_role("region", name="Homologação")
    expect(staging.get_by_role("checkbox", name="Obrigatória em Homologação")).not_to_be_checked()
    expect(staging.get_by_text("Qualquer registro")).to_be_visible()
    screenshot(page, "022-configuracoes-politicas-1440")
    assert axe_violations(page) == []

    # Justificativa: alterada aqui, aparece igual em Alertas → Regras (SC-001).
    staging.get_by_text("Obrigatória em Homologação").click()
    expect(page.get_by_text("Homologação: justificativa obrigatória para parar, pausar e remover.", exact=True)).to_be_visible()
    expect(staging.get_by_role("checkbox", name="Obrigatória em Homologação")).to_be_checked()
    expect(staging.get_by_text("Alterada por Mariana Costa", exact=False)).to_be_visible()
    assert ["justification_policy", "staging", True] in submitted(page)
    goto(page, "alertas?vista=regras")
    expect(page.get_by_role("checkbox", name="Homologação")).to_be_checked()

    # Registros permitidos: editados aqui, aparecem em Registros → Política por estágio.
    goto(page, "configuracoes")
    page.get_by_role("button", name="Editar os registros permitidos em Desenvolvimento").click()
    drawer = page.get_by_role("dialog")
    drawer.locator("#rp-registries").fill("quay.io")
    drawer.get_by_role("button", name="Salvar política").click()
    expect(page.get_by_role("region", name="Desenvolvimento").get_by_text("quay.io")).to_be_visible()
    goto(page, "registros?vista=politica")
    expect(page.get_by_role("list", name="Registros permitidos em Desenvolvimento").get_by_text("quay.io")).to_be_visible()


def test_installation_shows_versions_health_and_agents_with_link_to_hosts(page: Page, scenario) -> None:
    scenario("keycloak-down")
    goto(page, "configuracoes?vista=instalacao")
    versions = page.get_by_role("region", name="Versões")
    expect(versions.get_by_text("Plano de controle")).to_be_visible()
    expect(versions.get_by_text("0.1.0").first).to_be_visible()
    expect(versions.get_by_text("PodInventory (2026-10-16) · atualizado")).to_be_visible()
    deps = page.get_by_role("region", name="Dependências")
    expect(deps.get_by_text("Indisponível", exact=True)).to_be_visible()
    expect(deps.get_by_text("Keycloak indisponível")).to_be_visible()
    agents = page.get_by_role("region", name="Agentes")
    expect(agents.get_by_role("table", name="Versão do agente")).to_be_visible()
    screenshot(page, "022-configuracoes-instalacao-1440")
    assert axe_violations(page) == []
    agents.get_by_role("link", name=re.compile(r"ver em Hosts")).click()
    expect(page).to_have_url(re.compile(r"/hosts\?.*q=Atualizar"))


def test_retentions_and_limits_are_read_only_with_their_source(page: Page) -> None:
    goto(page, "configuracoes?vista=limites")
    retention = page.get_by_role("region", name="Retenções")
    row = retention.get_by_role("row", name=re.compile("Métricas por host"))
    expect(row.get_by_text("2 dias")).to_be_visible()
    expect(row.get_by_text("Configurado na instalação")).to_be_visible()
    expect(retention.get_by_role("row", name=re.compile("Comandos concluídos")).get_by_text("Padrão do Dokpod")).to_be_visible()
    expect(page.get_by_role("region", name="Terminal").get_by_text("10 min")).to_be_visible()
    expect(page.locator("#st-panel input, #st-panel textarea, #st-panel select")).to_have_count(0)
    screenshot(page, "022-configuracoes-limites-1440")
    assert axe_violations(page) == []


def test_menu_item_hidden_and_page_forbidden_without_administration(page: Page, scenario) -> None:
    scenario("readonly")
    goto(page)
    menu = page.get_by_role("navigation", name="Menu principal")
    expect(menu.get_by_role("link", name="Alertas")).to_be_visible()
    expect(menu.get_by_role("link", name="Configurações")).to_have_count(0)
    goto(page, "configuracoes?vista=instalacao")
    expect(page.get_by_text("Configurações exigem administração de algum host.")).to_be_visible()


def test_keyboard_tabs_and_narrow_widths(page: Page) -> None:
    goto(page, "configuracoes")
    tab = page.get_by_role("tab", name="Políticas")
    tab.focus()
    page.keyboard.press("ArrowRight")
    expect(page).to_have_url(re.compile(r"vista=instalacao"))
    expect(page.get_by_role("tab", name="Instalação")).to_be_focused()
    for width in (1100, 390, 320):
        page.set_viewport_size({"width": width, "height": 900})
        for view in ("", "?vista=instalacao", "?vista=limites"):
            goto(page, f"configuracoes{view}")
            assert no_horizontal_scroll(page), f"rolagem horizontal em configuracoes{view} a {width}px"
    screenshot(page, "022-configuracoes-limites-320")
