import React from 'react';
import TOCMobile from '@theme-original/DocItem/TOC/Mobile';
import useTranslationMissing from '@site/src/hooks/useTranslationMissing';

// Sem tradução o corpo é substituído pelo aviso; o sumário em PT não se aplica.
export default function DocItemTOCMobileWrapper(props) {
  return useTranslationMissing() ? null : <TOCMobile {...props} />;
}
