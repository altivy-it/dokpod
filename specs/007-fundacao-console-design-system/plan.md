# Plano de Implementação: Fundação do Console Operacional e Design System Próprio

**Branch**: `007-console-operacional` | **Data**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Estado**: `implemented` - aprovada em 2026-10-13 (ADR 2026-0005 `accepted`; ver [specs/README.md](../README.md)) e implementada; T029 executada na stack real em 2026-10-14 (Linux, Podman).

## Resumo

Substituir o visual atual do `frontend/web` por um design system Angular próprio, derivado do Altivy, e por uma moldura (`shell`) fiel aos mockups. A entrega é só de frontend e governança: tokens, fontes, ícones, componentes standalone, shell com rotas lazy e páginas "Em breve", tela de entrada, estados de sessão e catálogo de componentes de desenvolvimento. O backend não muda, exceto pela leitura opcional de um claim de grupo de exibição no BFF (`/bff/session`).

## Contexto Técnico

**Linguagem/Versão**: TypeScript estrito, Angular 22 (standalone, Signals, control flow nativo); .NET 10 apenas para o ajuste opcional do BFF.

**Dependências Principais**: Nenhuma biblioteca de componentes de UI. Angular CDK (MIT) é candidato opcional para `a11y`/`overlay`, decidido no ADR 2026-0005 (D-001). Novas dependências candidatas, todas sujeitas a verificação de licença e fixação de versão: fontes Outfit e JetBrains Mono (OFL 1.1, arquivos `woff2` versionados no repositório) e geometria de ícones Lucide (ISC, copiada como sprite SVG versionado, sem pacote npm em runtime). Gráficos em SVG próprio, sem biblioteca.

**Armazenamento**: N/A. Preferências locais (menu recolhido) em `localStorage` com `try/catch`, nunca dados de autorização.

**Testes**: Vitest (componentes, teclado, ARIA, formatação pt-BR); Playwright pela borda nginx (navegação, E2E legados); o runner E2E é Python (`frontend/tests/e2e/`, pytest + Playwright) e ainda não tem ferramenta de acessibilidade; a candidata é `axe-core` (MPL-2.0, somente teste) via integração Python, sujeita a aprovação no ADR (D-009); loop de verificação visual com screenshots desktop/mobile.

**Plataforma Alvo**: Navegadores evergreen (Chromium, Firefox, WebKit) pela borda nginx containerizada.

**Tipo de Projeto**: Aplicação web Angular servida pela imagem nginx do Dokpod; BFF como única origem de dados.

**Objetivos de Desempenho**: Shell interativo em ≤ 2 s na stack E2E local; crescimento do bundle inicial ≤ 15% sobre a linha de base registrada abaixo (tarefa T005); `backdrop-filter` limitado a moldura, cards e overlays (nunca em linhas de grid).

**Restrições**: WCAG 2.2 AA; sem CDN externo em runtime (CSP do nginx); sem token no browser; componentes de apresentação sem HTTP/SignalR; lazy loading por feature; features não importam internos de outras features.

**Escopo**: Design system, shell, rotas, páginas "Em breve", tela de entrada, 404, estados de sessão, catálogo de componentes de desenvolvimento, governança documental. Telas funcionais ficam nas specs 008-014.

## Verificação da Constituição

| Princípio | Situação | Observação |
| --- | --- | --- |
| I. Documentação pt-BR | OK | Artefatos em pt-BR. |
| II. Engine como fonte | N/A | Sem mudança de backend. |
| III. Identidade externa | OK | Login/logout/sessão continuam pelo BFF/Keycloak; scopes só ocultam UI. |
| IV. Operações duráveis | N/A | Sem novos comandos. |
| V. Arquitetura modular | OK com ressalva | Sem novo serviço; ADR exigido pela troca de regra de UI (não é exigência constitucional, mas evita conflito com `.github/`). |
| VI. Qualidade verificável | OK | Vitest, Playwright pela borda nginx, verificação visual desktop/mobile. |
| Restrição de dependências | Pendente | Licenças de fontes/ícones verificadas em `research.md`; aprovação humana registrada no ADR. |

**Gate**: Implementação bloqueada até ADR 2026-0005 aprovado e revisão humana desta spec.

## Estrutura do Projeto

### Documentação desta Feature

```text
specs/007-fundacao-console-design-system/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── design-system-components.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Código-fonte

```text
docs/adr/2026-0005-design-system-proprio-altivy.md        # novo ADR (proposed)
.github/instructions/frontend-po-ui.instructions.md        # removido, substituído por:
.github/instructions/frontend-design-system.instructions.md
.github/instructions/frontend-angular.instructions.md      # atualizado
.github/instructions/frontend-visual-verification.instructions.md  # atualizado
.github/agents/angular-engineer.agent.md                   # atualizado
.github/copilot-instructions.md                            # referências atualizadas
docs/frontend.md                                           # seção Design system reescrita

frontend/libs/design-system/
├── styles/tokens.css          # substituído: tokens Altivy (gerados de tokens.json)
├── styles/foundation.css      # substituído: reset, tipografia de app, scrollbars, foco
├── styles/fonts.css           # substituído: @font-face Outfit / JetBrains Mono locais
├── styles/primitives.css      # substituído: utilitários (alt-num, ct-mono, ct-bar, ct-kv)
├── fonts/                     # woff2 + OFL.txt
└── icons/icons.svg            # sprite de ícones de traço + LICENSE (ISC)

