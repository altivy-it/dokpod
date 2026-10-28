# Avaliação de Bug: CPU do container inflada no Podman (spec 009, SC-001)

- **Slug**: agent-cpu-podman-system-usage
- **Criada em**: 2026-10-28
- **Origem**: medição da spec 009 T018 (SC-001) na VM da spec 005, com o agente Podman real, executada pela ordem de pendências aprovada em 2026-10-28
- **Veredito**: valid
- **Severidade**: medium

## Relato (literal ou resumido)

Com um container limitado a 2 vCPU e um laço ocupando 1 núcleo, o console mostrou a CPU entre 181% e 235% do limite (média 214%), enquanto o cgroup do container e o `podman stats` mediam 1 núcleo (50% do limite). A memória ficou a 0,4 ponto percentual do cgroup.

## Sintoma

No Podman, `cpuPercentOfLimit` e `cpuPercentOfHost` ficam várias vezes acima do uso real e passam de 100% do limite, o que é impossível. O desvio varia com a carga do restante do host.

## Reprodução

1. Host Podman 6.1.3 rootless com 8 vCPU e o agente conectado.
2. `podman run -d --cpus 2 --memory 512m alpine:3.22 sleep infinity` e, dentro dele, um laço `while :; do :; done`.
3. `GET /api/v1/environments/{id}/containers/{containerId}/metrics`: `cpuPercentOfLimit` ≈ 214%. O `cpu.stat` do cgroup dá 1,00 núcleo e o `podman stats`, 100,76%.
4. Contadores brutos de `GET /v1.41/containers/{id}/stats?stream=false` em 10,2 s: `Δtotal_usage` = 1,004e9 ns/s (1 núcleo, correto); `Δsystem_cpu_usage` = 1,256e9 ns/s com `online_cpus` = 8. No Docker, o `system_cpu_usage` avança cerca de 8e9 ns/s num host de 8 vCPU.

## Caminhos de Código Suspeitos

- `backend/libs/Dokpod.Domain/Metrics/CpuUsageCalculator.cs:Container`: `share = Δcontainer / Δsystem` e `percentOfLimit = share * cpus / limite`, a fórmula do Docker, que supõe `system_cpu_usage` como soma do tempo de todas as CPUs.
- `backend/libs/Dokpod.Agent.Infrastructure/Engines/Docker/DockerEngineClient.cs` (leitura de `cpu_stats`), usado também para o Podman pela API compatível.
- `backend/libs/Dokpod.Agent.Application/Metrics/ResourceSampler.cs:BuildContainerSample`.

## Hipótese de Causa Raiz

Confiança: high. A API compatível do Podman preenche `system_cpu_usage` com outra base de tempo (no teste, perto do tempo ocupado do host, não do tempo total de todas as CPUs). `share * online_cpus` então multiplica o uso real por um fator que muda com a carga do host (6,4 no teste isolado e cerca de 4,3 na medição pelo console).

## Correção Proposta

**Preferencial**: calcular a CPU pelo tempo de parede entre as duas leituras, sem depender de `system_cpu_usage`: `núcleos = Δtotal_usage / Δt` (com `Δt` do campo `read` das duas respostas de stats, ou do relógio do agente), `percentOfHost = núcleos / online_cpus`, `percentOfLimit = núcleos / limite`. Vale para Docker e Podman.

**Alternativas**:

- Manter a fórmula do Docker e só trocar no Podman: dois caminhos para o mesmo número e dependência de detalhe de implementação do Podman.
- Ler o cgroup do container pelo agente: exige montar diretórios do host, o que a spec 009 FR-001 proíbe.

**Arquivos Prováveis de Alteração**:

- `backend/libs/Dokpod.Domain/Metrics/CpuUsageCalculator.cs`
- `backend/libs/Dokpod.Agent.Application/Engines/IContainerEngine.cs` (`EngineContainerStats` com o instante da leitura)
- `backend/libs/Dokpod.Agent.Infrastructure/Engines/Docker/DockerEngineClient.cs`
- `backend/libs/Dokpod.Agent.Application/Metrics/ResourceSampler.cs`

**Testes a Adicionar ou Atualizar**:

- Unidade do calculador: 1 núcleo em 15 s com limite de 2 → 50%; `system_cpu_usage` com base do Podman não altera o resultado.
- Contrato do cliente do engine com respostas reais gravadas de Docker e Podman.
- Repetir a medição da VM (SC-001 ≤ 5 p.p.).

## Riscos e Considerações

- Docker: o resultado deve ficar igual ao atual, que segue a fórmula oficial; conferir com um host Docker real.
- O Dokpod ainda não tem matriz de qualificação Podman para métricas; o achado entra na spec 006.

## Perguntas em Aberto

- Nenhuma.
