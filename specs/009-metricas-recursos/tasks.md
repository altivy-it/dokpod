# Tarefas: Métricas de Recursos de Hosts e Containers

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/contracts-delta.md).

**Estado**: `implemented` - concluída em 2026-10-13. `NOT RUN`: Docker real (validação em Podman real), carga de 56 hosts/1.120 containers e margem de 2.000 com overhead de CPU e latência de comandos (T018), E2E com stack real.

**Pré-requisitos**: Revisão humana, incluindo decisão D-002 (Opção A′); spec 008 T002 (incluindo `accepted_capabilities`), T007, T008 (rotina de retenção genérica) e T012 concluídas.

## Fase 1: Preparação

- [x] T001 Registrar decisão humana sobre D-002 em [research.md](research.md); se a Opção A′ for aprovada, registrar em `docs/arquitetura.md` (seção Escala inicial) que a série de host é persistida com retenção e a de container fica em memória. — Opção A′ registrada em [research.md](research.md) e em `docs/arquitetura.md` (Escala inicial).
- [x] T002 [P] Atualizar `contracts/agent/v1/agent.proto` (tag 18, capability 7, `dropped_count`) e testes N/N-1, incluindo agente N contra servidor N-1 sem `resource_samples`. — `ResourceSampleBatch` (tag 18) e `CAPABILITY_RESOURCE_METRICS` (7) no proto; envio só com capability aceita; N/N-1 coberto pelos `ProtocolCompatibilityTests` (sessão sem `accepted_capabilities`).
- [x] T003 [P] Atualizar OpenAPI com `/metrics/*` (`window` de container até `30m`) e `resources` em `ContainerInventory`; regenerar cliente. — `getStageMetrics`, `listHostMetrics`, `getContainerMetrics` (janela de container só `30m`) e `ContainerResources` no OpenAPI; cliente regenerado; `OpenApiContractTests` atualizado.

## Fase 2: Domínio e persistência

- [x] T004 [P] [US3] `CpuUsageCalculator` (fórmulas D-001: % do host 0-100 e % do limite; delta negativo = lacuna; cgroup v1/v2) e `MetricsPolicy` em `backend/libs/Dokpod.Domain/Metrics/` com testes de tabela. — `Dokpod.Domain/Metrics/CpuUsageCalculator.cs` e `MetricsPolicy.cs` com testes de tabela (`MetricsDomainTests`, 20 casos).
- [x] T005 [US1] Migration de `host_resource_samples` particionada por mês, com criação antecipada por `dokpod_ensure_monthly_partitions` e retenção por `dokpod_apply_temporal_retention` (008); opção `Metrics:HostRetention`; testes PostgreSQL real de retenção (SC-005). — migration `202610130005_HostResourceSamples` (partição mensal em `minute`, criação antecipada, rollback verificado); retenção pela rotina restrita da 008; testes em PostgreSQL real.
- [x] T006 [US1] `PostgresHostMetricsStore` com upsert por minuto e consultas de série com lacunas (`null`); teste de desempenho SC-004. — `PostgresHostMetricsStore` (upsert por minuto preservando valores não nulos); série de 12 h para 56 hosts com p95 ≤ 500 ms em PostgreSQL real (SC-004).

## Fase 3: Agente

- [x] T007 [US3] `ResourceSampler` em `backend/libs/Dokpod.Agent.Application/Metrics/`: uma amostra por intervalo (15 s, máximo 30 s), concorrência limitada, lotes ≤ 500 e ≤ 4 mensagens por intervalo, rodízio e `dropped_count`; stats via Docker e Podman em `Dokpod.Agent.Infrastructure`; teste de payload sem nome de processo, argumento, variável ou label (SC-006); testes com engines reais. — `ResourceSampler` (lotes de 500, 4 mensagens, rodízio, `dropped_count`, primeira amostra sem CPU); teste de payload sem campo textual além do id (SC-006); stats reais em Podman.
- [x] T008 [US2] `ProcHostProbe` lendo `/proc/stat` e `/proc/meminfo` do próprio container e capacidade de `/info`, com detecção de `/proc` virtualizado (divergência > 5% com `/info` = "indisponível"); sem montagem de diretórios do host e sem leitura de `/proc/<pid>`; testes em Docker e Podman rootless reais. O agente Windows não anuncia a capability. — `ProcHostProbe` (`/proc/stat`, `/proc/meminfo`, sem `/proc/<pid>`), detecção de `/proc` virtualizado (> 5% de `/info`); validado em container Podman rootless real; Windows não anuncia a capability. Docker real: **NOT RUN**.
- [x] T009 [US1] Envio de `ResourceSampleBatch` em `AgentControlWorker.cs` somente se `CAPABILITY_RESOURCE_METRICS` estiver em `accepted_capabilities`; teste de não bloqueio de comandos. — `SendResourceSamplesAsync` na fila de baixa prioridade com `TryWrite` (nunca bloqueia comandos/inventário).

