"""Spec 025 (T018, História 1): na composição Business com a capability image-update-notice, Imagens, Containers e o
detalhe do container mostram o estado da verificação em texto, com o motivo e a data; "Verificar agora" é aceito (202)
e o segundo pedido no mesmo minuto é recusado com Retry-After (429). Na Community Edition nada disso aparece. A
consulta real ao registro é coberta pelos testes do agente (T009–T012)."""

import re

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, screenshot


def env(n: int) -> str:
    return f"00000000-0000-4000-8000-{n:012d}"


def cid(name: str) -> str:
    return (name.encode().hex() * 4)[:64]


def test_images_show_the_states_and_check_now_is_rate_limited(page: Page, scenario) -> None:
    scenario("business-images")
    goto(page, "imagens")
    table = page.get_by_role("table")
    expect(table.get_by_role("columnheader", name="Nova versão")).to_be_visible()
    expect(table.get_by_text("Nova versão disponível", exact=True).first).to_be_visible()
    expect(table.get_by_text("Atualizada", exact=True).first).to_be_visible()
    expect(table.get_by_text("Falha na verificação", exact=True).first).to_be_visible()
    expect(table.get_by_text("Não verificável", exact=True).first).to_be_visible()
    expect(table.get_by_text(re.compile(r"Limite de requisições do registro atingido · verificada em")).first).to_be_visible()
    expect(table.get_by_text(re.compile(r"Registro fora da política do estágio")).first).to_be_visible()
    screenshot(page, "025-imagens-aviso-1440")
    assert axe_violations(page) == []

    actions = page.get_by_role("button", name="Ações de registry.empresa.com/loja/checkout:5.2.0 em srv-prod-01")
    actions.click()
    page.get_by_role("menuitem", name="Verificar agora").click()
    expect(page.get_by_text(re.compile(r"A verificação de nova versão em srv-prod-01 foi pedida")).first).to_be_visible()

    actions.click()
    page.get_by_role("menuitem", name="Verificar agora").click()
    expect(page.get_by_text(re.compile(r"há menos de 1 minuto\. Tente de novo em 42 s\.")).first).to_be_visible()

    submitted = page.request.get(f"{BASE_URL}__mock/submitted").json()
    assert [item for item in submitted if item[0] == "image_update_check"] == [["image_update_check", "srv-prod-01"]]


def test_containers_and_detail_show_container_behind(page: Page, scenario) -> None:
    scenario("business-images")
    goto(page, "containers")
    table = page.get_by_role("table")
    expect(table.get_by_role("columnheader", name="Nova versão")).to_be_visible()
    expect(table.get_by_text("Versão nova no host; o container usa a anterior", exact=True)).to_be_visible()
    expect(table.get_by_text("Nova versão disponível", exact=True).first).to_be_visible()
    screenshot(page, "025-containers-aviso-1440")
    assert axe_violations(page) == []

    goto(page, f"containers/{env(2)}/{cid('redis-sessoes')}")
    note = page.locator('[data-test="image-update"]')
    expect(note.get_by_text("Versão nova no host; o container usa a anterior", exact=True).first).to_be_visible()
    expect(note).to_contain_text("verificada em")
    screenshot(page, "025-detalhe-aviso-1440")


def test_community_edition_shows_nothing(page: Page, scenario) -> None:
    scenario("default")
    calls: list[str] = []
    page.on("request", lambda request: calls.append(request.url) if "update-check" in request.url else None)

    goto(page, "imagens")
    expect(page.get_by_role("table")).to_be_visible()
    expect(page.get_by_role("columnheader", name="Nova versão")).to_have_count(0)
    goto(page, "containers")
    expect(page.get_by_role("table")).to_be_visible()
    expect(page.get_by_role("columnheader", name="Nova versão")).to_have_count(0)
    goto(page, f"containers/{env(2)}/{cid('redis-sessoes')}")
    expect(page.locator('[data-test="image-update"]')).to_have_count(0)
    assert calls == []
