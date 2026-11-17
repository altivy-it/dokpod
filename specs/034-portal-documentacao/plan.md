# Plano de Implementação: Portal de documentação do Dokpod

**Branch**: `034-portal-documentacao` | **Data**: 2026-11-17 | **Spec**: [spec.md](./spec.md)

**Entrada**: Especificação da funcionalidade em `/specs/034-portal-documentacao/spec.md`

## Resumo

Criar em `docs/portal/` um site Docusaurus 3.10.2 (modo docs-only) com duas linhas
de demonstração (`1.0` e `1.1`) e uma linha `next` de preview, nos idiomas
`pt-BR` (raiz), `en` (`/en/`) e `es` (`/es/`). A identidade Altivy entra por um
snapshot versionado de tokens, fontes Outfit e símbolos, com `SHA256SUMS`, mapeado
para as variáveis `--ifm-*` nos temas escuro e claro. O tema segue o contrato da
landing: Claro, Escuro e Sistema, com escuro como padrão. Um menu de três opções
(eject de `ColorModeToggle`) e o script `theme-init.js` no `<head>` controlam a
chave `altivy.docs.theme` (research D6). A autodetecção de idioma é um script estático
carregado no `<head>` que age apenas na entrada neutra `/`. Traduções ausentes
são sinalizadas por um plugin local de status mais um wrapper de `DocItem/Content`,
em vez do fallback silencioso do Docusaurus. O CI ganha dois jobs:
`docs-portal` (build, validações e artefato com manifesto de integridade) e
`docs-portal-e2e` (Playwright e axe sobre o artefato servido com CSP estrita).
Ambos entram no `needs` de `result` e nenhum usa secrets ou faz deploy.

## Contexto Tecnico

**Linguagem/Versão**: JavaScript (ESM) e JSX/MDX no Docusaurus 3.10.2 com React
19.3.1. O build roda em Node 24.22.1 (imagem `lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1`,
já usada pelo job frontend). Sem TypeScript, para não adicionar compilador.

**Dependências Principais** (versões fixadas, sem `^`/`~`):
`@docusaurus/core` 3.10.2 (MIT), `@docusaurus/preset-classic` 3.10.2 (MIT),
`pagefind` 1.6.0 (MIT, busca local; ver D8), `react`/`react-dom` 19.3.1 (MIT),
`@mdx-js/react` e `clsx` (MIT, versões que o preset exige, fixadas via lockfile).
Somente para teste: `@playwright/test` 1.65.1 (Apache-2.0) e `axe-core` 4.14.0
(MPL-2.0, mesmo pacote e versão já aceitos em `frontend/web`, só em teste e não
distribuído). Detalhes em [research.md](./research.md).

**Persistência**: N/A. Somente `localStorage` do navegador para preferências
(`altivy.docs.theme`, `theme` do Docusaurus e `dokpod.docs.locale`). Contrato em
[contracts/preferencias.md](./contracts/preferencias.md).

**Testes**:
- Build com `onBrokenLinks: 'throw'`, `onBrokenMarkdownLinks: 'throw'` e
  `onBrokenAnchors: 'throw'` para os três idiomas.
- `npm run check:translations`: falha se uma tradução marcada como obrigatória
  faltar ou se uma fixture violar o esquema de front matter.
- `npm run check:artifact`: lista permitida de arquivos, `SHA256SUMS`, ausência de
  source maps, `node_modules`, fixtures-fonte e URLs de terceiros.
- Playwright 1.65.1 (container `mcr.microsoft.com/playwright:v1.65.1-noble`)
  sobre o artefato servido por `tests/serve.mjs` (servidor estático sem
  dependências que aplica a CSP de produção): versão, idioma, tema, busca,
  traduções ausentes, armazenamento bloqueado e axe A/AA, em 320, 768 e 1280 px.

**Plataforma Alvo**: Saída estática HTML/CSS/JS, servível por qualquer nginx. A
publicação em `docs.dokpod.altivy.it` fica fora desta feature.

**Tipo de Projeto**: Site estático de documentação, isolado da aplicação.

