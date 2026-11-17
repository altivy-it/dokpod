// Portal de documentação do Dokpod (feature 034). Saída estática, sem Node em runtime.
import altivyPrismTheme from './src/prism/altivy.js';

const locales = ['pt-BR', 'en', 'es'];

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Dokpod',
  tagline: 'Manual versionado do Dokpod',
  favicon: 'img/icon-32.png',
  url: 'https://docs.dokpod.altivy.it',
  baseUrl: '/',
  trailingSlash: true,
  baseUrlIssueBanner: false,
  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',
  markdown: {
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },
  staticDirectories: ['static', 'src/brand/public'],
  i18n: {
    defaultLocale: 'pt-BR',
    locales,
    localeConfigs: {
      'pt-BR': {label: 'Português', htmlLang: 'pt-BR'},
      en: {label: 'English', htmlLang: 'en'},
      es: {label: 'Español', htmlLang: 'es'},
    },
  },
  // Carregados no <head>, antes do script de cor do Docusaurus (início do <body>).
  scripts: [
    {src: '/js/theme-init.js'},
    {src: '/js/locale-entry.js'},
    {src: '/js/search-loader.js', defer: true},
  ],
  plugins: ['./plugins/translation-status/index.mjs', './plugins/strict-csp/index.mjs'],
  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        blog: false,
        pages: false,
        docs: {
          routeBasePath: '/',
          sidebarPath: './sidebars.js',
          lastVersion: '0.7',
          includeCurrentVersion: true,
          versions: {
            current: {label: 'Preview (não publicado)', path: 'next', banner: 'unreleased', noIndex: true},
            '0.7': {label: '0.7 (prévia)', banner: 'none'},
          },
        },
        theme: {
          customCss: './src/css/custom.css',
        },
        sitemap: {ignorePatterns: ['/next/**', '/en/next/**', '/es/next/**']},
      }),
    ],
  ],
  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      colorMode: {
        defaultMode: 'dark',
        disableSwitch: false,
        respectPrefersColorScheme: true,
      },
      navbar: {
        title: 'Dokpod',
        logo: {
          alt: 'Dokpod',
          src: 'img/symbol-light.svg',
          srcDark: 'img/symbol-dark.svg',
          width: 28,
          height: 28,
        },
        items: [
          {type: 'docSidebar', sidebarId: 'manual', label: 'Manual', position: 'left'},
          {type: 'custom-search', position: 'right'},
          {type: 'custom-versionSwitch', position: 'right'},
          {type: 'localeDropdown', position: 'right'},
        ],
      },
      footer: {
        links: [
          {
            title: 'Idiomas',
            // pathname:// gera link estático fora do roteador, navegável sem JavaScript.
            items: [
              {label: 'Português', href: 'pathname:///'},
              {label: 'English', href: 'pathname:///en/'},
              {label: 'Español', href: 'pathname:///es/'},
            ],
          },
        ],
        copyright: 'Dokpod · Altivy · Licença AGPL-3.0-only',
      },
      prism: {
        theme: altivyPrismTheme,
        darkTheme: altivyPrismTheme,
        additionalLanguages: ['bash', 'powershell', 'yaml'],
      },
    }),
};

export default config;
