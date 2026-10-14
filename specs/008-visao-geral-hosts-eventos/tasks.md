# Tarefas: Visão Geral, Hosts, Eventos e Inventário Enriquecido

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/).

**Estado**: `draft` - aguardando revisão humana.

**Pré-requisitos**: Revisão humana e autorização; spec 007 com shell e componentes (T021-T023 de 007) antes da fase de frontend.

**Testes**: Obrigatórios; engine e PostgreSQL reais para efeitos e persistência; Podman real para US5.

**Numeração**: IDs T001-T026 preservados porque as specs 009-014 os referenciam; tarefas incluídas após a análise usam T027 em diante e aparecem na fase em que executam.

## Fase 1: Preparação

- [ ] T001 Submeter spec/plan/tasks à revisão humana; confirmar valores-limite propostos (heartbeat perdido, desatualização, retenção de eventos, limite de eventos) e registrar em [spec.md](spec.md).
- [ ] T002 [P] Atualizar `contracts/agent/v1/agent.proto` conforme [agent-proto-delta.md](contracts/agent-proto-delta.md) (incluindo `SessionEstablished.accepted_capabilities = 6`, D-009) e regenerar `Dokpod.Agent.Contracts`; testes de compatibilidade N/N-1 (mensagens antigas desserializam, tags preservadas) em `backend/tests/Dokpod.ControlPlane.Api.Tests/`.
- [ ] T003 [P] Atualizar `contracts/openapi/dokpod-control-plane.v1.yaml` conforme [openapi-delta.md](contracts/openapi-delta.md) (filtros novos em `listEnvironments`, sem `listHosts`; `setEnvironmentStage` sem `If-Match`); ajustar `OpenApiContractTests`; regenerar cliente em `frontend/web/src/app/data-access/generated/`.

## Fase 2: Fundação de domínio e persistência (bloqueante)

- [ ] T004 [P] [US2] Domínio: `EnvironmentStage`, `AgentDescriptor`, `EngineVersion` (parse/compare tolerante), `HostSituation` (ordem de avaliação de FR-004, incluindo `Disabled` e host estável sem deltas = `UpToDate`) em `backend/libs/Dokpod.Domain/` com testes em `backend/tests/Dokpod.Domain.Tests/`.
- [ ] T005 [P] [US4] Domínio: campos enriquecidos em `ContainerInventory` e `ContainerDisplayStatus` (FR-006) com testes de tabela cobrindo todas as combinações estado/saúde/código/OOM.
- [ ] T006 [P] [US3] Domínio: `OperationalEvent` (tipos, incluindo `events.dropped`; severidades; sanitização e limites de parâmetros; indicação de divergência de relógio > 5 min) com testes.
- [ ] T007 [US1] Migration expand em `backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/`: `environments.stage`, `agent_descriptors`, `agent_connections`, colunas novas de `inventory_containers`; configurações EF; teste de schema e rollback em PostgreSQL real.
- [ ] T008 [US3] Migration de `environment_events` particionada por mês, com criação antecipada pela função existente `dokpod_ensure_monthly_partitions`; função nova genérica `dokpod_apply_temporal_retention` (parametrizada por tabela e coluna; reutilizada pela 009) (D-004: remove partições inteiramente vencidas e apaga em lotes as linhas vencidas da partição do corte) e agendamento; opção `EventRetention:Period`; testes em PostgreSQL real de criação antecipada, rollover, retenção com período reduzido e inserções concorrentes não bloqueadas (SC-006).

## Fase 3: Agente (US4, US3, US5)

- [ ] T009 [US4] Estender `IContainerEngine` e `DockerEngineClient` (`backend/libs/Dokpod.Agent.Infrastructure/Engines/Docker/`) para descritor de host (nome, CPUs, memória, API, rootless) e inventário enriquecido (portas, criação, início, saúde, reinícios, código de saída, OOM); testes unitários e de integração Docker real.
- [ ] T010 [US3] Assinatura de eventos `/events` com reconexão, filtro de allowlist, mapeamento D-002, dedup, agregação (incluindo `die` após `oom` = um evento), limite e `dropped_count` em `backend/libs/Dokpod.Agent.Application/Events/EngineEventPublisher.cs`; envio de `EngineEventBatch` em `AgentControlWorker.cs` somente se `CAPABILITY_ENGINE_EVENTS` estiver em `accepted_capabilities`; anunciar a capability; testes com Docker real (OOM gera um único evento de erro, unhealthy, restart, remoção de volume) e medição da latência engine → envio.
- [ ] T011 [US5] Adapter `PodmanCompatEngineClient` em `backend/libs/Dokpod.Agent.Infrastructure/Engines/Podman/` (detecção, inventário, eventos, start/stop/restart/delete pela API compatível; sem pods) e seleção de adapter em `backend/apps/Dokpod.Agent/Program.cs`; capabilities somente comprovadas; testes de integração em Podman real rootless e rootful (novo projeto `backend/tests/Dokpod.Agent.Podman.IntegrationTests/`).
- [ ] T027 [US5] Infraestrutura E2E: adicionar a `deploy/e2e/docker-compose-dokpod.yaml` um agente em Podman rootless (host ou container com Podman aninhado, conforme viabilidade) com identidade própria, documentado em `deploy/e2e/README.md`; registrar se o runner de CI suporta Podman ou se a execução fica manual com evidência.

## Fase 4: API (US1-US4)

