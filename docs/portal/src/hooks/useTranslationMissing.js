import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import {usePluginData} from '@docusaurus/useGlobalData';
import {useDoc, useDocsVersion} from '@docusaurus/plugin-content-docs/client';

// true quando o tópico aberto não tem texto no idioma atual (plugin translation-status).
export default function useTranslationMissing() {
  const {metadata} = useDoc();
  const {version} = useDocsVersion();
  const {i18n} = useDocusaurusContext();
  const status = usePluginData('translation-status');
  const available = status?.[version]?.[metadata.id] ?? ['pt-BR'];
  return !available.includes(i18n.currentLocale);
}
