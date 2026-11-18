// Portal de documentação do Dokpod (feature 034). Saída estática, sem Node em runtime.
import altivyPrismTheme from './src/prism/altivy.js';

const locales = ['pt-BR', 'en', 'es'];

// Mesmos favicons da landing Altivy (src/layouts/partials/head.html): PNG neutro
// como fallback e versões por esquema de cor do navegador.
const icon = (href, attributes = {}) => ({tagName: 'link', attributes: {rel: 'icon', type: 'image/png', href, ...attributes}});
const faviconTags = [
  icon('/img/icon-192.png', {sizes: '192x192'}),
  icon('/img/icon-32.png', {sizes: '32x32', media: '(prefers-color-scheme: light)'}),
  icon('/img/icon-16.png', {sizes: '16x16', media: '(prefers-color-scheme: light)'}),
  icon('/img/icon-dark-48.png', {sizes: '48x48', media: '(prefers-color-scheme: dark)'}),
  icon('/img/icon-dark-32.png', {sizes: '32x32', media: '(prefers-color-scheme: dark)'}),
  icon('/img/icon-dark-16.png', {sizes: '16x16', media: '(prefers-color-scheme: dark)'}),
  {tagName: 'link', attributes: {rel: 'apple-touch-icon', sizes: '192x192', href: '/img/icon-192.png'}},
];

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Dokpod',
  tagline: 'Manual versionado do Dokpod',
  favicon: 'img/icon-48.png',
  headTags: faviconTags,
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
          lastVersion: '0.8',
          includeCurrentVersion: true,
          versions: {
            current: {label: 'Preview (não publicado)', path: 'next', banner: 'unreleased', noIndex: true},
            '0.8': {label: '0.8 (prévia)', banner: 'none'},
            '0.7': {label: '0.7 (prévia)', banner: 'unmaintained'},
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
