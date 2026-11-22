"""Spec 026 (T020, História 1): na edição Business com a capability change-windows, Configurações → Políticas define,
edita e remove a janela de mudança de um estágio. Sem environment:manage em todos os hosts do estágio, a janela fica
só para leitura; com a licença restrita, editar fica bloqueado e remover continua. Na Community Edition a seção não
existe e nenhuma rota de janelas é chamada. Cálculo, gate e auditoria são cobertos pelos testes da API (T016)."""

import re

from playwright.sync_api import Page, Request, expect

from conftest import BASE_URL, axe_violations, goto, screenshot


def submitted(page: Page) -> list:
    return [list(item) for item in page.request.get(f"{BASE_URL}__mock/submitted").json() if str(item[0]).startswith("change_window")]


def section(page: Page):
    return page.get_by_role("region", name="Janelas de mudança")


def stage(page: Page, name: str):
    return section(page).locator(f'[data-stage="{name}"]')


def test_define_edit_and_remove_a_window(page: Page, scenario) -> None:
    scenario("business-cw")
    goto(page, "configuracoes?vista=politicas")
    production = stage(page, "production")
    expect(production.get_by_text("Sem janela: ações aceitas a qualquer hora.", exact=True)).to_be_visible()

    production.get_by_role("button", name="Definir a janela de Produção").click()
    form = production.get_by_role("form", name="Janela de Produção")
    form.get_by_label("Fuso horário (IANA)").fill("America/Sao_Paulo")
    expect(form.get_by_role("checkbox", name="seg")).to_be_checked()
    expect(form.get_by_role("checkbox", name="sáb")).not_to_be_checked()
    assert axe_violations(page) == []
    form.get_by_label("Fim").fill("22:00")
    expect(form.get_by_text("Início e fim precisam ser diferentes.", exact=True)).to_be_visible()
    expect(form.get_by_role("button", name="Salvar")).to_be_disabled()
    form.get_by_label("Fim").fill("06:00")
    form.get_by_role("button", name="Salvar").click()

    expect(page.get_by_text("Janela de Produção salva.", exact=True)).to_be_visible()
    expect(production.get_by_text("seg, ter, qua, qui, sex 22:00–06:00 (cruza a meia-noite)", exact=True)).to_be_visible()
    expect(production.get_by_text("Fora da janela de mudança", exact=True)).to_be_visible()
    screenshot(page, "026-janela-definida-1440")

    production.get_by_role("button", name="Editar a janela de Produção").click()
    form = production.get_by_role("form", name="Janela de Produção")
    form.get_by_role("button", name="Adicionar faixa").click()
    form.get_by_role("button", name="Salvar").click()
    expect(page.get_by_text("Janela de Produção salva.", exact=True).last).to_be_visible()
    expect(production.get_by_text("sáb 08:00–12:00", exact=True)).to_be_visible()

    production.get_by_role("button", name="Remover a janela de Produção").click()
    expect(page.get_by_text("Janela de Produção removida. As ações voltam a ser aceitas a qualquer hora.", exact=True)).to_be_visible()
    expect(production.get_by_text("Sem janela: ações aceitas a qualquer hora.", exact=True)).to_be_visible()

    assert submitted(page) == [
        ["change_window_save", "production", 1, "America/Sao_Paulo"],
        ["change_window_save", "production", 2, "America/Sao_Paulo"],
        ["change_window_remove", "production"],
    ]


def test_without_manage_on_the_stage_the_window_is_read_only(page: Page, scenario) -> None:
    scenario("business-cw-readonly")
    goto(page, "configuracoes?vista=politicas")
    production = stage(page, "production")

    expect(production.get_by_text("seg, ter, qua, qui, sex 22:00–06:00 (cruza a meia-noite)", exact=True)).to_be_visible()
    expect(production.get_by_text("Editar exige environment:manage em todos os hosts do estágio.", exact=True)).to_be_visible()
    expect(production.get_by_role("button")).to_have_count(0)
    expect(stage(page, "staging").get_by_role("button", name="Definir a janela de Homologação")).to_be_enabled()


