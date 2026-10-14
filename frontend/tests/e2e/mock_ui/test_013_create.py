"""Spec 013 (T016/T017): criação completa, validações, segredo do cofre, conflitos, retry sem duplicar, acessibilidade e mobile."""

import json
import re
import time

from playwright.sync_api import Page, expect

from conftest import BASE_URL, axe_violations, goto, no_horizontal_scroll, screenshot


def submitted(page: Page) -> list:
    return [s for s in page.request.get(f"{BASE_URL}__mock/submitted").json() if s[0] == "create"]


def open_drawer(page: Page):
    goto(page, "containers")
    page.get_by_role("button", name="Novo container").click()
    drawer = page.get_by_role("dialog", name="Criar e iniciar container")
    expect(drawer).to_be_visible()
    return drawer


def pick(drawer, label: str, option: str) -> None:
    drawer.get_by_role("button", name=re.compile(f"^{label} ")).click()
    drawer.page.get_by_role("option", name=re.compile(option)).first.click()


def test_create_container_end_to_end_with_vault_secret(page: Page) -> None:
    started = time.monotonic()
    drawer = open_drawer(page)
    pick(drawer, "Engine", "Podman")
    pick(drawer, "Host", "srv-prod-03")
    expect(drawer.get_by_text("srv-prod-03").first).to_be_visible()
    drawer.get_by_label("Imagem", exact=True).fill("registry.empresa.com/pedidos/api:3.14.2")
    drawer.get_by_label("Nome do container").fill("api-pedidos-b21e")
    drawer.get_by_label("Porta do host").fill("8085")
    drawer.get_by_label("Porta do container").fill("80")
    drawer.get_by_role("button", name="Adicionar variável").click()
    drawer.get_by_label("Nome da variável").last.fill("DATABASE_URL")
    drawer.get_by_label("Valor da variável").last.fill("postgres://canario-013-e2e@db/pedidos")
    drawer.get_by_role("checkbox", name="Valor sensível (oculto)").last.check()
    drawer.get_by_role("button", name="Usar segredo do cofre").click()
    drawer.get_by_label("Nome da variável").last.fill("JWT_SECRET")
    drawer.get_by_label("Volume").fill("vol-pedidos-uploads:/srv/app/uploads:rw")
    drawer.get_by_role("checkbox", name="Ativar health check em GET /healthz").check()
    expect(drawer.get_by_text("3 tentativas antes de reiniciar", exact=False)).to_be_visible()

    command = drawer.locator(".nc-command")
    expect(command).to_contain_text("podman run -d --name api-pedidos-b21e -p 8085:80 -e DATABASE_URL=***")
    expect(command).to_contain_text("--secret jwt-pedidos,type=env,target=JWT_SECRET")
    expect(command).to_contain_text("--health-on-failure=restart")
    expect(command).not_to_contain_text("canario-013-e2e")
    assert axe_violations(page) == []
    screenshot(page, "013-drawer-preenchido-1440")

    drawer.get_by_role("button", name="Criar e iniciar").click()
    progress = drawer.get_by_role("region", name="Andamento da criação")
    expect(progress.get_by_text("Em execução")).to_be_visible(timeout=15_000)
    elapsed = time.monotonic() - started
    print(f"SC-001 (mock): do clique em 'Novo container' até 'Em execução' em {elapsed:.1f}s")
    assert elapsed < 60
    screenshot(page, "013-drawer-concluido-1440")
    body = json.loads(submitted(page)[0][3])
    assert body["environment"][1] == {"name": "JWT_SECRET", "secret": {"kind": "podman_secret", "name": "jwt-pedidos"}, "sensitive": True}
    assert body["volumes"] == [{"type": "volume", "source": "vol-pedidos-uploads", "target": "/srv/app/uploads", "readOnly": False}]
    assert body["healthcheck"]["onFailure"] == "restart"
    assert "canario-013-e2e" not in page.url
    progress.get_by_role("link", name="Abrir detalhe do container").click()
    expect(page).to_have_url(re.compile(r"/containers/00000000-0000-4000-8000-000000000003/"))


def test_inline_validation_conflicts_and_docker_health_help(page: Page) -> None:
    drawer = open_drawer(page)
    pick(drawer, "Engine", "Docker")
    pick(drawer, "Host", "srv-prod-01")
    drawer.get_by_label("Imagem", exact=True).fill("user:senha@registry.empresa.com/app")
    drawer.get_by_label("Nome do container").fill("checkout-web")
    drawer.get_by_label("Porta do host").fill("8080")
    drawer.get_by_label("Porta do container").fill("8080")
    expect(drawer.get_by_text("Referência inválida", exact=False)).to_be_visible()
    expect(drawer.get_by_text("Já existe um container com esse nome neste host.")).to_be_visible()
    expect(drawer.get_by_text("já está publicada neste host", exact=False)).to_be_visible()
    expect(drawer.get_by_role("button", name="Criar e iniciar")).to_be_disabled()
    drawer.get_by_role("checkbox", name="Ativar health check em GET /healthz").check()
    expect(drawer.get_by_text("antes de marcar como não saudável", exact=False)).to_be_visible()
    drawer.get_by_role("checkbox", name="Remover automaticamente ao parar").check()
    expect(drawer.get_by_text("logs e estado se perdem", exact=False)).to_be_visible()
    expect(drawer.get_by_text('exige a política de reinício "no"', exact=False)).to_be_visible()
    assert axe_violations(page) == []
    screenshot(page, "013-drawer-erros-1440")


def test_port_conflict_failure_is_readable_and_disconnected_host_is_not_selectable(page: Page) -> None:
    drawer = open_drawer(page)
    drawer.get_by_role("button", name=re.compile("^Host ")).click()
    expect(page.get_by_role("option", name=re.compile("srv-dev-01"))).to_have_attribute("aria-disabled", "true")
    page.keyboard.press("Escape")
    pick(drawer, "Engine", "Docker")
    pick(drawer, "Host", "srv-prod-02")
    drawer.get_by_label("Imagem", exact=True).fill("nginx:1.27")
    drawer.get_by_label("Nome do container").fill("web-conflito")
    drawer.get_by_label("Porta do host").fill("9999")
    drawer.get_by_label("Porta do container").fill("80")
    drawer.get_by_role("button", name="Criar e iniciar").click()
    expect(drawer.get_by_text("A porta do host já está em uso; o container criado foi removido.")).to_be_visible(timeout=15_000)
    screenshot(page, "013-drawer-falha-1440")


def test_overview_new_container_opens_drawer_and_escape_closes_it(page: Page) -> None:
    goto(page)
    page.get_by_role("link", name="Novo container").click()
    drawer = page.get_by_role("dialog", name="Criar e iniciar container")
    expect(drawer).to_be_visible()
    page.keyboard.press("Escape")
    expect(drawer).to_be_hidden()
    expect(page).not_to_have_url(re.compile(r"novo=1"))


def test_readonly_cannot_create(page: Page, scenario) -> None:
    scenario("readonly")
    goto(page, "containers")
    expect(page.get_by_role("button", name="Novo container")).to_be_disabled()


def test_drawer_mobile_has_no_horizontal_scroll(page: Page) -> None:
    page.set_viewport_size({"width": 390, "height": 900})
    drawer = open_drawer(page)
    expect(drawer.get_by_label("Imagem", exact=True)).to_be_visible()
    assert no_horizontal_scroll(page)
    screenshot(page, "013-drawer-vazio-390")
    assert axe_violations(page) == []
