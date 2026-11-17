---
description: "Lista de tarefas da feature 034 — Portal de documentação do Dokpod"
---

# Tarefas: Portal de documentação do Dokpod

**Entrada**: artefatos de `/specs/034-portal-documentacao/` (plan, spec, research,
data-model, contracts, quickstart)

**Testes**: incluídos, porque a spec exige testes independentes por história e
critérios SC-001 a SC-007 verificáveis.

**Organização**: tarefas agrupadas por história. Todos os caminhos são relativos
à raiz do repositório. Todo comando roda em container conforme
[quickstart.md](./quickstart.md).

## Formato: `[ID] [P?] [História] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente).
- **[USn]**: história de usuário da spec.

---

## Fase 1: Preparação

**Objetivo**: projeto npm isolado com versões fixadas.

- [x] T001 Criar `docs/portal/package.json`.
  - Campos: `"private": true` (sem `"type": "module"`; ver desvios no plan.md), `engines.node: ">=24.22.1 <25"`
    e `packageManager: "npm@12.2.1"`.
  - Dependências exatas: `@docusaurus/core` 3.10.2, `@docusaurus/preset-classic`
    3.10.2, `pagefind` 1.6.0 (substitui `@easyops-cn/docusaurus-search-local`, research D8), `react` e `react-dom` 19.3.1.
  - `devDependencies`: `@playwright/test` 1.65.1 e `axe-core` 4.14.0.
  - Scripts: `build`, `serve:test`, `check:translations`, `build:manifest`,
    `check:artifact` e `test:e2e`.
- [x] T002 Gerar `docs/portal/package-lock.json` no container
  `lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1` (`npm install --package-lock-only`).
  Conferir que não há `^`/`~` nas dependências diretas e que as licenças
  transitivas são permissivas (`npm query` ou leitura do lockfile).
  Registrar exceções no PR.
- [x] T003 [P] Criar `docs/portal/.gitignore` com `node_modules/`, `.docusaurus/`,
  `build/`, `artifacts/`, `test-results/` e `playwright-report/`.
- [x] T004 [P] Criar `docs/portal/README.md` em pt-BR com objetivo, comandos do
  quickstart, atualização do snapshot da marca e a regra de que nenhum deploy
  sai deste diretório.

---

## Fase 2: Fundação (bloqueante)

**Objetivo**: site configurado com marca, linhas e idiomas; build estrito
funcionando.

- [x] T005 Criar o snapshot da marca em `docs/portal/src/brand/`, copiando de Altivy `main`:
  - `docs/brand/tokens/tokens.css` → `tokens/tokens.css`;
  - `docs/brand/fonts/Outfit-Variable-Latin.woff2` e `LICENSE-Outfit-1.1.txt` → `fonts/`;
  - `docs/brand/assets/dist/symbol-dark.svg`, `symbol-light.svg` e `icon-32.png`.

  Gerar `docs/portal/src/brand/SHA256SUMS` e `SOURCE.md`, com o commit de origem
  e os comandos de atualização. Conferir os hashes contra os `SHA256SUMS` da Altivy.
- [x] T006 Criar `docs/portal/docusaurus.config.js` com:
  - `url` provisório `https://docs.dokpod.altivy.it` e `baseUrl: '/'`;
  - `onBrokenLinks`, `onBrokenMarkdownLinks` e `onBrokenAnchors` em `'throw'`;
  - `i18n` (`pt-BR` padrão, `en`, `es`, `localeConfigs`);
  - preset classic em modo docs-only (`routeBasePath: '/'`, `blog: false`);
  - `lastVersion: '1.1'` e `versions` com rótulos, `banner` e `noIndex` conforme
    [data-model.md](./data-model.md);
  - `colorMode: { defaultMode: 'dark', respectPrefersColorScheme: true }`;
  - `scripts` com `/js/theme-init.js` e `/js/locale-entry.js`;
  - favicon local e busca Pagefind (navbar `custom-search` e
    `/js/search-loader.js`), conforme a research D8;
  - rodapé com links explícitos para `/`, `/en/` e `/es/`, renderizados no HTML
    estático (navegação sem JavaScript).
- [x] T007 [P] Criar `docs/portal/sidebars.js` (autogerado por diretório) e as
  fixtures sintéticas em pt-BR em `docs/portal/docs/`. A linha `next` tem
  `introducao.md`, `instalacao.md`, `operacao.md` e `recursos-avancados.md`, todos
  com front matter `title`, `description` e `fixture: true` e com o aviso
  "Conteúdo de demonstração".
