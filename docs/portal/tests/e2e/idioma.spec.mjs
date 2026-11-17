import {expect, pickFromNavbarDropdown, test} from './fixtures.mjs';

const LOCALE_MENU = {desktopName: /^(Português|English|Español)$/, mobileName: /^(Línguas|Languages|Idiomas)$/};

test.describe('idioma (SC-002, SC-004)', () => {
  for (const [locale, expected, lang] of [
    ['pt-BR', /\/$/, 'pt-BR'],
    ['en-US', /\/en\/$/, 'en'],
    ['es-MX', /\/es\/$/, 'es'],
    ['fr-FR', /\/$/, 'pt-BR'],
  ]) {
    test(`entrada neutra com navegador em ${locale}`, async ({browser, baseURL}) => {
      const context = await browser.newContext({locale, baseURL});
      const page = await context.newPage();
      await page.goto('/');
      await expect(page).toHaveURL(new RegExp(`^${baseURL}${expected.source.slice(1)}`));
      await expect(page.locator('html')).toHaveAttribute('lang', lang);
      await context.close();
    });
  }

  test('links profundos não são redirecionados', async ({browser, baseURL}) => {
    const context = await browser.newContext({locale: 'es-ES', baseURL});
    const page = await context.newPage();
    await page.goto('/next/instalacao/');
    await expect(page).toHaveURL(`${baseURL}/next/instalacao/`);
    await page.goto('/en/instalacao/');
    await expect(page).toHaveURL(`${baseURL}/en/instalacao/`);
    await expect(page.locator('html')).toHaveAttribute('lang', 'en');
    await context.close();
  });

  test('a escolha manual prevalece e a troca preserva linha e tópico', async ({page}) => {
    await page.goto('/instalacao/');
    await pickFromNavbarDropdown(page, {...LOCALE_MENU, item: 'Español'});
    await expect(page).toHaveURL(/\/es\/instalacao\/$/);
    await expect(page.locator('html')).toHaveAttribute('lang', 'es');
    await expect(page.getByRole('heading', {name: 'Escenarios'})).toBeVisible();
    await expect.poll(() => page.evaluate(() => localStorage.getItem('dokpod.docs.locale'))).toBe('es');
    await page.goto('/');
    await expect(page).toHaveURL(/\/es\/$/);
  });

  test('tradução ausente mostra aviso e link para o português', async ({page}) => {
    await page.goto('/en/next/hosts-remotos/');
    const missing = page.getByTestId('traducao-ausente');
    await expect(missing).toBeVisible();
    await expect(missing.getByRole('link')).toHaveAttribute('href', '/next/hosts-remotos/');
    await expect(page.locator('meta[name="robots"]').last()).toHaveAttribute('content', /noindex/);
    await expect(page.getByRole('heading', {name: 'Adicionar um host'})).toHaveCount(0);
    await expect(page.locator('.theme-doc-toc-desktop, .theme-doc-toc-mobile')).toHaveCount(0);
  });

  test('armazenamento bloqueado mantém a leitura', async ({page}) => {
    await page.addInitScript(() => {
      Object.defineProperty(window, 'localStorage', {get() { throw new Error('bloqueado'); }});
    });
    await page.goto('/');
    await expect(page.locator('h1')).toBeVisible();
    await page.goto('/instalacao/');
    await expect(page.getByRole('heading', {name: 'Cenários'})).toBeVisible();
  });
});

test.describe('sem JavaScript', () => {
  test.use({javaScriptEnabled: false});

  test('conteúdo legível e rodapé com idiomas', async ({page}) => {
    await page.goto('/instalacao/');
    await expect(page.getByRole('heading', {name: 'Cenários'})).toBeVisible();
    const footer = page.locator('footer');
    await expect(footer.locator('a[href="/en/"]')).toHaveCount(1);
    await expect(footer.locator('a[href="/es/"]')).toHaveCount(1);
  });
});
