# Tarefas: Volumes

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/volumes-api.md), [quickstart.md](quickstart.md).

**Estado**: `in-progress` - implementação iniciada em 2026-10-15.

## Fase 1: Contrato e domínio

- [ ] T001 Proto aditivo (D-007), compatibilidade N/N-1 e tabela de alocação em `specs/README.md`.
- [ ] T002 Domínio: nome de volume, comando `RemoveVolume` (parâmetro e hash), scope `volume:delete`, ação de auditoria `volume.remove`; testes.

## Fase 2: Agente

- [ ] T003 Engine: `ListVolumesAsync`, montagens na lista de containers, `RemoveVolumeAsync`, `GetVolumeUsageAsync`; testes com HTTP simulado.
- [ ] T004 Builder do snapshot de volumes e publicação por capability (sessão, eventos `volume`, mudanças de montagem, pedido de snapshot); testes.
- [ ] T005 Execução do `REMOVE_VOLUME` (conferência de uso, sem force, códigos) e consulta `VOLUME_USAGE`; testes.

## Fase 3: Plano de controle

- [ ] T006 Migration `202610150003_VolumeInventory` com Down; store PostgreSQL (projeção, revisão, busca, resumo); testes em PostgreSQL real.
- [ ] T007 Sessão gRPC: capabilities, páginas, revisão no heartbeat, invalidação `volumes`; capability no despacho de comandos.
- [ ] T008 `VolumeRemovalService` (scope, registro, capability, sessão, justificativa da política) e endpoints de lista, detalhe, uso e remoção; OpenAPI e cliente.

## Fase 4: Console

- [ ] T009 Tela Operação → Volumes (filtros na URL, resumo, lista, detalhe com uso sob demanda) e link no detalhe do container; Vitest.
- [ ] T010 Remoção individual e em lote (confirmação reforçada, justificativa, acompanhamento e retomada); Vitest.

## Fase 5: Verificação

- [ ] T011 E2E simulada (`test_018_*.py`) com axe.
- [ ] T012 Carga (SC-005) em PostgreSQL real.
- [ ] T013 Stack real Podman: quickstart, SC-001 a SC-004.
- [ ] T014 Documentação (`docs/seguranca.md`, `docs/frontend.md`, `docs/configuracao-keycloak.md`) e evidências.
