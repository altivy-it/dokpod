# Pesquisa: Fundação do Console Operacional e Design System Próprio

**Data**: 2026-10-12 | **Spec**: [spec.md](spec.md)

## D-001 Biblioteca de UI

- **Decisão**: Componentes Angular próprios, derivados do Altivy, em `frontend/web/src/app/design-system/`.
- **Justificativa**: Escolha explícita do solicitante (2026-10-12). Os mockups dependem de moldura, vidro, grid e gráficos que PO UI não oferece sem reescrita de estilos.
- **Alternativas consideradas**: PO UI tematizado (regra vigente, menor fidelidade); adotar o bundle React do Altivy via wrapper (mistura de frameworks, rejeitado); Angular CDK como base de acessibilidade (MIT, mantido pela equipe Angular): **recomendado avaliar** para `overlay`, `a11y` (FocusTrap, LiveAnnouncer, ListKeyManager) e `scrolling`, pois reduz o risco do grid e do drawer. Exige aprovação humana como dependência; registrar no ADR.
- **Estado**: requer aprovação humana via ADR 2026-0005.

## D-002 Conflito com instruções e documentação vigentes

- **Evidência**: `.github/instructions/frontend-po-ui.instructions.md` (PO UI exclusivo); `frontend-angular.instructions.md` e `docs/frontend.md` ("Evite ... grandes raios, glassmorphism, gradientes decorativos"); `frontend-visual-verification.instructions.md` (critério "usa exclusivamente componentes PO UI"); `architecture.instructions.md:31`, `testing.instructions.md:17`, `agents/angular-engineer.agent.md`, `agents/solution-architect.agent.md:27`, `agents/code-reviewer.agent.md:19`, `COPILOT_GUIDE.md:191-193` e o servidor MCP `po-ui` em `.vscode/mcp.json`.
- **Decisão**: Após a aprovação humana do ADR, substituir por `frontend-design-system.instructions.md` e atualizar todos os demais no mesmo PR de governança; nenhuma instrução muda antes da aprovação. A constituição não cita PO UI, então não exige emenda constitucional.
- **Estado**: pendente de aprovação humana.

## D-003 Fontes

- **Decisão**: Outfit (400/500/600) e JetBrains Mono (400/500) em `woff2`, versionados em `frontend/libs/design-system/fonts/` com `OFL.txt`; `font-display: swap`; subset latin + latin-ext.
- **Justificativa**: CSP do nginx e operação offline impedem Google Fonts em runtime. OFL 1.1 é compatível com distribuição AGPL-3.0-only (fontes como dados).
- **A verificar**: versão exata e hash dos arquivos; inclusão em `docs/licenciamento.md`.

## D-004 Ícones

- **Decisão**: Sprite SVG próprio com os glifos usados pelos mockups (gauge, box, grid, package, columns, link, monitor, inbox, alert, shield, settings, refresh, download, plus, play, pause, trash, list, command, eye, info, chart-line, chevron-down, check, x, key, flag, tool, activity, search, bell, menu), geometria copiada do Lucide (ISC) com atribuição.
- **Alternativas**: pacote `lucide-angular` (dependência runtime extra, rejeitado).

## D-005 Gráficos

- **Decisão**: Componentes SVG próprios: linha (múltiplas séries, área opcional, linha de referência, eixo y fixo, tooltip por teclado/mouse) e rosca (itens, centro, legenda). Tabela de dados oculta acessível como alternativa textual.
- **Alternativas**: bibliotecas de gráficos (dependência pesada, estética divergente).

## D-006 Grid de dados

- **Decisão**: Implementar `dok-data-grid` com o subconjunto usado em `Containers.dc.html`: colunas tipadas (texto, número com casas/sufixo, status, mono), coluna travada, ordenação, filtro por coluna `filterable`, agrupamento opcional por coluna `groupable` (P3), seleção múltipla com checkbox, ações por linha em menu, densidade, busca, paginação por cursor delegada à fachada, exportação CSV da página carregada e rodapé "atualizado em".
- **Semântica**: `role="grid"` com navegação por setas apenas se a medição de usabilidade justificar; caso contrário `table` nativa com cabeçalhos clicáveis e checkboxes, mais simples e robusta para leitores de tela. **Recomendação**: `table` nativa.

## D-007 Estágio global

- **Decisão**: Query param `stage` (`production`, `staging`, `development`) mantido por `StageFilterService` (Signal + Router). Ausente = todos.
- **Dependência**: o atributo de estágio do ambiente é criado na spec 008; até lá, o atalho aparece desabilitado com tooltip.

## D-008 Grupo de exibição do usuário

- **Decisão**: O BFF já retorna `name`/`subject`; incluir campo opcional `displayGroup` lido de claim configurável (`Bff:Session:DisplayGroupClaim`), nunca a lista completa de grupos.
- **Estado**: opcional; sem configuração o campo não aparece.

## D-009 Verificação automatizada de acessibilidade

- **Evidência**: O runner E2E é Python (`frontend/tests/e2e/playwright.config.py`, pytest) e não tem ferramenta de acessibilidade instalada.
- **Decisão proposta**: `axe-core` (MPL-2.0) via integração Python para Playwright, somente como dependência de teste, com versão fixada; falha em violações `serious`/`critical` (SC-003).
- **A verificar**: compatibilidade da MPL-2.0 com a distribuição AGPL-3.0-only (dependência de teste, não distribuída na imagem); manutenção do pacote de integração Python; registro em `docs/licenciamento.md`.
- **Alternativas**: auditoria manual com leitor de tela apenas (não automatizável); Lighthouse (cobertura menor, mais lento).
- **Estado**: requer aprovação humana via ADR 2026-0005.
