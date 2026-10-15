# Modelo de Dados: Visão geral com KPIs navegáveis

Sem mudança de schema no PostgreSQL nem no protocolo gRPC.

## Card de KPI navegável (frontend)

| Card | Valor | Destino (`/containers`) |
| --- | --- | --- |
| Em execução | `running` | `status=running` |
| Parados | `stopped` + `paused` | `status=stopped&status=paused` |
| Com falha ou reiniciando | `failed` + `restarting` | `status=failed&status=restarting` |
| CPU média do cluster | agregado D-005 | `status=running&sort=-cpu` |
| Memória alocada | agregado D-005 | `status=running&sort=-memory` |
| Imagens sem uso | spec 012 | `/imagens?semUso=1` (sem mudança) |

O estágio vem da URL atual (`queryParamsHandling="merge"`). Estados: carregando (não navegável), valor, indisponível (navegável), parcial ("parcial: N de M hosts").

## Ordenação por consumo (aplicação)

`ContainerSort` ganha `CpuDesc`, `CpuAsc`, `MemoryDesc`, `MemoryAsc`.

Chave de ordenação: `(grupo, valor, nome, ambiente, containerId)`.

- `grupo` = 0 com amostra recente; 1 sem amostra (sempre no fim).
- `valor` = CPU exibida (`cpuPercentOfLimit` limitado a 100 ou `cpuPercentOfHost`, a mesma regra da coluna) ou `memoryUsedBytes`.
- Direção aplica-se apenas a `valor`; desempate sempre ascendente por nome, ambiente e id.

Cursor: `SortKey` = `grupo|valor|nome` (valor invariante com 6 casas; vazio no grupo 1), mais ambiente e id, no formato de cursor existente da spec 010.

## Agregado atual do estágio (aplicação)

Por host habilitado e legível com ao menos um container com amostra recente:

| Campo | Origem |
| --- | --- |
| CPU do host (% da capacidade) | soma de `cpuPercentOfHost` dos containers |
| Núcleos | descritor do host |
| Memória usada | soma de `memoryUsedBytes` dos containers |
| Memória total | amostra recente do host; na falta, memória do descritor |

`StageMetricsCurrent(CpuPercent, MemoryPercent, HostsSampled, HostsTotal, SampledAtUtc)` mantém a forma; `HostsSampled` passa a contar hosts com amostra recente de containers.
