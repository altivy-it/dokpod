import React from 'react';
import TOCDesktop from '@theme-original/DocItem/TOC/Desktop';
import useTranslationMissing from '@site/src/hooks/useTranslationMissing';

// Sem tradução o corpo é substituído pelo aviso; o sumário em PT não se aplica.
export default function DocItemTOCDesktopWrapper(props) {
  return useTranslationMissing() ? null : <TOCDesktop {...props} />;
}
