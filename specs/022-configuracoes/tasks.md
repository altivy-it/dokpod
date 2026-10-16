# Tarefas: Configurações

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/settings-api.md), [quickstart.md](quickstart.md).

**Estado**: `in-progress` - implementação iniciada em 2026-10-16.

- [ ] T001 Versão do produto (D-001) e teste de consistência; `GET /bff/version`.
- [ ] T002 Acesso, instalação e parâmetros no plano de controle (D-002 a D-004); `canEdit` da política de justificativa (D-005); testes, inclusive canário de segredo.
- [ ] T003 OpenAPI e cliente.
- [ ] T004 Tela Governança → Configurações (Políticas, Instalação, Retenções e limites) e item de menu por acesso; Vitest.
- [ ] T005 E2E simulada (`test_022_*.py`) com axe.
- [ ] T006 Stack real: quickstart, SC-001 a SC-004.
- [ ] T007 Documentação e evidências.
