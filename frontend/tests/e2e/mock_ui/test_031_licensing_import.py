"""Spec 031 (T029, História 2): na edição Business, importar a licença de referência atualiza o estado; um arquivo
adulterado é recusado com mensagem tipada, sem ecoar o conteúdo, e a licença ativa continua em vigor. A verificação
criptográfica real é coberta pelos testes da API com PostgreSQL (T021); aqui fica a jornada pela borda nginx."""

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, screenshot

VALID = "mock-license.e2e-fixture.not-signed"
TAMPERED = "conteudo-adulterado-secreto.e2e.fixture"


def submitted(page: Page) -> list:
    return page.request.get(f"{BASE_URL}__mock/submitted").json()


def upload(page: Page, content: str) -> None:
    section = page.get_by_role("region", name="Licença")
    section.locator("#lic-file").set_input_files({"name": "licenca.jws", "mimeType": "application/jose", "buffer": content.encode()})
    section.get_by_label("Slot da instalação").fill("prd")
    section.get_by_role("button", name="Importar licença").click()


def test_import_valid_then_refuse_tampered(page: Page, scenario) -> None:
    scenario("business")
    goto(page, "configuracoes?vista=instalacao")
    section = page.get_by_role("region", name="Licença")
    expect(section.get_by_text("Sem licença", exact=True)).to_be_visible()
    assert axe_violations(page) == []

    upload(page, VALID)
    expect(page.get_by_text("Licença importada.", exact=True)).to_be_visible()
    expect(section.get_by_text("Licença válida", exact=True)).to_be_visible()
    expect(section.get_by_text("lic_e2e · revisão 1")).to_be_visible()
    screenshot(page, "031-licenca-importada-1440")

    upload(page, TAMPERED)
    expect(page.get_by_text("O arquivo foi recusado na verificação. A licença atual continua em vigor.", exact=True)).to_be_visible()
    expect(section.get_by_text("lic_e2e · revisão 1")).to_be_visible()
    expect(page.get_by_text("conteudo-adulterado-secreto")).to_have_count(0)
    assert [list(item) for item in submitted(page) if item[0] == "license_import"] == [
        ["license_import", "prd", "accepted"],
        ["license_import", "prd", "rejected"],
    ]
