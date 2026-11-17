# Pesquisa: Portal de documentação do Dokpod

Data da verificação: 2026-11-17. Versões e licenças consultadas no registry do npm,
no MCR e no código-fonte das tags citadas.

## D1. Plataforma e versões

- **Decisão**: Docusaurus 3.10.2 (`@docusaurus/core` e `@docusaurus/preset-classic`, MIT,
  `node >=20`, React `^18 || ^19`) com React/React DOM 19.3.1 (MIT). Versões exatas
  em `docs/portal/package.json` e lockfile commitado.
- **Racional**: versionamento de docs (`docs:version`), i18n por locale e tema
  claro/escuro nativos; a saída é estática.
- **Alternativas rejeitadas**: MkDocs Material (exige Python no build e fica fora
  do plano aprovado); Hugo (versionamento de docs manual); VitePress (sem
  versionamento nativo).

## D2. Ambiente de build e teste

- **Decisão**: build, verificação de traduções e montagem do artefato na imagem
  `lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1`, que já está no CI e no
  ambiente de trabalho. Os testes rodam em `mcr.microsoft.com/playwright:v1.65.1-noble`,
  em job separado, sobre o artefato baixado.
- **Racional**: a imagem Playwright 1.65.1 traz Node 26 (`NODE_VERSION=26` no
  Dockerfile da tag) sem minor fixado. O build reprodutível fica em Node 24.22.1
  exato, alinhado a `frontend/web`. Os testes só precisam servir arquivos estáticos.
- **Alternativa rejeitada**: `node:24.22.1-bookworm-slim`, porque seria uma imagem
  nova sem ganho real.

## D3. Rotas, versões e preview

- **Decisão**: modo docs-only (`routeBasePath: '/'`).
  - `versions.json` com `["1.1", "1.0"]` e `lastVersion: '1.1'` na raiz.
  - A linha `1.0` fica em `/1.0/`.
  - `current` vira `next`, com rótulo "Preview (não publicado)", `banner: 'unreleased'`
    e `noIndex: true`.
  - Todas as linhas de fixture têm o rótulo e o banner "Demonstração" (FR-013).
- **Troca de linha**: um navbar item `custom-versionSwitch` substitui o
  `docsVersionDropdown`. Ele usa `useActiveDocContext().alternateDocVersions`.
  - Se o tópico existe na linha de destino, abre o mesmo tópico.
  - Se não existe, abre o índice da linha com `?topico-ausente=<docId>`, e o
    índice mostra o aviso (história 1, cenário 2).
- **Alternativa rejeitada**: o dropdown nativo, que cai em silêncio no documento
  principal da versão.

## D4. Idiomas e autodetecção

- **Decisão**: `i18n.defaultLocale: 'pt-BR'` na raiz, com `locales: ['pt-BR','en','es']`
  e `localeConfigs` com `htmlLang` e rótulos nativos.
- **Autodetecção**: o script `static/js/locale-entry.js` é carregado por
  `scripts` no `<head>` (arquivo de mesma origem, compatível com CSP). Ele age
  apenas quando:
  1. `location.pathname` é exatamente a `baseUrl`;
  2. não há query `locale`;
  3. não há escolha manual válida salva.

  Ele compara `navigator.languages` em ordem pelo prefixo (`pt`, `en`, `es`) e,
  para EN ou ES, faz `location.replace` para `/en/` ou `/es/`. Um idioma não
  suportado mantém PT.
- **Escolha manual**: um wrapper de `NavbarItem/LocaleDropdownNavbarItem` usa
  `onClickCapture` e lê o atributo `lang` dos links do dropdown (que o componente
  3.10.2 emite). Ele grava `dokpod.docs.locale` antes da navegação de página
  inteira.
- **Racional**: precedência da spec (URL explícita > escolha manual > navegador >
  PT) sem redirecionar links profundos.
- **Limitação aceita**: sem JavaScript não há autodetecção; o seletor de idiomas
  continua acessível (FR-008).

## D5. Tradução ausente

