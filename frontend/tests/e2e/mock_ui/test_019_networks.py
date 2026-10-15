"""Spec 019 (T011): Redes — lista com redes do sistema e host sem inventário, busca por sub-rede, detalhe com link do
container, remoção individual e em lote (quantidade em Produção), retomada após recarga e acessibilidade."""

import re

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, no_horizontal_scroll, screenshot


def submitted(page: Page) -> list:
    return page.request.get(f"{BASE_URL}__mock/submitted").json()


def test_list_summary_system_networks_unavailable_host_and_unused_filter(page: Page) -> None:
    goto(page, "redes")
    expect(page.get_by_role("heading", name="Redes nos hosts")).to_be_visible()
    expect(page.get_by_role("link", name="Redes")).to_have_attribute("aria-current", "page")
    table = page.get_by_role("table")
    expect(table.get_by_text("rede-pedidos").first).to_be_visible()
    expect(table.get_by_text("Do sistema", exact=True).first).to_be_visible()
    expect(page.get_by_text("Inventário indisponível em: srv-prod-04", exact=False)).to_be_visible()
    page.get_by_role("button", name=re.compile(r"sem uso\s*, filtrar a lista")).click()
    expect(page).to_have_url(re.compile(r"semUso=1"))
    expect(table.get_by_text("Em uso", exact=True)).to_have_count(0)
    expect(table.get_by_text("Do sistema", exact=True)).to_have_count(0)
    screenshot(page, "019-redes-sem-uso-1440")
    assert axe_violations(page) == []


def test_subnet_search_detail_and_container_link(page: Page) -> None:
    goto(page, "redes?q=10.89.71")
    table = page.get_by_role("table")
    expect(table.get_by_text("rede-orfa-srv-prod-01")).to_be_visible()
    expect(table.get_by_role("row")).to_have_count(2)  # cabeçalho + 1 rede

    goto(page, "containers")
    page.get_by_role("table").get_by_text("checkout-web").first.click()
    link = page.get_by_role("list", name="Redes do container").get_by_role("link", name="rede-pedidos")
    expect(link).to_be_visible()
    link.click()
    expect(page).to_have_url(re.compile(r"redes\?.*rede=rede-pedidos"))
    drawer = page.get_by_role("dialog", name="rede-pedidos")
    expect(drawer.get_by_text(re.compile(r"containers? conectados?"))).to_be_visible()
    expect(drawer.get_by_role("link", name="checkout-web")).to_be_visible()
    expect(drawer.get_by_text("Remoção indisponível: em uso", exact=False)).to_be_visible()
    expect(drawer.get_by_text("172.20.1.0/24")).to_be_visible()
    assert axe_violations(page) == []


def test_remove_one_production_network_requires_justification(page: Page) -> None:
    goto(page, "redes?q=rede-orfa-srv-prod-01")
    page.get_by_role("table").get_by_text("rede-orfa-srv-prod-01").first.click()
    page.get_by_role("dialog", name="rede-orfa-srv-prod-01").get_by_role("button", name="Remover").click()
    confirm = page.get_by_role("dialog", name="Remover rede-orfa-srv-prod-01?")
    expect(confirm.get_by_text("Containers não são desconectados", exact=False)).to_be_visible()
    remove = confirm.get_by_role("button", name="Remover", exact=True)
    expect(remove).to_be_disabled()
    confirm.get_by_label("Justificativa (obrigatória pela política do estágio)").fill("Rede de deploy antigo, chamado 4521")
    assert axe_violations(page) == []
    remove.click()
    progress = page.get_by_role("region", name="Andamento da remoção de redes")
    expect(progress.get_by_text("Removida", exact=True)).to_be_visible(timeout=15_000)
    assert ["remove_network", "srv-prod-01", "rede-orfa-srv-prod-01", "Rede de deploy antigo, chamado 4521"] in submitted(page)
    expect(page.get_by_role("table").get_by_text("rede-orfa-srv-prod-01")).to_have_count(0, timeout=10_000)


def test_batch_skips_cluster_and_disconnected_needs_count_and_resumes(page: Page) -> None:
    goto(page, "redes?q=net")
    page.get_by_role("checkbox", name="Selecionar página").click()
    page.get_by_role("button", name=re.compile(r"^Remover \(\d+\)$")).click()
    confirm = page.get_by_role("dialog", name=re.compile(r"Remover \d+ redes\?"))
    count = re.search(r"Remover (\d+) redes", confirm.inner_text()).group(1)
    expect(confirm.get_by_text("rede de cluster", exact=False).first).to_be_visible()   # cluster-overlay (swarm)
    expect(confirm.get_by_text("host desconectado", exact=False).first).to_be_visible()  # srv-dev-01
    confirm.get_by_label("Justificativa (obrigatória pela política do estágio)").fill("Limpeza das redes de cache")
    confirm.get_by_label(re.compile(rf"Digite {count} para confirmar")).fill(count)
    confirm.get_by_role("button", name="Remover", exact=True).click()
    expect(confirm).to_have_count(0, timeout=15_000)  # todos os comandos enviados antes de recarregar
    expect(page.get_by_role("region", name="Andamento da remoção de redes")).to_be_visible()
    page.reload(wait_until="networkidle")
    progress = page.get_by_role("region", name="Andamento da remoção de redes")
    expect(progress.get_by_text("Removida", exact=True)).to_have_count(int(count), timeout=15_000)
    assert len([s for s in submitted(page) if s[0] == "remove_network"]) == int(count)  # retomada não reenviou
    progress.get_by_role("button", name="Fechar andamento da remoção").click()
    expect(progress).to_have_count(0)


def test_keyboard_and_narrow_width(page: Page) -> None:
    page.set_viewport_size({"width": 390, "height": 900})
    goto(page, "redes")
    expect(page.get_by_role("heading", name="Redes nos hosts")).to_be_visible()
    assert no_horizontal_scroll(page)
    page.set_viewport_size({"width": 1440, "height": 900})
    page.get_by_role("button", name="Atualizar").focus()
    page.keyboard.press("Tab")
    expect(page.get_by_role("button", name=re.compile(r"^Remover"))).to_be_focused()
    page.keyboard.press("Enter")
    expect(page.locator(".alt-toast__msg").get_by_text("Selecione redes na lista", exact=False)).to_be_visible()
    assert axe_violations(page) == []
