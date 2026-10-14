"""Spec 009 (T017): métricas na visão geral, hosts, lista e detalhe; estados indisponível, lacuna e parado."""

import re

from playwright.sync_api import Page, expect

from conftest import axe_violations, goto, screenshot


def kpi(page: Page, description: str):
    return page.get_by_role("group", name=re.compile(description))


def test_overview_shows_cpu_memory_kpis_chart_and_host_cpu(page: Page) -> None:
    goto(page)
    expect(kpi(page, "CPU média do cluster")).not_to_have_attribute("aria-label", re.compile("indisponível"))
    expect(kpi(page, "Memória alocada")).to_contain_text("61,2")
    chart = page.locator("section", has=page.get_by_role("heading", name="Uso de recursos do cluster"))
    expect(chart.get_by_text("Limite 80%").first).to_be_visible()
    expect(chart.get_by_text("Limite operacional de 80% definido para escalonamento automático.")).to_be_visible()
    chart.get_by_role("button", name="Ver como tabela").click()
    gap_row = chart.locator("tbody tr").nth(4)
    expect(gap_row.locator("td").nth(1)).to_have_text("—")
    hosts = page.locator("section", has=page.get_by_role("heading", name="Hosts e engines"))
    expect(hosts.get_by_label(re.compile(r"^CPU \d+%")).first).to_be_visible()
    expect(hosts.get_by_label("CPU indisponível")).to_be_visible()  # srv-dev-01 desconectado
    assert axe_violations(page) == []
    screenshot(page, "009-visao-geral-1440")


def test_metrics_unavailable_when_no_host_publishes_samples(page: Page, scenario) -> None:
    scenario("no-metrics")
    goto(page)
    expect(kpi(page, "CPU média do cluster")).to_have_attribute("aria-label", "CPU média do cluster: indisponível")
    expect(kpi(page, "CPU média do cluster")).to_contain_text("indisponível")
    expect(page.get_by_text(re.compile("nenhum host autorizado publica amostras"))).to_be_visible()


def test_hosts_grid_has_cpu_and_memory_columns(page: Page) -> None:
    goto(page, "hosts")
    grid = page.get_by_role("table")
    expect(grid.get_by_role("columnheader", name=re.compile("CPU"))).to_be_visible()
    expect(grid.get_by_role("columnheader", name=re.compile("Memória"))).to_be_visible()
    row = grid.locator("tr", has=page.get_by_text("srv-dev-01", exact=True))
    expect(row.get_by_label("CPU indisponível")).to_be_visible()


def test_container_list_and_detail_show_resources_and_stopped_state(page: Page) -> None:
    goto(page, "containers")
    grid = page.get_by_role("table")
    running = grid.locator("tr", has=page.get_by_text("api-pedidos-7f9c", exact=True))
    expect(running.locator("td").filter(has_text=re.compile(r"\d+,\d")).first).to_be_visible()
    stopped = grid.locator("tr", has=page.get_by_text("relatorios-bi", exact=True))
    expect(stopped).to_contain_text("—")

    running.get_by_text("api-pedidos-7f9c", exact=True).click()
    resources = page.locator("section", has=page.get_by_role("heading", name="Recursos agora"))
    expect(resources.get_by_text(re.compile(r"MiB de 1 GiB"))).to_be_visible()
    expect(resources.get_by_role("progressbar", name="CPU")).to_be_visible()
    page.get_by_role("tab", name="Estatísticas").click()
    expect(page.get_by_text("Série parcial", exact=False)).to_be_visible()
    screenshot(page, "009-detalhe-estatisticas-1440")

    goto(page, "containers")
    page.get_by_role("table").get_by_text("relatorios-bi", exact=True).click()
    expect(page.locator("section", has=page.get_by_role("heading", name="Recursos agora")).get_by_text("Container parado: sem consumo de recursos.")).to_be_visible()
