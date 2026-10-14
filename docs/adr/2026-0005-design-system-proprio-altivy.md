# ADR 2026-0005: Design system próprio derivado do Altivy para o console

- **Status:** accepted
- **Data:** 2026-10-12
- **Origem:** IA assistida, a pedido do solicitante
- **Revisor humano:** zocatel (solicitante e responsável pelo produto)
- **Aprovação:** 2026-10-13, decisão explícita do solicitante na sessão de trabalho: "abandone o POUI e implemente o novo Design" e "Execute tudo baseado no novo design, ele é a fonte da verdade de UI e de funcionalidade de backend".

## Contexto

Os mockups do console operacional (`docs/design/app`) usam o design system Altivy: tema escuro, superfícies de vidro, raios grandes, tipografia Outfit, grid de dados, gráficos e drawer próprios. As instruções vigentes (`.github/instructions/frontend-po-ui.instructions.md`) tornam PO UI a única biblioteca de UI aprovada, e `docs/frontend.md` e `frontend-angular.instructions.md` proíbem glassmorphism e raios grandes. PO UI não está instalado em `frontend/web`.

Em 2026-10-12 o solicitante escolheu implementar componentes Angular próprios, fiéis ao mockup, em vez de PO UI tematizado.

## Decisão proposta

1. O Dokpod mantém um design system próprio em `frontend/web/src/app/design-system/` e `frontend/libs/design-system/`, derivado dos tokens e diretrizes do Altivy, sem biblioteca de componentes de terceiros.
2. PO UI deixa de ser exigido; `frontend-po-ui.instructions.md` é substituído por `frontend-design-system.instructions.md`.
3. Vidro, raios e tipografia seguem o Altivy, com limites: vidro apenas em moldura, cards e overlays; conteúdo denso em painel opaco; fallback opaco para `prefers-reduced-transparency`; WCAG 2.2 AA obrigatório.
4. Dependências admitidas, sujeitas a verificação de licença e versão fixada: fontes Outfit e JetBrains Mono (OFL 1.1), versionadas no repositório. A geometria dos ícones vem do kit Altivy (`docs/design/app/project/ds/altivy`), traço no estilo Lucide desenhado no próprio kit, sem dependência externa. Angular CDK **não** é adotado: foco preso, teclado e overlays são implementados nos componentes próprios, com testes.
5. Acessibilidade verificada automaticamente no E2E (pytest + Playwright) com `axe-core` (MPL-2.0), somente como dependência de teste, com versão fixada; falha em violações `serious`/`critical`.
6. As instruções em `.github/`, os agentes, `docs/frontend.md` e `.vscode/mcp.json` só são alterados depois da aprovação deste ADR, em um único PR de governança.

## Consequências

- Maior custo de manutenção de componentes (grid, gráficos, drawer) em troca de fidelidade ao mockup.
- Acessibilidade passa a ser responsabilidade integral do Dokpod e é verificada por testes.
- Especificação de referência: [specs/007-fundacao-console-design-system](../../specs/007-fundacao-console-design-system/spec.md).

## Decisões complementares (2026-10-13)

- Angular CDK: não adotado.
- `axe-core`: aprovado somente como dependência de teste do runner E2E, com versão fixada.
- As duas decisões seguem a recomendação registrada na análise da spec 007, sob a delegação explícita do solicitante para executar com as recomendações.

## Pendências

Verificação de licenças das fontes registrada em `docs/licenciamento.md` (tarefa T006 da spec 007).
