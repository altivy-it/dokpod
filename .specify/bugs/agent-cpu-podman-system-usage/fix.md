# Correção de Bug: CPU do container inflada no Podman (spec 009, SC-001)

- **Slug**: agent-cpu-podman-system-usage
- **Corrigido em**: 2026-10-28
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

A CPU do container passa a ser calculada em núcleos pelo tempo de parede entre duas leituras (`Δtotal_usage / Δt`), com `Δt` vindo do campo `read` das respostas de stats do engine, ou do relógio do agente se o campo faltar. O `system_cpu_usage` deixa de ser lido.

## Alterações

| Arquivo | Alteração | Notas |
| --- | --- | --- |
| `backend/libs/Dokpod.Domain/Metrics/CpuUsageCalculator.cs` | modificado | `Container(previous, current, elapsed, limit)`; `ContainerCpuCounters` sem o contador do sistema |
| `backend/libs/Dokpod.Agent.Application/Engines/IContainerEngine.cs` | modificado | `EngineContainerStats` sem `SystemCpuNanos`, com `ReadAt` |
| `backend/libs/Dokpod.Agent.Infrastructure/Engines/Docker/DockerEngineClient.cs` | modificado | lê `read`; ignora o valor vazio do Go (ano 1) |
| `backend/libs/Dokpod.Agent.Application/Metrics/ResourceSampler.cs` | modificado | usa o intervalo entre os `read`, com o relógio do agente como reserva |
| `specs/009-metricas-recursos/research.md` | modificado | D-001 com a fórmula nova e o motivo |
| `backend/tests/...` | testes | ver abaixo |

## Testes Adicionados ou Atualizados

- `MetricsDomainTests.Container_ComputesCoresFromWallClockOverHostAndLimit` (4 casos) e `Container_ResetCountersAreGapsNotZeroOrSpikes` (contador que volta, intervalo nulo e negativo).
- `ResourceSamplerTests.CpuUsesTheEngineReadInstantWhenAvailable`: ciclo de 15 s com leituras do engine a 10 s de distância.
- `ContainerStatsParsingTests`: resposta real do Podman 6.1.3 (contadores, memória e `read`) e `read` vazio, inválido ou nulo.
- `PodmanEngineCompatibilityTests.Metrics_StatsOfRunningContainerAndHostProcMatchEngine`: passa a exigir o `read` em vez do contador do sistema (projeto fora da solução; só compilado).

## Verificação Local

- `dotnet test Dokpod.slnx -c Release` (imagem `lzocateli/dotnet-sdk:10.0.402-noble-v1`): Domain 165, Agent.Application 131, Agent.Infrastructure 31, ControlPlane.Application 168, ControlPlane.Api 96 (82 de integração ignorados sem PostgreSQL), Bff 29, Architecture 5; 0 falhas.
- `dotnet build` dos projetos de integração Docker e Podman: sem erros.

## Desvios da Avaliação

- Nenhum.

## Acompanhamento

- Medir também num host Docker real.
