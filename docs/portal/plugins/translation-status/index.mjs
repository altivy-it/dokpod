// Mapeia, por linha e tópico, os idiomas que têm o texto. O wrapper de
// DocItem/Content usa o mapa para mostrar o aviso de tradução ausente.
import fs from 'node:fs';
import path from 'node:path';

const DEFAULT_LOCALE = 'pt-BR';

function listDocIds(dir) {
  if (!fs.existsSync(dir)) {
    return [];
  }
  const ids = [];
  const walk = (current) => {
    for (const entry of fs.readdirSync(current, {withFileTypes: true})) {
      const full = path.join(current, entry.name);
      if (entry.isDirectory()) {
        walk(full);
      } else if (/\.mdx?$/.test(entry.name)) {
        ids.push(path.relative(dir, full).replace(/\.mdx?$/, '').split(path.sep).join('/'));
      }
    }
  };
  walk(dir);
  return ids.sort();
}

function readVersionNames(siteDir) {
  const file = path.join(siteDir, 'versions.json');
  const versioned = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : [];
  return ['current', ...versioned];
}

export function collectTranslationStatus(siteDir, locales) {
  const status = {};
  for (const version of readVersionNames(siteDir)) {
    const sourceDir = version === 'current'
      ? path.join(siteDir, 'docs')
      : path.join(siteDir, 'versioned_docs', `version-${version}`);
    const i18nFolder = version === 'current' ? 'current' : `version-${version}`;
    status[version] = {};
    for (const docId of listDocIds(sourceDir)) {
      status[version][docId] = [DEFAULT_LOCALE];
    }
    for (const locale of locales.filter((item) => item !== DEFAULT_LOCALE)) {
      const translated = listDocIds(
        path.join(siteDir, 'i18n', locale, 'docusaurus-plugin-content-docs', i18nFolder),
      );
      for (const docId of translated) {
        if (status[version][docId]) {
          status[version][docId].push(locale);
        }
      }
    }
  }
  return status;
}

export default function translationStatusPlugin(context) {
  return {
    name: 'translation-status',
    async contentLoaded({actions}) {
      actions.setGlobalData(collectTranslationStatus(context.siteDir, context.i18n.locales));
    },
  };
}