**Metas de Desempenho**: Lighthouse não entra no gate deste piloto. Medidas
usadas: build abaixo de 5 min no CI, página inicial abaixo de 300 KB de
JavaScript comprimido e sem CLS causado por troca de tema (script de cor no
`<head>`).

**Restrições**:
- CSP sem `unsafe-inline`/`unsafe-eval`. Os scripts inline gerados pelo
  Docusaurus entram por hash, calculado no build e gravado no manifesto.
- Nenhuma requisição a terceiros (fontes, busca, analytics).
- Sem Node em runtime.
- Sem secrets, DNS, TLS ou deploy.
- Tudo em container.
- `docs/plan/` não é tocado.

**Escala/Escopo**: 3 linhas (`1.0`, `1.1`, `next`) × 3 idiomas × cerca de 4
tópicos sintéticos. Um tópico fica sem tradução de propósito para testar o aviso.

## Verificação da Constituição

*GATE pré-pesquisa: aprovado. Revisão pós-design: aprovado, sem violações.*

| Princípio | Como o plano atende |
|-----------|---------------------|
| I. Documentação em pt-BR | Artefatos Spec Kit, comentários e o idioma padrão do portal em pt-BR; EN/ES são traduções do conteúdo público. |
| II. Engine local / workloads | Não toca engine, inventário nem workloads. |
| III. Identidade e menor privilégio | Jobs com `permissions: contents: read`, `persist-credentials: false`, sem secrets nem ambiente protegido. |
| IV. Operações duráveis | N/A, sem operação de runtime. O build é determinístico e o artefato tem manifesto de integridade. |
| V. Arquitetura modular | Portal isolado em `docs/portal/`, com `package.json` e lockfile próprios. Não altera backend, frontend nem contratos. |
| VI. Qualidade verificável | Testes proporcionais ao risco (build estrito, tradução, artefato, e2e com CSP e axe). `NOT RUN` é registrado como falha de evidência. A regra de E2E pela borda nginx protege jornadas com API/BFF; o portal é estático, sem API/BFF, e o e2e usa um servidor estático que reproduz a CSP de produção. A validação no nginx real fica para a feature de publicação. |
| Restrições: containers | Build no container Node 24.22.1 do frontend; e2e no container Playwright; comandos locais via `docker run` ([quickstart.md](./quickstart.md)). |
| Restrições: dependências | Licenças MIT/Apache-2.0 verificadas no registry. `axe-core` (MPL-2.0) já é dependência de teste aceita no repositório. Versões exatas e lockfile. |
| Processo | Spec Kit seguido. Implementação só após `/speckit-tasks`, `/speckit-analyze` e autorização humana. |

## Estrutura do Projeto

### Documentação desta Funcionalidade

```text
specs/034-portal-documentacao/
├── spec.md
├── plan.md               # Este arquivo
├── research.md           # Decisões, versões, licenças e alternativas
├── data-model.md         # Linha, Tópico, Preferência, Artefato
├── quickstart.md         # Validação local em containers
├── contracts/
│   ├── rotas.md          # URLs de versão/idioma e regra da entrada neutra
│   ├── preferencias.md   # Chaves de localStorage e precedência
│   └── artefato.md       # Layout e manifesto do artefato
├── checklists/requirements.md
└── tasks.md              # Gerado por /speckit-tasks
```

### Código-Fonte (raiz do repositório)

