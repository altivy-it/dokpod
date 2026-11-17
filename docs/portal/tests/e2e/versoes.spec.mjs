import {expect, pickFromNavbarDropdown, test} from './fixtures.mjs';

const VERSION_MENU = {desktopName: /^Versão do manual:/, mobileName: 'Versões do manual'};
const PUBLISHED = '0.7 (prévia)';
const PREVIEW = 'Preview (não publicado)';
// Seções que só existem em uma das linhas da página de instalação.
const lab = (page) => page.getByRole('heading', {name: 'Avaliação em laboratório'});
const installer = (page) => page.getByRole('heading', {name: 'Cenários'});

test.describe('versões (SC-001)', () => {
  test('cada linha mostra o próprio conteúdo e mantém após reload', async ({page}) => {
    await page.goto('/instalacao/');
    await expect(lab(page)).toBeVisible();
    await page.reload();
    await expect(lab(page)).toBeVisible();

    await page.goto('/next/instalacao/');
    await expect(installer(page)).toBeVisible();
    await page.reload();
    await expect(installer(page)).toBeVisible();
    await expect(lab(page)).toHaveCount(0);
  });

  test('a troca de versão preserva o tópico', async ({page}) => {
    await page.goto('/next/instalacao/');
    await pickFromNavbarDropdown(page, {...VERSION_MENU, item: PUBLISHED});
    await expect(page).toHaveURL(/\/instalacao\/$/);
    await expect(page).not.toHaveURL(/\/next\//);
    await expect(lab(page)).toBeVisible();

    await pickFromNavbarDropdown(page, {...VERSION_MENU, item: PREVIEW});
    await expect(page).toHaveURL(/\/next\/instalacao\/$/);
    await expect(installer(page)).toBeVisible();
  });

  test('tópico ausente na versão de destino mostra aviso', async ({page}) => {
    await page.goto('/next/hosts-remotos/');
    await pickFromNavbarDropdown(page, {...VERSION_MENU, item: PUBLISHED});
    await expect(page).toHaveURL(/\/\?topico-ausente=hosts-remotos$/);
    await expect(page).not.toHaveURL(/\/next\//);
    const notice = page.getByTestId('topico-ausente');
    await expect(notice).toBeVisible();
    await expect(notice).toContainText('hosts-remotos');
  });

  test('preview tem banner e noindex', async ({page}) => {
    await page.goto('/next/instalacao/');
    await expect(page.locator('.theme-doc-version-banner')).toBeVisible();
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
  });

  test('a linha publicada é indexável e só ela está no sitemap', async ({page, request}) => {
    await page.goto('/instalacao/');
    await expect(page.locator('.theme-doc-version-banner')).toHaveCount(0);
    for (const content of await page.locator('meta[name="robots"]').evaluateAll((metas) => metas.map((meta) => meta.content))) {
      expect(content).not.toMatch(/noindex/);
    }
    const sitemap = await (await request.get('/sitemap.xml')).text();
    expect(sitemap).toContain('/instalacao/</loc>');
    expect(sitemap).not.toContain('/next/');
  });
});
