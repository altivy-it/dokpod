# Pesquisa: Pods

## D-001 - Inventário pela API libpod

- **Decisão**: `GET /libpod/pods/json` (Podman); no Docker a rota não existe e o agente publica "sem pods" (`engine_without_pods`). Capability `POD_INVENTORY` (21), `AgentMessage.pod_snapshot_page` (26, até 500 pods e 256 KiB), `Heartbeat.pod_revision` (6), `SnapshotScope.PODS` (6). Republicação em eventos `pod` e `container` do engine (a situação do pod deriva dos containers) e a pedido do servidor.
- **Campos**: `Id`, `Name`, `Status` (`Running`, `Degraded`, `Exited`, `Stopped`, `Paused`, `Created`, `Dead`, `Error`), `Created`, `Labels`, `Networks`, `InfraId` e `Containers` (`Id`, `Names`, `Status`, `RestartCount`).
- **Verificado**: no host de laboratório, `pod_dokpod` (API, BFF, web e agente) e `pod_altivy-identity` existem sem container de infraestrutura (criados pelo `podman-compose`).

## D-002 - Pod do agente

- **Decisão**: o agente lê o próprio identificador em `/proc/self/mountinfo` (identificadores de 64 hex) e em `/etc/hostname` (prefixo de 12 caracteres, padrão do Podman); o pod com algum membro que corresponde é o "pod do agente". Fora de container, nenhum. Verificado no laboratório: hostname `8762fe176328` = prefixo do container do agente.

## D-003 - Projeção e busca

- **Decisão**: `inventory_pods (environment_id, pod_id, name, status, created_at, labels, networks, infra_id, containers jsonb, container_count, running_count, agent_pod, observed_at)`, substituída por snapshot com revisão; filtros por situação, host e container; busca em nome, labels e nomes de containers; resumo por situação; hosts Docker e sem capability informados à parte.

## D-004 - Ações

- **Decisão**: `CommandKind.POD_ACTION` (11) com `PodActionParameters { pod_id, name, action }` (parâmetro 11, ações `start`, `stop`, `restart`, `pause`, `unpause`); capability `POD_ACTIONS` (22); scopes `container:start`, `container:stop`, `container:restart`, `container:pause` (pausar e retomar); hash v2 `pod=<id>;action=<ação>`; auditoria `pod.<ação>` (categoria `pods`).
- O agente chama `POST /libpod/pods/{id}/{ação}` (304 = já no estado, sucesso), relê o pod e lista em `CommandResult.failed_containers` (9) os membros fora do estado esperado (falha `pod_partial_failure`); 404 → `pod_not_found`; recusa `agent_pod` para parar, pausar e reiniciar o próprio pod.
- O servidor guarda os nomes em `agent_commands.failed_containers` e os devolve no status do comando.

## D-005 - Alocação no contrato do agente

`AgentMessage.payload` 26; `Heartbeat` 6; `SnapshotScope` 6; `Capability` 21 e 22; `CommandKind` 11; `AgentCommand.parameters` 11; `CommandResult` 9.
