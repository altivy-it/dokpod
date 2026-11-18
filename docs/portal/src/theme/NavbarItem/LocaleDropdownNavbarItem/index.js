import React, {useEffect} from 'react';
import LocaleDropdownNavbarItem from '@theme-original/NavbarItem/LocaleDropdownNavbarItem';

const STORAGE_KEY = 'dokpod.docs.locale';
const LOCALE_BY_LANG = {'pt-BR': 'pt-BR', en: 'en', es: 'es'};

// Grava a escolha manual de idioma; a entrada neutra "/" passa a respeitá-la.
// Os itens do seletor são <a lang="..."> com navegação completa de página.
function rememberLocale(event) {
  const link = event.target instanceof Element ? event.target.closest('a[lang]') : null;
  const locale = link && LOCALE_BY_LANG[link.getAttribute('lang')];
  if (!locale) {
    return;
  }
  try {
    window.localStorage.setItem(STORAGE_KEY, locale);
  } catch (error) {
    // Sem armazenamento a troca de idioma segue funcionando, só não é lembrada.
  }
}

export default function LocaleDropdownNavbarItemWrapper(props) {
  useEffect(() => {
    document.addEventListener('click', rememberLocale, true);
    return () => document.removeEventListener('click', rememberLocale, true);
  }, []);
  return <LocaleDropdownNavbarItem {...props} />;
}
