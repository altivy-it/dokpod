# Plano de Implementação: Redes

**Branch**: `019-redes` | **Data**: 2026-10-15 | **Spec**: [spec.md](spec.md)

**Estado**: `approved` - plano derivado da spec aprovada em 2026-10-15.

## Resumo

Inventário de redes por host publicado pelo agente (capability nova, snapshots paginados com revisão no heartbeat, no desenho das specs 012 e 018), projeção no plano de controle com "em uso" calculado das conexões de rede dos containers (inclusive parados) e marcação das redes do sistema, tela Operação → Redes com detalhe e remoção de redes sem uso por comando durável, com scope `network:delete`, confirmação e justificativa conforme a política do estágio.

## Contexto Técnico

**Linguagem/Versão**: .NET 10 (agente e plano de controle), protobuf v1 (aditivo), EF Core com scripts SQL embutidos, OpenAPI 3.1, Angular 22.

**Dependências Principais**: existentes; nenhuma nova.

**Armazenamento**: migration `202610150004_NetworkInventory`: `inventory_network_projections` (revisão por host) e `inventory_networks` (por host e identificador, sub-redes, labels e containers em `jsonb`); comando tipo 10 com parâmetro `remove_network`; `network:delete` nos registros que já permitem `container:delete` (D-006).

**Testes**: domínio (identificador e nome de rede, redes do sistema, comando), agente (conexões de containers parados, builder, remoção recusada em uso), PostgreSQL real (projeção, busca por sub-rede, carga de 2.000 redes), aplicação (scope, sistema, Swarm, capability, justificativa), Vitest, E2E simulada e stack real (Podman).

**Plataforma-alvo**: agente (Docker e Podman pela API compatível) e plano de controle; contrato do agente aditivo (N/N-1).

**Metas de desempenho**: rede nova ou removida visível em até 30 s (SC-002); primeira página em até 2 s no p95 com 2.000 redes em 56 hosts (SC-005).

**Restrições**: nunca desconectar containers para remover; redes do sistema e Swarm nunca removidas; opções do driver não publicadas; agente N-1 sem a capability não recebe comandos novos.

## Verificação da Constituição

| Princípio | Situação |
| --- | --- |
| I. Documentação em pt-BR | OK. |
| II. Engine local como fonte | OK: inventário vem do engine pelo agente; remoção só pelo agente, que confere o uso antes. |
| III. Identidade externa e menor privilégio | OK: `network:delete` por host no Keycloak; leitura com `environment:read`. |
| IV. Operações duráveis | OK: remoção pelo pipeline de comandos (journal-before-ack, idempotência, reconciliação). |
| V. Modularidade e compatibilidade | OK: campos e enums novos no proto; servidor N-1 ignora a capability; agente N-1 fica "inventário indisponível". |
| VI. Qualidade verificável | OK: testes por camada, PostgreSQL real, E2E simulada e stack real Podman. Docker real `NOT RUN` nesta máquina. |

## Estrutura do Projeto

```text
contracts/agent/v1/agent.proto                           # NetworkSnapshotPage, RemoveNetworkParameters, enums
backend/libs/Dokpod.Domain/Networks/                     # identificador, nome, redes do sistema, códigos
backend/libs/Dokpod.Agent.Application/Networks/          # builder do snapshot
backend/libs/Dokpod.Agent.Infrastructure/Engines/Docker/ # /networks, NetworkSettings dos containers, DELETE /networks/{id}
backend/apps/Dokpod.Agent/AgentControlWorker.cs          # publicação por capability
backend/libs/Dokpod.ControlPlane.Application/Networks/   # projeção, busca, remoção
backend/libs/Dokpod.ControlPlane.Infrastructure/         # store PostgreSQL + migration 202610150004
backend/apps/Dokpod.ControlPlane.Api/                    # sessão gRPC, endpoints
contracts/openapi/dokpod-control-plane.v1.yaml
frontend/web/src/app/features/networks/                  # tela, detalhe, remoção em lote
frontend/tests/e2e/mock/ e mock_ui/test_019_*.py
```

## Rastreabilidade

| Requisito | Desenho |
| --- | --- |
| FR-001, FR-010 | Capability `NETWORK_INVENTORY`, `NetworkSnapshotPage`, `Heartbeat.network_revision`, `SnapshotScope.NETWORKS` (D-001, D-002). |
| FR-002, FR-003, FR-004 | `INetworkInventoryStore` (substituição por snapshot), busca com resumo e cursor, detalhe por identificador ou nome (D-003). |
| FR-005 a FR-008 | `CommandKind.REMOVE_NETWORK` + `RemoveNetworkParameters`, capability `NETWORK_REMOVE`, `NetworkRemovalService` (D-004, D-005). |
| FR-009 | Rota `/redes`, link no detalhe do container. |
