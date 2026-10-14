"""Spec 012 (T013): inventário de imagens, pull em 2 hosts com falha legível, aviso de tag mutável, KPI e acessibilidade."""

import re
import time

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, no_horizontal_scroll, screenshot


def submitted(page: Page) -> list:
    return page.request.get(f"{BASE_URL}__mock/submitted").json()


def test_images_list_summary_unused_filter_and_partial_count(page: Page) -> None:
    goto(page, "imagens")
    expect(page.get_by_role("heading", name="Imagens nos hosts")).to_be_visible()
    table = page.get_by_role("table")
    expect(table.get_by_text("pedidos/api:3.14.1", exact=False).first).to_be_visible()
    expect(page.get_by_text("Contagem parcial", exact=False)).to_be_visible()
    page.get_by_role("button", name=re.compile(r"sem uso\s*, filtrar a lista")).click()
    expect(page).to_have_url(re.compile(r"semUso=1"))
    expect(table.get_by_text("Em uso", exact=True)).to_have_count(0)
    expect(table.get_by_text("Sem uso", exact=True).first).to_be_visible()
    screenshot(page, "012-imagens-sem-uso-1440")
    assert axe_violations(page) == []


def test_overview_kpi_links_to_unused_images_and_pull_button_opens_drawer(page: Page) -> None:
    goto(page)
    kpi = page.get_by_role("link", name=re.compile("imagens sem uso"))
    expect(kpi).to_be_visible()
    kpi.click()
    expect(page).to_have_url(re.compile(r"imagens\?.*semUso=1"))
    goto(page)
    page.get_by_role("link", name="Baixar imagem").click()
    expect(page.get_by_role("dialog", name="Baixar imagem")).to_be_visible()
    expect(page).not_to_have_url(re.compile(r"baixar=1"))


def test_pull_on_two_hosts_tracks_each_and_shows_readable_failure(page: Page) -> None:
    goto(page, "imagens")
    page.get_by_role("button", name="Baixar imagem").click()
    drawer = page.get_by_role("dialog", name="Baixar imagem")
    field = drawer.get_by_label("Referência da imagem")
    field.fill("user:senha@registry.empresa.com/app")
    field.blur()
    expect(drawer.get_by_text("sem credenciais", exact=False)).to_be_visible()
    field.fill("registry.empresa.com/privado/app:1")
    expect(drawer.get_by_text("é mutável", exact=False)).to_be_visible()
    expect(drawer.get_by_text("agente sem suporte a download")).to_be_visible()   # srv-prod-04 (agente 0.8.2)
    expect(drawer.get_by_text("agente desconectado")).to_be_visible()           # srv-dev-01 (todos os estágios)
    drawer.get_by_role("checkbox", name=re.compile("srv-prod-01")).check()
    drawer.get_by_role("checkbox", name=re.compile("srv-prod-02")).check()
    assert axe_violations(page) == []
    started = time.monotonic()
    drawer.get_by_role("button", name="Baixar em 2 hosts").click()
    progress = drawer.get_by_role("region", name="Andamento do download")
    expect(progress.get_by_text("Falhou", exact=True)).to_have_count(2, timeout=15_000)
    expect(progress.get_by_text("recusou as credenciais", exact=False).first).to_be_visible()
    elapsed = time.monotonic() - started
    assert sorted(x[1] for x in submitted(page)) == ["srv-prod-01", "srv-prod-02"]
    screenshot(page, "012-pull-falha-1440")
    print(f"SC-002 (mock): pull em 2 hosts até estado terminal em {elapsed:.1f}s")

    drawer.get_by_role("button", name="Novo download").click()
    drawer.get_by_label("Referência da imagem").fill("nginx:1.28")
    drawer.get_by_role("checkbox", name=re.compile("srv-prod-02")).uncheck()   # a seleção anterior é mantida
    drawer.get_by_role("button", name="Baixar imagem").click()
    expect(drawer.get_by_role("region", name="Andamento do download").get_by_text("Concluído", exact=True)).to_be_visible(timeout=15_000)
    drawer.get_by_text("Fechar", exact=True).click()
    page.get_by_role("searchbox", name="Busca rápida").fill("nginx:1.28")
    expect(page.get_by_role("table").get_by_text("nginx:1.28", exact=False)).to_be_visible(timeout=10_000)


def test_readonly_cannot_pull(page: Page, scenario) -> None:
    scenario("readonly")
    goto(page, "imagens")
    expect(page.get_by_role("button", name="Baixar imagem")).to_be_disabled()


def test_images_mobile_has_no_horizontal_scroll(page: Page) -> None:
    page.set_viewport_size({"width": 390, "height": 900})
    goto(page, "imagens")
    expect(page.get_by_role("heading", name="Imagens nos hosts")).to_be_visible()
    assert no_horizontal_scroll(page)
    screenshot(page, "012-imagens-390")
    page.get_by_role("button", name="Baixar imagem").click()
    expect(page.get_by_role("dialog", name="Baixar imagem")).to_be_visible()
    assert no_horizontal_scroll(page)
    assert axe_violations(page) == []
