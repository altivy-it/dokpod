---
name: "Frontend Design System"
description: "Use ao criar, alterar ou revisar telas, formulários, tabelas, navegação e componentes visuais Angular do Dokpod com o design system próprio derivado do Altivy."
applyTo: "frontend/**"
---

# Design system próprio (Altivy)

- O Dokpod usa um design system próprio em `frontend/web/src/app/design-system/` (componentes) e `frontend/web/src/styles/` (tokens, fontes e utilitários), derivado dos tokens e componentes do Altivy em `docs/design/app/project/ds/altivy` (ADR 2026-0005). Não adote biblioteca de componentes de terceiros (PO UI, Material, PrimeNG, Bootstrap).
- Os mockups em `docs/design/app` são a fonte da verdade de layout, texto, estados e densidade. Antes de criar uma tela, abra o board correspondente e reproduza estrutura, hierarquia e cópia em pt-BR.
- Reutilize os componentes `dok-*` existentes antes de criar outro. Componente novo segue o padrão de classes `alt-*` do kit, tem testes de teclado/ARIA e entra no catálogo `/dev/componentes`.
- Cores, espaçamentos, raios, sombras e tipografia vêm somente de tokens (`--surface-base`, `--teal-light`, `--radius-control` etc.). Não use valores literais quando houver token.
- `teal-light` é o único acento. `teal` nunca é texto; `slate` não é texto corrido. Todo status leva palavra ou ícone além da cor.
- Vidro (`alt-glass`) apenas em moldura, cards e overlays. Conteúdo denso (grid, logs, JSON, tabelas) vai em `alt-panel` opaco. Há fallback opaco para `prefers-reduced-transparency` e navegadores sem `backdrop-filter`.
- Ícones vêm de `dok-icon` (geometria do kit Altivy, viewBox 24, traço 2). Não adicione outra biblioteca de ícones.
- Números com `tabular-nums` e formato pt-BR (`1.234,56`, `30/09/2026 14:37`), pelos pipes de `design-system/format`.
- WCAG 2.2 AA obrigatório: foco visível `2px solid teal-light`, teclado completo, `prefers-reduced-motion` respeitado e auditoria automatizada sem violações sérias ou críticas.

Consulte `frontend-angular.instructions.md`, `frontend-visual-verification.instructions.md` e `docs/frontend.md`.
