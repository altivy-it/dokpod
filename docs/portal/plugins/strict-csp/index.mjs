// Mantém o HTML compatível com style-src 'self': o tema clássico injeta o sprite
// de ícones como <svg style="display: none;"> fora da raiz React. Aqui ele passa
// a usar a classe .portal-svg-sprite (custom.css). Qualquer outro style="" no
// HTML final interrompe o build.
import fs from 'node:fs/promises';
import path from 'node:path';

const SPRITE_FROM = '<svg style="display: none;">';
const SPRITE_TO = '<svg class="portal-svg-sprite" aria-hidden="true" focusable="false">';

async function htmlFiles(dir) {
  const entries = await fs.readdir(dir, {withFileTypes: true, recursive: true});
  return entries
    .filter((entry) => entry.isFile() && entry.name.endsWith('.html'))
    .map((entry) => path.join(entry.parentPath ?? entry.path, entry.name));
}

export default function strictCspPlugin() {
  return {
    name: 'strict-csp',
    async postBuild({outDir}) {
      const offenders = [];
      for (const file of await htmlFiles(outDir)) {
        const html = (await fs.readFile(file, 'utf8')).replace(SPRITE_FROM, SPRITE_TO);
        await fs.writeFile(file, html);
        if (/<[a-zA-Z][^>]*\sstyle=/.test(html.replace(/<script\b[\s\S]*?<\/script>/g, ''))) {
          offenders.push(path.relative(outDir, file));
        }
      }
      if (offenders.length) {
        throw new Error(`style="" inline quebra a CSP (style-src 'self') em: ${offenders.join(', ')}`);
      }
    },
  };
}
