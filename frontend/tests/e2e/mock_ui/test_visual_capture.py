"""Loop de verificação visual (specs 007-014): captura app e board do mockup lado a lado.

Evidência de desenvolvimento, não gate formal (frontend-visual-verification.instructions.md).
Rodar com `-m visual`; as imagens ficam em artifacts/e2e/mock/visual/.
"""

import pytest
from playwright.sync_api import Page

from conftest import BASE_URL, REPORTS

PAIRS = [
    ("01-visao-geral", "Main.dc.html", ""),
    ("02-containers", "Containers.dc.html", "containers"),
    ("04-novo-container", "NovoContainer.dc.html", "containers?novo=1"),
]


@pytest.mark.visual
@pytest.mark.parametrize("name,board,path", PAIRS)
@pytest.mark.parametrize("width", [1440, 390])
def test_capture_app_and_board(page: Page, name: str, board: str, path: str, width: int) -> None:
    out = REPORTS / "visual"
    out.mkdir(parents=True, exist_ok=True)
    page.set_viewport_size({"width": width, "height": 1000})
    page.goto(f"{BASE_URL}design/project/{board}", wait_until="networkidle")
    page.wait_for_timeout(800)
    page.screenshot(path=str(out / f"{name}-{width}-board.png"))
    page.goto(BASE_URL + path, wait_until="networkidle")
    page.wait_for_timeout(500)
    page.screenshot(path=str(out / f"{name}-{width}-app.png"))
