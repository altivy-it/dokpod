# Tarefas: Volumes

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/volumes-api.md), [quickstart.md](quickstart.md).

**Estado**: `done` - implementada e validada em 2026-10-15.

## Fase 1: Contrato e domínio

- [x] T001 Proto aditivo (D-007), compatibilidade N/N-1 e tabela de alocação em `specs/README.md`. Evidência: `agent.proto` só aditivo (campos, mensagens e valores de enum novos); o servidor só usa inventário e remoção de volumes quando o agente anuncia as capabilities 16/17 (`AgentSupports`, `volumesAccepted`); suítes de transporte aprovadas com as mudanças.
- [x] T002 Domínio: nome de volume, comando `RemoveVolume` (parâmetro e hash), scope `volume:delete`, ação de auditoria `volume.remove`; testes. Evidência: `VolumeDomainTests` (domínio: 129 aprovados).

## Fase 2: Agente

- [x] T003 Engine: `ListVolumesAsync`, montagens na lista de containers, `RemoveVolumeAsync`, `GetVolumeUsageAsync`; testes com HTTP simulado. Evidência: `VolumeAgentTests` (lista sem `Mountpoint`, 204/404/409/500 na remoção, formatos Docker e Podman do `system/df`).
- [x] T004 Builder do snapshot de volumes e publicação por capability (sessão, eventos `volume`, mudanças de montagem, pedido de snapshot); testes. Evidência: `VolumeAgentTests` (páginas, limites, revisão monotônica); agente: 106 aprovados.
- [x] T005 Execução do `REMOVE_VOLUME` (conferência de uso, sem force, códigos) e consulta `VOLUME_USAGE`; testes. Evidência: `VolumeAgentTests` (container parado montando o volume → `volume_in_use` sem chamar o engine).

## Fase 3: Plano de controle

- [x] T006 Migration `202610150003_VolumeInventory` com Down; store PostgreSQL (projeção, revisão, busca, resumo); testes em PostgreSQL real. Evidência: Up/Down/Up no banco descartável; `VolumesIntegrationTests` (revisão obsoleta, busca em nome/label/container, curinga escapado, resumo, detalhe).
- [x] T007 Sessão gRPC: capabilities, páginas, revisão no heartbeat, invalidação `volumes`; capability no despacho de comandos. Evidência: build e `TransportBoundaryTests`; sem projeção (sem banco) o servidor não confirma as capabilities de volume.
- [x] T008 `VolumeRemovalService` (scope, registro, capability, sessão, justificativa da política) e endpoints de lista, detalhe, uso e remoção; OpenAPI e cliente. Evidência: `VolumeRemovalServiceTests` (achou a fila recusando o tipo 9, corrigido); `CommandStore_PersistsRemoveVolumeAsHostCommandWithParameters`; contrato OpenAPI e cliente regenerado.

## Fase 4: Console

- [x] T009 Tela Operação → Volumes (filtros na URL, resumo, lista, detalhe com uso sob demanda) e link no detalhe do container; Vitest. Evidência: `volumes.spec.ts` (11 testes); Vitest: 150 aprovados; bundle inicial 418,95 kB.
- [x] T010 Remoção individual e em lote (confirmação reforçada, justificativa, acompanhamento e retomada); Vitest. Evidência: `volumes.spec.ts` (lote, limite de 50, reenvio com a mesma chave, retomada sem reenvio, confirmação por nome/quantidade).

## Fase 5: Verificação

- [x] T011 E2E simulada (`test_018_*.py`) com axe. Evidência: `run-mock-e2e.sh`: suíte completa aprovada (84), com `test_018_volumes.py` (5) e axe sem violações sérias/críticas; o teste de "Em breve" da spec 007 passou a usar Redes.
- [x] T012 Carga (SC-005) em PostgreSQL real. Evidência: SC-005: p95 de 22 ms na primeira página com 5.040 volumes em 56 hosts (meta: 2 s).
- [x] T013 Stack real Podman: quickstart, SC-001 a SC-004. Evidência: stack real Podman (migration, imagens novas conferidas por id, scope criado e concedido). SC-001: 14 de 14 volumes e 4 de 4 em uso iguais ao engine; SC-002: 23 volumes e a montagem na projeção em 8 s, e volume liberado 7,6 s após remover o container; SC-003: remoção aceita pela API logo após criar um container no volume → `volume_in_use` no agente, volume intacto; container parado mantém "em uso"; SC-004: 20 volumes removidos em 3,2 s com 4 interações; auditoria `volume.remove` com ator e justificativa. SC-001 em Docker **NOT RUN** (sem Docker nesta máquina; coberto por testes do cliente Docker).
- [x] T014 Documentação (`docs/seguranca.md`, `docs/frontend.md`, `docs/configuracao-keycloak.md`) e evidências. Evidência: `docs/seguranca.md` (threat model), `docs/frontend.md`, `docs/configuracao-keycloak.md`; scripts de Keycloak com `volume:delete`.

## Achados da validação na stack real

- O gateway local da plataforma de identidade usa `proxy_intercept_errors on` no `server` e troca o corpo de **toda** resposta 4xx da API do Dokpod por uma página HTML; o status chega certo, mas o `code` do problema (`volume_in_use`, `justification_required`...) se perde. Não é código do Dokpod: corrigido em 2026-10-15 com `proxy_intercept_errors off` na rota `/dokpod/(bff|api/v1|hubs...)` de `platforms/identity/gateway/conf.d/identity.conf` (repositório `architecture-knowledge-base`, alteração local ainda sem commit); conferido na stack real: 409 chega como `application/problem+json` com `code` `volume_in_use`.

## Atualização da spec 023 (2026-10-17)

- **Docker real**: volumes iguais ao `docker` CLI; remoção de volume sem uso pelo console e recusa de volume em uso (`volume_in_use`).
