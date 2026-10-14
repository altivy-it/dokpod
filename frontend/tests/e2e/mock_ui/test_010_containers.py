"""Spec 010 (T015): busca, filtros, lote (sucesso/falha/ignorados), remoção reforçada, pausa, CSV e somente leitura."""

import re

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, screenshot


def row(page: Page, name: str):
    return page.get_by_role("table").locator("tr", has=page.get_by_text(name, exact=True))


def select(page: Page, *names: str) -> None:
    for name in names:
        page.get_by_role("checkbox", name=re.compile(f"^Selecionar {re.escape(name)} em")).click()


def submitted(page: Page) -> list:
    return page.request.get(f"{BASE_URL}__mock/submitted").json()


def test_search_filters_and_chips_update_the_url(page: Page) -> None:
    goto(page, "containers")
    page.get_by_role("searchbox", name="Busca rápida").fill("api")
    expect(page).to_have_url(re.compile(r"q=api"))
    expect(page.get_by_role("table").locator("tbody tr")).to_have_count(2)
    page.get_by_role("searchbox", name="Busca rápida").fill("")
    page.get_by_role("button", name="Filtros").click()
    page.get_by_role("menuitemcheckbox", name=re.compile("Podman")).click()
    expect(page).to_have_url(re.compile(r"engine=podman"))
    expect(page.get_by_text("Engine:")).to_be_visible()
    page.get_by_role("button", name="Remover filtro Engine Podman").click()
    expect(page).not_to_have_url(re.compile(r"engine="))
    assert axe_violations(page) == []


def test_batch_restart_reports_each_container_and_skips_ineligible(page: Page) -> None:
    goto(page, "containers")
    select(page, "checkout-web", "postgres-principal", "relatorios-bi")
    expect(page.get_by_text("3 containers selecionados")).to_be_visible()
    page.get_by_role("button", name="Reiniciar", exact=True).first.click()
    drawer = page.get_by_role("dialog")
    expect(drawer.get_by_text("2 containers afetados")).to_be_visible()
    expect(drawer.get_by_text("1 ignorado")).to_be_visible()
    drawer.get_by_role("button", name="Reiniciar").click()
    progress = page.get_by_role("region", name="Andamento do lote")
    expect(progress.get_by_text("Concluído", exact=True)).to_have_count(2, timeout=15_000)
    assert sorted(submitted(page)) == [["restart", "checkout-web"], ["restart", "postgres-principal"]]


def test_batch_start_shows_individual_failure(page: Page) -> None:
    goto(page, "containers")
    select(page, "migracao-catalogo", "relatorios-bi")
    page.get_by_role("button", name="Iniciar", exact=True).click()
    page.get_by_role("dialog").get_by_role("button", name="Iniciar").click()
    progress = page.get_by_role("region", name="Andamento do lote")
    expect(progress.get_by_text("Falhou: start_failed")).to_be_visible(timeout=15_000)
    expect(progress.get_by_text("Concluído", exact=True)).to_have_count(1)


def test_production_bulk_removal_requires_typing_the_quantity(page: Page) -> None:
    goto(page, "containers")
    select(page, "migracao-catalogo", "relatorios-bi")
    page.get_by_role("button", name="Remover", exact=True).click()
    drawer = page.get_by_role("dialog")
    expect(drawer.get_by_text("Volumes não são removidos.")).to_be_visible()
    confirm = drawer.get_by_role("button", name="Remover")
    expect(confirm).to_be_disabled()
    drawer.get_by_label(re.compile("Digite 2 para confirmar")).fill("2")
    expect(confirm).to_be_enabled()
    screenshot(page, "010-remocao-reforcada-1440")
    confirm.click()
    expect(page.get_by_role("region", name="Andamento do lote").get_by_text("Concluído", exact=True)).to_have_count(2, timeout=15_000)


def test_pause_and_resume_from_row_menu(page: Page) -> None:
    goto(page, "containers")
    page.get_by_role("button", name="Ações de prometheus em srv-prod-04").click()
    page.get_by_role("menuitem", name="Pausar").click()
    page.get_by_role("dialog").get_by_role("button", name="Pausar").click()
    expect(page.get_by_role("region", name="Andamento do lote").get_by_text("Concluído", exact=True)).to_be_visible(timeout=15_000)
    page.get_by_role("button", name="Ações de worker-notificacoes em srv-prod-03").click()
    expect(page.get_by_role("menuitem", name="Retomar")).to_be_visible()
    expect(page.get_by_role("menuitem", name="Pausar")).to_have_count(0)


def test_csv_export_downloads_the_loaded_rows(page: Page) -> None:
    goto(page, "containers")
    with page.expect_download() as info:
        page.get_by_role("button", name="Exportar CSV").click()
    download = info.value
    assert download.suggested_filename == "containers-todos.csv"
    content = open(download.path(), encoding="utf-8-sig").read()
    assert content.splitlines()[0].startswith("Nome;Situação;Engine;Host;Imagem")
    assert "api-pedidos-7f9c" in content


def test_stop_two_failed_containers_in_four_interactions(page: Page) -> None:
    """SC-005: filtrar pela situação, selecionar a página, acionar Parar e confirmar."""
    goto(page, "containers")
    page.get_by_role("button", name="1 reiniciando").click()  # 1
    page.get_by_role("checkbox", name="Selecionar página").click()  # 2
    page.get_by_role("button", name="Parar", exact=True).click()  # 3
    page.get_by_role("dialog").get_by_role("button", name="Parar").click()  # 4
    expect(page.get_by_role("region", name="Andamento do lote").get_by_text("Concluído", exact=True)).to_be_visible(timeout=15_000)


def test_read_only_user_gets_no_mutation_actions(page: Page, scenario) -> None:
    scenario("readonly")
    goto(page, "containers")
    page.get_by_role("button", name="Ações de checkout-web em srv-prod-01").click()
    expect(page.get_by_role("menuitem", name="Ver logs")).to_be_visible()
    for action in ("Reiniciar", "Parar", "Pausar", "Remover"):
        expect(page.get_by_role("menuitem", name=action)).to_have_count(0)
