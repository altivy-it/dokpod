"""Spec 021 (T011): Pods — lista com situação, pod do agente e hosts sem pods, detalhe com link do container e de volta,
ação com justificativa, falha parcial com os containers que falharam, pod do agente protegido e acessibilidade."""

import re

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, no_horizontal_scroll, screenshot


def submitted(page: Page) -> list:
    return page.request.get(f"{BASE_URL}__mock/submitted").json()


def test_list_statuses_agent_pod_and_hosts_without_pods(page: Page) -> None:
    goto(page, "pods")
    expect(page.get_by_role("heading", name="Pods do Podman")).to_be_visible()
    expect(page.get_by_role("link", name="Pods")).to_have_attribute("aria-current", "page")
    table = page.get_by_role("table")
    expect(table.get_by_text("pod_dokpod (pod do agente)")).to_be_visible()
    expect(table.get_by_text("Degradado", exact=True)).to_be_visible()
    expect(page.get_by_text("Sem pods (o Docker não tem pods)", exact=False)).to_be_visible()
    expect(page.get_by_text("Inventário de pods indisponível em: srv-prod-04", exact=False)).to_be_visible()
    page.get_by_role("button", name=re.compile(r"degradado\s*, filtrar a lista")).click()
    expect(page).to_have_url(re.compile(r"status=degraded"))
    expect(table.get_by_text("pod-pedidos", exact=True)).to_be_visible()
    expect(table.get_by_text("pod-batch", exact=True)).to_have_count(0)
    screenshot(page, "021-pods-degradados-1440")
    assert axe_violations(page) == []


def test_detail_links_containers_and_container_detail_links_back(page: Page) -> None:
    goto(page, "pods?q=pod-pedidos")
    page.get_by_role("table").get_by_text("pod-pedidos", exact=True).click()
    drawer = page.get_by_role("dialog", name="pod-pedidos")
    expect(drawer.get_by_text(re.compile(r"\d de 2 containers em execução"))).to_be_visible()
    link = drawer.get_by_role("link", name="api-pedidos-7f9c")
    expect(link).to_be_visible()
    assert axe_violations(page) == []
    link.click()
    expect(page).to_have_url(re.compile(r"containers/"))
    back = page.get_by_role("list", name="Pod do container").get_by_role("link", name="pod-pedidos")
    expect(back).to_be_visible()
    back.click()
    expect(page.get_by_role("dialog", name="pod-pedidos")).to_be_visible()


def test_stop_with_justification_and_partial_failure_lists_containers(page: Page) -> None:
    goto(page, "pods?q=pod-pedidos")
    page.get_by_role("table").get_by_text("pod-pedidos", exact=True).click()
    page.get_by_role("dialog", name="pod-pedidos").get_by_role("button", name="Parar", exact=True).click()
    confirm = page.get_by_role("dialog", name="Parar o pod pod-pedidos?")
    stop = confirm.get_by_role("button", name="Parar", exact=True)
    expect(stop).to_be_disabled()
    confirm.get_by_label("Justificativa (obrigatória pela política do estágio)").fill("Janela de manutenção do pedidos")
    assert axe_violations(page) == []
    stop.click()
    # O detalhe continua aberto com a situação nova; o painel de andamento fica na página.
    page.get_by_role("dialog", name="pod-pedidos").get_by_role("button", name="Fechar").first.click()
    progress = page.get_by_role("region", name="Andamento da ação em pods")
    expect(progress.get_by_text("Concluído", exact=True)).to_be_visible(timeout=15_000)
    assert ["pod_stop", "srv-prod-03", "pod-pedidos", "Janela de manutenção do pedidos"] in submitted(page)
    progress.get_by_role("button", name="Fechar andamento da ação").click()

    goto(page, "pods?q=pod-batch")
    page.get_by_role("table").get_by_text("pod-batch", exact=True).click()
    page.get_by_role("dialog", name="pod-batch").get_by_role("button", name="Iniciar", exact=True).click()
    page.get_by_role("dialog", name="Iniciar o pod pod-batch?").get_by_role("button", name="Iniciar", exact=True).click()
    page.get_by_role("dialog", name="pod-batch").get_by_role("button", name="Fechar").first.click()
    progress = page.get_by_role("region", name="Andamento da ação em pods")
    expect(progress.get_by_text("Falhou: parte dos containers não mudou de estado")).to_be_visible(timeout=15_000)
    expect(progress.get_by_text("(migracao-catalogo)")).to_be_visible()


def test_agent_pod_offers_no_stop_and_batch_skips_it(page: Page) -> None:
    goto(page, "pods?q=pod_dokpod")
    page.get_by_role("table").get_by_text("pod_dokpod (pod do agente)").click()
    drawer = page.get_by_role("dialog", name="pod_dokpod")
    expect(drawer.get_by_text("Pod do agente: o console não para", exact=False)).to_be_visible()
    expect(drawer.get_by_role("button", name="Parar")).to_have_count(0)
    drawer.get_by_role("button", name="Fechar").first.click()
    goto(page, "pods")
    page.get_by_role("checkbox", name="Selecionar página").click()
    page.get_by_role("button", name="Reiniciar", exact=True).click()
    confirm = page.get_by_role("dialog", name=re.compile(r"Reiniciar"))
    expect(confirm.get_by_text("pod do agente (protegido)")).to_be_visible()


def test_narrow_width_and_keyboard(page: Page) -> None:
    page.set_viewport_size({"width": 390, "height": 900})
    goto(page, "pods")
    expect(page.get_by_role("heading", name="Pods do Podman")).to_be_visible()
    assert no_horizontal_scroll(page)
    page.set_viewport_size({"width": 1440, "height": 900})
    page.get_by_role("button", name="Iniciar", exact=True).focus()
    page.keyboard.press("Enter")
    expect(page.locator(".alt-toast__msg").get_by_text("Selecione pods na lista", exact=False)).to_be_visible()
