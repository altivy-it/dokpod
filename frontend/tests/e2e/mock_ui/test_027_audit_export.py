"""Spec 027 (T023, História 1): na edição Business com a capability audit-siem-export, Configurações → Instalação
cria um destino syslog, envia o evento de teste, desativa e respeita o limite de 5. Com a licença restrita, criar,
editar, reativar e testar ficam bloqueados e desativar continua. Na Community Edition a seção não existe e nenhuma
rota da exportação é chamada. O envio real é coberto pelos testes da API (T015–T017)."""

import re

from playwright.sync_api import Page, Request, expect

from conftest import BASE_URL, axe_violations, goto, screenshot


def submitted(page: Page) -> list:
    return [list(item) for item in page.request.get(f"{BASE_URL}__mock/submitted").json() if str(item[0]).startswith("audit_export")]


def section(page: Page):
    return page.get_by_role("region", name="Exportação para SIEM")


def create(page: Page, name: str, host: str = "siem.exemplo.interno") -> None:
    region = section(page)
    region.get_by_role("button", name="Novo destino").click()
    form = region.get_by_role("form", name="Novo destino")
    form.get_by_label("Nome").fill(name)
    form.get_by_label("Host").fill(host)
    form.get_by_label("Secret montado (opcional)").fill("siem-soc")
    form.get_by_text("Containers", exact=True).click()
    form.get_by_role("button", name="Salvar destino").click()
    expect(page.get_by_text("Destino criado. O envio começa pelos próximos eventos da trilha.", exact=True).last).to_be_visible()
    expect(region.locator(f'[data-destination="{name}"]')).to_be_visible()


def test_create_test_deactivate_and_limit(page: Page, scenario) -> None:
    scenario("business-siem")
    goto(page, "configuracoes?vista=instalacao")
    region = section(page)
    expect(region.get_by_text("Nenhum destino configurado.", exact=True)).to_be_visible()

    region.get_by_role("button", name="Novo destino").click()
    mandatory = region.get_by_role("checkbox", name="Exportação para SIEM")
    expect(mandatory).to_be_checked()
    expect(mandatory).to_be_disabled()
    assert axe_violations(page) == []
    region.get_by_role("button", name="Cancelar").click()

    create(page, "siem-soc")
    row = region.locator('[data-destination="siem-soc"]')
    expect(row.get_by_text("Em dia", exact=True)).to_be_visible()
    expect(row.get_by_text("siem-soc: CA", exact=False)).to_be_visible()
    screenshot(page, "027-destino-criado-1440")

    row.get_by_role("button", name="Enviar evento de teste").click()
    expect(page.get_by_text("Evento de teste entregue a siem-soc.", exact=True)).to_be_visible()

    create(page, "siem-fora", host="siem-fora.interno")
    region.locator('[data-destination="siem-fora"]').get_by_role("button", name="Enviar evento de teste").click()
    expect(page.get_by_text("Evento de teste não entregue: Conexão recusada ou host não encontrado.", exact=True)).to_be_visible()

    row.get_by_role("button", name="Desativar").click()
    expect(row.get_by_text("Desativado", exact=True)).to_be_visible()
    expect(row.get_by_role("button", name="Reativar")).to_be_enabled()

    for name in ("siem-3", "siem-4", "siem-5"):
        create(page, name)
    expect(region.get_by_role("button", name="Novo destino")).to_be_disabled()
    expect(region.get_by_text("Limite de 5 destinos atingido.", exact=True)).to_be_visible()

    events = submitted(page)
    assert ["audit_export_create", "siem-soc", True] in events
    assert ["audit_export_test", "siem-soc", True] in events
    assert ["audit_export_test", "siem-fora", False] in events
    assert ["audit_export_update", "siem-soc", False] in events
    assert len([event for event in events if event[0] == "audit_export_create"]) == 5


