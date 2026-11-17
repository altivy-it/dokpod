// Confere front matter das fixtures e traduções obrigatórias (translation-policy.json).
// Sem dependências; termina com código 1 e a lista do que falta.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {collectTranslationStatus} from '../plugins/translation-status/index.mjs';

const siteDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const policy = JSON.parse(fs.readFileSync(path.join(siteDir, 'translation-policy.json'), 'utf8'));
const REQUIRED_FIELDS = ['title', 'description'];
const errors = [];

function markdownFiles(dir) {
  if (!fs.existsSync(dir)) {
    return [];
  }
  return fs.readdirSync(dir, {withFileTypes: true, recursive: true})
    .filter((entry) => entry.isFile() && /\.mdx?$/.test(entry.name))
    .map((entry) => path.join(entry.parentPath ?? entry.path, entry.name));
}

function frontMatter(file) {
  const match = fs.readFileSync(file, 'utf8').match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!match) {
    return null;
  }
  const fields = {};
  for (const line of match[1].split(/\r?\n/)) {
    const pair = line.match(/^([A-Za-z_][\w-]*):\s*(.*)$/);
    if (pair) {
      fields[pair[1]] = pair[2].trim();
    }
  }
  return fields;
}

const contentDirs = ['docs', 'versioned_docs', 'i18n'].map((dir) => path.join(siteDir, dir));
for (const file of contentDirs.flatMap(markdownFiles)) {
  const relative = path.relative(siteDir, file);
  const fields = frontMatter(file);
  if (!fields) {
    errors.push(`${relative}: front matter ausente`);
    continue;
  }
  for (const field of REQUIRED_FIELDS) {
    if (!fields[field]) {
      errors.push(`${relative}: campo obrigatório "${field}" ausente no front matter`);
    }
  }
  if (fields.fixture !== 'true') {
    errors.push(`${relative}: conteúdo de demonstração precisa de "fixture: true"`);
  }
}

const status = collectTranslationStatus(siteDir, ['pt-BR', ...policy.requiredLocales]);
for (const [version, rule] of Object.entries(policy.versions)) {
  if (!status[version]) {
    errors.push(`translation-policy.json: versão "${version}" não existe no site`);
    continue;
  }
  if (rule.required !== 'all') {
    continue;
  }
  for (const [docId, locales] of Object.entries(status[version])) {
    if (rule.optional.includes(docId)) {
      continue;
    }
    for (const locale of policy.requiredLocales) {
      if (!locales.includes(locale)) {
        errors.push(`tradução obrigatória ausente: ${version}/${locale}/${docId}`);
      }
    }
  }
}

if (errors.length) {
  console.error(`check:translations falhou (${errors.length}):`);
  for (const error of errors) {
    console.error(`  - ${error}`);
  }
  console.error('Corrija o front matter ou crie a tradução em i18n/<locale>/docusaurus-plugin-content-docs/version-<versão>/.');
  process.exit(1);
}
console.log('check:translations ok');
