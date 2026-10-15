"""Spec 018 (T011): Volumes — lista com resumo e host sem inventário, detalhe com uso sob demanda, link do container,
remoção individual em Produção e em lote com confirmação reforçada, retomada após recarga e acessibilidade."""

import re

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, no_horizontal_scroll, screenshot


def submitted(page: Page) -> list:
    return page.request.get(f"{BASE_URL}__mock/submitted").json()


def test_list_summary_unavailable_host_and_unused_filter(page: Page) -> None:
    goto(page, "volumes")
    expect(page.get_by_role("heading", name="Volumes nos hosts")).to_be_visible()
    expect(page.get_by_role("link", name="Volumes")).to_have_attribute("aria-current", "page")
    table = page.get_by_role("table")
    expect(table.get_by_text("vol-pedidos-uploads").first).to_be_visible()
    expect(table.get_by_text("api-pedidos-7f9c", exact=False).first).to_be_visible()   # container que monta
    expect(page.get_by_text("Inventário indisponível em: srv-prod-04", exact=False)).to_be_visible()
    page.get_by_role("button", name=re.compile(r"sem uso\s*, filtrar a lista")).click()
    expect(page).to_have_url(re.compile(r"semUso=1"))
    expect(table.get_by_text("Em uso", exact=True)).to_have_count(0)
    expect(table.get_by_text("(anônimo)", exact=False).first).to_be_visible()
    screenshot(page, "018-volumes-sem-uso-1440")
    assert axe_violations(page) == []


def test_detail_shows_mounts_and_size_on_demand_and_container_links_back(page: Page) -> None:
    goto(page, "volumes?q=pedidos-uploads")
    page.get_by_role("table").get_by_text("vol-pedidos-uploads").first.click()
    drawer = page.get_by_role("dialog", name="vol-pedidos-uploads")
    expect(drawer.get_by_text(re.compile(r"montam? este volume"))).to_be_visible()
    expect(drawer.get_by_text("Remoção indisponível: em uso", exact=False)).to_be_visible()
    drawer.get_by_role("button", name="Consultar tamanho").click()
    expect(drawer.get_by_text("MiB", exact=False)).to_be_visible()
    expect(page).to_have_url(re.compile(r"volume=vol-pedidos-uploads"))
    assert axe_violations(page) == []

    goto(page, "containers")
    page.get_by_role("table").get_by_text("checkout-web").first.click()
    link = page.get_by_role("list", name="Volumes montados").get_by_role("link", name="vol-pedidos-uploads")
    expect(link).to_be_visible()
    link.click()
    expect(page).to_have_url(re.compile(r"volumes\?.*volume=vol-pedidos-uploads"))
    expect(page.get_by_role("dialog", name="vol-pedidos-uploads")).to_be_visible()


def test_remove_one_production_volume_requires_name_and_justification(page: Page) -> None:
    goto(page, "volumes?q=dados-srv-prod-01")
    page.get_by_role("table").get_by_text("dados-srv-prod-01").first.click()
    page.get_by_role("dialog", name="dados-srv-prod-01").get_by_role("button", name="Remover").click()
    confirm = page.get_by_role("dialog", name="Remover dados-srv-prod-01?")
    expect(confirm.get_by_text("apagados de forma definitiva", exact=False)).to_be_visible()
    remove = confirm.get_by_role("button", name="Remover", exact=True)
    expect(remove).to_be_disabled()
    confirm.get_by_label("Justificativa (obrigatória pela política do estágio)").fill("Limpeza do volume legado, chamado 4521")
    expect(remove).to_be_disabled()
    confirm.get_by_label(re.compile("Digite o nome do volume")).fill("dados-srv-prod-01")
    assert axe_violations(page) == []
    remove.click()
    progress = page.get_by_role("region", name="Andamento da remoção de volumes")
    expect(progress.get_by_text("Removido", exact=True)).to_be_visible(timeout=15_000)
    assert ["remove_volume", "srv-prod-01", "dados-srv-prod-01", "Limpeza do volume legado, chamado 4521"] in submitted(page)
    expect(page.get_by_role("table").get_by_text("dados-srv-prod-01")).to_have_count(0, timeout=10_000)


def test_batch_removal_skips_in_use_needs_count_and_resumes_after_reload(page: Page) -> None:
    goto(page, "volumes?semUso=1&q=cache-antigo")
    table = page.get_by_role("table")
    expect(table.get_by_text("cache-antigo").first).to_be_visible()
    page.get_by_role("checkbox", name="Selecionar página").click()
    page.get_by_role("button", name=re.compile(r"^Remover \(\d+\)$")).click()
    confirm = page.get_by_role("dialog", name=re.compile(r"Remover \d+ volumes\?"))
    count = re.search(r"Remover (\d+) volumes", confirm.get_by_role("heading").first.inner_text()).group(1)
    expect(confirm.get_by_text("host desconectado", exact=False)).to_be_visible()   # srv-dev-01 fica fora
    confirm.get_by_label("Justificativa (obrigatória pela política do estágio)").fill("Limpeza de caches antigos do lote")
    confirm.get_by_label(re.compile(rf"Digite {count} para confirmar")).fill(count)
    confirm.get_by_role("button", name="Remover", exact=True).click()
    progress = page.get_by_role("region", name="Andamento da remoção de volumes")
    expect(progress).to_be_visible()
    page.reload(wait_until="networkidle")
    progress = page.get_by_role("region", name="Andamento da remoção de volumes")
    expect(progress.get_by_text("Removido", exact=True)).to_have_count(int(count), timeout=15_000)
    sent = [s for s in submitted(page) if s[0] == "remove_volume"]
    assert len(sent) == int(count)                                                   # retomada não reenviou
    progress.get_by_role("button", name="Fechar andamento da remoção").click()
    expect(progress).to_have_count(0)


def test_keyboard_and_narrow_width(page: Page) -> None:
    page.set_viewport_size({"width": 390, "height": 900})
    goto(page, "volumes")
    expect(page.get_by_role("heading", name="Volumes nos hosts")).to_be_visible()
    assert no_horizontal_scroll(page)
    page.set_viewport_size({"width": 1440, "height": 900})
    page.get_by_role("button", name="Atualizar").focus()
    page.keyboard.press("Tab")
    expect(page.get_by_role("button", name=re.compile(r"^Remover"))).to_be_focused()
    page.keyboard.press("Enter")
    expect(page.locator(".alt-toast__msg").get_by_text("Selecione volumes na lista", exact=False)).to_be_visible()
    assert axe_violations(page) == []
