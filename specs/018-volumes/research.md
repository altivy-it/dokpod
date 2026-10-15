# Pesquisa: Volumes

## D-001 - Inventário por snapshot, como as imagens

- **Decisão**: capability `VOLUME_INVENTORY` (16); `AgentMessage.volume_snapshot_page` (23) com páginas de até 500 volumes e 256 KiB; `Heartbeat.volume_revision` (4); `SnapshotScope.VOLUMES` (3). O agente republica quando o conjunto muda (eventos `volume` do engine ou mudança nas montagens dos containers) e na sessão nova; revisão divergente no heartbeat faz o servidor pedir snapshot (no máximo 1/min).
- **Justificativa**: mesmo desenho já validado na spec 012; sem deltas, a projeção é reconstruível.

## D-002 - "Em uso" pelas montagens dos containers

- **Decisão**: o agente lê `Mounts` de `/containers/json?all=1` (todos os containers, inclusive parados) e publica, por volume, os containers que o montam (id, nome, destino, somente leitura). O caminho no host (`Mountpoint`) não é publicado.
- **Justificativa**: uma chamada já feita para o inventário; o engine é a fonte (constituição II).

## D-003 - Projeção e busca

- **Decisão**: `volume_inventory (environment_id, name, anonymous, driver, scope, created_at, labels jsonb, mounts jsonb, observed_at)`, substituída inteira por snapshot e protegida por revisão; busca por estágio, host, engine, sem uso, anônimo e texto (nome, label, container), ordenação por nome/criação/host e cursor por chave; resumo com total e sem uso.

## D-004 - Tamanho sob demanda

- **Decisão**: `ContainerQueryKind.VOLUME_USAGE` (7) com o nome do volume; o agente consulta `/system/df?type=volume` (com fallback para `/system/df`) e devolve `UsageData.Size` e `RefCount` quando o engine informa. Sem cache no servidor.
- **Justificativa**: `system df` percorre os volumes e é caro; só o detalhe precisa.

## D-005 - Remoção

- **Decisão**: `CommandKind.REMOVE_VOLUME` (9) com `RemoveVolumeParameters { name }` (parâmetro 9), capability `VOLUME_REMOVE` (17), scope `volume:delete`. O agente relista as montagens imediatamente antes e recusa com `volume_in_use`; chama `DELETE /volumes/{name}` sem `force`; mapeia 404 → `volume_not_found`, 409 → `volume_in_use`, outros → `volume_remove_failed`. Justificativa conforme a política do estágio (spec 016), registrada só na auditoria do comando.
- **Alternativas**: `force=true` (descartada: apagaria dados em uso); prune do engine (descartado: remove sem lista explícita).

## D-006 - Scope novo em ambientes existentes

- **Decisão**: a migration acrescenta `volume:delete` à lista de scopes permitidos dos registros que já têm `container:delete` (mesma classe de ação destrutiva). A concessão por usuário continua no Keycloak (`provision-environment-authorization.ps1 -Scope volume:delete`).
- **Justificativa**: a lista de scopes do registro é fixada no cadastro e não há rota de atualização; sem isso, nenhum ambiente existente poderia remover volumes.

## D-007 - Alocação no contrato do agente

`AgentMessage.payload` 23; `Heartbeat` 4; `SnapshotScope` 3; `Capability` 16 e 17; `CommandKind` 9; `AgentCommand.parameters` 9; `ContainerQueryKind` 7.
