// Servidor estático de teste, sem dependências. Aplica a CSP da research D9 para
// que os testes e2e reprovem qualquer recurso ou estilo inline não previsto.
import fs from 'node:fs';
import http from 'node:http';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {inlineScripts, listFiles, sha256} from '../scripts/lib.mjs';

const portalDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const root = path.resolve(portalDir, process.env.SITE_DIR ?? 'build');
const port = Number(process.env.PORT ?? 4173);
const host = process.env.HOST ?? '127.0.0.1';
const TYPES = {
  '.html': 'text/html; charset=utf-8', '.css': 'text/css; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8', '.json': 'application/json', '.xml': 'application/xml',
  '.txt': 'text/plain; charset=utf-8', '.svg': 'image/svg+xml', '.png': 'image/png',
  '.ico': 'image/x-icon', '.woff2': 'font/woff2', '.webmanifest': 'application/manifest+json',
  '.pagefind': 'application/octet-stream', '.pf_meta': 'application/octet-stream',
  '.pf_index': 'application/octet-stream', '.pf_fragment': 'application/octet-stream',
  '.pf_filter': 'application/octet-stream',
};

if (!fs.existsSync(path.join(root, 'index.html'))) {
  console.error(`serve: ${root} não contém index.html; rode o build ou ajuste SITE_DIR.`);
  process.exit(1);
}

function scriptHashes() {
  const manifest = path.join(root, '..', 'portal-manifest.json');
  if (fs.existsSync(manifest)) {
    return JSON.parse(fs.readFileSync(manifest, 'utf8')).csp.scriptHashes;
  }
  const hashes = new Set();
  for (const file of listFiles(root).filter((name) => name.endsWith('.html'))) {
    for (const script of inlineScripts(fs.readFileSync(path.join(root, file), 'utf8'))) {
      hashes.add(`sha256-${sha256(script, 'base64')}`);
    }
  }
  return [...hashes].sort();
}

// 'wasm-unsafe-eval' é exigido pelo Pagefind (busca local em WebAssembly).
const csp = [
  "default-src 'self'",
  `script-src 'self' 'wasm-unsafe-eval' ${scriptHashes().map((hash) => `'${hash}'`).join(' ')}`,
  "style-src 'self'", "font-src 'self'", "img-src 'self' data:", "connect-src 'self'",
  "base-uri 'self'", "frame-ancestors 'none'", "object-src 'none'", "form-action 'self'",
].join('; ');

const headers = (type) => ({
  'Content-Type': type ?? 'application/octet-stream',
  'Content-Security-Policy': csp,
  'X-Content-Type-Options': 'nosniff',
  'Referrer-Policy': 'strict-origin-when-cross-origin',
  'Cache-Control': 'no-store',
});

function resolve(urlPath) {
  const decoded = decodeURIComponent(urlPath);
  const target = path.resolve(root, `.${decoded}`);
  if (target !== root && !target.startsWith(`${root}${path.sep}`)) {
    return null;
  }
  if (fs.existsSync(target) && fs.statSync(target).isDirectory()) {
    if (!decoded.endsWith('/')) {
      return {redirect: `${urlPath}/`};
    }
    const index = path.join(target, 'index.html');
    return fs.existsSync(index) ? {file: index} : null;
  }
  return fs.existsSync(target) ? {file: target} : null;
}

http.createServer((request, response) => {
  const url = new URL(request.url, 'http://localhost');
  let found = null;
  try {
    found = resolve(url.pathname);
  } catch {
    found = null;
  }
  if (found?.redirect) {
    response.writeHead(301, {...headers('text/plain; charset=utf-8'), Location: `${found.redirect}${url.search}`});
    response.end();
    return;
  }
  const status = found ? 200 : 404;
  const file = found?.file ?? path.join(root, '404.html');
  response.writeHead(status, headers(TYPES[path.extname(file)]));
  response.end(request.method === 'HEAD' ? undefined : fs.readFileSync(file));
}).listen(port, host, () => {
  console.log(`serve: ${path.relative(portalDir, root) || '.'} em http://${host}:${port}`);
});
