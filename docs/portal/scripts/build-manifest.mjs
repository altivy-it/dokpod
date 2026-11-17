// Empacota build/ em artifacts/docs-portal-site/ com SHA256SUMS e portal-manifest.json
// (contrato em specs/034-portal-documentacao/contracts/artefato.md).
import {execFileSync} from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {collectTranslationStatus} from '../plugins/translation-status/index.mjs';
import {inlineScripts, listFiles, sha256, sha256sums} from './lib.mjs';

const siteDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const buildDir = path.join(siteDir, 'build');
const outDir = path.join(siteDir, 'artifacts', 'docs-portal-site');
const LOCALES = ['pt-BR', 'en', 'es'];

if (!fs.existsSync(path.join(buildDir, 'index.html'))) {
  console.error('build/ ausente: rode "npm run build" antes de "npm run build:manifest".');
  process.exit(1);
}

function commitSha() {
  if (process.env.GITHUB_SHA) {
    return process.env.GITHUB_SHA;
  }
  try {
    return execFileSync('git', ['-c', 'safe.directory=*', 'rev-parse', 'HEAD'], {cwd: siteDir, encoding: 'utf8'}).trim();
  } catch {
    console.error('Não foi possível obter o commit: defina GITHUB_SHA ou rode dentro do repositório git.');
    process.exit(1);
  }
}

fs.rmSync(outDir, {recursive: true, force: true});
fs.mkdirSync(outDir, {recursive: true});
fs.cpSync(buildDir, path.join(outDir, 'site'), {recursive: true});
const siteOut = path.join(outDir, 'site');
fs.writeFileSync(path.join(outDir, 'SHA256SUMS'), sha256sums(siteOut));

const scriptHashes = new Set();
for (const file of listFiles(siteOut).filter((name) => name.endsWith('.html'))) {
  for (const script of inlineScripts(fs.readFileSync(path.join(siteOut, file), 'utf8'))) {
    scriptHashes.add(`sha256-${sha256(script, 'base64')}`);
  }
}

const versions = JSON.parse(fs.readFileSync(path.join(siteDir, 'versions.json'), 'utf8'));
const status = collectTranslationStatus(siteDir, LOCALES);
const missing = [];
for (const [version, docs] of Object.entries(status)) {
  for (const [docId, locales] of Object.entries(docs)) {
    for (const locale of LOCALES.filter((item) => !locales.includes(item))) {
      missing.push({version: version === 'current' ? 'next' : version, locale, docId});
    }
  }
}

const brandSource = fs.readFileSync(path.join(siteDir, 'src/brand/SOURCE.md'), 'utf8').match(/commit `([0-9a-f]{40})`/);
const docusaurus = JSON.parse(fs.readFileSync(path.join(siteDir, 'node_modules/@docusaurus/core/package.json'), 'utf8'));

const manifest = {
  schema: 1,
  product: 'dokpod',
  commit: commitSha(),
  generator: {docusaurus: docusaurus.version, node: process.versions.node},
  locales: LOCALES,
  versions: [
    ...versions.map((id, index) => ({id, state: 'demonstracao', path: index === 0 ? '/' : `/${id}/`})),
    {id: 'next', state: 'preview', path: '/next/'},
  ],
  translations: {missing},
  brand: {
    source: `altivy@${brandSource ? brandSource[1] : 'desconhecido'}`,
    sha256sums: sha256(fs.readFileSync(path.join(siteDir, 'src/brand/SHA256SUMS'))),
  },
  csp: {scriptHashes: [...scriptHashes].sort()},
};
fs.writeFileSync(path.join(outDir, 'portal-manifest.json'), `${JSON.stringify(manifest, null, 2)}\n`);
console.log(`build:manifest ok: ${listFiles(siteOut).length} arquivos, ${scriptHashes.size} hash(es) de script inline em ${path.relative(siteDir, outDir)}`);
