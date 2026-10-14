# Plano de Implementação: Visão Geral, Hosts, Eventos e Inventário Enriquecido

**Branch**: `007-console-operacional` | **Data**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Estado**: `implemented` - ver [tasks.md](tasks.md).

## Resumo

Backend primeiro: estender o contrato do agente (descritor de host/engine, campos enriquecidos de container, lote de eventos do engine), adicionar observação de eventos e adapter Podman (API compatível) no agente, persistir estágio, descritor, conexão e eventos no PostgreSQL, expor resumo, hosts e eventos na API v1 e emitir invalidações. Depois frontend: Visão geral e Hosts sobre o design system da spec 007, substituindo `features/environments`.

## Contexto Técnico

**Linguagem/Versão**: .NET 10 (API, aplicação, domínio, infraestrutura, agente), Protobuf v1, OpenAPI 3.1, Angular 22.

**Dependências Principais**: Existentes (gRPC mTLS, EF Core/Npgsql, Keycloak PEP, SignalR, BFF). Nenhuma nova dependência.

**Armazenamento**: PostgreSQL schema do plano de controle: alteração de `environments` (estágio), novas tabelas `agent_descriptors`, `agent_connections`, `environment_events` (particionada por mês, retenção configurável por rotina nova), colunas novas em `inventory_containers`.

**Testes**: Domínio (mapeamento de situação, situação do host, comparação de versões), aplicação (resumo, eventos, autorização), contrato (OpenAPI, protobuf N/N-1), PostgreSQL real (migrations expand, partições, retenção), Docker e Podman reais (eventos, inventário enriquecido), Playwright pela borda nginx.

**Plataforma Alvo**: Docker Linux; Podman Linux rootless/rootful (qualificação formal em 006).

**Objetivos de Desempenho**: Resumo ≤ 300 ms p95 com 56 hosts/1.120 containers; evento visível ≤ 5 s; invalidação ≤ 1/s por host.

**Restrições**: Engine como fonte; somente agente acessa engine; N/N-1; expand-contract; falha fechada de autorização; sem secrets/logs/env em eventos.

**Escopo**: Ver spec. Fora: ack de eventos, tela Alertas, pods (atributo, evento e gestão), métricas (009), imagens (012).

## Verificação da Constituição

| Princípio | Situação | Observação |
| --- | --- | --- |
| II. Engine como fonte | OK | Eventos e descritor são observações do agente; projeção reconstruível. |
| III. Menor privilégio | OK | Todas as consultas filtram por `environment:read`; estágio e revogação por `environment:manage`. Eventos sanitizados. |
| IV. Operações duráveis | OK | Alteração de estágio é mutação auditada com antiforgery; não é comando ao agente. |
| V. Compatibilidade | OK | Tags protobuf novas; campos opcionais; migrations expand; capability nova `CAPABILITY_ENGINE_EVENTS`; `SessionEstablished.accepted_capabilities` garante N/N-1 nos dois sentidos (D-009), com teste de agente N contra servidor N-1. |
| VI. Qualidade | OK | Docker e Podman reais; Podman não qualificado por Docker. |
| Tabelas temporais | OK | `environment_events` particionada por mês, criação antecipada pela função de `202609220002_ScheduleTemporalPartitions`; retenção por rotina nova (D-004), porque a existente só cria partições. |

## Estrutura do Projeto

### Documentação

`spec.md`, `plan.md`, `research.md`, `data-model.md`, `contracts/openapi-delta.md`, `contracts/agent-proto-delta.md`, `quickstart.md`, `checklists/requirements.md`, `tasks.md`.

### Código-fonte afetado

