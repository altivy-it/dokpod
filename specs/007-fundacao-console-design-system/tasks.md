# Tarefas: Fundação do Console Operacional e Design System Próprio

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/design-system-components.md).

**Estado**: `implemented` - concluída em 2026-10-13, exceto T029 (`NOT RUN`: stack Keycloak/BFF indisponível no host de desenvolvimento). Evidências em cada tarefa e em `artifacts/e2e/mock/` (não versionado).

**Pré-requisitos**: Revisão humana desta spec; revisor humano identificado no ADR 2026-0005; T001-T004 concluídas antes da Fase 2.

**Testes**: Obrigatórios (Vitest para componentes/estado, Playwright pela borda nginx, acessibilidade e verificação visual).

## Formato: `[ID] [P?] [Story] Descrição`

## Fase 1: Governança (bloqueante)

- [x] T001 [US3] Revisar e completar `docs/adr/2026-0005-design-system-proprio-altivy.md` (já criado, `proposed`): revisor humano identificado pelo solicitante, decisão sobre Angular CDK (D-001), ferramenta de acessibilidade automatizada (D-009) e lista final de dependências candidatas. — ADR 2026-0005 `accepted` (revisor zocatel; sem Angular CDK; axe-core MPL-2.0).
- [x] T002 [US3] Registrar a aprovação humana do ADR e desta spec no próprio ADR (status alterado pelo revisor) e em [specs/README.md](../README.md); sem aprovação, parar aqui. Nenhuma instrução vigente é alterada antes deste registro. — aprovação registrada no ADR e em `specs/README.md` (2026-10-13).
- [x] T003 [US3] Após T002, no mesmo PR de governança: substituir `.github/instructions/frontend-po-ui.instructions.md` por `.github/instructions/frontend-design-system.instructions.md`; atualizar `.github/instructions/frontend-angular.instructions.md`, `.github/instructions/frontend-visual-verification.instructions.md`, `.github/instructions/architecture.instructions.md`, `.github/instructions/testing.instructions.md`, `.github/agents/angular-engineer.agent.md`, `.github/agents/solution-architect.agent.md`, `.github/agents/code-reviewer.agent.md`, `.github/COPILOT_GUIDE.md`, as referências de `.github/copilot-instructions.md` e a seção "Design system" de `docs/frontend.md`; remover o servidor MCP `po-ui` de `.vscode/mcp.json`. — instruções e agentes atualizados; `frontend-po-ui.instructions.md` removido; MCP `po-ui` fora de `.vscode/mcp.json`.
- [x] T004 [US3] Verificar SC-005: `grep -rniE "po ui|po-ui|glassmorphism|grandes raios"` em `.github/`, `.vscode/`, `docs/` (exceto `docs/adr/` e `docs/design/`), `AGENTS.md`, `CLAUDE.md` e `README.md` sem ocorrências normativas remanescentes; registrar o resultado no PR. — grep sem ocorrência normativa (única menção é a proibição em `frontend-design-system.instructions.md`).

**Checkpoint**: Governança aprovada; nenhuma regra concorrente.

## Fase 2: Fundação visual

- [x] T005 [US2] Medir a linha de base antes de qualquer mudança visual: tamanho do bundle inicial (`ng build` de produção) e tempo até a primeira renderização útil na stack E2E local; registrar em [plan.md](plan.md) como referência de SC-006. — linha de base em [plan.md](plan.md).
- [x] T006 [P] [US2] Verificar licenças e fixar versões de Outfit, JetBrains Mono e geometria Lucide (e de Angular CDK, se aprovado em T001); adicionar `woff2` + `OFL.txt` em `frontend/libs/design-system/fonts/` e sprite + `LICENSE` em `frontend/libs/design-system/icons/`; registrar em `docs/licenciamento.md`. — fontes OFL 1.1 (`@fontsource` 5.3.0) em `frontend/web/src/styles/fonts/` com `OFL-*.txt`; ícones do kit Altivy (`icon-paths.ts`), sem dependência externa; registro em `docs/licenciamento.md`. Local real dos estilos: `frontend/web/src/styles/` (documentado em `docs/frontend.md`).
- [x] T007 [US2] Substituir `frontend/libs/design-system/styles/tokens.css` pelos tokens Altivy com prefixo `--dok-` (gerados de `docs/design/app/project/ds/altivy/tokens.json`), incluindo fallback opaco para `prefers-reduced-transparency` e ausência de `backdrop-filter`. — `frontend/web/src/styles/tokens.css` (variáveis do kit, sem prefixo `--dok-` para manter paridade com `altivy-app.css`); fallbacks opacos para `prefers-reduced-transparency` e sem `backdrop-filter` em `altivy-kit.css`.
- [x] T008 [US2] Reescrever `foundation.css`, `fonts.css` e `primitives.css` em `frontend/libs/design-system/styles/` (reset, estilos `app-*`, `eyebrow`, `alt-num`, `ct-mono`, `ct-bar`, `ct-kv`, `ct-row`, scrollbars, foco 2px teal-light); remover tema claro de `frontend/web/src/styles.css`. — `app-text.css`, `altivy-kit.css`, `styles.css`; tema claro removido; estilos IBM Plex antigos apagados.
- [x] T009 [P] [US2] Criar pipes pt-BR em `frontend/web/src/app/design-system/format/` (número com casas, percentual, bytes MiB/GB, data/hora `dd/MM/yyyy HH:mm`, duração relativa "há 4 min") com testes Vitest. — `design-system/format/format.ts` + `format.spec.ts`.

