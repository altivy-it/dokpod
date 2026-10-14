"""Spec 007 SC-006: tempo até o shell interativo na stack local (borda nginx + mock)."""

import json

from playwright.sync_api import Page, expect

from conftest import BASE_URL, REPORTS


def test_shell_becomes_interactive_within_two_seconds(page: Page) -> None:
    samples = []
    for _ in range(5):
        page.goto("about:blank")
        page.goto(BASE_URL, wait_until="commit")
        expect(page.get_by_role("navigation", name="Menu principal")).to_be_visible()
        expect(page.get_by_role("link", name="Containers", exact=True)).to_be_enabled()
        samples.append(page.evaluate("performance.now()"))
    samples.sort()
    median = samples[len(samples) // 2]
    REPORTS.mkdir(parents=True, exist_ok=True)
    (REPORTS / "007-shell-interactive.json").write_text(json.dumps({"samples_ms": samples, "median_ms": median}))
    assert median <= 2000, f"shell interativo em {median:.0f} ms (meta ≤ 2000 ms)"
