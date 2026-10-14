# Pesquisa: Imagens e Pull

## D-001 Parâmetros de comando

- **Decisão**: `AgentCommand` ganha `oneof parameters` (tag 7 `PullImageParameters`, tag 8 reservada para `CreateContainerParameters` da 013). Parâmetros persistem em `agent_command_parameters` para redespacho e reconciliação.
- **Hash**: o código atual (`ContainerCommandService.ComputePayloadHash`) usa texto `"v1\n" + kind + container + revisão`, sem prazo. Tipos parametrizados usam `"v2\n" + kind + alvo + forma canônica dos parâmetros` (texto UTF-8 com campos em ordem fixa; referência normalizada, ex.: `nginx` → `docker.io/library/nginx:latest`). O prazo continua fora do hash, para que um retry com prazo novo não vire conflito. Protobuf determinístico foi rejeitado por não ser canônico entre linguagens e versões. `v1` continua valendo para os tipos 1-6.
- **Compatibilidade**: Agente N-1 rejeita `CommandKind` desconhecido; campo `parameters` desconhecido é ignorado com segurança porque o tipo já é rejeitado.

## D-002 Alvo do pull

- O comando de pull tem alvo = host (sem `container_id`). A chave de idempotência é por host; o frontend gera uma por host.
- **Persistência**: hoje `agent_commands.container_id` e `expected_container_revision` são `NOT NULL` e o domínio exige container com 64 hex. Migration expand: as duas colunas passam a aceitar nulo com `CHECK ((kind BETWEEN 1 AND 6) = (container_id IS NOT NULL AND expected_container_revision IS NOT NULL))`; `ck_agent_commands_kind` ampliada para `BETWEEN 1 AND 7`. O domínio ganha alvo `HostTarget` ao lado do alvo de container.

## D-003 Reconciliação de pull indeterminado

- Após resposta perdida, o agente consulta o journal; se ausente, verifica o engine. Referência por digest: presença conclui `succeeded`. Referência por tag: conclui `succeeded` somente se a imagem com a tag foi criada ou etiquetada depois do aceite do comando (journal); caso contrário, `indeterminate` (a tag pode ser antiga).

## D-004 Registro de laboratório

- `registry:2` (Distribution, Apache-2.0) em `deploy/e2e` com versão fixada por digest e registro em `docs/licenciamento.md`, usuário de teste, apenas para testes; credenciais de laboratório fora do repositório (gitleaks).

## D-005 Assinatura

- Fora do escopo; ver premissas da spec. Registrar como candidato a ADR futuro.

## D-006 "Em uso" pelo ID da imagem

- **Decisão**: `ContainerState` ganha `image_id = 15` (Docker/Podman `ImageID` de `/containers/json`; a tag 14 continua não alocada); coluna `inventory_containers.image_id`. "Em uso" = existe container projetado no mesmo host com `image_id` igual. `lookupImage` normaliza a referência e compara com `repo_tags`/`repo_digests`.
- **Alternativa rejeitada**: comparar `image_reference` textual (falha com `nginx` × `docker.io/library/nginx:latest` e com tag movida).
- **Compatibilidade**: agente N-1 não envia `image_id`; containers sem `image_id` não marcam imagem como em uso e o resumo indica "contagem parcial".

## D-007 Limites e revisão do inventário de imagens

- `ImageSnapshotPage` ≤ 256 KiB e ≤ 200 imagens; tags e digests limitados a 64 cada (excedente truncado com `truncated = true`). Mantém margem para o limite gRPC de 1 MiB (011 D-001).
- `Heartbeat.image_revision = 3` permite ao servidor detectar delta perdido; `SnapshotRequest.scope = 2` (`SNAPSHOT_SCOPE_CONTAINERS`/`SNAPSHOT_SCOPE_IMAGES`) pede snapshot só de imagens. Ausente = containers (N-1).

## D-008 Retenção de comandos

- `agent_commands` hoje só tem criação de partições, sem retenção. Aplicar `dokpod_apply_temporal_retention` (008 D-004) a `agent_commands` e `agent_command_parameters` (proposta: 90 dias; valor final é decisão humana). Comandos não terminais nunca são removidos.

## D-009 Threat model do pull

- Riscos: imagem maliciosa (supply chain), injeção na referência repassada a `/images/create?fromImage=`, referência com credencial embutida, esgotamento de disco, uso do host como proxy para registros internos.
- Mitigações: gramática OCI estrita na API e no agente, parâmetros codificados na URL do engine, scope `image:pull` por host, lista opcional de registros permitidos por estágio, auditoria, prazo e `insufficient_storage`. Verificação de assinatura continua fora do escopo (D-005) e fica registrada como risco residual.