def test_restricted_license_blocks_changes_but_not_deactivation(page: Page, scenario) -> None:
    scenario("business-siem-restricted")
    goto(page, "configuracoes?vista=instalacao")
    region = section(page)
    row = region.locator('[data-destination="siem-soc"]')

    expect(region.get_by_text("Licença restrita", exact=False)).to_be_visible()
    expect(region.get_by_role("button", name="Novo destino")).to_be_disabled()
    expect(row.get_by_role("button", name="Editar")).to_be_disabled()
    expect(row.get_by_role("button", name="Enviar evento de teste")).to_be_disabled()
    assert axe_violations(page) == []
    screenshot(page, "027-licenca-restrita-1440")

    row.get_by_role("button", name="Desativar").click()
    expect(row.get_by_text("Desativado", exact=True)).to_be_visible()
    expect(row.get_by_role("button", name="Reativar")).to_be_disabled()
    expect(row.get_by_role("button", name="Excluir")).to_be_enabled()
    assert submitted(page) == [["audit_export_update", "siem-soc", False]]


def test_community_edition_has_no_audit_export_section(page: Page) -> None:
    seen: list[str] = []

    def record(request: Request) -> None:
        if "/audit-export" in request.url:
            seen.append(request.url)

    page.on("request", record)
    goto(page, "configuracoes?vista=instalacao")
    expect(page.get_by_role("heading", name="Versões")).to_be_visible()
    expect(page.get_by_role("heading", name="Exportação para SIEM")).to_have_count(0)
    assert seen == []


def test_business_without_the_capability_hides_the_section(page: Page, scenario) -> None:
    scenario("business")
    goto(page, "configuracoes?vista=instalacao")
    expect(page.get_by_role("region", name="Licença")).to_be_visible()
    expect(page.get_by_role("heading", name="Exportação para SIEM")).to_have_count(0)


def test_unavailable_destination_with_gap_shows_the_persistent_warning(page: Page, scenario) -> None:
    """T028: destino fora há dias mostra situação, fila "10.000+", idade do mais antigo, último erro e a lacuna."""
    scenario("business-siem-gap")
    goto(page, "configuracoes?vista=instalacao")
    row = section(page).locator('[data-destination="siem-soc"]')

    expect(row.get_by_text("Lacuna recente", exact=True)).to_be_visible()
    expect(row.get_by_text("pendentes: 10.000+", exact=False)).to_be_visible()
    expect(row.get_by_text("(mais antigo há 2 dias)", exact=False)).to_be_visible()
    expect(row.get_by_text("Conexão recusada ou host não encontrado", exact=False)).to_be_visible()
    expect(row.get_by_text("Lacuna na exportação", exact=True)).to_be_visible()
    expect(row.get_by_text("audit_export.gap", exact=False)).to_be_visible()
    assert axe_violations(page) == []
    screenshot(page, "027-lacuna-1440")


def test_otlp_destination_with_path_and_token(page: Page, scenario) -> None:
    """T032: protocolo OTLP com caminho, porta padrão 4318 e o token do secret mostrado só como presença."""
    scenario("business-siem")
    goto(page, "configuracoes?vista=instalacao")
    region = section(page)
    region.get_by_role("button", name="Novo destino").click()
    form = region.get_by_role("form", name="Novo destino")
    form.get_by_role("button", name=re.compile("Protocolo")).click()
    page.get_by_role("option", name="OpenTelemetry (OTLP/HTTP)").click()
    expect(form.get_by_label("Porta", exact=True)).to_have_value("4318")
    expect(form.get_by_text("token (vai como Authorization: Bearer)", exact=False)).to_be_visible()
    form.get_by_label("Nome").fill("otel-soc")
    form.get_by_label("Host").fill("otel.exemplo.interno")
    form.get_by_label("Caminho").fill("/coletor/v1/logs")
    form.get_by_label("Secret montado (opcional)").fill("otel-soc")
    assert axe_violations(page) == []
    form.get_by_role("button", name="Salvar destino").click()

    row = region.locator('[data-destination="otel-soc"]')
    expect(row.get_by_text("OTLP · otel.exemplo.interno:4318/coletor/v1/logs", exact=False)).to_be_visible()
    row.get_by_role("button", name="Enviar evento de teste").click()
    expect(page.get_by_text("Evento de teste entregue a otel-soc.", exact=True)).to_be_visible()
    page.wait_for_timeout(600)
    screenshot(page, "027-otlp-1440")
