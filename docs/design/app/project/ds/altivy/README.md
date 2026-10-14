Altivy é uma assessoria técnica independente. O sistema é **dark-first** e usa **liquid glass** como única forma de elevação: vidro translúcido sobre um fundo escuro com véus teal e azul. Tudo é Outfit, peso máximo 600, e um único acento — `teal-light`.

## Voz e conteúdo

- Português do Brasil, frases completas e sóbrias. Fala com a empresa na terceira pessoa ("A Altivy ajuda empresas a…") e com o leitor em "você" só nas CTAs e perguntas ("Minha empresa não procura DevOps…").
- Títulos são afirmações de resultado, não rótulos: "Tecnologia alinhada ao resultado do negócio", "Do cenário atual a um caminho executável".
- CTAs são verbo + objeto em caixa de frase: "Conversar sobre o desafio", "Identificar meu cenário", "Iniciar conversa".
- Bullets descrevem entregáveis verificáveis: "Oportunidades ordenadas por impacto", "Plano executável com validação por etapa".
- Sem emoji, sem exclamação, sem números de vaidade.

## Cor

- Fundo de página: sempre `Page` — `surface-base` com `veil-teal`, `veil-teal-deep` e `veil-blue`. O vidro precisa dos véus para refratar.
- Títulos em `text-strong`; texto corrido em `foreground`; texto secundário, lede, navegação e rótulos em `muted`.
- `teal-light` é o único acento: eyebrow, ícones, bullets, links, numeração ativa, anel de foco e preenchimento da CTA primária.
- Texto sobre `teal-light` é sempre `ink` (11.03:1).
- `teal` (#0F766E) **nunca é texto** — 2.98:1 sobre ink reprova AA. Só preenchimento, traço e véu.
- `slate` (#718096) **não é texto corrido** — 4.06:1 sobre ink. Só numeral grande (≥34px), ícone decorativo e divisor.
- `blue` existe só no véu do fundo.
- Nenhum token usa matiz entre 275° e 330° em OKLCH (roxo/lavanda). Os hues do sistema ficam entre 180° e 270°, de teal a azul-acinzentado. Não introduza roxo.

### Contraste medido (sobre ink #1A202C)

| Par | Razão | Uso |
| --- | --- | --- |
| `teal-light` sobre ink | 11.03:1 | Acento, eyebrow, ícone |
| `teal` sobre ink | 2.98:1 ✕ | Reprova — banido como texto |
| ink sobre `teal-light` | 11.03:1 | CTA primária |
| `foreground` | 13.24:1 | Corpo de texto |
| `muted` | 7.23:1 | Texto secundário |
| `slate` | 4.06:1 ✕ | Rebaixado a borda e divisor |

Sobre o fundo real (`surface-base`) as razões sobem: `muted` 8.38:1, `slate` 4.71:1. Sobre `glass-overlay`, `muted` fica em 5.13:1 — o mínimo que o sistema aceita para texto secundário.

## Superfícies — liquid glass

| Nível | Preenchimento | Blur | Uso |
| --- | --- | --- | --- |
| `surface-base` | #0D1118 + véus | — | Fundo |
| glass-raised | branco 10 → 4.5 → 2% a 145° | `backdrop-raised` (22px, sat 170%) | Cards, header, CTA secundária, FAQ aberto |
| glass-overlay | branco 17 → 8 → 5% a 145° | `backdrop-overlay` (32px, sat 190%) | Banner de CTA, popover, menu, hover |

Todo vidro leva borda de 1px (`glass-border` / `glass-border-strong`), reflexo de 1px no topo e sombra de profundidade (`shadow-glass` / `shadow-glass-overlay`). Não use sombras sem vidro nem vidro sem véus.

## Tipografia

- **Outfit** (Google Fonts, 400/500/600) em tudo: títulos, texto corrido, numeração e rótulos. Carregue `https://fonts.googleapis.com/css2?family=Outfit:wght@400;500;600&display=swap`.
- Peso máximo 600 — a marca não usa 700 nem 800.
- Títulos com tracking negativo: `hero` −0.035em, `display` −0.03em, `h2-section` −0.025em, `h2` −0.02em.
- Medida de 60–75 caracteres no texto corrido (`body`); 40–56 em lede e coluna estreita.
- `eyebrow` e `label` sempre em MAIÚSCULAS com tracking aberto (0.16em / 0.12em).
- Numeração 01–09 (`index`) é estrutura: repouso `slate` 400, hover `muted` 500, ativo `teal-light` 600.

## Layout

- Desktop a 1280px com gutter `space-64`; seções com `space-76` de padding vertical.
- Grids **assimétricos**: hero em 7/4 com `space-72` de gap, FAQ em 4/7, rodapé em 5/3/3/4. Não centralize blocos de texto.
- Cabeçalho de seção → conteúdo: `space-46`. Grids de cards: `space-20`.
- Símbolo da marca pode sangrar pela margem do hero a 5% de opacidade.

## Raios

`radius-pill` em botões, header e trilho · `radius-card` (22px) em cards · `radius-lg` (18px) em superfícies e tabelas · `radius-md` (16px) em FAQ aberto e avisos · `radius-banner` (32px) no banner final.

## Estados e foco

- Foco visível: `outline: 2px solid teal-light; outline-offset: 3px` em todo elemento interativo (≥9.88:1 sobre qualquer superfície).
- Hover: links `muted` → `foreground`; CTA primária brilho +5%; secundária sobe para glass-overlay; numeral `slate` → `muted`.

## Movimento

- Hero entra em 700ms; parallax limitado a 12%.
- Blocos revelam ao entrar no viewport, uma vez só; grids em stagger de 70ms.
- O trilho do método preenche conforme a seção entra.
- Tudo suprimido sob `prefers-reduced-motion`.

## Iconografia

Ícones de traço no estilo Lucide: viewBox 24, `stroke-width` 2 (2.2 em setas de botão, 2.4 em checks), pontas e junções arredondadas, sem preenchimento. Cor `teal-light` para ícones de acento, `muted` para chevrons fechados, `ink` dentro da CTA primária. Tamanhos 14–18px. Glifos usados: seta →, chevron, check, info, ×.

## Regras anti-template

- Método como trilho de progresso (`MethodTrack`), não três cards iguais.
- Numeração como estrutura, não decoração.
- Sem gradientes roxos, sem emoji em cards, sem cards com borda colorida à esquerda.

## Produto e app

A mesma linguagem vale para telas de produto, só que mais densa. Os componentes de app foram portados da família New do Core Platform e redesenhados no visual Altivy.

- **Base densa:** envolva telas de app em `Page` com a classe `alt-app` (14px). Use os estilos `app-title` (20px), `app-heading` (16px), `app-body` (14px), `app-small` (13px), `app-caption` e `app-label` (12px) e `kpi` (28px). Números sempre com `tabular-nums` (`alt-num`) e formato pt-BR (`1.234,56`, `30/09/2026 14:37`).
- **Moldura:** `AppShell` (menu lateral de vidro + `AppHeader` + conteúdo) sobre `shell-bg` com os véus. Não há variantes de cor: o shell é um só, escuro.
- **Superfícies:** blocos de conteúdo são `CardBase` em vidro. Conteúdo denso (data grid, gantt, mapas, tabelas longas) vai em `surface-panel` opaco (`alt-panel`), para não empilhar vidro sobre vidro. Sobreposições são `CorporateDrawer`; a busca global é `CommandPalette`.
- **Seleção e foco:** item ativo e linha selecionada em `accent-tint` com borda `accent-border`. Hover em `hover-wash`. Inputs em `input-fill` com foco teal-light e anel de 4px.
- **Status:** `signal-success`, `signal-warning`, `signal-error` e `signal-info`, com fundos soft `success-tint`, `warning-tint`, `error-tint-strong` e `info-tint`. **Todo status leva palavra ou ícone**: sucesso e erro diferem pouco em luminosidade (1.59:1). Mapa: crítico → error, alerta/parcial → warning, OK/em dia → success, em trânsito/informativo → info, neutro → muted.
- **Gráficos:** SVG desenhado à mão com `chart-1` a `chart-6` e grade `chart-grid`; eixos em `muted` 12px. `chart-6` (slate) é meta ou período anterior.
- **Raios de app:** `radius-control` (12px) em inputs, menus e botões de toolbar; `radius-sm` (8px) em tiles e células; pílula em botões, chips e abas.
- **Barras de rolagem:** finas, polegar em pílula `scrollbar-thumb` sobre trilho transparente, `scrollbar-thumb-hover` (teal) no hover e ao arrastar. Vale para a página e para todo elemento rolável dentro de `Page`; não estilize barras por componente. Para esconder a barra em trilhos horizontais (abas), use `scrollbar-width:none`.
- **Sem borda colorida à esquerda:** prioridade e seleção usam ponto, chip ou tint, nunca uma faixa lateral.
- **Kit compartilhado:** `Altivy.Icon` (ícones de traço) e `Altivy._` (`cx`, `nf` para números pt-BR, `tone()` para cores de status). Classes utilitárias: `alt-ctl`, `alt-field`, `alt-tbtn`, `alt-chip`, `alt-dot`, `alt-count`, `alt-popover`, `alt-menu-item`, `alt-skel`.

### Inventário

| Grupo | Componentes |
| --- | --- |
| Layout | `AppShell`, `AppHeader`, `LayoutEditavel` |
| Ações | `Button` (CTAs da landing + variantes de app), `SplitButton`, `ShortcutButton`, `SideRailAction`, `ThemeSwitch`, `ExportarXlsx` |
| Navegação | `Tabs` |
| Superfícies e sobreposições | `CardBase`, `CorporateDrawer`, `CustomBackdrop`, `CommandPalette` |
| Feedback | `Toast` (+ `Altivy.notify`), `Notificacoes`, `StatusChip`, `StatusCircle`, `EmptyStates`, `CredentialWarning` |
| Formulários | `CampoBusca`, `FilterAutocomplete`, `VirtualizedAutocomplete`, `FormBody`, `FormCRUD`, `JsonFormsRenderers`, `CustomSlider`, `DropFiles`, `SeletorSemanas` |
| Dados | `EnhancedDataGrid`, `EnhancedDataTable`, `ColumnPicker`, `InfoKPI`, `DashWidgets`, `SmartReport`, `CustomMap` |
| Gráficos | `Graphics`, `GraficoBase`, `GradeSLA` |
| Conteúdo | `Accordion`, `Utilities` |
| Admin | `PerfilSeguranca`, `AdmFilas`, `AdmPipeline` |
| Módulo · CCO | `CcoTorreTabela`, `CcoAcoesLinha`, `CcoCelulas`, `CcoEntrega`, `CcoEstadia`, `CcoIndicadores`, `CcoJornada`, `CcoMacros`, `CcoTelemetria`, `CteLeitor` |
| Módulo · PPL | `PplAgendaSemanal`, `PplCardColumnDay`, `PplCenarios`, `PplDiario`, `PplDocumentos`, `PplEventCard`, `PplFiltrosPainel`, `PplFrotaTimeline`, `PplGanttMotorista`, `PplGridProgramacao`, `PplHistoricoEntrega`, `PplInfoBoxAderencia`, `PplKpiDonuts`, `PplLane`, `PplMapaAreas`, `PplPosicaoFrota`, `PplStatusBars`, `PplWizard`, `PlanOperacaoCard` |
| Módulo · BI | `BiGraficos`, `BiSelecaoMeses`, `BiSemaforo`, `BiTotalizadores`, `BridgeVendas` |
| Módulo · RH | `RhGraficoCard`, `RhFiltros`, `RhCampoRepetidor` |
| Módulo · Helpdesk | `HdCaixaEntrada`, `HdChamado`, `HdDashboard`, `HdFiltrosLote`, `HdHistorico`, `HdSlaGut` |
| Módulo · Manutenção | `ManBoxes`, `ManCabecalhoPagina`, `ManEquipe`, `ManQuiosque` |
| Módulo · Violações | `ViolCampoDinamico`, `ViolCatalogoWizard`, `ViolEtapas`, `ViolRegrasBoard`, `ViolSimulador` |
| Módulo · Cliente | `UxClientesAdmin`, `UxEntregaProgresso`, `UxFiltroPagador`, `UxWidgetsCliente` |

Cada componente aceita os dados por props e, sem props, mostra um exemplo realista. Mapas são SVG estilizado, sem tiles nem biblioteca de mapas; o editor de cercas (`PplMapaAreas`) não desenha polígonos novos sem um motor de geometria.
