import React from 'react';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Translate, {translate} from '@docusaurus/Translate';

const LANDING_ROOTS = {'pt-BR': '', en: '/en', es: '/es'};
const PRODUCT_ROOTS = {'pt-BR': '/produtos', en: '/en/products', es: '/es/productos'};

export default function Footer() {
  const {siteConfig, i18n: {currentLocale, locales, localeConfigs}} = useDocusaurusContext();
  const {product, productSlug, publicRepository, license, licenseUrl} = siteConfig.customFields.portal;
  const landing = `https://altivy.it${LANDING_ROOTS[currentLocale]}/`;
  const productUrl = `https://altivy.it${PRODUCT_ROOTS[currentLocale]}/${productSlug}/`;
  const github = publicRepository || 'https://github.com/altivy-it/';

  return (
    <footer className="portal-footer">
      <div className="container">
        <div className="portal-footer__grid">
          <div className="portal-footer__about">
            <a className="portal-footer__brand" href={landing} aria-label="Altivy">
              <img className="portal-footer__mark--dark" src="/img/symbol-dark.svg" width="26" height="26" alt="" />
              <img className="portal-footer__mark--light" src="/img/symbol-light.svg" width="26" height="26" alt="" />
              <span>Altivy</span>
            </a>
            <p>
              <Translate id="portal.footer.about" values={{product}}>
                {'Documentação versionada do {product}. Guias de instalação, operação e evolução por versão.'}
              </Translate>
            </p>
          </div>
          <nav aria-label={translate({id: 'portal.footer.product', message: 'Produto'})}>
            <h2 className="portal-footer__label"><Translate id="portal.footer.product">Produto</Translate></h2>
            <ul>
              <li><a href={productUrl}><Translate id="portal.footer.productPage" values={{product}}>{'Conhecer o {product}'}</Translate></a></li>
              <li><a href={github}>{publicRepository ? 'GitHub' : 'GitHub · Altivy'}</a></li>
              {publicRepository && <li><a href={`${publicRepository}/releases`}><Translate id="portal.footer.releases">Releases</Translate></a></li>}
            </ul>
          </nav>
          <nav aria-label={translate({id: 'portal.footer.company', message: 'Sobre a Altivy'})}>
            <h2 className="portal-footer__label"><Translate id="portal.footer.company">Sobre a Altivy</Translate></h2>
            <ul>
              <li><a href={landing}><Translate id="portal.footer.institutional">Site institucional</Translate></a></li>
              <li><a href="https://github.com/altivy-it/">GitHub · Altivy</a></li>
            </ul>
          </nav>
          <div>
            <h2 className="portal-footer__label"><Translate id="portal.footer.contact">Contato</Translate></h2>
            <p><Translate id="portal.footer.contactText">Converse com a Altivy sobre o seu projeto.</Translate></p>
            <a className="portal-footer__email" href="mailto:contato@altivy.it">contato@altivy.it</a>
          </div>
        </div>
        <div className="portal-footer__legal">
          <p>
            {product} · Altivy
            {license && licenseUrl && <> · <a href={licenseUrl}><Translate id="portal.footer.license">Licença</Translate> {license}</a></>}
          </p>
          <nav aria-label={translate({id: 'portal.footer.languages', message: 'Idiomas do manual'})}>
            <ul className="portal-footer__languages">
              {locales.map((locale) => (
                <li key={locale}>
                  <a href={locale === 'pt-BR' ? '/' : `/${locale}/`} lang={localeConfigs[locale].htmlLang}
                    hrefLang={localeConfigs[locale].htmlLang} aria-current={locale === currentLocale ? 'page' : undefined}>
                    {localeConfigs[locale].label}
                  </a>
                </li>
              ))}
            </ul>
          </nav>
        </div>
      </div>
    </footer>
  );
}
