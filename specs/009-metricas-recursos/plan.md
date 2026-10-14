# Plano de Implementação: Métricas de Recursos de Hosts e Containers

**Branch**: `007-console-operacional` | **Data**: 2026-10-12 | **Spec**: [spec.md](spec.md)

**Estado**: `draft` - aguardando revisão humana.

## Resumo

O agente (Linux) coleta métricas de containers por `/containers/{id}/stats?stream=false&one-shot=true` e métricas do host por `/proc/stat` e `/proc/meminfo` do próprio container (que refletem o host) mais `/info`, sem montar diretórios do host, e envia uma amostra por intervalo em `ResourceSampleBatch`. A API mantém o último valor e a série de 30 min dos containers em memória e persiste só a série de host (1 min, 48 h) no PostgreSQL. Agentes Windows ficam fora. Endpoints de métricas alimentam Visão geral, Hosts, lista, detalhe e criação.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, Protobuf v1, OpenAPI 3.1, Angular 22.

**Dependências Principais**: Existentes. Nenhuma nova.

**Armazenamento**: `host_resource_samples` particionada por mês, agregada por minuto, retenção 48 h (configurável) pela rotina genérica da 008. Séries de container e último valor somente em memória da API (efêmeros, recompostos pelas próximas amostras).

**Testes**: Domínio (fórmulas D-001, reinício de contadores, ponderação), aplicação (agregados, buffer de 30 min, autorização), PostgreSQL real (partições, retenção, desempenho), Docker/Podman rootless reais (comparação com `stats`, leitura de `/proc` do container), payload sem dados de processo, carga e margem (SC-003).

**Objetivos de Desempenho**: Ver SC-002 a SC-004.

**Restrições**: Sem conteúdo de container; sem montagem de diretórios do host; capability confirmada por `accepted_capabilities`; N/N-1; partição/retenção obrigatórias para a tabela de host.

## Verificação da Constituição

| Item | Situação | Observação |
| --- | --- | --- |
| "Métricas de container não são armazenadas como série temporal ilimitada no banco transacional" (arquitetura) | OK, sujeito a revisão humana | Opção A′ (D-002): série de container só em memória (30 min); só a série de host (pequena, 48 h) no PostgreSQL. |
| Novas fronteiras exigem threat model | OK | Nenhuma montagem de host nova; o agente lê apenas `/proc/stat` e `/proc/meminfo` do próprio container e nunca `/proc/<pid>` (D-003). |
| Novo datastore | OK | Não há; PostgreSQL existente. |
| Compatibilidade | OK | Mensagem nova com tag nova e capability `CAPABILITY_RESOURCE_METRICS`. |
| Tabelas temporais | OK | `host_resource_samples` com partição mensal, criação antecipada pela função existente e retenção pela rotina genérica da 008. |

## Estrutura do Projeto

```text
contracts/agent/v1/agent.proto                         # ResourceSampleBatch (tag 18), capability 7
contracts/openapi/dokpod-control-plane.v1.yaml         # /metrics/*
backend/libs/Dokpod.Domain/Metrics/                    # ResourceSample, CpuUsageCalculator, MetricsPolicy
backend/libs/Dokpod.ControlPlane.Application/Metrics/  # MetricsIngestionService, MetricsQueryService, IHostMetricsStore, LatestMetricsCache, ContainerMetricsBuffer
backend/libs/Dokpod.ControlPlane.Infrastructure/       # migration + PostgresHostMetricsStore (upsert por minuto)
backend/apps/Dokpod.ControlPlane.Api/Metrics/MetricsEndpoints.cs
backend/libs/Dokpod.Agent.Application/Metrics/         # ResourceSampler, IHostResourceProbe
backend/libs/Dokpod.Agent.Infrastructure/Metrics/      # ProcHostProbe (/proc do próprio container + /info), engine stats (Docker/Podman)
frontend/web/src/app/data-access/metrics.facade.ts
frontend/web/src/app/features/overview|hosts|containers|container-detail  # consumo
```

## Fases

1. Contratos e capability. 2. Domínio de cálculo. 3. Persistência e retenção da série de host. 4. Coleta no agente (Docker, Podman; Windows fora). 5. API, cache e buffer de container. 6. Frontend (preencher placeholders, polling de 30 s). 7. Carga, margem e precisão.

## Riscos

| Risco | Mitigação |
| --- | --- |
| Custo de `stats` por container em hosts densos | `one-shot`, concorrência limitada, intervalo máximo de 30 s e rodízio de containers entre ciclos com `dropped_count`. |
| `/proc` virtualizado (lxcfs) | Detecção por divergência com `/info`; CPU/memória do host "indisponível". |
| Perda da série de container em reinício da API | Aceita (30 min); documentada na interface como série parcial. |
| Escrita intensa | Só hosts gravam (≈ 1 upsert por host por minuto); agregação por minuto em memória na API. |
