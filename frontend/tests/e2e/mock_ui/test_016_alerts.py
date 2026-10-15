"""Spec 016 (T017): tela Alertas, sino e menu, reconhecer, regras, silenciamento, política e ações com justificativa."""

import re

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, screenshot


def submitted(page: Page) -> list:
    return page.request.get(f"{BASE_URL}__mock/submitted").json()


def grid_rows(page: Page):
    return page.get_by_role("table").locator("tbody tr")


def test_menu_and_bell_show_open_alerts_of_the_selected_stage(page: Page) -> None:
    goto(page, "")
    menu = page.get_by_role("navigation").get_by_role("link", name=re.compile("^Alertas"))
    expect(menu).to_contain_text("5")
    bell = page.get_by_role("button", name=re.compile("^Alertas abertos"))
    expect(bell).to_have_accessible_name("Alertas abertos, 5")
    bell.click()
    panel = page.get_by_role("dialog", name="Alertas abertos")
    expect(panel.locator("a.dok-notif__item")).to_have_count(5)
    expect(panel.get_by_text("CPU de srv-prod-02 em 91% (limite 80%)")).to_be_visible()
    screenshot(page, "016-sino-1440")
    assert axe_violations(page) == []
    panel.get_by_text("Container migracao-catalogo encerrado com erro (código 1)").click()
    expect(page).to_have_url(re.compile(r"/alertas\?.*alerta=0a1e0000-0000-4000-8000-000000000001"))
    expect(page.get_by_role("dialog").get_by_text("Linha do tempo")).to_be_visible()

    goto(page, "?stage=staging")
    expect(page.get_by_role("button", name=re.compile("^Alertas abertos"))).to_have_accessible_name("Alertas abertos, 1")


def test_open_view_filters_by_severity_and_state_in_the_url(page: Page) -> None:
    goto(page, "alertas")
    expect(page.locator(".dok-page-head").get_by_role("heading", name="Alertas", level=1)).to_be_visible()
    expect(grid_rows(page)).to_have_count(5)
    page.get_by_role("button", name=re.compile(r"alta\s*, filtrar")).click()
    expect(page).to_have_url(re.compile(r"severidade=high"))
    expect(grid_rows(page)).to_have_count(3)
    page.get_by_role("button", name=re.compile(r"^reconhecido\s*, filtrar")).click()
    expect(page).to_have_url(re.compile(r"estado=acknowledged"))
    expect(grid_rows(page)).to_have_count(1)
    expect(page.get_by_text("Agente desconectado").first).to_be_visible()
    screenshot(page, "016-alertas-abertos-1440")
    assert axe_violations(page) == []


def test_acknowledge_requires_a_comment_and_lands_in_the_timeline(page: Page) -> None:
    goto(page, "alertas?alerta=0a1e0000-0000-4000-8000-000000000002")
    drawer = page.get_by_role("dialog")
    expect(drawer.get_by_text("Container gateway-01 reiniciou 3 ou mais vezes em 10 minutos")).to_be_visible()
    drawer.get_by_role("button", name="Reconhecer").click()
    confirm = drawer.get_by_role("button", name="Reconhecer")
    expect(confirm).to_be_disabled()
    drawer.get_by_label("Comentário").fill("curto")
    expect(confirm).to_be_disabled()
    drawer.get_by_label("Comentário").fill("Investigando o loop do gateway")
    expect(confirm).to_be_enabled()
    confirm.click()
    timeline = drawer.locator("ol")
    expect(timeline.get_by_text("Investigando o loop do gateway")).to_be_visible()
    expect(timeline.get_by_text("Reconhecido")).to_be_visible()
    assert axe_violations(page) == []


def test_history_lists_resolved_alerts_with_resolution(page: Page) -> None:
    goto(page, "alertas?vista=historico")
    expect(grid_rows(page)).to_have_count(2)
    expect(page.get_by_text("resolvido manualmente")).to_be_visible()
    expect(page.get_by_text("condição normalizada")).to_be_visible()
    expect(page.get_by_label("Resolvidos desde")).to_be_visible()
    assert axe_violations(page) == []


def test_rules_view_edits_toggles_and_creates_rules(page: Page) -> None:
    goto(page, "alertas?vista=regras")
    expect(grid_rows(page)).to_have_count(9)
    expect(page.get_by_text("Container reiniciado automaticamente: 3 vezes em 10 min")).to_be_visible()
    page.get_by_role("button", name="Ações de Comando indeterminado").click()
    page.get_by_role("menuitem", name="Desativar").click()
    expect(page.get_by_text('Regra "Comando indeterminado" desativada.', exact=True)).to_be_visible()
    assert ["rule", "Comando indeterminado", False] in submitted(page)

    page.get_by_role("button", name="Nova regra").click()
    drawer = page.get_by_role("dialog")
    drawer.get_by_label("Nome", exact=True).fill("Memória dos containers de API")
    drawer.get_by_text("Limite de CPU ou memória").click()
    drawer.get_by_label("Métrica").select_option("container_memory")
    drawer.get_by_label("Limite", exact=True).fill("90")
    drawer.get_by_label("Containers (padrão de nome)").fill("api-*")
    screenshot(page, "016-nova-regra-1440")
    assert axe_violations(page) == []
    drawer.get_by_role("button", name="Criar regra").click()
    expect(page.get_by_text("Regra salva. Vale para as próximas avaliações.", exact=True)).to_be_visible()
    expect(grid_rows(page)).to_have_count(10)
    expect(page.get_by_text("Memória do container acima de 90% por 5 min")).to_be_visible()