- [x] T008 Gerar as linhas `1.0` e `1.1` com `npx docusaurus docs:version` no container.
  - Ajustar o texto para conteúdo distinto e verificável por linha (por exemplo, o
    marcador "Linha 1.0" em cada tópico).
  - Remover `recursos-avancados.md` da `1.0`, para que o tópico só exista na `1.1`.
  - Confirmar `docs/portal/versions.json` igual a `["1.1","1.0"]`.
- [x] T009 [P] Criar `docs/portal/src/css/custom.css`:
  - importar `../brand/tokens/tokens.css`;
  - `@font-face` Outfit local (400–600);
  - mapear `--ifm-*` (cores, tipografia, navbar, código, links) para os tokens Altivy
    em `:root` e `[data-theme='light']`;
  - aplicar o acento único `teal-light` e o vidro só em navbar e dropdowns;
  - não usar hex soltos fora do mapeamento.
- [x] T010 [P] Gerar o esqueleto de traduções do tema com
  `npx docusaurus write-translations --locale en` e `--locale es`. Traduzir
  `docs/portal/i18n/{en,es}/code.json`, `docusaurus-theme-classic/navbar.json`,
  `footer.json` e `docusaurus-plugin-content-docs/version-*.json` (rótulos de
  linha e banners).
- [x] T011 Criar `docs/portal/translation-policy.json`: todos os tópicos são
  obrigatórios em EN e ES nas linhas `1.0` e `1.1`, exceto
  `1.1/recursos-avancados`, marcado como opcional. Implementar
  `docs/portal/scripts/check-translations.mjs`, sem dependências, que:
  - valida o front matter obrigatório e `fixture: true`;
  - falha com a lista `linha/locale/docId` quando falta uma tradução obrigatória;
  - termina com código diferente de zero e mensagem acionável.
- [x] T012 Rodar o passo 1 do quickstart (sem `build:manifest`/`check:artifact`)
  e confirmar o build dos três locales sem avisos de link quebrado.

**Checkpoint**: o site compila em pt-BR, en e es com a marca aplicada.

---

## Fase 3: História 1 — Consultar o manual da versão correta (P1) 🎯 MVP

**Objetivo**: navegar entre linhas sem fallback silencioso.

**Teste independente**: abrir `/` e `/1.0/`, comparar os marcadores de linha,
recarregar um link profundo e trocar para uma linha sem o tópico.

### Testes

- [x] T013 [P] [US1] Criar `docs/portal/tests/serve.mjs`.
  - Servidor HTTP estático sem dependências, com raiz em `SITE_DIR`.
  - Resolve `rota/` → `rota/index.html` e devolve `404.html`.
  - Aplica a CSP da research D9, lendo os hashes de `portal-manifest.json`
    quando ele existe, e os cabeçalhos `X-Content-Type-Options: nosniff` e
    `Referrer-Policy`.
- [x] T014 [P] [US1] Criar `docs/portal/tests/playwright.config.mjs`.
  - `webServer` = `node tests/serve.mjs` e `use.baseURL`.
  - Projetos para 320, 768 e 1280 px em Chromium.
  - `reporter` list + html em `playwright-report/`, `retries: 0`.
- [x] T015 [US1] Criar `docs/portal/tests/e2e/versoes.spec.mjs` cobrindo SC-001:
  - marcador correto em `/instalacao` e `/1.0/instalacao`;
  - reload mantendo a linha;
  - troca `1.1 → 1.0` em `instalacao`, preservando o `docId`;
  - troca `1.1 → 1.0` em `recursos-avancados`, que chega a
    `/1.0/?topico-ausente=recursos-avancados` com aviso visível e sem conteúdo da `1.1`;
  - `next` com banner de preview e `<meta name="robots" content="noindex">`.

### Implementação

- [x] T016 [US1] Criar `docs/portal/src/theme/NavbarItem/VersionSwitch.js`.
  - Usa `useVersions`, `useActiveDocContext` e `useDocsPreferredVersion` de
    `@docusaurus/plugin-content-docs/client`.
  - Para cada versão, o link aponta para o mesmo `docId` quando ele existe; senão,
    para `mainDoc` com `?topico-ausente=<docId>`.
  - Acessível por teclado, com `aria-current` na linha ativa.
- [x] T017 [US1] Criar `docs/portal/src/theme/NavbarItem/ComponentTypes.js`
  registrando `custom-versionSwitch`. Trocar `docsVersionDropdown` por ele no
  `navbar.items` de `docs/portal/docusaurus.config.js`.
