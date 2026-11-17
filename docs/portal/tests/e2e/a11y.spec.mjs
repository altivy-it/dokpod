import fs from 'node:fs';
import {createRequire} from 'node:module';
import {expect, test} from './fixtures.mjs';

// Injetado via CDP (page.evaluate): addScriptTag criaria um script inline barrado pela CSP.
const axeSource = fs.readFileSync(createRequire(import.meta.url).resolve('axe-core/axe.min.js'), 'utf8');
const PAGES = ['/', '/agentes/', '/next/instalacao/', '/en/', '/es/', '/en/next/hosts-remotos/', '/?topico-ausente=hosts-remotos'];

for (const mode of ['dark', 'light']) {
  test.describe(`acessibilidade no tema ${mode}`, () => {
    for (const path of PAGES) {
      test(`axe WCAG 2.1 AA em ${path}`, async ({page}) => {
        await page.addInitScript((value) => localStorage.setItem('altivy.docs.theme', value), mode);
        await page.goto(path);
        await expect(page.locator('html')).toHaveAttribute('data-theme', mode);
        await page.waitForLoadState('networkidle');
        await page.evaluate(axeSource);
        const result = await page.evaluate(() => window.axe.run(document, {
          runOnly: {type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']},
        }));
        const summary = result.violations.map((violation) => ({
          id: violation.id,
          nodes: violation.nodes.map((node) => node.target.join(' ')).slice(0, 5),
        }));
        expect(summary).toEqual([]);
      });
    }
  });
}

test('sem overflow horizontal', async ({page}) => {
  for (const path of ['/', '/agentes/', '/next/instalacao/', '/en/next/hosts-remotos/']) {
    await page.goto(path);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow, path).toBeLessThanOrEqual(0);
  }
});