def test_justification_policy_per_stage(page: Page) -> None:
    goto(page, "alertas?vista=regras")
    staging = page.get_by_role("checkbox", name=re.compile("^Homologação"))
    expect(page.get_by_role("checkbox", name=re.compile("^Produção"))).to_be_checked()
    expect(staging).not_to_be_checked()
    staging.check()
    expect(page.get_by_text("Homologação: justificativa obrigatória para parar, pausar e remover.", exact=True)).to_be_visible()
    goto(page, "containers")
    page.get_by_role("button", name="Ações de api-pedidos-hml em srv-hml-01").click()
    page.get_by_role("menuitem", name="Parar").click()
    expect(page.get_by_role("dialog").get_by_text("Justificativa (obrigatória pela política do estágio)")).to_be_visible()


def test_silence_view_creates_and_ends_silences(page: Page) -> None:
    goto(page, "alertas?vista=silenciamentos")
    expect(grid_rows(page)).to_have_count(1)
    expect(page.get_by_text("Janela de carga noturna dos jobs batch")).to_be_visible()
    page.get_by_role("button", name="Novo silenciamento").click()
    drawer = page.get_by_role("dialog")
    drawer.get_by_label("Host").select_option(label="srv-prod-02")
    drawer.get_by_label("Justificativa").fill("Atualização do kernel do host")
    assert axe_violations(page) == []
    drawer.get_by_role("button", name="Silenciar").click()
    expect(page.get_by_text("Silenciamento criado.", exact=True)).to_be_visible()
    expect(grid_rows(page)).to_have_count(2)
    page.get_by_role("button", name=re.compile("^Ações de srv-prod-02")).click()
    page.get_by_role("menuitem", name="Encerrar agora").click()
    expect(page.get_by_text(re.compile("^Silenciamento encerrado"))).to_be_visible()
    expect(grid_rows(page)).to_have_count(1)


def test_stop_pause_and_delete_require_justification_in_production(page: Page) -> None:
    goto(page, "containers")
    for action, name, host in (("Parar", "checkout-web", "srv-prod-01"), ("Pausar", "redis-sessoes", "srv-prod-02"), ("Remover", "migracao-catalogo", "srv-batch-01")):
        page.get_by_role("button", name=f"Ações de {name} em {host}").click()
        page.get_by_role("menuitem", name=action).click()
        drawer = page.get_by_role("dialog")
        confirm = drawer.get_by_role("button", name=action)
        expect(drawer.get_by_text("Justificativa (obrigatória pela política do estágio)")).to_be_visible()
        expect(confirm).to_be_disabled()
        drawer.get_by_label(re.compile("Justificativa")).fill(f"Manutenção planejada de {name}")
        if action == "Parar":
            screenshot(page, "016-parar-com-justificativa-1440")
            assert axe_violations(page) == []
        confirm.click()
        expect(page.get_by_role("region", name="Andamento do lote").get_by_text("Concluído", exact=True)).to_be_visible(timeout=15_000)
    sent = submitted(page)
    assert ["stop", "checkout-web", "Manutenção planejada de checkout-web"] in sent
    assert ["pause", "redis-sessoes", "Manutenção planejada de redis-sessoes"] in sent
    assert ["delete", "migracao-catalogo", "Manutenção planejada de migracao-catalogo"] in sent


def test_restart_has_no_justification_and_development_is_optional(page: Page) -> None:
    goto(page, "containers")
    page.get_by_role("button", name="Ações de checkout-web em srv-prod-01").click()
    page.get_by_role("menuitem", name="Reiniciar").click()
    expect(page.get_by_role("dialog").get_by_label(re.compile("Justificativa"))).to_have_count(0)
    page.get_by_role("dialog").get_by_role("button", name="Cancelar").click()
    page.get_by_role("button", name="Ações de api-pedidos-hml em srv-hml-01").click()
    page.get_by_role("menuitem", name="Parar").click()
    drawer = page.get_by_role("dialog")
    expect(drawer.get_by_text("Justificativa (opcional)")).to_be_visible()
    drawer.get_by_role("button", name="Parar").click()
    expect(page.get_by_role("region", name="Andamento do lote").get_by_text("Concluído", exact=True)).to_be_visible(timeout=15_000)
    assert ["stop", "api-pedidos-hml"] in submitted(page)


def test_intentional_stop_label_in_list_and_detail(page: Page) -> None:
    goto(page, "containers")
    row = page.get_by_role("table").locator("tr", has=page.get_by_text("relatorios-bi", exact=True))
    expect(row.get_by_text("Parado intencionalmente")).to_be_visible()
    row.get_by_text("relatorios-bi", exact=True).click()
    expect(page.get_by_text("Parado intencionalmente", exact=True).first).to_be_visible()
    expect(page.get_by_role("note")).to_contain_text("por u-carlos")
    expect(page.get_by_role("note")).to_contain_text("Relatórios suspensos até o fechamento do mês")
    assert axe_violations(page) == []


def test_read_only_user_sees_alerts_without_actions(page: Page, scenario) -> None:
    scenario("readonly")
    goto(page, "alertas?alerta=0a1e0000-0000-4000-8000-000000000001")
    drawer = page.get_by_role("dialog")
    expect(drawer.get_by_text("Ações exigem alert:manage neste host.")).to_be_visible()
    expect(drawer.get_by_role("button", name="Reconhecer")).to_have_count(0)
    goto(page, "alertas?vista=regras")
    expect(page.get_by_role("button", name="Nova regra")).to_have_count(0)
    expect(page.get_by_role("checkbox", name=re.compile("^Produção"))).to_be_disabled()
