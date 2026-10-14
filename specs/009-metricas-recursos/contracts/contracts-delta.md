# Contratos: Delta de Métricas (009)

## Protobuf

```proto
message AgentMessage {
  oneof payload {
    // 10-17 existentes/008
    ResourceSampleBatch resource_samples = 18;
  }
}

message ResourceSampleBatch {
  google.protobuf.Timestamp sampled_at = 1;
  HostResourceSample host = 2;
  repeated ContainerResourceSample containers = 3; // máx. 500 por mensagem, 4 mensagens por intervalo
  uint32 dropped_count = 4;                         // containers não amostrados no ciclo
}

message HostResourceSample {
  optional double cpu_percent = 1;
  optional uint64 memory_used_bytes = 2;
  optional uint64 memory_total_bytes = 3;
}

message ContainerResourceSample {
  string container_id = 1;
  optional double cpu_percent_of_limit = 2; // pode exceder 100 por instantes
  optional double cpu_percent_of_host = 3;  // 0-100 (D-001)
  optional uint64 memory_used_bytes = 4;
  optional uint64 memory_limit_bytes = 5;
  optional uint64 net_rx_bps = 6;
  optional uint64 net_tx_bps = 7;
  optional uint64 blk_read_bps = 8;
  optional uint64 blk_write_bps = 9;
  optional double cpu_limit_cores = 10;
}

enum Capability { CAPABILITY_RESOURCE_METRICS = 7; }
```

**Compatibilidade**: o agente só envia `resource_samples` se `CAPABILITY_RESOURCE_METRICS` estiver em `SessionEstablished.accepted_capabilities` (008 D-009). Agentes Windows não anunciam a capability. Nenhum campo transporta nome de processo, argumento, variável ou label.

## OpenAPI

| Método e rota | operationId | Autorização | Resposta |
| --- | --- | --- | --- |
| `GET /api/v1/metrics/overview?stage=&window=12h&step=1h` | `getStageMetrics` | `environment:read` por host | `{ current: {cpuPercent, memoryPercent, hostsSampled, hostsTotal, sampledAt}, series: {x[], cpu[], memory[]}, operationalLimitPercent }` (valores `null` = lacuna) |
| `GET /api/v1/metrics/hosts?stage=` | `listHostMetrics` | `environment:read` | `[{environmentId, cpuPercent, memoryUsedBytes, memoryTotalBytes, sampledAt}]` |
| `GET /api/v1/environments/{environmentId}/containers/{containerId}/metrics?window=30m&step=1m` | `getContainerMetrics` | `environment:read` | `{ current: ContainerResourceView, series: {x[], cpu[], memory[]}, operationalLimitPercent }` |

`ContainerInventory` (lista) ganha `resources` opcional: `{cpuPercent, memoryUsedBytes, memoryLimitBytes, sampledAt}`. Container parado não traz `resources`.

`getContainerMetrics` aceita `window` somente até `30m` (série em memória); `window` maior retorna 400. A série pode vir parcial após reinício da API.

Parâmetros `window` ∈ {`30m`, `1h`, `6h`, `12h`, `24h`} e `step` ∈ {`1m`, `5m`, `15m`, `1h`}; combinações acima de 1.440 pontos retornam 400.
