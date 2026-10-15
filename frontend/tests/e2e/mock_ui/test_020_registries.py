"""Spec 020 (T010): Registros — lista consolidada com origens, inseguro e fora da política, detalhe por host com link para
Imagens, política por estágio com impacto e edição, somente leitura sem permissão e acessibilidade."""

import re

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, no_horizontal_scroll, screenshot


def submitted(page: Page) -> list:
    return page.request.get(f"{BASE_URL}__mock/submitted").json()


def test_list_origins_insecure_outside_policy_and_unavailable_host(page: Page) -> None:
    goto(page, "registros")
    expect(page.get_by_role("heading", name="Registros de imagens")).to_be_visible()
    expect(page.get_by_role("link", name="Registros")).to_have_attribute("aria-current", "page")
    table = page.get_by_role("table")
    expect(table.get_by_text("registry.empresa.com", exact=True)).to_be_visible()
    expect(table.get_by_text("mirror.empresa.com", exact=True)).to_be_visible()
    expect(page.get_by_text("Configuração do engine indisponível em: srv-prod-04", exact=False)).to_be_visible()
    page.get_by_role("button", name=re.compile(r"inseguros\s*, filtrar a lista")).click()
    expect(page).to_have_url(re.compile(r"inseguros=1"))
    expect(table.get_by_text("registry.local:5000", exact=True)).to_be_visible()
    expect(table.get_by_role("row")).to_have_count(2)
    screenshot(page, "020-registros-inseguros-1440")
    assert axe_violations(page) == []


def test_detail_per_host_never_shows_credential_values_and_links_to_images(page: Page) -> None:
    goto(page, "registros?registro=registry.empresa.com")
    drawer = page.get_by_role("dialog", name="registry.empresa.com")
    expect(drawer.get_by_text("Credenciais aparecem só como", exact=False)).to_be_visible()
    expect(drawer.get_by_text("Credencial no agente").first).to_be_visible()
    link = drawer.get_by_role("link", name=re.compile(r"Ver as \d+ imagens em srv-prod-01"))
    expect(link).to_be_visible()
    assert axe_violations(page) == []
    link.click()
    expect(page).to_have_url(re.compile(r"imagens\?.*q=registry\.empresa\.com"))


def test_policy_view_shows_impact_and_saves_with_audit_trail(page: Page) -> None:
    goto(page, "registros?vista=politica")
    production = page.get_by_role("region", name="Produção")
    expect(production.get_by_text("registry.empresa.com")).to_be_visible()
    expect(production.get_by_text("Valor inicial da configuração da API")).to_be_visible()
    expect(page.get_by_role("region", name="Homologação").get_by_text("Qualquer registro")).to_be_visible()
    page.get_by_role("button", name="Editar a política de Homologação").click()
    drawer = page.get_by_role("dialog", name="Registros permitidos em Homologação")
    field = drawer.get_by_label(re.compile("Registros permitidos"))
    field.fill("https://quay.io")
    expect(drawer.get_by_text("Registros inválidos", exact=False)).to_be_visible()
    expect(drawer.get_by_role("button", name="Salvar política")).to_be_disabled()
    field.fill("Quay.io")
    expect(drawer.get_by_text(re.compile(r"\d+ image(m|ns) em 1 host fica fora da lista|Nenhuma imagem"))).to_be_visible()
    screenshot(page, "020-registros-politica-1440")
    assert axe_violations(page) == []
    drawer.get_by_role("button", name="Salvar política").click()
    staging = page.get_by_role("region", name="Homologação")
    expect(staging.get_by_text("quay.io")).to_be_visible()
    expect(staging.get_by_text("Definida no console por", exact=False)).to_be_visible()
    assert ["registry_policy", "staging", "quay.io"] in submitted(page)


def test_policy_is_read_only_without_manage(page: Page, scenario) -> None:
    scenario("readonly")
    goto(page, "registros?vista=politica")
    expect(page.get_by_role("region", name="Produção").get_by_text("somente leitura", exact=False)).to_be_visible()
    expect(page.get_by_role("button", name=re.compile("Editar a política"))).to_have_count(0)


def test_narrow_width(page: Page) -> None:
    page.set_viewport_size({"width": 390, "height": 900})
    goto(page, "registros")
    expect(page.get_by_role("heading", name="Registros de imagens")).to_be_visible()
    assert no_horizontal_scroll(page)
    goto(page, "registros?vista=politica")
    assert no_horizontal_scroll(page)
