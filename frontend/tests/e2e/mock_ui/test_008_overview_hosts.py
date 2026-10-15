"""Spec 008 (T023): visão geral por estágio, sino, hosts (cadastro, estágio, revogação), permissões e acessibilidade."""

import re
import uuid

from playwright.sync_api import Page, expect

from conftest import axe_violations, goto, screenshot


def test_overview_counts_follow_the_global_stage(page: Page) -> None:
    goto(page)
    expect(page.get_by_text("7 hosts, 11 containers sob gestão")).to_be_visible()
    page.get_by_role("button", name=re.compile("Produção")).first.click()
    expect(page.get_by_text("5 hosts, 9 containers sob gestão")).to_be_visible()
    expect(page.get_by_text(re.compile("Produção", re.I)).first).to_be_visible()
    events = page.locator("section", has=page.get_by_role("heading", name="Eventos recentes"))
    expect(events.get_by_text("Container encerrado com erro (código 1)")).to_be_visible()
    assert axe_violations(page) == []


def test_bell_lists_open_alerts_and_closes_with_escape(page: Page) -> None:
    # Spec 016 FR-006: o sino passou a listar alertas abertos no lugar de eventos que pedem atenção.
    goto(page)
    bell = page.get_by_role("button", name=re.compile("^Alertas abertos"))
    bell.click()
    dialog = page.get_by_role("dialog", name="Alertas abertos")
    expect(dialog.get_by_text("Container migracao-catalogo encerrado com erro (código 1)")).to_be_visible()
    # Regressão (2026-10-14): o painel ficava por baixo dos cards da Visão geral; o ponto central de cada
    # item precisa pertencer ao painel, não ao conteúdo da página.
    for item in dialog.locator(".dok-notif__item").all():
        box = item.bounding_box()
        assert box is not None
        covering = page.evaluate(
            """([x, y]) => {
                const el = document.elementFromPoint(x, y);
                if (el?.closest('.dok-notif')) return null;
                const chain = [];
                for (let n = el; n && chain.length < 8; n = n.parentElement) chain.push(n.tagName.toLowerCase() + '.' + [...n.classList].join('.'));
                return chain.join(' < ');
            }""",
            [box["x"] + box["width"] / 2, box["y"] + box["height"] / 2],
        )
        assert covering is None, f"o painel do sino está coberto por: {covering}"
    expect(dialog.get_by_text("Agente desconectado", exact=True)).to_be_visible()
    expect(dialog.get_by_text("Comando reiniciar concluído")).to_have_count(0)
    page.keyboard.press("Escape")
    expect(dialog).to_be_hidden()
    expect(bell).to_be_focused()


def test_hosts_register_change_stage_and_revoke(page: Page) -> None:
    goto(page, "hosts")
    expect(page.get_by_role("banner").get_by_role("heading", name="Hosts")).to_be_visible()
    grid = page.get_by_role("table")
    expect(grid.get_by_text("srv-prod-03")).to_be_visible()

    page.get_by_role("button", name="Cadastrar host").first.click()
    drawer = page.get_by_role("dialog", name="Cadastrar host")
    drawer.get_by_label("Identificador provisionado").fill(str(uuid.uuid4()))
    drawer.get_by_label("Nome").fill("srv-novo-01")
    drawer.get_by_label("Host", exact=True).fill("srv-novo-01.empresa.local")
    drawer.get_by_role("button", name="Cadastrar host").click()
    expect(drawer).to_be_hidden()
    expect(grid.get_by_text("srv-novo-01", exact=True)).to_be_visible()

    page.get_by_role("button", name="Ações de srv-hml-01").click()
    page.get_by_role("menuitem", name="Alterar estágio").click()
    stage_drawer = page.get_by_role("dialog", name="Alterar estágio")
    stage_drawer.get_by_role("button", name=re.compile("Estágio")).click()
    page.get_by_role("option", name="Produção").click()
    stage_drawer.get_by_role("button", name="Salvar estágio").click()
    expect(stage_drawer).to_be_hidden()
    row = grid.locator("tr", has=page.get_by_text("srv-hml-01", exact=True))
    expect(row.get_by_text("Produção")).to_be_visible()

    page.get_by_role("button", name="Ações de srv-prod-02").click()
    page.get_by_role("menuitem", name="Revogar identidade do agente").click()
    revoke = page.get_by_role("dialog", name="Revogar identidade do agente")
    revoke.get_by_role("button", name="Revogar identidade").click()
    expect(revoke).to_be_hidden()
    row = grid.locator("tr", has=page.get_by_text("srv-prod-02", exact=True))
    expect(row.get_by_text("Desconectado")).to_be_visible()
    screenshot(page, "008-hosts-1440")


def test_read_only_user_sees_no_management_actions(page: Page, scenario) -> None:
    scenario("readonly")
    goto(page, "hosts")
    page.get_by_role("button", name="Ações de srv-prod-01").click()
    expect(page.get_by_role("menuitem", name="Alterar estágio")).to_have_count(0)
    expect(page.get_by_role("menuitem", name="Revogar identidade do agente")).to_have_count(0)


def test_empty_installation_shows_empty_states(page: Page, scenario) -> None:
    scenario("empty")
    goto(page)
    expect(page.get_by_text(re.compile("Nenhum host"))).to_be_visible()
    assert axe_violations(page) == []


def test_overview_with_56_hosts_and_1120_containers_loads_within_two_seconds(page: Page, scenario) -> None:
    """SC-003 (T029): carga da visão geral medida do início da navegação até o resumo visível."""
    import json

    from conftest import REPORTS

    scenario("large")
    samples = []
    for _ in range(3):
        page.goto("about:blank")
        start = page.evaluate("performance.timeOrigin + performance.now()")
        goto(page)
        expect(page.get_by_text("56 hosts, 1.120 containers sob gestão")).to_be_visible()
        samples.append(page.evaluate("performance.timeOrigin + performance.now()") - start)
    REPORTS.mkdir(parents=True, exist_ok=True)
    (REPORTS / "008-overview-56-hosts.json").write_text(json.dumps({"samples_ms": samples}))
    assert max(samples) <= 2000, samples
