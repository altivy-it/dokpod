# Pesquisa: Métricas de Recursos

## D-001 Fonte e fórmulas das métricas de container

- **Fonte**: `GET /containers/{id}/stats?stream=false&one-shot=true` (Docker e API compatível Podman), com concorrência limitada.
- **Núcleos usados**: `Δcpu_usage.total_usage / Δt`, com `Δt` entre os campos `read` das duas leituras (ou o relógio do agente, se faltar). Revisto em 2026-10-28: a fórmula original dividia por `Δsystem_cpu_usage`, que na API compatível do Podman não segue a base do Docker e inflava a CPU várias vezes (bug `agent-cpu-podman-system-usage`).
- **CPU % do host** (0-100): `núcleos / online_cpus × 100`.
- **CPU % do limite**: `núcleos ÷ limite_em_núcleos × 100`, com `limite_em_núcleos = NanoCpus / 1e9` (ou `CpuQuota / CpuPeriod`). Sem limite, a referência é o host e a resposta indica `cpuLimitCores = null`. Pode exceder 100 por instantes; a exibição limita a 100 com indicação.
- **Memória usada**: `usage - inactive_file` (cgroup v2) ou `usage - cache` (v1).
- **Contadores reiniciados**: delta negativo descarta a amostra (lacuna), nunca gera valor negativo ou pico.
- **Alternativa**: stream contínuo de stats (uma conexão por container; rejeitado pelo custo).

## D-002 Armazenamento de séries

| Opção | Prós | Contras |
| --- | --- | --- |
| A. PostgreSQL para host e container, 1 min, retenção 24/48 h | Sobrevive a restart. | ~1,6 M linhas/dia e ~75 upserts/s com 1.120 containers para atender uma janela de 30 min; contraria `docs/arquitetura.md` ("métricas de container não são armazenadas como série temporal ... no banco transacional"). |
| **A′. Host no PostgreSQL (1 min, 48 h) e container em memória (30 min)** (**recomendada**) | ~80 mil linhas/dia; compatível com a arquitetura (instância única de API, memória local como cache efêmero); sem escrita por container. | Reinício da API perde até 30 min de série de container (recomposta pelas próximas amostras). |
| B. Tudo em memória | Zero escrita. | Perde 12 h de série de host em restart/deploy. |
| C. Banco de séries temporais dedicado | Adequado ao problema. | Novo datastore exige ADR e operação; desproporcional. |

**Estado**: Opção A′ aprovada em 2026-10-13 (autorização de execução com as recomendações, [specs/README.md](../README.md)) e implementada (`host_resource_samples`, `ContainerMetricsBuffer`). Memória estimada da série de container: 1.120 containers × 30 pontos × ~80 bytes ≈ 2,7 MB (5 MB na margem de 2.000 containers).

## D-003 Métricas do host

- **Linux**: `/proc/stat` e `/proc/meminfo` não são isolados por namespace; lidos de dentro do container do agente, refletem o host. Não há montagem de `/proc` do host (que exporia `cmdline`/`environ` de todos os processos e exigiria threat model). Número de CPUs e memória total vêm de `/info` do engine.
- **Detecção de virtualização de `/proc`** (lxcfs ou similar): se `MemTotal` de `/proc/meminfo` divergir de `/info` em mais de 5%, CPU e memória do host ficam "indisponível".
- **Podman rootless**: mesma leitura; validar em teste real.
- **Windows**: fora desta spec; o agente Windows não anuncia `CAPABILITY_RESOURCE_METRICS`.

## D-004 Agregados do estágio

- CPU média = Σ(cpu% × cpus) / Σ cpus dos hosts com amostra no minuto. Memória = Σ usada / Σ total. Hosts sem amostra no minuto são excluídos e a resposta informa `hostsSampled`/`hostsTotal`.

## D-005 Cadência, lotes e agregação

- Agente: uma amostra por intervalo (15 s padrão, máximo 30 s), sem média prévia; até 500 containers por mensagem e 4 mensagens por intervalo; acima disso, rodízio entre ciclos e `dropped_count`.
- API: o último valor é atualizado a cada amostra (atende SC-002); o agregado por minuto é calculado em memória (média incremental com `sample_count`) e, para hosts, gravado por upsert ao fechar o minuto.

## D-006 Retenção da série de host

- Tabela `host_resource_samples` particionada por mês, com criação antecipada pela função existente `dokpod_ensure_monthly_partitions` e retenção pela rotina genérica criada na 008 (`dokpod_apply_temporal_retention`, D-004 da 008), parametrizada por tabela.

## D-007 Atualização na interface

- Consulta periódica a cada 30 s, somente com a aba visível (`visibilitychange`); sem invalidação SignalR, para não gerar uma emissão por amostra.
