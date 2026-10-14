# Contratos: Delta da Lista Unificada e Pausa (010)

## OpenAPI

### `GET /api/v1/containers` (`listContainers`)

Parâmetros: `stage`, `environmentId` (repetível), `engine` (`docker`\|`podman`, repetível), `displayStatus` (repetível), `q` (2-128), `sort` (`name`, `-name`, `displayStatus`, `environment`, `createdAt`, `-createdAt`; CPU e memória não são ordenáveis no servidor, D-007), `cursor`, `limit` (1-100, padrão 50).

Resposta `ContainerListPage`:

```yaml
ContainerListPage:
  required: [containers, summary]
  properties:
    containers: { type: array, maxItems: 100, items: { $ref: '#/components/schemas/ContainerListItem' } }
    summary:
      type: object
      properties:
        total: { type: integer }
        byDisplayStatus: { type: object, additionalProperties: { type: integer } }   # chaves: as 6 situações da 008
    observedAt: { type: string, format: date-time }   # mais antigo entre hosts incluídos
    nextCursor: { type: [string, 'null'] }
ContainerListItem:
  allOf:
    - $ref: '#/components/schemas/ContainerInventory'   # enriquecido em 008/009
    - properties:
        environmentId: { type: string, format: uuid }
        environmentName: { type: string }
        engine: { type: string, enum: [docker, podman] }
        allowedActions: { type: array, items: { type: string } }   # interseção scope × capability × situação
```

Autorização: `environment:read` por host; 400 para filtros inválidos; nunca 403 por host individual (hosts sem permissão são simplesmente excluídos).

### Comandos

- `SubmitContainerCommandRequest.action` e `ContainerCommandStatus.action`: enum passa a `[start, stop, restart, delete, pause, unpause]`.
- `EnvironmentState.scopes`: inclui `container:pause`.
- Códigos de falha novos: `not_running` (pausa sem container em execução), `not_paused` (retomada sem container pausado), `capability_not_supported`. Revisão divergente segue `expectedContainerRevision` existente.
- Scope `container:pause` provisionado no Keycloak (D-008).

## Protobuf

```proto
enum CommandKind {
  // 0-4 existentes
  COMMAND_KIND_PAUSE_CONTAINER = 5;
  COMMAND_KIND_UNPAUSE_CONTAINER = 6;
}
enum Capability {
  CAPABILITY_CONTAINER_PAUSE = 8;
  CAPABILITY_CONTAINER_UNPAUSE = 9;
}
```

Agente N-1 recebe `CommandKind` desconhecido → rejeita (`COMMAND_ACCEPTANCE_REJECTED`, `unsupported_command`); o servidor não despacha pausa a sessões cujo `Hello` não anunciou a capability (regra de negociação do [registro de contratos](../../README.md#registro-de-alocação-de-contratos)). Teste: agente N-1 contra servidor N e agente N contra servidor N-1.
