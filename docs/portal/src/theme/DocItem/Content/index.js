import React, {useEffect, useState} from 'react';
import Content from '@theme-original/DocItem/Content';
import Head from '@docusaurus/Head';
import Heading from '@theme/Heading';
import Translate from '@docusaurus/Translate';
import {useLocation} from '@docusaurus/router';
import {useDoc, useDocsVersion} from '@docusaurus/plugin-content-docs/client';
import useTranslationMissing from '@site/src/hooks/useTranslationMissing';

// A query só existe no navegador; ler em efeito evita divergência na hidratação.
function useMissingTopic() {
  const {search} = useLocation();
  const [topic, setTopic] = useState(null);
  useEffect(() => {
    setTopic(new URLSearchParams(search).get('topico-ausente'));
  }, [search]);
  return topic;
}

function MissingTopicNotice({topic, version}) {
  return (
    <div className="portal-notice" role="status" data-testid="topico-ausente">
      <p className="portal-notice__title">
        <Translate id="portal.missingTopic.title">Tópico indisponível nesta versão</Translate>
      </p>
      <p>
        <Translate
          id="portal.missingTopic.body"
          values={{topic: <code>{topic}</code>, version: <strong>{version}</strong>}}>
          {'O tópico {topic} não existe na versão {version}. Você está na página inicial desta versão.'}
        </Translate>
      </p>
    </div>
  );
}

function MissingTranslation({permalink}) {
  const sourcePath = permalink.replace(/^\/(en|es)\//, '/').replace(/\/?$/, '/');
  return (
    <div className="theme-doc-markdown markdown" data-testid="traducao-ausente">
      <Head>
        <meta name="robots" content="noindex, nofollow" />
      </Head>
      <header>
        <Heading as="h1">
          <Translate id="portal.missingTranslation.title">Tradução indisponível</Translate>
        </Heading>
      </header>
      <div className="portal-notice" role="status">
        <p>
          <Translate id="portal.missingTranslation.body">
            Este tópico ainda não foi traduzido para o idioma escolhido.
          </Translate>
        </p>
        <p>
          {/* Link de página inteira: a troca de idioma não passa pelo roteador. */}
          <a href={sourcePath} lang="pt-BR" hrefLang="pt-BR">
            <Translate id="portal.missingTranslation.link">Ler em português</Translate>
          </a>
        </p>
      </div>
    </div>
  );
}

export default function DocItemContentWrapper(props) {
  const {metadata} = useDoc();
  const version = useDocsVersion();
  const translationMissing = useTranslationMissing();
  const missingTopic = useMissingTopic();

  if (translationMissing) {
    return <MissingTranslation permalink={metadata.permalink} />;
  }

  return (
    <>
      {missingTopic && <MissingTopicNotice topic={missingTopic} version={version.label} />}
      <div data-pagefind-body="" data-pagefind-filter={`version:${version.version}`}>
        <Content {...props} />
      </div>
    </>
  );
}