def test_restricted_license_blocks_editing_but_not_removal(page: Page, scenario) -> None:
    scenario("business-cw-restricted")
    goto(page, "configuracoes?vista=politicas")
    region = section(page)
    production = stage(page, "production")

    expect(region.get_by_text("Licença restrita", exact=True)).to_be_visible()
    expect(production.get_by_role("button", name="Editar a janela de Produção")).to_be_disabled()
    production.get_by_role("button", name="Remover a janela de Produção").click()
    expect(page.get_by_text("Janela de Produção removida. As ações voltam a ser aceitas a qualquer hora.", exact=True)).to_be_visible()
    assert submitted(page) == [["change_window_remove", "production"]]


def test_community_edition_has_no_change_window_section(page: Page, scenario) -> None:
    calls: list[str] = []

    def record(request: Request) -> None:
        if "/api/v1/change-windows" in request.url:
            calls.append(request.url)

    page.on("request", record)
    goto(page, "configuracoes?vista=politicas")
    expect(page.locator("#dok-main").get_by_role("heading", name="Configurações")).to_be_visible()
    expect(section(page)).to_have_count(0)
    assert calls == []


# História 2 (T028): fora da janela, o aviso aparece antes do clique; num lote misto, só os itens fora da janela ficam
# de fora, cada um com o motivo; e a recusa 409 da API aparece quando a janela fecha entre a leitura e o clique.

def container_row(page: Page, name: str):
    return page.get_by_role("table").locator("tr", has=page.get_by_text(name, exact=True))


def select_containers(page: Page, *names: str) -> None:
    for name in names:
        page.get_by_role("checkbox", name=re.compile(f"^Selecionar {re.escape(name)} em")).click()


def all_submitted(page: Page) -> list:
    return [list(item) for item in page.request.get(f"{BASE_URL}__mock/submitted").json()]


def test_detail_warns_before_the_click_and_disables_the_actions(page: Page, scenario) -> None:
    scenario("business-cw-closed")
    goto(page, "containers")
    container_row(page, "checkout-web").get_by_text("checkout-web", exact=True).click()

    notice = page.get_by_role("status").filter(has_text="Fora da janela de mudança")
    expect(notice).to_be_visible()
    expect(notice).to_contain_text("(America/Sao_Paulo)")
    expect(page.get_by_role("button", name="Reiniciar", exact=True)).to_be_disabled()
    expect(page.get_by_role("button", name="Parar", exact=True)).to_be_disabled()
    expect(page.get_by_role("button", name="Abrir terminal")).to_be_disabled()
    expect(page.get_by_role("button", name="Remover container")).to_be_disabled()
    screenshot(page, "026-fora-da-janela-1440")
    assert axe_violations(page) == []


def test_mixed_batch_skips_only_the_hosts_outside_the_window(page: Page, scenario) -> None:
    scenario("business-cw-closed")
    goto(page, "containers")
    select_containers(page, "checkout-web", "api-pedidos-hml")
    page.get_by_role("button", name="Reiniciar", exact=True).first.click()

    drawer = page.get_by_role("dialog")
    expect(drawer.get_by_text("Container afetado", exact=True)).to_be_visible()
    expect(drawer.get_by_text("1 ignorado")).to_be_visible()
    expect(drawer.get_by_text(re.compile("fora da janela de mudança \\(abre em .*America/Sao_Paulo"))).to_be_visible()
    drawer.get_by_role("button", name="Reiniciar").click()

    progress = page.get_by_role("region", name="Andamento do lote")
    expect(progress.get_by_text("Concluído", exact=True)).to_have_count(1, timeout=15_000)
    assert all_submitted(page) == [["restart", "api-pedidos-hml"]]


def test_refusal_from_the_api_is_shown_with_the_reason(page: Page, scenario) -> None:
    scenario("business-cw-race")
    goto(page, "containers")
    select_containers(page, "checkout-web")
    page.get_by_role("button", name="Reiniciar", exact=True).first.click()
    page.get_by_role("dialog").get_by_role("button", name="Reiniciar").click()

    progress = page.get_by_role("region", name="Andamento do lote")
    expect(progress.get_by_text("Falhou: fora da janela de mudança")).to_be_visible(timeout=15_000)
    assert all_submitted(page) == [["change_window_denied", "restart", "checkout-web"]]
