# Plano de Implementação: Volumes

**Branch**: `018-volumes` | **Data**: 2026-10-15 | **Spec**: [spec.md](spec.md)

**Estado**: `implemented` - plano derivado da spec aprovada em 2026-10-15; executado e integrado em 2026-10-15 (PR #23).

## Resumo

Inventário de volumes por host publicado pelo agente (capability nova, snapshots paginados com revisão no heartbeat, no mesmo desenho das imagens da spec 012), projeção no plano de controle com "em uso" calculado das montagens dos containers, tela Operação → Volumes com detalhe (tamanho sob demanda) e remoção de volumes sem uso por comando durável (capability e tipo de comando novos), com scope `volume:delete`, confirmação reforçada e justificativa conforme a política do estágio.

## Contexto Técnico

**Linguagem/Versão**: .NET 10 (agente e plano de controle), protobuf v1 (aditivo), EF Core com scripts SQL embutidos, OpenAPI 3.1, Angular 22.

**Dependências Principais**: existentes; nenhuma nova.

**Armazenamento**: migration `202610150003_VolumeInventory`: `volume_inventory` (por host e nome, com montagens em `jsonb`) e `volume_inventory_revisions`; inclusão de `volume:delete` nos registros que já permitem `container:delete` (D-006).

**Testes**: domínio (nome de volume, códigos), agente (builder de snapshot, montagens, remoção recusada em uso), PostgreSQL real (projeção, "em uso", busca, carga de 5.000 volumes), aplicação (comando, scope, capability, justificativa), Vitest, E2E simulada e stack real (Podman).

**Plataforma-alvo**: agente (Docker e Podman pela API compatível) e plano de controle; contrato do agente aditivo (N/N-1).

**Metas de desempenho**: volume novo ou removido visível em até 30 s (SC-002); primeira página em até 2 s no p95 com 5.000 volumes em 56 hosts (SC-005).

**Restrições**: nunca forçar remoção; nunca publicar o caminho do volume no host; volume em uso (inclusive por container parado) não é removido; agente N-1 sem a capability não recebe comandos novos.

## Verificação da Constituição

| Princípio | Situação |
| --- | --- |
| I. Documentação em pt-BR | OK. |
| II. Engine local como fonte | OK: inventário vem do engine pelo agente; remoção só pelo agente, que confere o uso antes. |
| III. Identidade externa e menor privilégio | OK: `volume:delete` por host no Keycloak; leitura com `environment:read`. |
| IV. Operações duráveis | OK: remoção pelo pipeline de comandos (journal-before-ack, idempotência, reconciliação). |
| V. Modularidade e compatibilidade | OK: campos e enums novos no proto (aditivos); servidor N-1 ignora a capability; agente N-1 fica "inventário indisponível". |
| VI. Qualidade verificável | OK: testes por camada, PostgreSQL real, E2E simulada e stack real Podman. Docker real `NOT RUN` nesta máquina. |

## Estrutura do Projeto

```text
contracts/agent/v1/agent.proto                         # VolumeSnapshotPage, RemoveVolumeParameters, enums
backend/libs/Dokpod.Domain/Volumes/                    # nome de volume, códigos de remoção
backend/libs/Dokpod.Agent.Application/Volumes/         # builder do snapshot e remoção
backend/libs/Dokpod.Agent.Infrastructure/Engines/Docker/ # /volumes, Mounts dos containers, DELETE /volumes/{name}, system df
backend/apps/Dokpod.Agent/AgentControlWorker.cs        # publicação por capability
backend/libs/Dokpod.ControlPlane.Application/Volumes/  # projeção, busca, remoção
backend/libs/Dokpod.ControlPlane.Infrastructure/       # store PostgreSQL + migration 202610150003
backend/apps/Dokpod.ControlPlane.Api/                  # sessão gRPC, endpoints
contracts/openapi/dokpod-control-plane.v1.yaml
frontend/web/src/app/features/volumes/                 # tela, detalhe, remoção em lote
frontend/tests/e2e/mock/ e mock_ui/test_018_*.py
```

## Rastreabilidade

| Requisito | Desenho |
| --- | --- |
| FR-001, FR-010 | Capability `VOLUME_INVENTORY`, `VolumeSnapshotPage`, `Heartbeat.volume_revision`, `SnapshotScope.VOLUMES` (D-001, D-002). |
| FR-002, FR-003 | `IVolumeInventoryStore` (substituição por snapshot), busca com resumo e cursor (D-003). |
| FR-004 | `ContainerQueryKind.VOLUME_USAGE` sob demanda (D-004). |
| FR-005 a FR-008 | `CommandKind.REMOVE_VOLUME` + `RemoveVolumeParameters`, capability `VOLUME_REMOVE`, `VolumeRemovalService` (D-005). |
| FR-009 | Rota `/volumes`, link no detalhe do container. |