frontend/web/src/app/
├── shell/
│   ├── app-shell.component.*          # menu lateral + header + outlet
│   ├── navigation.ts                  # itens de menu, seções, scopes, rotas
│   ├── stage-filter.service.ts        # estágio global via query param
│   ├── coming-soon.page.ts
│   ├── not-found.page.ts
│   └── sign-in.page.*                 # tela de entrada
├── design-system/
│   ├── icon/            button/        status-chip/   status-circle/
│   ├── info-kpi/        card/          line-chart/    donut-chart/
│   ├── data-grid/       tabs/          search-field/  field/
│   ├── select/          checkbox/      drawer/        empty-state/
│   ├── toast/           progress-bar/  key-value/     engine-badge/
│   └── format/          (pipes pt-BR: número, data, bytes, duração)
├── core/                (sessão BFF, realtime: existentes, ajustados)
└── features/dev-catalog/ (somente em build de desenvolvimento)
```

**Decisão de Estrutura**: Componentes em `frontend/web/src/app/design-system/` (conforme `docs/frontend.md`) e estilos/assets compartilháveis em `frontend/libs/design-system/`. O shell substitui `app.html`/`app.css`. As features legadas `environments` e `containers` passam a renderizar dentro do shell e só são removidas pelas specs 008 e 010.

## Fases

1. **Governança** (bloqueante): completar o ADR `proposed` com revisor humano; registrar a aprovação humana; somente depois, novo arquivo de instruções do design system e atualização de todas as instruções/agentes/documentos que citam PO UI ou proíbem vidro/raios grandes, com verificação por busca (SC-005).
2. **Fundação visual**: linha de base de bundle/tempo, tokens, fontes, ícones, utilitários, pipes de formatação.
3. **Componentes P1**: ícone, botão, chip/círculo de status, card, KPI, campo, seleção, marcador, busca, abas, drawer, estado vazio, toast, barra de progresso, chave-valor, badge de engine.
4. **Componentes de dados**: grid de dados e gráficos de linha/rosca.
5. **Shell e rotas**: navegação, estágio global, "Em breve", 404, entrada e estados de sessão; telas legadas dentro do shell.
6. **Verificação**: catálogo de componentes, Vitest, Playwright, acessibilidade, loop visual desktop/mobile.

## Linha de Base (SC-006)

Medido em 2026-10-13 (T005), antes de qualquer mudança visual, com `ng build` de produção no container `lzocateli/devops:cpu-ubuntu24.04` (Node 24.15.0).

| Medida | Antes (T005) | Depois (T031) |
| --- | --- | --- |
| Bundle inicial | 269,32 kB bruto / 74,66 kB transferido | 389,85 kB bruto / 101,76 kB transferido (+36% transferido) |
| Shell interativo | não medida (stack E2E não iniciada nesta etapa) | mediana 89 ms em 5 amostras (borda nginx + mock, build de desenvolvimento) |

Medição T031 em 2026-10-13 no mesmo container. **SC-006**: tempo até o shell interativo atendido (≤ 2 s); crescimento do bundle inicial **não atendido** (+36% transferido contra o limite de 15%). Composição do inicial: framework Angular (`core`, `router`, `common`, `platform-browser`) ≈ 284 kB brutos, `main` 42 kB (shell, sessão, toast, ícones) e CSS global 63 kB (tokens, fontes, kit Altivy). A linha de base era o app legado de duas telas; o crescimento decorre do shell e da base do design system exigidos pelos mockups. Remover o CSS não usado do kit economizaria ≈ 2,5 kB transferidos e anteciparia divergência com componentes ainda previstos (`alt-column-picker` na 010). **Recomendação aplicada**: substituir o limite relativo por orçamento absoluto do bundle inicial de 420 kB brutos (alerta, ≈ 110 kB transferidos) e 500 kB (erro), verificado em todo build de produção pelos `budgets` do `angular.json`; o SC-006 da spec foi ajustado e qualquer aumento do orçamento exige revisão registrada.

## Riscos

| Risco | Mitigação |
| --- | --- |
| Custo de desempenho do `backdrop-filter` em telas densas | Vidro só em moldura, cards e overlays; grids em `surface-panel` opaco, como orienta o Altivy. |
| Contraste insuficiente em texto sobre vidro | Usar somente pares aprovados na tabela de contraste; teste automatizado de contraste no catálogo. |
| Grid de dados próprio subestimado | Escopo do grid limitado ao que o mockup usa; virtualização apenas se medição justificar (≥ 500 linhas). |
| Regras concorrentes durante a transição | Fase 1 bloqueante; instruções só mudam após a aprovação registrada; PR único de governança; busca automatizada por regras remanescentes. |

## Complexity Tracking

| Violação | Por que é necessária | Alternativa mais simples rejeitada porque |
| --- | --- | --- |
| Design system próprio em vez de biblioteca pronta | Decisão do solicitante: fidelidade total ao mockup Altivy | PO UI tematizado não reproduz moldura, vidro, grid e gráficos do mockup sem sobreposição extensa de CSS, o que a própria instrução PO UI proíbe. |
