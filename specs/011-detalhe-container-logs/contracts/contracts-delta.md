# Contratos: Delta de Detalhe, Inspeção e Logs (011)

## Protobuf

```proto
message ControlPlaneMessage {
  oneof payload {
    // 10-13 existentes
    ContainerQueryRequest container_query = 14;
    LogStreamControl log_stream_control = 15;
  }
}

message AgentMessage {
  oneof payload {
    // 10-18 existentes/008/009
    ContainerQueryResponse container_query_response = 19;
    LogChunk log_chunk = 20;
  }
}

message ContainerQueryRequest {
  string query_id = 1;
  ContainerQueryKind kind = 2;
  string container_id = 3;
  google.protobuf.Timestamp deadline = 4;
  LogQuery logs = 5;             // somente kind = LOGS
}

message LogQuery {
  uint32 tail = 1;
  google.protobuf.Timestamp since = 2;
  google.protobuf.Timestamp until = 3;
  string contains = 4;           // ≤ 128, literal (sem regex)
  uint32 max_bytes = 5;
}

message ContainerQueryResponse {
  string query_id = 1;
  QueryResultState state = 2;    // OK, NOT_FOUND, NOT_SUPPORTED, TOO_LARGE, FAILED
  string failure_code = 3;
  bytes sanitized_json = 4;      // SUMMARY / INSPECT (UTF-8 JSON)
  repeated LogLine lines = 5;    // LOGS
  bool truncated = 6;
  uint32 sequence = 7;           // parte, a partir de 0
  bool final = 8;                // última parte da consulta
  repeated string redacted_paths = 9;  // INSPECT
}
// Cada mensagem ≤ 256 KiB (limite gRPC do plano de controle: 1 MiB). A API monta as partes até Logs:MaxResponseBytes.

message LogLine {
  google.protobuf.Timestamp timestamp = 1;
  LogStream stream = 2;
  string text = 3;               // já mascarado
  string level = 4;              // inferido no agente; vazio = "—"
}

message LogStreamControl {
  string session_id = 1;
  LogStreamAction action = 2;    // START, STOP
  string container_id = 3;
  string contains = 4;
  uint32 max_lines_per_second = 5;
  google.protobuf.Timestamp expires_at = 6;
  uint32 max_bytes_per_second = 7;
}

message LogChunk {
  string session_id = 1;
  repeated LogLine lines = 2;
  uint32 omitted_lines = 3;
  bool ended = 4;
  string end_reason = 5;
}
// LogChunk ≤ 256 KiB. Mensagens de consulta e LogChunk vão na fila de baixa prioridade do agente.

enum ContainerQueryKind { CONTAINER_QUERY_KIND_UNSPECIFIED = 0; CONTAINER_QUERY_KIND_SUMMARY = 1; CONTAINER_QUERY_KIND_INSPECT = 2; CONTAINER_QUERY_KIND_LOGS = 3; }
enum QueryResultState { QUERY_RESULT_STATE_UNSPECIFIED = 0; QUERY_RESULT_STATE_OK = 1; QUERY_RESULT_STATE_NOT_FOUND = 2; QUERY_RESULT_STATE_NOT_SUPPORTED = 3; QUERY_RESULT_STATE_TOO_LARGE = 4; QUERY_RESULT_STATE_FAILED = 5; }
enum LogStream { LOG_STREAM_UNSPECIFIED = 0; LOG_STREAM_STDOUT = 1; LOG_STREAM_STDERR = 2; }
enum LogStreamAction { LOG_STREAM_ACTION_UNSPECIFIED = 0; LOG_STREAM_ACTION_START = 1; LOG_STREAM_ACTION_STOP = 2; }
enum Capability { CAPABILITY_CONTAINER_INSPECT = 10; CAPABILITY_CONTAINER_LOGS = 11; }
```

## OpenAPI

| Método e rota | operationId | Autorização | Resposta |
| --- | --- | --- | --- |
| `GET /api/v1/environments/{environmentId}/containers/{containerId}` | `getContainer` | `environment:read` | `ContainerDetail` (= `ContainerListItem` + `restartsLast24h`, `engineVersion`, `hostName`) |
| `GET .../containers/{containerId}/summary` | `getContainerSummary` | `container:inspect` | `ContainerConfigSummary` |
| `GET .../containers/{containerId}/inspect` | `inspectContainer` | `container:inspect` | `{ json: object, truncated: boolean, redactedPaths: string[] }` |
| `GET .../containers/{containerId}/logs?tail=&since=&until=&contains=` | `getContainerLogs` | `container:logs` | `{ lines: LogLineView[], truncated, omittedLines }`; `LogLineView` = `LogLine` + `level` inferido |

Erros: 403 (scope), 404 (container), 409 (`agent_disconnected`), 422 (`not_supported`), 429 (`too_many_requests`), 504 (`agent_timeout`).

`contains` é texto literal (≤ 128) e é removido dos logs de acesso do nginx, do BFF e da API.

**Negociação**: segue a regra de [specs/README.md](../../README.md): o servidor só envia 14/15 a agentes que anunciaram 10/11; o agente só envia 19/20 se o servidor confirmou as capabilities. Testes nos dois sentidos (agente N × servidor N-1 e servidor N × agente N-1).

## SignalR (`/hubs/control-plane`)

- `StreamContainerLogs(environmentId, containerId, contains?)` → `IAsyncEnumerable<LogChunkView>`; autorização `container:logs` reavaliada ao iniciar e a cada 60 s (igual à 014); encerramento no cancelamento do cliente, na expiração, no logout ou na expiração da sessão do BFF, sempre com `STOP` ao agente; limites de 10 sessões por agente e 3 por usuário (`too_many_requests`).
