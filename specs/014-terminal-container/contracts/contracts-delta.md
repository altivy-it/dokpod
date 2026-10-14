# Contratos: Delta de Terminal (014)

## Protobuf

```proto
service AgentControl {
  rpc Connect(stream AgentMessage) returns (stream ControlPlaneMessage);
  rpc Exec(stream ExecFrame) returns (stream ExecFrame);
}

message ControlPlaneMessage {
  oneof payload {
    // ... até 15
    ExecOpenRequest exec_open = 16;
  }
}

message ExecOpenRequest {
  string exec_session_id = 1;
  string container_id = 2;
  uint32 cols = 3;
  uint32 rows = 4;
  google.protobuf.Timestamp expires_at = 5;
  uint32 max_output_bytes_per_second = 6;
}

message ExecFrame {
  oneof frame {
    ExecHandshake handshake = 1;   // agente → API, primeiro frame
    bytes stdin = 2;               // API → agente
    bytes output = 3;              // agente → API (stdout/stderr TTY)
    ExecResize resize = 4;         // API → agente
    ExecStatus status = 5;         // ambos: opened, closed(reason), error(code)
  }
}

message ExecHandshake { string exec_session_id = 1; string session_id = 2; uint64 fencing_token = 3; }
message ExecResize { uint32 cols = 1; uint32 rows = 2; }
message ExecStatus { ExecState state = 1; string reason = 2; string shell = 3; bool container_user_is_root = 4; bool orphan_suspected = 5; } // reason: motivos de término/recusa do data-model, incl. shell_not_available

enum ExecState { EXEC_STATE_UNSPECIFIED = 0; EXEC_STATE_OPENED = 1; EXEC_STATE_CLOSED = 2; EXEC_STATE_ERROR = 3; }
enum Capability { CAPABILITY_CONTAINER_EXEC = 15; }
```

## OpenAPI

| Método e rota | operationId | Autorização | Descrição |
| --- | --- | --- | --- |
| `GET /api/v1/environments/{environmentId}/containers/{containerId}/terminal-policy` | `getTerminalPolicy` | `container:exec` | `{ allowed, reason?, requiresJustification, requiresRecentMfa, limits{} }` |

`EnvironmentState.scopes` inclui `container:exec`.

## SignalR (`/hubs/control-plane`)

- `OpenTerminal(OpenTerminalRequest { environmentId, containerId, cols, rows, justification? }, IAsyncEnumerable<TerminalInput> input) → IAsyncEnumerable<TerminalOutput>`
- `TerminalInput`: `{ data?: base64, resize?: {cols, rows} }`
- `TerminalOutput`: `{ data?: base64, status?: { state, reason, shell, containerUserIsRoot, warning? } }`
- Transporte exigido: WebSocket; negociação sem WebSocket recusa o terminal com `websocket_required`.
- Entrega de `ExecOpenRequest`: no máximo uma vez, sem journal; o agente deduplica por `exec_session_id` e recusa após `expires_at`.
