"""Spec 017 (T010): tela Auditoria, filtros na URL, detalhe com relacionados, atalho do container, exportação e acesso."""

import re

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, screenshot


def rows(page: Page):
    return page.get_by_role("table").locator("tbody tr")


def test_menu_opens_the_trail_of_the_last_24_hours(page: Page) -> None:
    goto(page, "")
    page.get_by_role("navigation").get_by_role("link", name="Auditoria").click()
    expect(page).to_have_url(re.compile(r"/auditoria"))
    expect(page.locator(".dok-page-head").get_by_role("heading", name="Auditoria", level=1)).to_be_visible()
    expect(rows(page)).to_have_count(6)  # o evento de 3 dias fica fora do padrão de 24 h
    expect(page.get_by_text("Mariana Costa").first).to_be_visible()
    expect(page.get_by_text("Parar container")).to_be_visible()
    expect(page.get_by_text(re.compile("Evento mais antigo disponível"))).to_be_visible()
    screenshot(page, "017-auditoria-1440")
    assert axe_violations(page) == []


def test_filters_are_combinable_and_kept_in_the_url(page: Page) -> None:
    goto(page, "auditoria")
    page.get_by_role("button", name=re.compile(r"^Terminal\s*, filtrar")).click()
    expect(page).to_have_url(re.compile(r"categoria=terminal"))
    expect(rows(page)).to_have_count(1)
    expect(page.get_by_text("Terminal negado")).to_be_visible()
    page.get_by_role("button", name=re.compile(r"Remover filtro Categoria")).click()
    page.get_by_role("button", name=re.compile(r"^negado\s*, filtrar")).click()
    expect(page).to_have_url(re.compile(r"resultado=denied"))
    expect(rows(page)).to_have_count(1)
    page.goto(page.url.replace("resultado=denied", "") + "&ator=Carlos", wait_until="networkidle")
    expect(rows(page)).to_have_count(2)
    page.get_by_label("Período", exact=True).select_option("7d")
    expect(page).to_have_url(re.compile(r"periodo=7d"))
    expect(rows(page)).to_have_count(2)
    page.get_by_label("Período", exact=True).select_option("personalizado")
    page.get_by_label("Desde").fill("2026-01-01")
    page.get_by_label("Desde").dispatch_event("change")
    expect(page.get_by_text("Período inválido")).to_be_visible()  # mais de 90 dias
    assert axe_violations(page) == []


def test_text_search_finds_justification_without_accents(page: Page) -> None:
    goto(page, "auditoria")
    page.get_by_role("searchbox", name="Busca rápida").fill("manutencao checkout")
    expect(page).to_have_url(re.compile(r"q=manutencao"))
    expect(rows(page)).to_have_count(1)
    expect(page.get_by_text("Parar container")).to_be_visible()
    expect(page.get_by_text("Busca:")).to_be_visible()


def test_event_detail_shows_masked_details_and_related_events(page: Page) -> None:
    goto(page, "auditoria")
    page.get_by_text("Parar container").click()
    drawer = page.get_by_role("dialog")
    expect(drawer.get_by_text("Janela de manutenção do checkout")).to_be_visible()
    expect(drawer.get_by_text("u-mariana")).to_be_visible()
    expect(drawer.get_by_role("link", name=re.compile("^636865"))).to_be_visible()  # alvo navegável (container)
    related = drawer.get_by_role("button", name=re.compile("Resultado do comando"))
    expect(related).to_be_visible()
    screenshot(page, "017-detalhe-1440")
    assert axe_violations(page) == []
    related.click()
    expect(page).to_have_url(re.compile(r"evento=0a0d1000-0000-4000-8000-000000000002"))
    expect(drawer.get_by_text("Agente").first).to_be_visible()


def test_container_detail_shortcut_filters_the_trail(page: Page) -> None:
    goto(page, "containers")
    page.get_by_role("table").get_by_text("checkout-web", exact=True).click()
    page.get_by_role("link", name="Ver auditoria").click()
    expect(page).to_have_url(re.compile(r"/auditoria\?.*q=636865"))
    expect(rows(page)).to_have_count(1)
    expect(page.get_by_text("Parar container")).to_be_visible()


def test_export_downloads_the_whole_filter_and_is_audited(page: Page) -> None:
    goto(page, "auditoria")
    with page.expect_download() as info:
        page.get_by_role("button", name="Exportar CSV").click()
    content = open(info.value.path(), encoding="utf-8-sig").read()
    assert content.splitlines()[0].startswith("Horário (UTC);Ator;Identificador do ator")
    assert len(content.strip().splitlines()) == 7
    page.get_by_role("button", name="Atualizar").click()
    expect(page.get_by_text("Auditoria exportada")).to_be_visible()


def test_user_without_audit_read_has_no_menu_and_no_access(page: Page, scenario) -> None:
    scenario("readonly")
    goto(page, "")
    expect(page.get_by_role("navigation").get_by_role("link", name="Auditoria")).to_have_count(0)
    goto(page, "auditoria")
    expect(page.get_by_text("Sem acesso à auditoria")).to_be_visible()
