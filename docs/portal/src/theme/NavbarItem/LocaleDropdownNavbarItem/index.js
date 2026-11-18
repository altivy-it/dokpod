import React, {useEffect} from 'react';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import {useAlternatePageUtils} from '@docusaurus/theme-common/internal';
import {translate} from '@docusaurus/Translate';
import {mergeSearchStrings, useHistorySelector} from '@docusaurus/theme-common';
import DropdownNavbarItem from '@theme/NavbarItem/DropdownNavbarItem';
import Icon, {MenuItemLabel} from '@site/src/components/Icon';

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

// Mesmos links do seletor nativo do Docusaurus; muda só a apresentação, que
// segue o menu de idioma da landing: globo + código ("PT") + chevron no botão,
// nomes completos na lista e check no idioma atual.
export default function LocaleDropdownNavbarItem({
  mobile,
  dropdownItemsBefore = [],
  dropdownItemsAfter = [],
  queryString,
  ...props
}) {
  const {
    siteConfig,
    i18n: {currentLocale, locales, localeConfigs},
  } = useDocusaurusContext();
  const alternatePageUtils = useAlternatePageUtils();
  const search = useHistorySelector((history) => history.location.search);
  const hash = useHistorySelector((history) => history.location.hash);

  useEffect(() => {
    document.addEventListener('click', rememberLocale, true);
    return () => document.removeEventListener('click', rememberLocale, true);
  }, []);

  const urlFor = (locale) => {
    const sameDomain = localeConfigs[locale].url === siteConfig.url;
    const base = sameDomain
      ? `pathname://${alternatePageUtils.createUrl({locale, fullyQualified: false})}`
      : alternatePageUtils.createUrl({locale, fullyQualified: true});
    return `${base}${mergeSearchStrings([search, queryString], 'append')}${hash}`;
  };

  const localeItems = locales.map((locale) => {
    const current = locale === currentLocale;
    return {
      label: <MenuItemLabel>{localeConfigs[locale].label}</MenuItemLabel>,
      lang: localeConfigs[locale].htmlLang,
      to: urlFor(locale),
      target: '_self',
      autoAddBaseUrl: false,
      'aria-current': current ? 'page' : undefined,
      // pathname:// não é NavLink, então a classe ativa vem daqui.
      className: current ? (mobile ? 'menu__link--active' : 'dropdown__link--active') : '',
    };
  });

  const currentLabel = localeConfigs[currentLocale].label;
  const label = mobile ? (
    translate({
      message: 'Languages',
      id: 'theme.navbar.mobileLanguageDropdown.label',
      description: 'The label for the mobile language switcher dropdown',
    })
  ) : (
    <>
      <Icon name="globe" />
      <span>
        {currentLocale.split('-')[0].toUpperCase()}
        <span className="visually-hidden">
          {`, ${translate({id: 'portal.locale.label', message: 'Idioma'})}: ${currentLabel}`}
        </span>
      </span>
      <Icon name="chevron" size={14} className="dropdown__chevron" />
    </>
  );

  return (
    <DropdownNavbarItem
      {...props}
      mobile={mobile}
      label={label}
      items={[...dropdownItemsBefore, ...localeItems, ...dropdownItemsAfter]}
    />
  );
}
