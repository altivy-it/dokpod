// Funções compartilhadas por build-manifest e check-artifact (sem dependências).
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';

export function listFiles(dir) {
  return fs.readdirSync(dir, {withFileTypes: true, recursive: true})
    .filter((entry) => entry.isFile())
    .map((entry) => path.relative(dir, path.join(entry.parentPath ?? entry.path, entry.name)).split(path.sep).join('/'))
    .sort();
}

export function sha256(buffer, encoding = 'hex') {
  return crypto.createHash('sha256').update(buffer).digest(encoding);
}

export function sha256sums(dir) {
  return listFiles(dir).map((file) => `${sha256(fs.readFileSync(path.join(dir, file)))}  ${file}\n`).join('');
}

export function parseSha256sums(text) {
  return text.split('\n').filter(Boolean).map((line) => {
    const match = line.match(/^([0-9a-f]{64}) {2}(.+)$/);
    if (!match) {
      throw new Error(`linha inválida em SHA256SUMS: ${line}`);
    }
    return {hash: match[1], file: match[2]};
  });
}

// Conteúdo de cada <script> executável sem src, como o navegador calcula o hash da CSP.
// Blocos de dados (application/ld+json) não são executados e ficam fora de script-src.
export function inlineScripts(html) {
  return [...html.matchAll(/<script(?![^>]*\ssrc=)([^>]*)>([\s\S]*?)<\/script>/g)]
    .filter((match) => !/\stype="(?!text\/javascript|module)[^"]+"/.test(match[1]))
    .map((match) => match[2]);
}
