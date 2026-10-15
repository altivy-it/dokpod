"""Spec 015 (T011): cards da Visão geral navegáveis para a tela Containers, por clique e por teclado."""

import re

from playwright.sync_api import Page, expect

from conftest import axe_violations, goto, screenshot


def card(page: Page, name: str):
    return page.get_by_role("link", name=re.compile(name))


def total_rows(page: Page) -> int:
    return page.get_by_role("table").locator("tbody tr").filter(has_not=page.locator(".alt-enhanced-data-grid__empty")).count()


def test_situation_cards_open_the_filtered_list_with_matching_totals(page: Page) -> None:
    for name, statuses, expected in (
        ("Em execução", ["running"], 6),
        ("Parados", ["stopped", "paused"], 3),
        ("Com falha ou reiniciando", ["failed", "restarting"], 2),
    ):
        goto(page)
        link = card(page, name)
        expect(link).to_contain_text(str(expected))
        link.click()
        expect(page).to_have_url(re.compile(r"/containers\?"))
        for status in statuses:
            expect(page).to_have_url(re.compile(f"status={status}"))
        expect(page.get_by_role("table").locator("tbody tr")).to_have_count(expected)
        assert total_rows(page) == expected
    assert axe_violations(page) == []


def test_cpu_and_memory_cards_open_running_containers_by_consumption(page: Page) -> None:
    for name, sort, field in (("CPU média do cluster", "-cpu", "cpuPercent"), ("Memória alocada", "-memory", "memoryUsedBytes")):
        goto(page)
        with page.expect_response(lambda r: "/api/v1/containers" in r.url and f"sort={sort}" in r.url) as response_info:
            card(page, name).click()
        expect(page).to_have_url(re.compile(f"sort={re.escape(sort)}"))
        expect(page).to_have_url(re.compile("status=running"))
        items = response_info.value.json()["containers"]
        values = [c["resources"][field] for c in items if c.get("resources")]
        assert values == sorted(values, reverse=True)
        first = items[0]["name"]
        expect(page.get_by_role("table").locator("tbody tr").first).to_contain_text(first)
        # O cabeçalho reflete a ordenação vinda da URL e não a troca por "nome".
        expect(page).to_have_url(re.compile(f"sort={re.escape(sort)}"))
    screenshot(page, "015-lista-por-memoria-1440")


def test_cards_work_from_the_keyboard_and_keep_the_stage(page: Page) -> None:
    goto(page)
    page.get_by_role("button", name=re.compile("Produção")).first.click()
    expect(page).to_have_url(re.compile("stage=production"))
    link = card(page, "Em execução")
    link.focus()
    expect(link).to_be_focused()
    screenshot(page, "015-cards-foco-1440")
    page.keyboard.press("Enter")
    expect(page).to_have_url(re.compile("stage=production"))
    expect(page).to_have_url(re.compile("status=running"))
    assert axe_violations(page) == []


def test_all_six_cards_are_links_with_accessible_names(page: Page) -> None:
    goto(page)
    for name in ("Em execução", "Parados", "Com falha ou reiniciando", "CPU média do cluster", "Memória alocada", "[Ii]magens? sem uso"):
        expect(card(page, name)).to_have_count(1)
    expect(card(page, "CPU média do cluster.*Abrir os containers ordenados por CPU")).to_have_count(1)
    expect(card(page, "Em execução.*Abrir a lista filtrada")).to_have_count(1)
    assert axe_violations(page) == []


def test_empty_situation_shows_a_specific_message(page: Page) -> None:
    goto(page, "containers?status=starting")
    expect(page.get_by_text("Nenhum container nesta situação.")).to_be_visible()
