# Tarefas: Redes

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/networks-api.md), [quickstart.md](quickstart.md).

**Estado**: `in-progress` - implementação iniciada em 2026-10-15.

## Fase 1: Contrato e domínio

- [ ] T001 Proto aditivo (D-007) e tabela de alocação em `specs/README.md`.
- [ ] T002 Domínio: identificador e nome de rede, redes do sistema, comando `RemoveNetwork` (parâmetro e hash), scope `network:delete`, ação de auditoria `network.remove`; testes.

## Fase 2: Agente

- [ ] T003 Engine: `ListNetworksAsync`, conexões de rede na lista de containers, `RemoveNetworkAsync`; testes com HTTP simulado.
- [ ] T004 Builder do snapshot de redes e publicação por capability (sessão, eventos `network`, mudanças de conexão, pedido de snapshot); testes.
- [ ] T005 Execução do `REMOVE_NETWORK` (conferência de uso, sistema, códigos); testes.

## Fase 3: Plano de controle

- [ ] T006 Migration `202610150004_NetworkInventory` com Down; store PostgreSQL (projeção, revisão, busca, resumo, detalhe por nome); testes em PostgreSQL real.
- [ ] T007 Sessão gRPC: capabilities, páginas, revisão no heartbeat, invalidação `networks`; capability no despacho de comandos.
- [ ] T008 `NetworkRemovalService` e endpoints de lista, detalhe e remoção; OpenAPI e cliente; auditoria no console.

## Fase 4: Console

- [ ] T009 Tela Operação → Redes (filtros na URL, resumo, lista, detalhe) e link no detalhe do container; Vitest.
- [ ] T010 Remoção individual e em lote (confirmação, justificativa, acompanhamento e retomada); Vitest.

## Fase 5: Verificação

- [ ] T011 E2E simulada (`test_019_*.py`) com axe.
- [ ] T012 Carga (SC-005) em PostgreSQL real.
- [ ] T013 Stack real Podman: quickstart, SC-001 a SC-004.
- [ ] T014 Documentação (`docs/seguranca.md`, `docs/frontend.md`, `docs/configuracao-keycloak.md`) e evidências.