```text
contracts/agent/v1/agent.proto                                   # campos/mensagens novas
contracts/openapi/dokpod-control-plane.v1.yaml                   # overview, hosts, events, stage

backend/libs/Dokpod.Domain/
├── Environments/EnvironmentRegistration.cs                      # Stage
├── Environments/EnvironmentStage.cs                             # novo
├── Hosts/AgentDescriptor.cs, HostSituation.cs, EngineVersion.cs # novos
├── Inventory/InventorySnapshot.cs                               # ContainerInventory enriquecido
├── Inventory/ContainerDisplayStatus.cs                          # mapeamento FR-006
└── Events/OperationalEvent.cs                                   # novo

backend/libs/Dokpod.ControlPlane.Application/
├── Hosts/HostQueryService.cs, AgentConnectionTracker.cs         # novos (último heartbeat em memória, varredura de conexões)
├── Overview/OverviewQueryService.cs                             # novo
├── Events/OperationalEventService.cs, IOperationalEventStore.cs # novos
└── Environments/EnvironmentRegistrationService.cs               # estágio

backend/libs/Dokpod.ControlPlane.Infrastructure/
├── Migrations/2026101200xx_*.cs                                 # expand: stage, descritor, conexão, eventos, colunas
├── Postgres*Store.cs                                            # novos/ajustados
└── Persistence/Configurations/*                                 # novos

backend/apps/Dokpod.ControlPlane.Api/
├── Agents/AgentControlService.cs                                # hello → descritor; eventos
├── Overview/OverviewEndpoints.cs, Events/EventEndpoints.cs, Environments/* (filtros e estágio)
└── Options/EngineSupportOptions.cs                              # versão mínima homologada

backend/libs/Dokpod.Agent.Application/
├── Engines/IContainerEngine.cs                                  # descritor de host, eventos, campos enriquecidos
└── Events/EngineEventPublisher.cs                               # dedup/agregação/limite
backend/libs/Dokpod.Agent.Infrastructure/
├── Engines/Docker/DockerEngineClient.cs                         # /events, inspect enriquecido
└── Engines/Podman/PodmanCompatEngineClient.cs                   # novo (API compatível)
backend/apps/Dokpod.Agent/AgentControlWorker.cs                  # loop de eventos

frontend/web/src/app/
├── data-access/ (cliente gerado + fachadas overview/hosts/events)
├── features/overview/   overview.page.*, overview.facade.ts
├── features/hosts/      hosts.page.*, hosts.facade.ts, host-register.drawer.*
└── features/environments/  (removido ao final)
```

**Decisão de Estrutura**: Novos módulos seguem as camadas existentes (Domínio → Aplicação → Infraestrutura → Host). O adapter Podman fica em `Engines/Podman` separado do Docker, como exige a arquitetura ("extensões Docker e Libpod ficam em adapters separados"); a parte comum usa a porta `IContainerEngine`.

## Fases

1. Contratos (proto + OpenAPI) e testes de contrato/N-1.
2. Domínio: estágio, descritor, situação do host, versão, situação do container, evento.
3. Persistência: migrations expand, stores, partições/retenção de eventos.
4. Agente: descritor de host, inventário enriquecido, eventos Docker, adapter Podman.
5. API: sessão grava descritor/conexão; endpoints overview/hosts/events/stage; invalidações.
6. Frontend: Visão geral, Hosts (cadastro/estágio/revogação), sino de notificações, contador Alertas; remoção de `features/environments`.
7. Verificação: Docker/Podman reais, PostgreSQL real, E2E e visual.

## Riscos

| Risco | Mitigação |
| --- | --- |
| Diferenças da API compatível do Podman | Capabilities anunciadas só após teste real; recusa explícita do resto. |
| Agente N contra servidor N-1 | `accepted_capabilities` (D-009); teste dedicado. |
| Falso "Desconectado" ou "Desatualizado" | Situação calculada por sessão e heartbeat em memória e por `inventory_revision`, não por `last_seen_at` nem por ausência de deltas (D-005). |
| Volume de eventos | Dedup, agregação e limite no agente; partição + retenção no banco. |
| Resumo caro com autorização por host | Reusar a decisão de autorização por ambiente com cache por requisição; agregação SQL por `environment_id` autorizado. |
