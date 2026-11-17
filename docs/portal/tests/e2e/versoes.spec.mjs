import {expect, pickFromNavbarDropdown, test} from './fixtures.mjs';

const VERSION_MENU = {desktopName: /^Versão do manual:/, mobileName: 'Versões do manual'};

test.describe('versões (SC-001)', () => {
  test('cada linha mostra o próprio marcador e mantém após reload', async ({page}) => {
    await page.goto('/instalacao/');
    await expect(page.getByText('Linha 1.1', {exact: true})).toBeVisible();
    await page.reload();
    await expect(page.getByText('Linha 1.1', {exact: true})).toBeVisible();

    await page.goto('/1.0/instalacao/');
    await expect(page.getByText('Linha 1.0', {exact: true})).toBeVisible();
    await page.reload();
    await expect(page.getByText('Linha 1.0', {exact: true})).toBeVisible();
    await expect(page.getByText('Linha 1.1', {exact: true})).toHaveCount(0);
  });

  test('a troca de versão preserva o tópico', async ({page}) => {
    await page.goto('/instalacao/');
    await pickFromNavbarDropdown(page, {...VERSION_MENU, item: '1.0 (demonstração)'});
    await expect(page).toHaveURL(/\/1\.0\/instalacao\/$/);
    await expect(page.getByText('Linha 1.0', {exact: true})).toBeVisible();
  });

  test('tópico ausente na versão de destino mostra aviso', async ({page}) => {
    await page.goto('/recursos-avancados/');
    await pickFromNavbarDropdown(page, {...VERSION_MENU, item: '1.0 (demonstração)'});
    await expect(page).toHaveURL(/\/1\.0\/\?topico-ausente=recursos-avancados$/);
    const notice = page.getByTestId('topico-ausente');
    await expect(notice).toBeVisible();
    await expect(notice).toContainText('recursos-avancados');
    await expect(page.getByText('Linha 1.1', {exact: true})).toHaveCount(0);
  });

  test('preview tem banner e noindex', async ({page}) => {
    await page.goto('/next/instalacao/');
    await expect(page.locator('.theme-doc-version-banner')).toBeVisible();
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
    await expect(page.getByText('Linha next', {exact: true})).toBeVisible();
  });

  test('linhas publicadas indicam conteúdo de demonstração', async ({page}) => {
    for (const path of ['/instalacao/', '/1.0/instalacao/']) {
      await page.goto(path);
      await expect(page.locator('.theme-admonition-note').first()).toContainText('demonstração');
      await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', /noindex/);
    }
  });
});
