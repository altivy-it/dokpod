import {expect, test} from './fixtures.mjs';

// Auditoria de marca em runtime (T037): pesos ≤ 600, Outfit em todo texto que não
// é código e nenhuma cor computada com matiz roxo/lavanda (OKLCH 275°–330°).
const PAGES = ['/', '/instalacao/', '/1.0/?topico-ausente=recursos-avancados', '/en/recursos-avancados/'];

for (const mode of ['dark', 'light']) {
  for (const path of PAGES) {
    test(`marca Altivy em ${path} (${mode})`, async ({page}) => {
      await page.addInitScript((value) => localStorage.setItem('altivy.docs.theme', value), mode);
      await page.goto(path);
      await expect(page.locator('html')).toHaveAttribute('data-theme', mode);
      const problems = await page.evaluate(() => {
        const found = new Set();
        const hue = (color) => {
          const match = color.match(/^rgba?\(([\d.]+),\s*([\d.]+),\s*([\d.]+)(?:,\s*([\d.]+))?\)$/);
          if (!match) return color === 'transparent' ? null : -1;
          const [r, g, b, a = 1] = match.slice(1).map(Number);
          if (a === 0) return null;
          const lin = (c) => ((c /= 255) <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4);
          const [lr, lg, lb] = [lin(r), lin(g), lin(b)];
          const l = Math.cbrt(0.4122214708 * lr + 0.5363325363 * lg + 0.0514459929 * lb);
          const m = Math.cbrt(0.2119034982 * lr + 0.6806995451 * lg + 0.1073969566 * lb);
          const s = Math.cbrt(0.0883024619 * lr + 0.2817188376 * lg + 0.6299787005 * lb);
          const A = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s;
          const B = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s;
          return Math.hypot(A, B) < 0.03 ? null : ((Math.atan2(B, A) * 180) / Math.PI + 360) % 360;
        };
        for (const element of document.querySelectorAll('body *')) {
          const style = getComputedStyle(element);
          if (style.display === 'none' || style.visibility === 'hidden') continue;
          const hasText = [...element.childNodes].some((node) => node.nodeType === 3 && node.textContent.trim());
          const isCode = element.closest('code, pre, kbd, samp');
          if (hasText && Number(style.fontWeight) > 600) found.add(`peso ${style.fontWeight} em <${element.localName} class="${element.className}">`);
          if (hasText && !isCode && !/Outfit/.test(style.fontFamily)) found.add(`fonte ${style.fontFamily} em <${element.localName}>`);
          for (const property of ['color', 'backgroundColor', 'borderTopColor', 'outlineColor']) {
            const value = hue(style[property]);
            if (value === -1) found.add(`formato de cor não verificado ${style[property]}`);
            else if (value !== null && value >= 275 && value <= 330) found.add(`matiz roxo ${style[property]} em ${property} de <${element.localName}>`);
          }
        }
        return [...found];
      });
      expect(problems).toEqual([]);
    });
  }
}
