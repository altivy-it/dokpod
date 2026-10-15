# Tarefas: Auditoria

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/audit-api.md), [quickstart.md](quickstart.md).

**Estado**: `in-progress` - implementação iniciada em 2026-10-15.

## Fase 1: Fundação

- [ ] T001 Domínio: `ActorName` no `AuditEvent`, ação `audit.exported` e detalhe `count`, categorias de ação; testes.
- [ ] T002 Migration `202610150001_AuditConsole` (coluna, sobrecarga da função, índice) com Down; writer grava o nome via `IAuditActorNameProvider`; testes em PostgreSQL real (gravação, hash, rollback).
- [ ] T003 [P] OpenAPI (contracts/audit-api.md), cliente regenerado e `OpenApiContractTests`.

## Fase 2: Jornadas 1 a 3 - consultar, filtrar e detalhar (P1)

- [ ] T004 Leitor PostgreSQL (`IAuditEventReader`): período, filtros, cursor, relacionados; testes em PostgreSQL real.
- [ ] T005 Serviço de consulta com `audit:read` por host, resumo e detalhe com 404 sem acesso; testes.
- [ ] T006 Endpoints `summary`, `events`, `events/{id}`; provedor do nome a partir do token.
- [ ] T007 Frontend: tela Governança → Auditoria (filtros na URL, lista paginada, drawer com relacionados e links), menu pela API e "Ver auditoria" no detalhe do container; Vitest.

## Fase 3: Jornada 4 - exportação (P2)

- [ ] T008 Exportação CSV (até 10.000, neutralização de fórmulas) e evento `audit.exported` por host; testes.
- [ ] T009 Frontend: botão Exportar com o filtro atual; Vitest.

## Fase 4: Verificação

- [ ] T010 E2E simulada (`test_017_*.py`) com axe.
- [ ] T011 Carga (SC-003, 1 milhão de eventos em 56 hosts) e exportação (SC-005) em PostgreSQL real.
- [ ] T012 Stack real: quickstart completo; SC-001, SC-002 e SC-004.
- [ ] T013 Documentação (`docs/seguranca.md`, `docs/frontend.md`, runbook de auditoria) e evidências nesta lista.