- [x] T018 [US1] Criar o componente `docs/portal/src/components/TopicoAusente.js`.
  Ele lê `topico-ausente` da query (`useLocation`) e mostra um aviso
  `role="status"` traduzido via `<Translate>`. Inserir o componente no índice de
  cada linha (`introducao.md` como `slug: /`, via MDX) e adicionar as chaves em
  `i18n/{en,es}/code.json`.
- [x] T019 [US1] Aplicar o rótulo "Demonstração" e o banner de fixture nas linhas
  `1.0` e `1.1` (`versions.<id>.label` e `banner`) e confirmar o FR-013 no
  T015. Rodar o quickstart e o `versoes.spec.mjs` até passar.

**Checkpoint**: MVP navegável por linha, testado de forma independente.

---

## Fase 4: História 2 — Ler no idioma e tema preferidos (P1)

**Objetivo**: autodetecção só na entrada neutra, tradução ausente explícita e
tema Claro/Escuro/Sistema.

**Teste independente**: variar `locale` do contexto Playwright e
`emulateMedia`, alternar idioma e tema e verificar que linha e tópico se mantêm.

### Testes

- [x] T020 [P] [US2] Criar `docs/portal/tests/e2e/idioma.spec.mjs` cobrindo SC-002 e SC-004:
  - `/` com `pt-BR`, `en-US`, `es-MX` e `fr-FR`;
  - links profundos `/1.0/instalacao` e `/en/instalacao` sem redirecionamento;
  - a escolha manual salva prevalece na entrada neutra;
  - a troca de idioma preserva linha e tópico;
  - `/en/recursos-avancados` mostra o aviso de tradução ausente, o link para
    `/recursos-avancados` e `noindex`, sem o texto em PT;
  - `localStorage` bloqueado (`addInitScript` que lança exceção) mantém a leitura;
  - com `javaScriptEnabled: false`, o conteúdo de `/instalacao` é legível e o
    rodapé oferece links para `/en/` e `/es/`.
- [x] T021 [P] [US2] Criar `docs/portal/tests/e2e/tema.spec.mjs` cobrindo SC-003:
  - primeira visita em escuro;
  - escolha Claro/Escuro persistida após reload;
  - Sistema segue `emulateMedia({ colorScheme })` ao vivo;
  - em Claro ou Escuro, a mudança do sistema é ignorada;
  - um valor inválido em `altivy.docs.theme` cai no escuro;
  - armazenamento bloqueado não quebra a página;
  - o menu é operável por teclado (Tab, Enter, Esc).

### Implementação

- [x] T022 [P] [US2] Criar `docs/portal/static/js/locale-entry.js` (ES5, IIFE, sem
  dependências) conforme [contracts/rotas.md](./contracts/rotas.md):
  - age só em `pathname === '/'` sem `?locale`;
  - precedência escolha salva > `navigator.languages` > PT;
  - `location.replace`;
  - `try/catch` em todo acesso a armazenamento.
- [x] T023 [US2] Criar o wrapper `docs/portal/src/theme/NavbarItem/LocaleDropdownNavbarItem/index.js`
  (swizzle `--wrap`). Um `<span>`/`div` com `onClickCapture` lê o `lang` do
  link clicado, mapeia para `pt-BR`/`en`/`es` e grava `dokpod.docs.locale` com
  `try/catch`.
- [x] T024 [P] [US2] Criar `docs/portal/plugins/translation-status/index.js`:
  - percorre `docs/`, `versioned_docs/` e `i18n/<loc>/docusaurus-plugin-content-docs/<versão>/`;
  - gera `{ [versao]: { [docId]: ['pt-BR', 'en', ...] } }`;
  - publica o mapa com `setGlobalData`.

  Registrar o plugin em `docs/portal/docusaurus.config.js`.
- [x] T025 [US2] Criar o wrapper `docs/portal/src/theme/DocItem/Content/index.js`.
  - Se o locale atual não está no mapa do `docId` e da linha, renderiza no lugar
    do conteúdo um aviso traduzido, com link explícito para o mesmo tópico em
    pt-BR, e `<Head><meta name="robots" content="noindex"/></Head>`.
  - Caso contrário, renderiza o original.
- [x] T026 [P] [US2] Criar `docs/portal/static/js/theme-init.js` (ES5, IIFE)
  conforme [contracts/preferencias.md](./contracts/preferencias.md). Lê
  `altivy.docs.theme` (padrão `dark`) e sincroniza a chave `theme`: grava
  `dark`/`light` ou remove para `system`.
