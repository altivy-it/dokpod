# Modelo de Dados: Métricas de Recursos

## host_resource_samples (particionada por mês em `minute`)

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `environment_id` | uuid | PK com `minute` |
| `minute` | timestamptz | truncado ao minuto |
| `cpu_percent` | real null | 0-100 |
| `memory_used_bytes` | bigint null | ≥ 0 |
| `memory_total_bytes` | bigint null | ≥ 0 |
| `sample_count` | smallint | amostras agregadas no minuto |

Retenção por `dokpod_apply_temporal_retention` (008), padrão 48 h.

## Séries de container (somente memória, API)

`ContainerMetricsBuffer`: por (host, container), buffer circular de 30 pontos de 1 minuto com `cpu_percent_of_host` (0-100), `cpu_percent_of_limit` (≥ 0, exibição limitada a 100), `cpu_limit_cores`, `memory_used_bytes`, `memory_limit_bytes`, `net_rx_bps`, `net_tx_bps`, `blk_read_bps`, `blk_write_bps`. Removido quando o container some do inventário ou após 30 min sem amostra. Não é persistido.

## Cache de último valor (API)

`LatestMetricsCache`: último valor por host e por container com `sampledAt`; expira após 3 intervalos sem amostra.

## Configuração

`Metrics:OperationalLimitPercent` (80), `Metrics:HostRetention` (48 h), `Agent:MetricsInterval` (15 s, máximo 30 s), `Agent:MetricsMaxContainersPerMessage` (500).