- **Problema**: o Docusaurus mostra o texto do locale padrão quando falta a
  tradução, o que viola FR-006 e SC-004.
- **Decisão**:
  - O plugin local `plugins/translation-status` compara, por linha, os arquivos de
    `versioned_docs`/`docs` com `i18n/<loc>/docusaurus-plugin-content-docs/<versão>/`.
    Ele publica o mapa por `setGlobalData`.
  - Um wrapper de `DocItem/Content` troca o corpo por um aviso com link para o
    mesmo tópico em PT quando o tópico não tem tradução. A página recebe
    `noindex` para não ser indexada como traduzida.
  - `translation-policy.json` lista os tópicos obrigatórios por idioma.
    `scripts/check-translations.mjs` falha o build quando falta um deles.
- **Fixture**: o tópico `recursos-avancados` existe só em PT na linha `1.1`, para
  exercitar o aviso.

## D6. Tema Claro/Escuro/Sistema

- **Achado**: o `ColorModeToggle` 3.10.2 só tem três estados quando
  `respectPrefersColorScheme: true`. Nesse modo, a primeira visita segue o sistema
  operacional, enquanto a landing Altivy abre no escuro (`altivy.theme`, padrão
  `dark`). O script de cor do Docusaurus vai em `preBodyTags`, depois do `<head>`.
- **Decisão**: manter o contrato da landing.
  - `colorMode: { defaultMode: 'dark', respectPrefersColorScheme: true }`.
  - O script `static/js/theme-init.js`, no `<head>`, lê `altivy.docs.theme`
    (`dark`, `light` ou `system`; ausente ou inválido vale `dark`). Ele sincroniza
    a chave `theme` do Docusaurus: grava `dark`/`light` ou remove a chave para
    `system`. O script de cor nativo então aplica o tema sem flash.
  - O `ColorModeToggle` é substituído (eject) por um menu de três opções, rotulado
    e acessível, no padrão do `theme-menu` da landing. Ele grava `altivy.docs.theme`
    e chama `setColorMode('dark' | 'light' | null)`.
  - Com o armazenamento bloqueado, as funções capturam a exceção e o tema escuro
    é aplicado.
- **Alternativas rejeitadas**:
  - O toggle nativo com padrão Sistema diverge do dark-first.
  - Um script inline próprio exigiria mais um hash na CSP.

## D7. Identidade visual

- **Decisão**: snapshot em `docs/portal/src/brand/`, copiado de Altivy `main`
  (`55dfa31` na data da pesquisa):
  - `tokens/tokens.css`;
  - `fonts/Outfit-Variable-Latin.woff2` e `fonts/LICENSE-Outfit-1.1.txt`;
  - `symbol-dark.svg`, `symbol-light.svg` e `icon-32.png` (favicon);
  - `SHA256SUMS`;
  - `SOURCE.md`, com o commit de origem e o procedimento de atualização.
- **Mapeamento**: o `tokens.css` Altivy já usa `:root` (escuro) e
  `:root[data-theme="light"]`, o mesmo atributo que o Docusaurus aplica em `<html>`.
  `src/css/custom.css` mapeia `--ifm-*` para os tokens nos dois temas e define a
  Outfit (400/500/600) como única família. Também aplica `teal-light` como único
  acento (`--ifm-color-primary`), vidro só em navbar e menus, e nenhum matiz roxo.
- **Validação**:
  - Teste de contraste AA por axe em ambos os temas.
  - `check-artifact` falha se encontrar URL de fonte externa.
  - O teste de integridade compara o `SHA256SUMS` do snapshot.
- **Licença**: Outfit sob SIL OFL 1.1, distribuída com a licença.

## D8. Busca local