- [x] T027 [US2] Fazer o eject de `ColorModeToggle` para
  `docs/portal/src/theme/ColorModeToggle/index.js` e `styles.module.css`, como
  menu de três opções.
  - Usa `<details>`/`<summary>` ou um botão com `aria-expanded`, mais `role="menu"`.
  - Opções com rótulo textual e ícone: Claro, Escuro, Sistema.
  - `aria-checked` na opção ativa.
  - Grava `altivy.docs.theme` e chama `setColorMode('dark' | 'light' | null)`
    de `useColorMode`.
  - Rótulos via `<Translate>`, com chaves em `i18n/{en,es}/code.json`.
- [x] T028 [US2] Completar as traduções EN e ES das fixtures obrigatórias em
  `docs/portal/i18n/{en,es}/docusaurus-plugin-content-docs/version-1.0/` e
  `version-1.1/`, sem `recursos-avancados`. Rodar `check:translations`, o build e
  os specs `idioma` e `tema` até passarem.

**Checkpoint**: histórias 1 e 2 funcionam de forma independente.

---

## Fase 5: História 3 — Manter o manual ligado ao produto (P2)

**Objetivo**: artefato íntegro e CI sem secrets nem deploy.

**Teste independente**: rodar os passos 1 e 2 do quickstart, conferir o
manifesto e forçar falhas (link quebrado e tradução obrigatória ausente).

### Testes

- [x] T029 [P] [US3] Criar `docs/portal/tests/e2e/busca.spec.mjs`, para o FR-010:
  - uma busca em `/1.0/` não retorna tópicos exclusivos da `1.1`;
  - uma busca em `/en/` retorna páginas EN;
  - nenhuma requisição sai da origem (`page.on('request')`).

  Se a filtragem por linha falhar, aplicar `searchContextByPaths` (research D8) em
  `docs/portal/docusaurus.config.js`.
- [x] T030 [P] [US3] Criar `docs/portal/tests/e2e/a11y.spec.mjs`.
  - Injeta `node_modules/axe-core/axe.min.js` com `page.addScriptTag`.
  - Roda `axe.run` com as tags `wcag2a`, `wcag2aa`, `wcag21a` e `wcag21aa` em `/`,
    `/1.0/instalacao`, `/en/` e `/es/`, nos temas escuro e claro.
  - Espera zero violações.
  - Escuta `securitypolicyviolation` em todas as páginas visitadas e falha em
    qualquer ocorrência.
  - Verifica que a família computada de `body` é Outfit.

### Implementação

- [x] T031 [US3] Criar `docs/portal/scripts/build-manifest.mjs`, que:
  - copia `build/` para `artifacts/docs-portal-site/site/`;
  - calcula o `SHA256SUMS`;
  - extrai o SHA-256 de cada `<script>` inline do HTML para `csp.scriptHashes`;
  - grava `portal-manifest.json` conforme [contracts/artefato.md](./contracts/artefato.md),
    com o commit via `GITHUB_SHA` ou `git rev-parse` e a lista de traduções
    ausentes do plugin.
- [x] T032 [US3] Criar `docs/portal/scripts/check-artifact.mjs`, que valida:
  - as extensões permitidas e os itens proibidos;
  - a ausência de URLs de terceiros em `src`/`href`/`url()`;
  - a conferência do `SHA256SUMS`;
  - o `SHA256SUMS` do snapshot de marca contra `src/brand/SHA256SUMS`.

  A saída diagnostica arquivo e regra violada.
- [x] T033 [US3] Adicionar o job `docs-portal` em `.github/workflows/ci.yml`:
  - container `lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1` com
    `--user 0`, `working-directory: docs/portal`, `permissions: contents: read`
    e `timeout-minutes: 20`;
  - checkout fixado pelo mesmo SHA, com `persist-credentials: false`;
  - passos `npm ci --no-audit --no-fund`, `check:translations`, `build`,
    `build:manifest` e `check:artifact`;
  - upload `docs-portal-site` com o `actions/upload-artifact` já fixado e
    `retention-days: 14`.
- [x] T034 [US3] Adicionar o job `docs-portal-e2e` em `.github/workflows/ci.yml`:
  - `needs: docs-portal`, com container `mcr.microsoft.com/playwright:v1.65.1-noble`
    e `options: --ipc=host`;
  - download do artefato com o `actions/download-artifact@9000827ccba6bdab643e8b6fd33ac0654aef8333`
    (v8.0.2, já usado nos workflows do repositório);
  - `npm ci` e `npm run test:e2e` com `SITE_DIR`;
  - upload de `playwright-report/` apenas em `failure()`.

  Incluir `docs-portal` e `docs-portal-e2e` no `needs` de `result`. Nenhum dos
  dois usa `secrets.*` ou `environment`.