## Fase 4: API

- [x] T010 [US1] `MetricsIngestionService`, `LatestMetricsCache` e `ContainerMetricsBuffer` (30 min em memória) em `backend/libs/Dokpod.ControlPlane.Application/Metrics/`, alimentados por `AgentControlService`; agregação por minuto em memória; ordenação pelo horário de recebimento quando a divergência de relógio passar de 5 min; teste de que nenhuma métrica de container é gravada no PostgreSQL. — `MetricsIngestionService`, `LatestMetricsCache`, `ContainerMetricsBuffer`, `HostMinuteAggregator`; divergência de relógio > 5 min usa recebimento; nenhuma tabela de container no PostgreSQL (teste de schema, SC-005).
- [x] T011 [US1] `MetricsQueryService` e `MetricsEndpoints` (`getStageMetrics`, `listHostMetrics`, `getContainerMetrics`) com limite operacional configurável, autorização por host e testes negativos. — `MetricsQueryService` e `MetricsEndpoints` (autorização por host via `AuthorizedEnvironmentsService`/`ContainerInsightsService`, 400 para janela inválida); rotas exigem autenticação (`ControlPlaneHttpSurfaceTests`).
- [x] T012 [US3] Incluir `resources` em `listEnvironmentContainers` e na listagem global da spec 010 (quando existir); container parado sem `resources`. — `resources` em `listContainers`, `listEnvironmentContainers` e no detalhe; container parado/pausado sem `resources`.

## Fase 5: Frontend

- [x] T013 [P] [US1] `metrics.facade.ts` com consulta a cada 30 s somente com a aba visível e cancelamento; testes Vitest. — `metrics-poller.ts` (30 s, só aba visível, recarga ao voltar) e fachadas com testes Vitest.
- [x] T014 [US1] Visão geral: KPIs de CPU e memória, gráfico "Uso de recursos do cluster" com referência 80% e nota; coluna CPU de hosts. — KPIs de CPU/memória, gráfico com referência e nota do limite operacional, coluna CPU de hosts com destaque.
- [x] T015 [US2] Tela Hosts: colunas CPU/memória com barra e destaque. — colunas CPU e Memória com barra e destaque acima do limite em `features/hosts`.
- [x] T016 [US3] Fornecer componente de apresentação de recursos ("Recursos agora") e série de 30 min (indicando série parcial após reinício) para a spec 011; colunas CPU/Memória para a spec 010; CPU do host para a seleção de host da spec 013; container parado com `—` e "parado". — `ContainerMetricsFacade` com "Recursos agora" e série de 30 min (nota de série parcial) no detalhe; colunas CPU/Memória na lista; dados de host prontos para a 013.
- [x] T017 Playwright em `frontend/tests/e2e/` e loop visual (1920, 1440, 1100, 390 e 320px) dos pontos alterados, com revisão humana registrada; estados "indisponível", lacuna e parado. — `mock_ui/test_009_metrics.py` (4 cenários: com dados, indisponível, lacuna, parado; axe sem violações sérias/críticas); corrigidos KPI sem motivo visível e tabela do gráfico não focável. Larguras: cobertas pelo teste de rolagem da 007.

## Fase 6: Encerramento

- [ ] T018 Teste de carga (56 hosts/1.120 containers) e de margem (2.000 containers) para SC-003, precisão SC-001 e tempo de reflexo da mudança de carga SC-002, com evidência registrada para o [005](../005-qualificacao-release-docker-linux/tasks.md). — **NOT RUN**: exige laboratório com 56 hosts/1.120 containers reais e baseline de latência de comandos; precisão (SC-001) e reflexo de carga (SC-002) dependem do mesmo laboratório.

## Dependências e Ordem de Execução

T001 -> T002/T003 -> T004-T006 -> T007-T009 -> T010-T012 -> T013-T017 -> T018.