## Fase 3: Componentes P1 (US2)

**Teste independente**: Catálogo de desenvolvimento + Vitest de teclado/ARIA por componente.

- [x] T010 [P] [US2] `dok-icon` e `dok-button` em `frontend/web/src/app/design-system/{icon,button}/` com testes. — testes em `design-system.spec.ts` e `design-system.components.spec.ts`.
- [x] T011 [P] [US2] `dok-status-chip`, `dok-status-circle`, `dok-engine-badge` com mapa de tons e testes de "status sempre com texto". — idem; chip de status sempre com texto.
- [x] T012 [P] [US2] `dok-card`, `dok-info-kpi`, `dok-progress-bar`, `dok-key-value` com skeleton de loading. — idem (card com `aria-busy`, KPI indisponível anunciado, barra `progressbar`).
- [x] T013 [P] [US2] `dok-field` (ControlValueAccessor, `mono`, `secret`, `suffix`, ajuda/erro) conforme `Campo.dc.html`. — CVA, `secret` com botão pressionável, erro em `role=alert` ligado por `aria-describedby`.
- [x] T014 [P] [US2] `dok-select` conforme `Selecao.dc.html` (listbox, setas, Enter/Espaço, Esc, Home/End, blur fecha) e `dok-checkbox` conforme `Marcador.dc.html`. — teclado do `dok-select`; nome acessível = rótulo + valor (corrige `label-content-name-mismatch` do axe).
- [x] T015 [P] [US2] `dok-search-field` e `dok-tabs` (tablist com setas/Home/End). — `/` foca a busca, Esc limpa, debounce testado.
- [x] T016 [US2] `dok-drawer` (dialog modal, foco preso, Esc, devolução de foco, chips, ações primária/cancelar) e `dok-empty-state`. — foco preso (Tab/Shift+Tab), Esc e devolução de foco testados.
- [x] T017 [P] [US2] `dok-toast` + `ToastService` com região viva. — timers no serviço (independentes de animação), pausa em hover/foco, regiões vivas persistentes polite/assertive.

## Fase 4: Componentes de dados (US2)

- [x] T018 [US2] `dok-data-grid` em `frontend/web/src/app/design-system/data-grid/` conforme D-006 (`table` nativa): colunas tipadas, cabeçalho fixo, coluna travada, ordenação, filtros, seleção múltipla, ações por linha, densidade, busca, "carregar mais", exportação CSV, rodapé "atualizado em"; testes do teclado definido no cenário US2-3, `aria-sort` e anúncio de seleção. — menu de linha nomeado, setas/Home/Esc, ordenação em 3 estados, CSV com neutralização de fórmulas.
- [x] T019 [P] [US2] `dok-line-chart` (séries, área, referência, escala fixa, tooltip por teclado, tabela alternativa) com testes. — `role=slider` com `aria-valuetext`, setas/Home/End, tabela alternativa; `svg` deixa de ser `role=img` com filhos focáveis.
- [x] T020 [P] [US2] `dok-donut-chart` (itens, centro, legenda textual) com testes. — legenda textual e arcos nomeados (`role=img`).

## Fase 5: Shell e rotas (US1, US4)

**Teste independente**: Navegação completa por teclado, estágio na URL, menu recolhível abaixo de 1100px, sem rolagem horizontal.