- [x] T035 [US3] Validar o workflow com actionlint em container (`--user 0:0`) e
  conferir que nenhum `gw-*.yml` nem `.github/CI-CD.md` foi alterado.
- [x] T036 [US3] Testes negativos locais, registrados no PR e revertidos depois:
  - um link interno quebrado em `docs/portal/versioned_docs/version-1.1/` deve
    falhar o build;
  - remover uma tradução obrigatória deve falhar o `check:translations`;
  - remover `title` do front matter de uma fixture deve falhar o `check:translations`;
  - criar um `.map` em `build/` deve falhar o `check:artifact`.

**Checkpoint**: as três histórias estão completas e validadas.

---

## Fase 6: Refinamento e aspectos transversais

- [x] T037 [P] Auditoria de marca: conferir `docs/portal/src/css/custom.css` e os
  componentes ejetados contra o design system Altivy.
  - Pesos ≤ 600.
  - Nenhum matiz roxo.
  - Cor só por token.
  - Vidro só como elevação.
  - Status com texto ou ícone.

  Registrar os desvios em `docs/portal/src/brand/SOURCE.md`.
- [x] T038 [P] Atualizar `docs/portal/README.md` e acrescentar um parágrafo em
  `docs/release-integrity.md` (ou no documento de distribuição equivalente) sobre
  a existência do artefato `docs-portal-site` e o fato de ele não ser publicado
  automaticamente. Não editar `docs/plan/`.
- [x] T039 Rodar o quickstart completo (passos 1 e 2) e registrar o comando, o
  resumo da saída e o resultado de cada critério SC-001 a SC-007 na descrição do
  PR. Itens não executados ficam como `NOT RUN`.

---

## Fase 7: Primeira versão publicada

Escopo acrescentado por pedido explícito ("implementar a primeira versão do
docs.dokpod"). Revoga o item 4 da estratégia para o Dokpod.

- [x] T040 Substituir as fixtures pelo manual real a partir das tags e runbooks
  do Dokpod: linha `0.8` (6 tópicos, inclui o instalador guiado e hosts
  remotos), linha `0.7` (5 tópicos), ambas em PT/EN/ES, e `next` só em PT.
- [x] T041 Tornar `0.8` e `0.7` indexáveis, ligar o sitemap sem `/next/`, banner
  de linha anterior na `0.7` e estado `publicada` no manifesto.
- [x] T042 Reescrever os e2e para o conteúdo real e corrigir o que eles acharam
  (peso de `<strong>`, sublinhado de links, tabelas roláveis, menu mobile).
- [x] T043 Criar `.github/workflows/docs-deploy.yml` (manual, ambiente
  `docs-production`, artefato do CI verde, checagem da CSP da borda, promoção
  atômica, smoke e rollback). Validado com actionlint e simulação local da
  promoção.
- [x] T044 Vhost e CSP de `docs.dokpod.altivy.it` no repositório Altivy,
  validados com `nginx -t` e com os e2e contra o Nginx em container.
- [ ] T045 Ação humana: criar o ambiente `docs-production` com os secrets da VPS,
  publicar a borda Altivy, conferir o certificado e disparar o `docs-deploy`.

---

## Dependências e ordem

- Fase 1 → Fase 2 → (Fase 3 ∥ Fase 4) → Fase 5 → Fase 6.
- US1 e US2 dependem só da Fundação. Elas compartilham `docusaurus.config.js`;
  edite esse arquivo de forma sequencial (T017 e T024).
- US3 depende do build da Fundação. T029 e T030 precisam das UIs de US1 e US2,
  porque testam busca e temas.
- T013 e T014 são infraestrutura de teste usada por todas as histórias.
- T033 depende de T031 e T032; T034 depende de T033 e dos specs e2e.

## Paralelismo

- **Fase 1**: T003 ∥ T004.
- **Fase 2**: T007 ∥ T009 ∥ T010, depois de T006.
- **US1**: T013 ∥ T014. T016 e T018 em paralelo, antes de T017.
- **US2**: T020 ∥ T021 ∥ T022 ∥ T024 ∥ T026. Depois, T023, T025 e T027.
- **US3**: T029 ∥ T030. T031 → T032 → T033 → T034.

## Estratégia de implementação

1. **MVP**: Fases 1 e 2 mais a História 1 (T001–T019). Já demonstra o
   versionamento com a marca Altivy.
2. **Incremento 2**: a História 2 completa os requisitos aprovados de idioma e tema.
3. **Incremento 3**: a História 3 coloca o portal no CI com artefato verificável.
4. Outros produtos ficam para features futuras. O deploy do Dokpod entrou na Fase 7.
