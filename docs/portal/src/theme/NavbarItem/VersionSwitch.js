import React from 'react';
import {
  useActiveDocContext,
  useDocsPreferredVersion,
  useDocsVersionCandidates,
  useVersions,
} from '@docusaurus/plugin-content-docs/client';
import {translate} from '@docusaurus/Translate';
import DropdownNavbarItem from '@theme/NavbarItem/DropdownNavbarItem';

// Igual ao seletor nativo, exceto quando o tópico não existe na linha de
// destino: em vez de cair na página inicial em silêncio, a URL leva
// ?topico-ausente=<docId> e a página mostra o aviso.
function targetFor(version, activeDocContext) {
  const sameDoc = activeDocContext.alternateDocVersions[version.name];
  if (sameDoc) {
    return sameDoc.path;
  }
  const mainDoc = version.docs.find((doc) => doc.id === version.mainDocId);
  const missing = activeDocContext.activeDoc?.id;
  return missing ? `${mainDoc.path}?topico-ausente=${encodeURIComponent(missing)}` : mainDoc.path;
}

export default function VersionSwitch({mobile, docsPluginId, onClick, ...props}) {
  const versions = useVersions(docsPluginId);
  const activeDocContext = useActiveDocContext(docsPluginId);
  const {savePreferredVersionName} = useDocsPreferredVersion(docsPluginId);
  const candidates = useDocsVersionCandidates(docsPluginId);
  const displayed = candidates[0] ?? versions[0];

  const items = versions.map((version) => {
    const isActive = version === activeDocContext.activeVersion;
    return {
      label: version.label,
      to: targetFor(version, activeDocContext),
      isActive: () => isActive,
      'aria-current': isActive ? 'true' : undefined,
      // No mobile, o onClick recebido fecha o painel; o do item o substituiria.
      onClick: (event) => {
        savePreferredVersionName(version.name);
        onClick?.(event);
      },
    };
  });

  const label = mobile
    ? translate({id: 'portal.versionSwitch.mobileLabel', message: 'Versões do manual'})
    : displayed.label;

  return (
    <DropdownNavbarItem
      {...props}
      onClick={onClick}
      mobile={mobile}
      label={label}
      aria-label={translate(
        {id: 'portal.versionSwitch.ariaLabel', message: 'Versão do manual: {version}'},
        {version: displayed.label},
      )}
      items={items}
      isActive={() => false}
    />
  );
}
