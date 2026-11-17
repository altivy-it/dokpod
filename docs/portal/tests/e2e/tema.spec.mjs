import {expect, test, themeButton} from './fixtures.mjs';

const theme = (page) => page.locator('html').getAttribute('data-theme');

async function choose(page, label) {
  const button = await themeButton(page);
  await button.click();
  await page.getByRole('menuitemradio', {name: label}).click();
}

test.describe('tema (SC-003)', () => {
  test('primeira visita é escura mesmo com sistema claro', async ({page}) => {
    await page.emulateMedia({colorScheme: 'light'});
    await page.goto('/');
    await expect.poll(() => theme(page)).toBe('dark');
  });

  for (const [label, value] of [['Claro', 'light'], ['Escuro', 'dark']]) {
    test(`${label} persiste após reload e ignora o sistema`, async ({page}) => {
      await page.goto('/instalacao/');
      await choose(page, label);
      await expect.poll(() => theme(page)).toBe(value);
      await page.reload();
      await expect.poll(() => theme(page)).toBe(value);
      await page.emulateMedia({colorScheme: value === 'light' ? 'dark' : 'light'});
      await expect.poll(() => theme(page)).toBe(value);
      await page.reload();
      await expect.poll(() => theme(page)).toBe(value);
    });
  }

  test('Sistema acompanha prefers-color-scheme ao vivo', async ({page}) => {
    await page.goto('/instalacao/');
    await choose(page, 'Sistema');
    await page.emulateMedia({colorScheme: 'light'});
    await expect.poll(() => theme(page)).toBe('light');
    await page.emulateMedia({colorScheme: 'dark'});
    await expect.poll(() => theme(page)).toBe('dark');
    await page.reload();
    await expect(page.locator('html')).toHaveAttribute('data-theme-choice', 'system');
    // O Docusaurus só escuta prefers-color-scheme depois da hidratação do React.
    await expect(async () => {
      await page.emulateMedia({colorScheme: 'dark'});
      await page.emulateMedia({colorScheme: 'light'});
      expect(await theme(page)).toBe('light');
    }).toPass({timeout: 10_000});
    expect(await page.evaluate(() => localStorage.getItem('altivy.docs.theme'))).toBe('system');
  });

  test('valor inválido cai no escuro', async ({page}) => {
    await page.addInitScript(() => localStorage.setItem('altivy.docs.theme', 'roxo'));
    await page.emulateMedia({colorScheme: 'light'});
    await page.goto('/');
    await expect.poll(() => theme(page)).toBe('dark');
  });

  test('armazenamento bloqueado não quebra a página', async ({page}) => {
    await page.addInitScript(() => {
      Object.defineProperty(window, 'localStorage', {get() { throw new Error('bloqueado'); }});
    });
    await page.goto('/instalacao/');
    await expect(page.getByRole('heading', {name: 'Avaliação em laboratório'})).toBeVisible();
    expect(['dark', 'light']).toContain(await theme(page));
  });

  test('menu operável por teclado', async ({page}) => {
    await page.goto('/instalacao/');
    const button = await themeButton(page);
    await button.focus();
    await page.keyboard.press('Enter');
    const menu = page.getByRole('menu', {name: 'Tema'});
    await expect(menu).toBeVisible();
    await expect(page.getByRole('menuitemradio', {name: 'Escuro'})).toBeFocused();
    await page.keyboard.press('Escape');
    await expect(menu).toBeHidden();
    await expect(button).toBeFocused();
    await page.keyboard.press('Enter');
    await page.keyboard.press('ArrowUp');
    await expect(page.getByRole('menuitemradio', {name: 'Claro'})).toBeFocused();
    await page.keyboard.press('Enter');
    await expect.poll(() => theme(page)).toBe('light');
    await expect(button).toBeFocused();
  });
});