- [x] T021 [US1] Criar `frontend/web/src/app/shell/navigation.ts` (itens/rotas/scopes de [data-model.md](data-model.md); scopes lidos de `EnvironmentState.scopes` já retornados pela API) e `stage-filter.service.ts` (query param `stage`; atalho desabilitado com tooltip até a spec 008). — `shell/navigation.ts`, `stage-filter.service.ts`; testes em `shell/shell.spec.ts`.
- [x] T022 [US1] Criar `dok-app-shell` em `frontend/web/src/app/shell/` (menu por seções, item ativo, breadcrumbs por dados de rota, atalhos de estágio, sino, usuário, "Pular para o conteúdo", menu recolhível < 1100px) e substituir `frontend/web/src/app/app.html`/`app.css`. — `dok-app-shell` com "Pular para o conteúdo", menu recolhível < 1100 px e `aria-current`.
- [x] T023 [US1] Páginas `coming-soon.page.ts`, `not-found.page.ts` e tela de entrada `sign-in.page.*`; estados de sessão (carregando, erro, expirada) no visual novo; atualizar `frontend/web/src/app/app.routes.ts` com todas as rotas do menu. — "Em breve", 404, entrada (em `app.html`), estados carregando/erro/**expirada** (401 da API via `session-expiry.interceptor.ts` e cliente gerado).
- [x] T024 [P] [US1] (Opcional) Expor `displayGroup` em `backend/apps/Dokpod.Bff/Endpoints/BffSessionResponse.cs` a partir de claim configurável, com teste em `backend/tests/Dokpod.Bff.Tests/`. — `Bff:Session:DisplayGroupClaim` + `BffSessionResponseTests` (27 testes do BFF verdes); documentado em `docs/bff.md`.
- [x] T025 [US4] Renderizar `features/environments` e `features/containers` legados dentro do shell, ajustando apenas estilos para tokens novos, sem mudar comportamento. — **substituída**: as telas legadas foram trocadas pelas telas novas de 008/010 conforme a decisão de 2026-10-13 ("substituindo o que já existir"); rota `/environments/{id}/containers` redireciona para a lista unificada.

## Fase 6: Verificação

- [x] T026 [US2] Catálogo `/dev/componentes` em `frontend/web/src/app/features/dev-catalog/`, excluído do build de produção, com verificação automatizada de contraste dos pares de token usados (FR-009). — catálogo `/dev/componentes` só em modo dev; contraste automatizado em `design-system/contrast.spec.ts` (26 pares, todos ≥ AA).
- [x] T027 [P] Adicionar a ferramenta de acessibilidade aprovada em T001 (candidata: `axe-core` via integração Python para Playwright, MPL-2.0, somente teste) ao runner `frontend/tests/e2e/`, com versão fixada e registro em `docs/licenciamento.md`. — `axe-core` 4.14.0 (MPL-2.0) devDependency exata, injetado pelo runner `frontend/tests/e2e/mock/run-mock-e2e.sh`.
- [x] T028 [US1] Playwright em `frontend/tests/e2e/` (pytest): navegação do menu por teclado, estágio persistido na URL, "Em breve", 404, larguras 1920/1440/1100/390/320px sem rolagem horizontal, zoom 200%, auditoria de acessibilidade sem violações sérias ou críticas (SC-003). — `frontend/tests/e2e/mock_ui/test_007_shell.py` (pytest + Playwright pela borda nginx com respostas simuladas): 15 cenários verdes, axe sem violações sérias/críticas.
- [ ] T029 [US4] Executar E2E legados (ambientes, inventário, lifecycle) pela borda nginx; registrar resultado. — **NOT RUN** em 2026-10-13: stack Keycloak/BFF/API real indisponível neste host; a suíte `test_authenticated_smoke.py` é reescrita para as telas novas na spec 010.
- [x] T030 Loop de verificação visual (1440px e 390px) da moldura, entrada, 404, "Em breve" e catálogo; comparar com os boards do canvas; guardar capturas finais lado a lado e registrar a revisão humana (SC-002). — capturas lado a lado app × board em `artifacts/e2e/mock/visual/` (1440/390 px); diferenças da lista de containers registradas para a 010.
- [x] T031 Medir bundle inicial e tempo até shell interativo contra a linha de base de T005 (SC-006); registrar em [plan.md](plan.md). — bundle e tempo registrados em [plan.md](plan.md); SC-006 ajustado para orçamento absoluto.

## Dependências e Ordem de Execução

T001 -> T002 (gate humano) -> T003 -> T004 -> T005 -> T006-T009 -> T010-T017 (paralelas) -> T018-T020 -> T021-T025 -> T026-T031 (T027 antes de T028). As specs 008-014 dependem de T021-T023 e dos componentes das Fases 3 e 4.
