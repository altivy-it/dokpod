# Contratos: Delta de Criação de Container (013)

## Protobuf

```proto
message AgentCommand {
  oneof parameters {
    PullImageParameters pull_image = 7;          // 012
    CreateContainerParameters create_container = 8;
  }
}

message CreateContainerParameters {
  string image = 1;
  string name = 2;
  repeated PortBinding ports = 3;                // de 008
  repeated EnvironmentVariable environment = 4;
  repeated VolumeMount volumes = 5;
  string network = 6;
  optional double cpu_limit_cores = 7;
  optional uint64 memory_limit_bytes = 8;
  RestartPolicy restart_policy = 9;
  HealthCheck healthcheck = 10;
  bool auto_remove = 11;
}

message EnvironmentVariable {
  string name = 1;
  oneof source { string value = 2; HostSecretRef secret = 3; }
  bool sensitive = 4;
}
message HostSecretRef { HostSecretKind kind = 1; string name = 2; }
message VolumeMount { VolumeMountType type = 1; string source = 2; string target = 3; bool read_only = 4; }
message RestartPolicy { RestartPolicyName name = 1; uint32 maximum_retry_count = 2; }
message HealthCheck { repeated string test = 1; uint32 interval_seconds = 2; uint32 timeout_seconds = 3; uint32 retries = 4; uint32 start_period_seconds = 5; HealthCheckOnFailure on_failure = 6; }

message CommandResult { /* 1-5 existentes; 6-7 da 012 */ string created_container_id = 8; } // tag registrada em specs/README.md

enum HostSecretKind { HOST_SECRET_KIND_UNSPECIFIED = 0; HOST_SECRET_KIND_PODMAN_SECRET = 1; HOST_SECRET_KIND_FILE = 2; }
enum VolumeMountType { VOLUME_MOUNT_TYPE_UNSPECIFIED = 0; VOLUME_MOUNT_TYPE_VOLUME = 1; VOLUME_MOUNT_TYPE_BIND = 2; }
enum HealthCheckOnFailure { HEALTH_CHECK_ON_FAILURE_UNSPECIFIED = 0; HEALTH_CHECK_ON_FAILURE_NONE = 1; HEALTH_CHECK_ON_FAILURE_RESTART = 2; } // RESTART só Podman
enum RestartPolicyName { RESTART_POLICY_NAME_UNSPECIFIED = 0; RESTART_POLICY_NAME_NO = 1; RESTART_POLICY_NAME_ON_FAILURE = 2; RESTART_POLICY_NAME_ALWAYS = 3; RESTART_POLICY_NAME_UNLESS_STOPPED = 4; }
enum CommandKind { COMMAND_KIND_CREATE_CONTAINER = 8; }
enum Capability { CAPABILITY_CONTAINER_CREATE = 14; }
enum ContainerQueryKind { CONTAINER_QUERY_KIND_HOST_SECRETS = 4; CONTAINER_QUERY_KIND_HOST_NETWORKS = 5; CONTAINER_QUERY_KIND_HOST_VOLUMES = 6; } // 011
```

## OpenAPI

| Método e rota | operationId | Autorização | Descrição |
| --- | --- | --- | --- |
| `GET /api/v1/environments/{environmentId}/create-options` | `getContainerCreateOptions` | `container:create` | `{ engine, engineVersion, rootless, secrets[] {kind, name}, networks[] {name, driver}, volumes[] {name}, allowedRegistries[]?, bindMountsAllowed: bool, limits{} }` (consulta sob demanda ao agente) |
| `POST /api/v1/environments/{environmentId}/containers` | `createContainer` | `container:create` | Header `Idempotency-Key`; corpo `ContainerCreationRequest` (espelho de `ContainerCreationSpec` + `deadlineUtc`); 202 `{ commandId }`; 400 com erros por campo (`ProblemDetails.errors`). |

`additionalProperties: false` em todo o corpo: qualquer campo fora da allowlist resulta em 400. `ContainerCommandStatus` ganha `result.createdContainerId` e nunca inclui parâmetros sensíveis. `EnvironmentState.scopes` inclui `container:create`. `networks[]` em `create-options` atende clientes da API; a interface não exibe rede. `allowedRegistries[]` vem de `Images:AllowedRegistriesByStage` (012). Falhas novas: `anonymous_volume_with_auto_remove`, `unsupported_option`, `parameters_unavailable`.
