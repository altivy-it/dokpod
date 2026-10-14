"""Spec 011 (T018): abas do detalhe, inspeção, logs (500 linhas, busca literal), permissões parciais e agente desconectado."""

import json
import re
import time

from playwright.sync_api import Page, expect

from conftest import REPORTS, axe_violations, goto, screenshot


def env(n: int) -> str:
    return f"00000000-0000-4000-8000-{n:012d}"


def cid(name: str) -> str:
    return (name.encode().hex() * 4)[:64]


API = f"containers/{env(3)}/{cid('api-pedidos-7f9c')}"


def test_tabs_are_keyboard_reachable_and_kept_in_the_url(page: Page) -> None:
    goto(page, f"{API}?aba=resumo")
    expect(page.get_by_role("banner").get_by_role("heading", name="api-pedidos-7f9c")).to_be_visible()
    tabs = page.get_by_role("tablist")
    tabs.get_by_role("tab", name="Resumo").focus()
    page.keyboard.press("ArrowRight")
    expect(tabs.get_by_role("tab", name="Logs")).to_be_focused()
    page.keyboard.press("Enter")
    expect(page).to_have_url(re.compile(r"aba=logs"))
    expect(page.get_by_role("tab", name="Terminal")).to_be_visible()
    assert axe_violations(page) == []


def test_inspection_shows_sanitized_json_and_redaction_notice(page: Page) -> None:
    goto(page, f"{API}?aba=inspecionar")
    panel = page.get_by_role("tabpanel")
    expect(panel.get_by_text('"Env": "<oculto>"', exact=False)).to_be_visible()
    expect(panel.get_by_text(re.compile(r"1 campos? ocultos? ou mascarados no agente"))).to_be_visible()
    expect(panel.get_by_role("button", name="Copiar")).to_be_visible()
    assert axe_violations(page) == []


def test_logs_load_500_lines_quickly_and_search_is_literal(page: Page) -> None:
    started = time.perf_counter()
    goto(page, f"{API}?aba=logs")
    log = page.get_by_role("log", name="Linhas de log")
    expect(log.get_by_text("Servidor iniciado na porta 80").first).to_be_visible()
    elapsed_ms = (time.perf_counter() - started) * 1000
    REPORTS.mkdir(parents=True, exist_ok=True)
    (REPORTS / "011-logs-500.json").write_text(json.dumps({"elapsed_ms": elapsed_ms}))
    assert elapsed_ms <= 2000, elapsed_ms
    expect(log.get_by_text("Authorization: Bearer ***").first).to_be_visible()

    with page.expect_request(re.compile(r"/logs\?.*contains=timeout")) as request:
        page.get_by_role("searchbox", name="Filtrar logs").fill("timeout")
        page.get_by_role("button", name="Buscar no histórico").click()
    assert "contains=timeout" in request.value.url
    expect(log.get_by_text("Timeout ao consultar postgres-principal:5432").first).to_be_visible()
    expect(log.get_by_text("Servidor iniciado na porta 80")).to_have_count(0)
    screenshot(page, "011-logs-1440")
    assert axe_violations(page) == []


def test_live_logs_explain_when_realtime_is_unavailable(page: Page) -> None:
    goto(page, f"{API}?aba=logs")
    page.get_by_role("button", name=re.compile("Pausado")).click()
    expect(page.get_by_role("status").filter(has_text="Ao vivo indisponível")).to_be_visible()


def test_partial_permissions_show_without_permission_states(page: Page, scenario) -> None:
    scenario("readonly")
    goto(page, f"{API}?aba=logs")
    expect(page.get_by_text("Você não tem o scope container:logs neste host.")).to_be_visible()
    page.get_by_role("tab", name="Inspecionar").click()
    expect(page.get_by_role("tabpanel").get_by_text("Sem permissão", exact=True)).to_be_visible()


def test_disconnected_agent_never_serves_cached_logs(page: Page) -> None:
    goto(page, f"containers/{env(7)}/{cid('checkout-dev')}?aba=logs")
    expect(page.get_by_text("Logs indisponíveis: agente desconectado")).to_be_visible()
    expect(page.get_by_text("Nenhum log é servido de cache.")).to_be_visible()
