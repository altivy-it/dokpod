# Pesquisa: Redes

## D-001 - Inventário por snapshot, como volumes e imagens

- **Decisão**: capability `NETWORK_INVENTORY` (18); `AgentMessage.network_snapshot_page` (24) com páginas de até 500 redes e 256 KiB; `Heartbeat.network_revision` (5); `SnapshotScope.NETWORKS` (4). O agente republica na sessão nova, em eventos `network` do engine, quando as conexões dos containers mudam e a pedido do servidor (revisão divergente, no máximo 1/min).
- **Justificativa**: desenho validado nas specs 012 e 018; sem deltas, a projeção é reconstruível.

## D-002 - "Em uso" pelas conexões dos containers

- **Decisão**: o agente lê `NetworkSettings.Networks` de `/containers/json?all=1` (todos os containers, inclusive parados) e publica, por rede, os containers conectados (id, nome e endereços IPv4/IPv6 quando houver). A ligação usa o identificador da rede e, sem ele, o nome. `Containers` de `/networks` não é usado: o engine só lista ali os containers em execução.
- **Redes do sistema**: `bridge`, `host`, `none`, `docker_gwbridge`, `podman` e qualquer rede com `Ingress`; marcadas pelo agente e conferidas pelo servidor na remoção.
- **Opções do driver** (`Options`) não são publicadas.

## D-003 - Projeção e busca

- **Decisão**: `inventory_networks (environment_id, network_id, name, driver, scope, internal, ipv6, attachable, system, created_at, subnets jsonb, labels jsonb, containers jsonb, container_count, truncated, observed_at)`, substituída inteira por snapshot e protegida por revisão; filtros por estágio, host, engine, driver, sem uso (sem conexões e fora do sistema) e sistema; busca em nome, identificador, sub-redes, labels e containers; ordenação por nome/criação/host; resumo com total e sem uso (fora do sistema). Detalhe por identificador (64 hex) ou, para o link do detalhe do container, pelo nome.

## D-004 - Remoção

- **Decisão**: `CommandKind.REMOVE_NETWORK` (10) com `RemoveNetworkParameters { network_id, name }` (parâmetro 10), capability `NETWORK_REMOVE` (19), scope `network:delete`. O servidor recusa rede do sistema (`network_system`), de escopo diferente de `local` (`network_not_local`) e com conexões (`network_in_use`). O agente relista os containers imediatamente antes e recusa `network_in_use`; chama `DELETE /networks/{id}`; mapeia 404 → `network_not_found`, 403/409 → `network_in_use`, outros → `network_remove_failed`. Nunca desconecta containers.
- **Alternativas**: `network prune` (descartado: remove sem lista explícita); desconectar antes de remover (descartado: altera serviços em execução).

## D-005 - Confirmação

- **Decisão**: remover rede não apaga dados; a confirmação lista afetadas e ignoradas e exige digitar a quantidade só em lote com rede de Produção (regra do lote de containers da spec 010). Justificativa pela política do estágio da spec 016 (ação equivalente a "remover").

## D-006 - Scope novo em ambientes existentes

- **Decisão**: como na spec 018, a migration acrescenta `network:delete` aos registros que já têm `container:delete`; a concessão por usuário continua no Keycloak (`provision-environment-authorization.ps1 -Scope network:delete`).

## D-007 - Alocação no contrato do agente

`AgentMessage.payload` 24; `Heartbeat` 5; `SnapshotScope` 4; `Capability` 18 e 19; `CommandKind` 10; `AgentCommand.parameters` 10.
