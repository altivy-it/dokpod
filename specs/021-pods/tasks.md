# Tarefas: Pods

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [contracts/](contracts/pods-api.md), [quickstart.md](quickstart.md).

**Estado**: `in-progress` - implementação iniciada em 2026-10-16.

## Fase 1: Contrato e domínio

- [ ] T001 Proto aditivo (D-005) e tabela de alocação.
- [ ] T002 Domínio: ações de pod, scopes, hash, auditoria `pod.*` (categoria `pods`), saída com containers que falharam; testes.

## Fase 2: Agente

- [ ] T003 Engine: `/libpod/pods/json`, ação de pod com releitura e containers fora do estado; Docker sem pods; testes.
- [ ] T004 Pod do agente (identificador próprio), builder do snapshot e publicação por capability; testes.
- [ ] T005 Execução do `POD_ACTION` (recusa do pod do agente, códigos); testes.

## Fase 3: Plano de controle

- [ ] T006 Migration `202610160001_PodInventory` com Down; store (projeção, busca, resumo, filtro por container); testes em PostgreSQL real.
- [ ] T007 Sessão gRPC (capabilities, páginas, revisão, invalidação `pods`, comando e `failed_containers`).
- [ ] T008 `PodActionService` e endpoints; OpenAPI e cliente; auditoria no console.

## Fase 4: Console

- [ ] T009 Tela Operação → Pods (lista, filtros na URL, resumo, detalhe) e pod no detalhe do container; Vitest.
- [ ] T010 Ações por pod e em lote (confirmação, justificativa, acompanhamento, retomada, containers que falharam); Vitest.

## Fase 5: Verificação

- [ ] T011 E2E simulada (`test_021_*.py`) com axe.
- [ ] T012 Carga (SC-005) em PostgreSQL real.
- [ ] T013 Stack real Podman: quickstart, SC-001 a SC-004.
- [ ] T014 Documentação e evidências.
