// Valida artifacts/docs-portal-site (ou ARTIFACT_DIR) conforme contracts/artefato.md.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {listFiles, parseSha256sums, sha256} from './lib.mjs';

const siteDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const artifactDir = path.resolve(siteDir, process.env.ARTIFACT_DIR ?? 'artifacts/docs-portal-site');
const siteOut = path.join(artifactDir, 'site');
const SITE_ORIGIN = 'https://docs.dokpod.altivy.it';
const ALLOWED = new Set(['.html', '.css', '.js', '.json', '.xml', '.txt', '.svg', '.png', '.ico', '.woff2',
  '.webmanifest', '.pagefind', '.pf_meta', '.pf_index', '.pf_fragment', '.pf_filter']);
const FORBIDDEN = [/\.map$/, /\.mdx?$/, /(^|\/)node_modules\//, /(^|\/)\.docusaurus\//,
  /(^|\/)test-results\//, /(^|\/)playwright-report\//];
const errors = [];
const fail = (file, rule) => errors.push(`${file}: ${rule}`);

if (!fs.existsSync(siteOut)) {
  console.error(`check:artifact: ${path.relative(siteDir, siteOut)} ausente; rode "npm run build:manifest".`);
  process.exit(1);
}

const files = listFiles(siteOut);
for (const file of files) {
  const ext = path.extname(file);
  if (FORBIDDEN.some((pattern) => pattern.test(file))) {
    fail(file, 'item proibido no artefato');
  } else if (!ALLOWED.has(ext)) {
    fail(file, `extensão "${ext || '(nenhuma)'}" fora da lista permitida`);
  }
}

const external = (url) => /^(https?:)?\/\//i.test(url) && !url.startsWith(SITE_ORIGIN);
for (const file of files.filter((name) => name.endsWith('.html'))) {
  const html = fs.readFileSync(path.join(siteOut, file), 'utf8');
  for (const match of html.matchAll(/<(?:script|img|iframe|source|video|audio)\b[^>]*\ssrc="([^"]+)"/g)) {
    if (external(match[1])) fail(file, `src de terceiros: ${match[1]}`);
  }
  for (const match of html.matchAll(/<link\b[^>]*\shref="([^"]+)"/g)) {
    if (external(match[1])) fail(file, `<link href> de terceiros: ${match[1]}`);
  }
}
for (const file of files.filter((name) => name.endsWith('.css'))) {
  const css = fs.readFileSync(path.join(siteOut, file), 'utf8');
  for (const match of css.matchAll(/url\(\s*['"]?([^'")]+)/g)) {
    if (external(match[1])) fail(file, `url() de terceiros: ${match[1]}`);
  }
}

const sums = parseSha256sums(fs.readFileSync(path.join(artifactDir, 'SHA256SUMS'), 'utf8'));
const listed = new Set(sums.map((entry) => entry.file));
for (const {hash, file} of sums) {
  const full = path.join(siteOut, file);
  if (!fs.existsSync(full)) fail(file, 'listado no SHA256SUMS mas ausente');
  else if (sha256(fs.readFileSync(full)) !== hash) fail(file, 'SHA256SUMS não confere');
}
for (const file of files.filter((name) => !listed.has(name))) fail(file, 'ausente do SHA256SUMS');

const brandDir = path.join(siteDir, 'src/brand');
const brandSums = fs.readFileSync(path.join(brandDir, 'SHA256SUMS'));
for (const {hash, file} of parseSha256sums(brandSums.toString('utf8'))) {
  const full = path.join(brandDir, file);
  if (!fs.existsSync(full) || sha256(fs.readFileSync(full)) !== hash) {
    fail(`src/brand/${file}`, 'snapshot de marca diverge de src/brand/SHA256SUMS');
  }
  if (file.startsWith('public/') && !files.some((name) => name === file.slice('public/'.length)
      && sha256(fs.readFileSync(path.join(siteOut, name))) === hash)) {
    fail(file.slice('public/'.length), 'asset de marca ausente ou alterado no site');
  }
  if (file.endsWith('.woff2') && !files.some((name) => name.endsWith('.woff2')
      && sha256(fs.readFileSync(path.join(siteOut, name))) === hash)) {
    fail(file, 'fonte da marca ausente ou alterada no site');
  }
}

const manifest = JSON.parse(fs.readFileSync(path.join(artifactDir, 'portal-manifest.json'), 'utf8'));
if (manifest.brand?.sha256sums !== sha256(brandSums)) {
  fail('portal-manifest.json', 'brand.sha256sums não corresponde a src/brand/SHA256SUMS');
}
if (!/^[0-9a-f]{40}$/.test(manifest.commit ?? '')) fail('portal-manifest.json', 'commit ausente ou inválido');
if (!Array.isArray(manifest.csp?.scriptHashes) || manifest.csp.scriptHashes.length === 0) {
  fail('portal-manifest.json', 'csp.scriptHashes vazio');
}

if (errors.length) {
  console.error(`check:artifact falhou (${errors.length}):`);
  for (const error of errors) console.error(`  - ${error}`);
  process.exit(1);
}
console.log(`check:artifact ok: ${files.length} arquivos conferidos`);
