import {expect, searchBox, test} from './fixtures.mjs';

async function search(page, term) {
  const box = searchBox(page);
  await box.fill(term);
  const status = page.locator('[role="status"][aria-live="polite"]').filter({hasText: /resultado|Nenhum|results?|No results|resultados?|Ningún/});
  await expect(status.first()).not.toHaveText('');
  return page.locator('a[href]').filter({has: page.locator('mark')});
}

test.describe('busca local (FR-010)', () => {
  test('busca na 1.0 não traz tópicos exclusivos da 1.1 e nada sai da origem', async ({page, baseURL}) => {
    const foreign = [];
    page.on('request', (request) => {
      if (!request.url().startsWith(baseURL)) foreign.push(request.url());
    });
    await page.goto('/1.0/instalacao/');
    const results = await search(page, 'Marcador');
    await expect(results.first()).toBeVisible();
    const hrefs = await results.evaluateAll((links) => links.map((link) => link.getAttribute('href')));
    expect(hrefs.length).toBeGreaterThan(0);
    for (const href of hrefs) expect(href).toMatch(/^\/1\.0\//);
    expect(hrefs.some((href) => href.includes('recursos-avancados'))).toBe(false);
    expect(foreign).toEqual([]);
  });

  test('busca em /en/ retorna páginas em inglês da versão aberta', async ({page}) => {
    await page.goto('/en/instalacao/');
    const results = await search(page, 'marker');
    await expect(results.first()).toBeVisible();
    const hrefs = await results.evaluateAll((links) => links.map((link) => link.getAttribute('href')));
    for (const href of hrefs) expect(href).toMatch(/^\/en\/(?!1\.0\/|next\/)/);
  });
});