```text
docs/portal/
├── package.json                 # versões exatas; engines node 24.22.1 <25
├── package-lock.json
├── docusaurus.config.js         # i18n, versões, colorMode, CSP-friendly scripts, plugins locais
├── sidebars.js
├── translation-policy.json      # tópicos com tradução obrigatória por idioma
├── docs/                        # linha next (preview, noindex)
├── versioned_docs/version-1.0/  # fixtures sintéticas
├── versioned_docs/version-1.1/
├── versioned_sidebars/
├── versions.json                # ["1.1", "1.0"]
├── i18n/{en,es}/                # traduções de docs, versões e tema
├── plugins/translation-status/  # gera mapa tópico×idioma×linha (globalData)
├── src/
│   ├── brand/                   # snapshot Altivy: tokens.css, fonts/, symbol-*.svg, SHA256SUMS, SOURCE.md
│   ├── css/custom.css           # mapeia --ifm-* para tokens Altivy (escuro e claro)
│   └── theme/
│       ├── ColorModeToggle/     # eject: menu Claro/Escuro/Sistema
│       ├── DocItem/Content/     # wrap: aviso de tradução ausente
│       ├── NavbarItem/ComponentTypes.js           # registra custom-versionSwitch
│       ├── NavbarItem/VersionSwitch.js            # troca de linha sem fallback silencioso
│       └── NavbarItem/LocaleDropdownNavbarItem/   # wrap: grava escolha manual
├── static/js/locale-entry.js    # autodetecção só na entrada neutra
├── static/js/theme-init.js      # aplica altivy.docs.theme antes do script de cor
├── scripts/
│   ├── check-translations.mjs
│   ├── check-artifact.mjs
│   └── build-manifest.mjs       # SHA256SUMS, hashes CSP, portal-manifest.json
└── tests/
    ├── serve.mjs                # servidor estático com CSP de produção
    ├── playwright.config.mjs
    └── e2e/{versoes,idioma,tema,busca,a11y}.spec.mjs
.github/workflows/ci.yml         # jobs docs-portal e docs-portal-e2e; ambos no needs de result
docs/portal/README.md            # como validar e atualizar o snapshot da marca
```

**Decisão de Estrutura**: o portal fica no repositório do produto, para versionar
a documentação junto do código. Ele usa um projeto npm independente em
`docs/portal/`, para não acoplar as dependências do Docusaurus ao Angular de
`frontend/web`. O conteúdo editorial real e os demais produtos ficam para
features próprias. O snapshot da marca é copiado (não montado) porque o build
não pode depender do repositório Altivy.

## Registro de Complexidade

Sem violações da constituição.

## Desvios registrados na implementação

| Item | Plano | Implementado | Motivo |
|------|-------|--------------|--------|
| Busca | `@easyops-cn/docusaurus-search-local` | Pagefind 1.6.0 + `static/js/search-loader.js` | Licença MPL-1.1 de `lunr-languages`; `import()` no SSR (D8) |
| CSP | `script-src 'self' <hashes>` | Mais `'wasm-unsafe-eval'` | WebAssembly do Pagefind (D9); requer aceite humano |
| `style=""` | Pendência | Prism vazio + eject de `Token`, `baseUrlIssueBanner: false`, plugin `strict-csp` | `style-src 'self'` sem `unsafe-hashes` (D9) |
| Módulos | `"type": "module"` | Sem `type`; plugins e scripts em `.mjs` | `require.resolveWeak` do webpack falha com ESM no `package.json` |
| Rotas | `/<docId>` | `/<docId>/` (`trailingSlash: true`) | URL canônica única para o servidor estático |
| Tópico ausente | MDX `TopicoAusente` | Aviso no wrapper de `DocItem/Content` (`?topico-ausente`) | Vale para qualquer página inicial de linha sem editar fixtures |
| TOC | — | Wrappers de `DocItem/TOC/{Desktop,Mobile}` ocultam o sumário em PT quando falta tradução | Âncoras quebradas em `/en/next/*` |
| Marca | `src/brand/fonts/LICENSE` | `src/brand/public/fonts/LICENSE-Outfit-1.1.txt` (servida com a fonte) | OFL exige distribuir a licença |
| Armazenamento bloqueado | Tema escuro | Segue `prefers-color-scheme` | Script de cor do Docusaurus; contrato de preferências atualizado |
| Auditoria de marca | Manual | Também `tests/e2e/marca.spec.mjs` (peso ≤ 600, Outfit, sem roxo) | Achou `.footer__title` em 700 (atalho `font: bold` do Infima) |
| Distribuição | Parágrafo em `docs/release-integrity.md` | Seção "Artefato `docs-portal-site`" em `docs/portal/README.md` | `release-integrity.md` é gerenciado pelo Groundwork (`Test-ManagedFiles.ps1`) |
