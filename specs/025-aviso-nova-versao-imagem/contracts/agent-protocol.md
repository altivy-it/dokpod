# Contrato: verificação de nova versão no protocolo do agente

Acréscimo a `contracts/agent/v1/agent.proto` ([D-004](../research.md#d-004-protocolo-agente-servidor)). Só campos e números novos: compatível com N/N-1.

```proto
enum Capability {
  // ... 0 a 22 existentes
  // Spec 025: consulta o registro pela API OCI Distribution e compara digests (D-001, D-002).
  CAPABILITY_IMAGE_UPDATE_CHECK = 23;
}

message ControlPlaneMessage {
  oneof payload {
    // ... 10 a 16 existentes
    // Spec 025: só a agentes com CAPABILITY_IMAGE_UPDATE_CHECK.
    ImageUpdateCheckRequest image_update_check = 17;
  }
}

message AgentMessage {
  oneof payload {
    // ... 10 a 26 existentes
    ImageUpdateCheckResultPage image_update_check_result = 27;
  }
}

// Até 500 itens; referências já filtradas pela política de registros do estágio.
message ImageUpdateCheckRequest {
  string request_id = 1;                      // UUID
  repeated ImageUpdateCheckItem items = 2;
}

message ImageUpdateCheckItem {
  string reference = 1;                       // como no container (spec 012), com tag; nunca só digest
  string image_id = 2;                        // imagem local com essa referência; vazio se ausente
}

// Página até 256 KiB; resultados na ordem dos itens.
message ImageUpdateCheckResultPage {
  string request_id = 1;
  uint32 page_number = 2;
  bool is_last_page = 3;
  repeated ImageUpdateCheckResult results = 4;
}

message ImageUpdateCheckResult {
  string reference = 1;
  string platform = 2;                        // os/arquitetura[/variante] da imagem local
  ImageUpdateState state = 3;
  string reason = 4;                          // data-model.md; vazio em UP_TO_DATE e UPDATE_AVAILABLE
  string remote_digest = 5;                   // digest do índice ou manifesto remoto da tag
  string matched_local_digest = 6;            // digest local que bateu (UP_TO_DATE)
  google.protobuf.Timestamp checked_at = 7;
}

enum ImageUpdateState {
  IMAGE_UPDATE_STATE_UNSPECIFIED = 0;
  IMAGE_UPDATE_STATE_UP_TO_DATE = 1;
  IMAGE_UPDATE_STATE_UPDATE_AVAILABLE = 2;
  IMAGE_UPDATE_STATE_NOT_CHECKABLE = 3;
  IMAGE_UPDATE_STATE_FAILED = 4;
}
```

## Regras

- O agente responde a todo pedido, inclusive com todos os itens em `FAILED`; nunca omite um item.
- `reason` e os digests nunca carregam mensagem do registro, cabeçalho, URL com credencial nem token.
- Limites e mapeamento HTTP → motivo: [D-005](../research.md#d-005-limites-no-agente).
- O servidor descarta páginas de `request_id` que não é o pendente do host, ou que chegam depois de 6 min do envio.
- Se o host desconectar no meio, o pedido é abandonado e o host continua vencido na agenda.
