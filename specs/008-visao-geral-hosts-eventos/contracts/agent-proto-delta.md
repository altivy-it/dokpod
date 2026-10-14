# Contrato do Agente: Delta v1 (008)

Somente adições compatíveis; nenhuma tag existente muda ou é reutilizada.

```proto
message AgentHello {
  // 1-8 existentes
  string host_name = 9;
  uint32 cpu_count = 10;
  uint64 memory_bytes = 11;
  string engine_api_version = 12;
  bool engine_rootless = 13;
}

message ContainerState {
  // 1-6 existentes
  google.protobuf.Timestamp created_at = 7;
  google.protobuf.Timestamp started_at = 8;
  ContainerHealth health = 9;
  uint32 restart_count = 10;
  optional int32 exit_code = 11;
  bool oom_killed = 12;
  repeated PortBinding ports = 13;
  // tag 14 não alocada: pods ficam para adapter Libpod futuro
}

message PortBinding {
  string host_ip = 1;
  uint32 host_port = 2;      // 0 = não publicada
  uint32 container_port = 3;
  TransportProtocol protocol = 4;
}

message SessionEstablished {
  // 1-5 existentes
  repeated Capability accepted_capabilities = 6; // ausente = servidor N-1
}

message AgentMessage {
  oneof payload {
    // 10-16 existentes
    EngineEventBatch engine_events = 17;
  }
}

message EngineEventBatch {
  repeated EngineEvent events = 1;   // máx. 100 por mensagem
  uint32 dropped_count = 2;
}

message EngineEvent {
  string source_key = 1;
  EngineEventType type = 2;
  google.protobuf.Timestamp occurred_at = 3;
  string subject_id = 4;
  string subject_name = 5;
  map<string, string> attributes = 6; // allowlist por tipo, valores ≤ 256
  uint32 aggregated_count = 7;
}

enum ContainerHealth {
  CONTAINER_HEALTH_UNSPECIFIED = 0;
  CONTAINER_HEALTH_NONE = 1;
  CONTAINER_HEALTH_STARTING = 2;
  CONTAINER_HEALTH_HEALTHY = 3;
  CONTAINER_HEALTH_UNHEALTHY = 4;
}

enum TransportProtocol {
  TRANSPORT_PROTOCOL_UNSPECIFIED = 0;
  TRANSPORT_PROTOCOL_TCP = 1;
  TRANSPORT_PROTOCOL_UDP = 2;
  TRANSPORT_PROTOCOL_SCTP = 3;
}

enum EngineEventType {
  ENGINE_EVENT_TYPE_UNSPECIFIED = 0;
  ENGINE_EVENT_TYPE_CONTAINER_DIED = 1;
  ENGINE_EVENT_TYPE_CONTAINER_OOM_KILLED = 2;
  ENGINE_EVENT_TYPE_CONTAINER_UNHEALTHY = 3;
  ENGINE_EVENT_TYPE_CONTAINER_RECOVERED = 4;
  ENGINE_EVENT_TYPE_CONTAINER_AUTO_RESTARTED = 5;
  ENGINE_EVENT_TYPE_CONTAINER_CREATED = 6;
  ENGINE_EVENT_TYPE_CONTAINER_REMOVED = 7;
  ENGINE_EVENT_TYPE_IMAGE_PULLED = 8;
  ENGINE_EVENT_TYPE_IMAGE_REMOVED = 9;
  ENGINE_EVENT_TYPE_VOLUME_REMOVED = 10;
}

enum Capability {
  // 0-5 existentes
  CAPABILITY_ENGINE_EVENTS = 6;
}
```

**Compatibilidade**: Servidor N aceita agente N-1 sem `engine_events` e sem campos novos (valores padrão = desconhecido). Servidor N preenche `SessionEstablished.accepted_capabilities` com as capabilities do `Hello` que processa. Agente N só envia `engine_events` se `CAPABILITY_ENGINE_EVENTS` estiver em `accepted_capabilities`; lista vazia = servidor N-1, que encerraria a sessão com `Unimplemented` ao receber payload desconhecido. Tipos de evento desconhecidos são descartados e contados, não aceitos. A mesma regra vale para os payloads novos das specs 009-014.
