"""Spec 031 (T014, História 1): na Community Edition, Configurações não mostra a seção Licença nem chama
`/api/v1/licensing/*` em nenhuma vista; com a edição Business informada pela API, a seção aparece (T016)."""

from playwright.sync_api import Page, Request, expect

from conftest import axe_violations, goto, screenshot

VIEWS = ("configuracoes", "configuracoes?vista=limites", "configuracoes?vista=instalacao")


def licensing_requests(page: Page) -> list[str]:
    seen: list[str] = []

    def record(request: Request) -> None:
        if "/api/v1/licensing" in request.url:
            seen.append(request.url)

    page.on("request", record)
    return seen


def test_community_edition_has_no_licensing_section_or_request(page: Page) -> None:
    seen = licensing_requests(page)
    for view in VIEWS:
        goto(page, view)
        expect(page.locator("#dok-main").get_by_role("heading", name="Configurações", exact=True)).to_be_visible()

    expect(page.get_by_role("heading", name="Versões")).to_be_visible()
    expect(page.get_by_text("Community Edition", exact=True)).to_be_visible()
    expect(page.get_by_role("heading", name="Licença")).to_have_count(0)
    goto(page, "")
    assert seen == []
    screenshot(page, "031-ce-sem-licenca-1440")


def test_business_edition_shows_the_licensing_section(page: Page, scenario) -> None:
    scenario("business")
    goto(page, "configuracoes?vista=instalacao")

    section = page.get_by_role("region", name="Licença")
    expect(section).to_be_visible()
    expect(page.get_by_text("Business", exact=True)).to_be_visible()
    assert axe_violations(page) == []