- [ ] T012 [US2] `AgentControlService` + `AgentConnectionTracker`: preencher `SessionEstablished.accepted_capabilities`; persistir descritor no `Hello`; gravar `connected`/início na abertura e no encerramento da sessão; último heartbeat e `inventory_revision` em memória, `last_seen_at` persistido ≤ 1/min só para exibição; eventos `agent.connected`/`agent.disconnected`/`engine.version_outdated`; invalidação `kind=host` e `kind=inventory` (≤ 1/s por host e tipo); aplicar campos enriquecidos na projeção; testes de aplicação e transporte.
- [ ] T013 [US3] `OperationalEventService` + `PostgresOperationalEventStore`: ingestão de `EngineEventBatch` (dedup por `source_key`; `events.dropped` a partir de `dropped_count`; marcação de divergência de relógio), eventos de comando a partir de `AgentCommandStatusService`; invalidação `kind=events` (≤ 1/s por host); testes com PostgreSQL real.
- [ ] T014 [US2] `EngineSupportOptions` com validação e `HostQueryService` (situação FR-004 a partir de sessão, heartbeat em memória e `inventory_revision`); filtros `stage`/`engine`/`situation`/`q` e campos novos em `GET /api/v1/environments` (sem filtros, comportamento atual preservado); testes de autorização negativa e de host estável sem deltas permanecendo "Em dia".
- [ ] T015 [US2] `PUT /api/v1/environments/{id}/stage` com `environment:manage`, idempotente (mesmo estágio sem mudança nem auditoria), auditoria com estágio anterior e novo; `stage` no cadastro; testes.
- [ ] T016 [US1] `OverviewQueryService` + `GET /api/v1/overview` agregando apenas hosts autorizados e habilitados; teste de desempenho com 56 hosts/1.120 containers em PostgreSQL real (SC-003).
- [ ] T017 [US3] `GET /api/v1/events` com filtros, cursor e autorização por host; testes negativos.
- [ ] T018 [US4] `listEnvironmentContainers` passa a retornar `displayStatus` e campos enriquecidos; teste de contrato.
- [ ] T028 [US2] Varredura de conexões em `AgentConnectionTracker`: na inicialização da API e a cada 60 s, marcar como desconectadas as conexões sem sessão ativa no `IAgentSessionStore`, gerando `agent.disconnected` uma única vez; testes de reinício do plano de controle com agente parado.

## Fase 5: Frontend (US1-US3)

**Teste independente**: Visão geral e Hosts pela borda nginx com dados sintéticos e reais.

- [ ] T019 [P] [US1] Fachadas `overview.facade.ts`, `hosts.facade.ts` (sobre `listEnvironments`), `events.facade.ts` em `frontend/web/src/app/data-access/` com Signals, cancelamento, estágio global e invalidação SignalR por `kind` (sem reordenar/mover foco); testes Vitest.
- [ ] T020 [US1] `features/overview/overview.page.*` conforme `Main.dc.html`: cabeçalho do estágio, ações condicionais, 6 KPIs (CPU/memória/imagens "indisponível" até 009/012), gráfico de recursos placeholder até 009, rosca por engine, card "Hosts e engines" (coluna CPU placeholder até 009), card "Eventos recentes"; estados carregando/vazio/erro/forbidden/desatualizado.
- [ ] T021 [US3] Formatação pt-BR dos eventos por tipo (textos do mockup, incluindo `events.dropped`), sinalização de divergência de relógio, painel do sino de notificações no shell e contador do item Alertas; testes.
- [ ] T022 [US2] `features/hosts/hosts.page.*` com `dok-data-grid` (estágio, engine, versão, SO/arquitetura, agente, situação incluindo "Desabilitado", containers, último contato), drawer de cadastro com estágio, alteração de estágio e revogação com confirmação; mover a lógica de `features/environments` e removê-la; rota `/hosts` no shell.
- [ ] T023 [US1] Playwright em `frontend/tests/e2e/` (pytest): Visão geral por estágio, contagens contra o engine com 2 hosts (Docker e Podman) e 30 containers em 6 situações (SC-001), eventos com latência engine → feed ≤ 5 s medida (SC-002), sino, hosts (cadastro/estágio/revogação), usuário sem permissão; acessibilidade com a ferramenta aprovada na 007 (T027 de 007).
- [ ] T024 Loop de verificação visual (1920, 1440, 1100, 390 e 320px) de Visão geral e Hosts, todos os estados; capturas lado a lado com `Main.dc.html` e revisão humana registrada, com o mesmo critério do SC-002 da 007.
- [ ] T029 [US1] Medir no Playwright o carregamento da Visão geral com 56 hosts e 1.120 containers sintéticos (≤ 2 s, SC-003) e registrar a evidência.

## Fase 6: Encerramento

- [ ] T025 Documentar versão mínima homologada e eventos em `docs/distribuicao.md`, `docs/arquitetura.md` (fluxo de eventos) e `docs/seguranca.md` (sanitização de eventos); atualizar runbook de partições com `environment_events`.
- [ ] T026 Validar agente N-1 contra servidor N (inventário e comandos) e agente N contra servidor N-1 (sessão mantida, nenhum payload novo enviado; SC-005) e registrar evidência para [005](../005-qualificacao-release-docker-linux/tasks.md); Podman para [006](../006-qualificacao-windows-podman/tasks.md).

## Dependências e Ordem de Execução

T001 -> T002/T003 -> T004-T008 -> T009-T011 (T011 pode seguir em paralelo após T009) e T027 -> T012-T018 e T028 -> T019-T024 e T029 -> T025-T026. Specs 009-014 dependem de T002 (incluindo `accepted_capabilities`), T007, T012 e T018.
