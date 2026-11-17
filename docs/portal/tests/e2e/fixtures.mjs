// Fixture comum: toda página é vigiada para violações de CSP e erros de JavaScript.
import {test as base, expect} from '@playwright/test';

export const test = base.extend({
  page: async ({page}, use, testInfo) => {
    const problems = [];
    await page.exposeFunction('__portalReportCsp', (detail) => problems.push(`CSP: ${detail}`));
    await page.addInitScript(() => {
      document.addEventListener('securitypolicyviolation', (event) => {
        window.__portalReportCsp(`${event.effectiveDirective} bloqueou ${event.blockedURI || 'inline'} em ${event.sourceFile || location.pathname}`);
      });
    });
    page.on('pageerror', (error) => problems.push(`pageerror: ${error.message}`));
    await use(page);
    expect(problems, `problemas em ${testInfo.title}`).toEqual([]);
  },
});

export {expect};

export async function usesMobileNav(page) {
  return page.locator('.navbar__toggle').isVisible();
}

// Abre o menu principal no mobile (no doc, o painel começa no menu da documentação).
export async function openMobilePrimaryMenu(page) {
  await page.locator('.navbar__toggle').click();
  await expect(page.locator('.navbar-sidebar')).toBeVisible();
  // Em páginas de doc o painel abre no menu secundário (sumário); a classe existe
  // desde o primeiro render, antes de a animação tornar o botão visível.
  const secondary = page.locator('.navbar-sidebar__items--show-secondary');
  if (await secondary.count()) {
    await page.locator('.navbar-sidebar__back').click();
    await expect(secondary).toHaveCount(0);
  }
}

// Escolhe um item de um dropdown da navbar, no desktop ou no menu mobile.
export async function pickFromNavbarDropdown(page, {desktopName, mobileName, item}) {
  if (await usesMobileNav(page)) {
    await openMobilePrimaryMenu(page);
    const sidebar = page.locator('.navbar-sidebar__item.menu').first();
    const link = sidebar.getByRole('link', {name: item, exact: true});
    // O dropdown que contém a página atual já vem expandido; clicar nele o recolheria.
    if (!(await link.isVisible())) {
      await sidebar.locator('.menu__link--sublist').filter({hasText: mobileName}).click();
    }
    await link.click();
  } else {
    const dropdown = page.locator('.navbar__item.dropdown').filter({has: page.getByRole('button', {name: desktopName})});
    await dropdown.hover();
    await dropdown.getByRole('link', {name: item, exact: true}).click();
  }
}

export async function themeButton(page) {
  if (await usesMobileNav(page)) {
    await page.locator('.navbar__toggle').click();
    return page.locator('.navbar-sidebar').getByRole('button', {name: /^Tema:/});
  }
  return page.locator('.navbar').getByRole('button', {name: /^Tema:/});
}

export function searchBox(page) {
  return page.getByRole('searchbox').filter({visible: true}).first();
}