- **Decisão (revisada na implementação)**: Pagefind 1.6.0 (MIT), executado depois
  do build (`pagefind --site build`). O índice é separado por `<html lang>` e o
  conteúdo indexado fica no wrapper de `DocItem/Content`, com
  `data-pagefind-filter="version:<linha>"`; o campo da navbar
  (`src/components/SearchBar`) filtra pela linha aberta. As consultas não saem
  do navegador.
  - Motivo da troca: `@easyops-cn/docusaurus-search-local` puxa
    `lunr-languages` (MPL-1.1) para pt/es, licença fora da política do repositório.
  - O Babel do bundle de servidor converte `import()` em `require`; por isso o
    `pagefind.js` é carregado por `static/js/search-loader.js`, que expõe
    `window.dokpodLoadPagefind`.
  - O WebAssembly do Pagefind exige `'wasm-unsafe-eval'` em `script-src` (D9).
- **Validação**: `tests/e2e/busca.spec.mjs` confirma que uma consulta na linha
  `1.0` não traz tópicos da `1.1`, que `/en/` traz páginas em inglês e que
  nenhuma requisição sai da origem.
- **Alternativa rejeitada**: Algolia DocSearch, porque é um serviço externo
  (FR-010).

## D9. CSP e artefato

- **Decisão**: o servidor de teste aplica `default-src 'self'; script-src 'self' <hashes>;
  style-src 'self'; font-src 'self'; img-src 'self' data:; connect-src 'self';
  base-uri 'self'; frame-ancestors 'none'; object-src 'none'`.
  - `scripts/build-manifest.mjs` calcula o SHA-256 de cada script inline das
    páginas geradas e grava a lista em `portal-manifest.json` (`csp.scriptHashes`).
  - O teste e2e falha em qualquer `securitypolicyviolation`.
- **Revisão na implementação**:
  - `script-src` recebe também `'wasm-unsafe-eval'`, exigido pelo Pagefind. Não
    libera `eval` de JavaScript; **desvio a confirmar na revisão humana**.
  - O único script inline executável é o de cor do Docusaurus (um hash).
    Blocos `application/ld+json` são dados e ficam fora de `script-src`.
  - Fontes de `style=""` removidas sem `unsafe-hashes`: tema Prism vazio, mais
    o eject de `CodeBlock/Line/Token` (cores por classe `.token.*`),
    `baseUrlIssueBanner: false` e o plugin `plugins/strict-csp`, que reescreve
    o sprite SVG do tema clássico para uma classe e reprova o build se sobrar
    algum `style=` no HTML.
  - `style` aplicado via CSSOM em runtime (ex.: `Collapsible`) não é afetado
    por `style-src`.
- **Artefato**: `build/` e `SHA256SUMS`, mais `portal-manifest.json` (commit,
  versões, locales, hashes CSP e versão do Docusaurus). Sem source maps,
  `node_modules`, `.docusaurus/`, fontes MD ou relatórios de teste. O upload usa o
  mesmo `actions/upload-artifact` fixado por SHA no CI atual, com retenção de 14 dias.

## D10. CI

- **Decisão**: o job `docs-portal` faz `npm ci --no-audit --no-fund`,
  `check:translations`, `build`, `build-manifest`, `check:artifact` e o upload
  `docs-portal-site`. O job `docs-portal-e2e` (`needs: docs-portal`) baixa o
  artefato, roda `npm ci` em `tests` e executa Playwright e axe, publicando os
  relatórios em caso de falha.
  - Ambos com `permissions: contents: read`, `persist-credentials: false` e
    `timeout-minutes: 20`.
  - Ambos entram no `needs` de `result`.
- **Racional**: o CI atual não tem filtro de caminhos. Um filtro exigiria nova
  action, e o custo estimado (de 3 a 5 min) é aceitável.
- **Fora do escopo**: CD, ambiente protegido, DNS, TLS e publicação, que ficam
  para uma feature posterior (ver o plano transversal da Altivy).

## D11. Dependências de teste

- `@playwright/test` 1.65.1 (Apache-2.0), igual à tag da imagem.
- `axe-core` 4.14.0 (MPL-2.0), injetado por `page.addScriptTag`. É o mesmo pacote e
  a mesma versão de `frontend/web`. Não se adiciona `@axe-core/playwright`, para
  não criar dependência nova.
- O servidor estático de teste é um script Node sem dependências.
