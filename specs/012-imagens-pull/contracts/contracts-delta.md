# Contratos: Delta de Imagens e Pull (012)

## Protobuf

```proto
message AgentMessage {
  oneof payload {
    // ... até 20
    ImageSnapshotPage image_snapshot_page = 21;
    ImageDelta image_delta = 22;
  }
}

message ImageSnapshotPage {           // ≤ 256 KiB e ≤ 200 imagens
  string snapshot_id = 1;
  uint64 image_revision = 2;
  uint32 page_number = 3;
  bool is_last_page = 4;
  repeated ImageState images = 5;
}

message ImageDelta {
  uint64 base_revision = 1;
  uint64 image_revision = 2;
  repeated ImageChange changes = 3;
}

message ImageChange { ChangeKind kind = 1; ImageState image = 2; string image_id = 3; }

message ImageState {
  string image_id = 1;
  repeated string repo_tags = 2;
  repeated string repo_digests = 3;
  uint64 size_bytes = 4;
  google.protobuf.Timestamp created_at = 5;
  string architecture = 6;
  string os = 7;
  bool truncated = 8;              // tags/digests acima de 64
}

message ContainerState {
  // 1-13 existentes/008; 14 não alocada
  string image_id = 15;
}

message Heartbeat {
  // 1-2 existentes
  uint64 image_revision = 3;
}

message SnapshotRequest {
  // 1 existente
  SnapshotScope scope = 2;         // ausente = containers (N-1)
}

enum SnapshotScope { SNAPSHOT_SCOPE_UNSPECIFIED = 0; SNAPSHOT_SCOPE_CONTAINERS = 1; SNAPSHOT_SCOPE_IMAGES = 2; }

message AgentCommand {
  // 1-6 existentes
  oneof parameters {
    PullImageParameters pull_image = 7;
    // 8 reservado: CreateContainerParameters (013)
  }
}

message PullImageParameters { string reference = 1; string platform = 2; } // platform vazio = plataforma do host; a API não expõe platform nesta spec

message CommandResult {
  // 1-5 existentes
  string resolved_image_digest = 6;
  uint64 image_size_bytes = 7;
}

enum CommandKind { COMMAND_KIND_PULL_IMAGE = 7; }
enum Capability { CAPABILITY_IMAGE_INVENTORY = 12; CAPABILITY_IMAGE_PULL = 13; }
```

## OpenAPI

| Método e rota | operationId | Autorização | Descrição |
| --- | --- | --- | --- |
| `GET /api/v1/images?stage=&environmentId=&engine=&unused=&q=&sort=&cursor=&limit=` | `listImages` | `environment:read` por host | `ImageListPage { images[], summary {total, unused, totalSizeBytes}, nextCursor }` |
| `GET /api/v1/environments/{environmentId}/images/lookup?reference=` | `lookupImage` | `environment:read` | `{ present, imageId?, sizeBytes?, createdAt?, digest? }` (sem consulta ao registro) |
| `POST /api/v1/environments/{environmentId}/images/pull` | `pullImage` | `image:pull` | Header `Idempotency-Key`; corpo `{ reference, deadlineUtc }`; 202 `{ commandId }` |

`ContainerCommandStatus` ganha `kind` (`container`\|`image`), `containerId` passa a nullable para comandos de host, e `result` opcional `{ resolvedImageDigest, imageSizeBytes }`. `OverviewSummary` ganha `images { total, unused, partial }`. `EnvironmentState.scopes` inclui `image:pull`.

Erros de `pullImage`: 400 `invalid_reference`, 403 (scope ou `registry_not_allowed`), 404 (host), 409 (`agent_disconnected`, conflito de idempotência), 422 (`not_supported`).

**Negociação**: segue a regra de [specs/README.md](../../README.md); servidor só envia `PULL_IMAGE` a agentes com capability 13; agente só envia 21/22 com capability 12 confirmada. Testes nos dois sentidos.
