import {expect, test} from './fixtures.mjs';

for (const [locale, root, section, label] of [
  ['pt-BR', '/', '/produtos', 'Conhecer o Dokpod'],
  ['en', '/en/', '/en/products', 'Explore Dokpod'],
  ['es', '/es/', '/es/productos', 'Conocer Dokpod'],
]) {
  test(`rodapé com produto, Altivy e idiomas (${locale})`, async ({page}) => {
    await page.goto(root);
    const footer = page.locator('footer');
    await footer.scrollIntoViewIfNeeded();
    await expect(footer.getByRole('link', {name: label, exact: true})).toHaveAttribute('href', `https://altivy.it${section}/dokpod/`);
    await expect(footer.getByRole('link', {name: 'Altivy', exact: true})).toHaveAttribute('href', `https://altivy.it${locale === 'pt-BR' ? '/' : `/${locale}/`}`);
    await expect(footer.getByRole('link', {name: 'GitHub', exact: true})).toHaveAttribute('href', 'https://github.com/altivy-it/dokpod');
    await expect(footer.getByRole('link', {name: 'GitHub · Altivy', exact: true})).toHaveAttribute('href', 'https://github.com/altivy-it/');
    await expect(footer.getByRole('link', {name: 'Releases', exact: true})).toHaveAttribute('href', 'https://github.com/altivy-it/dokpod/releases');
    await expect(footer.getByRole('link', {name: /AGPL-3.0-only/})).toHaveAttribute('href', 'https://github.com/altivy-it/dokpod/blob/main/LICENSE');
    await expect(footer.getByRole('link', {name: 'contato@altivy.it'})).toHaveAttribute('href', 'mailto:contato@altivy.it');
    await footer.getByRole('link', {name: 'Altivy', exact: true}).focus();
    await page.keyboard.press('Tab');
    await expect(footer.getByRole('link', {name: label, exact: true})).toBeFocused();
    await expect(footer.getByRole('link', {name: label, exact: true})).toHaveCSS('outline-style', 'solid');
    const languages = footer.locator('a[hreflang]');
    await expect(languages).toHaveCount(3);
    await expect(footer.locator(`a[hreflang="${locale}"]`)).toHaveAttribute('aria-current', 'page');
    const bounds = await footer.evaluate((element) => {
      const {left, right} = element.getBoundingClientRect();
      return {left, right, width: window.innerWidth, scroll: document.documentElement.scrollWidth};
    });
    expect(bounds.left).toBeGreaterThanOrEqual(0);
    expect(bounds.right).toBeLessThanOrEqual(bounds.width);
    expect(bounds.scroll).toBeLessThanOrEqual(bounds.width);
  });
}
