# Runbook: partições e retenção de comandos PostgreSQL

## Escopo

A migration `202609220001_PartitionAgentCommands` converte `dokpod.agent_commands` em uma tabela particionada mensalmente por `created_at_utc`. A tabela `dokpod.agent_command_keys` mantém a identidade global `(environment_id, command_id)` separada das partições para preservar deduplicação entre meses.

Tipos aceitos por `ck_agent_commands_kind`: 1 `start`, 2 `stop`, 3 `restart`, 4 `delete`, 5 `pause` e 6 `unpause` (spec 010); a 012 e a 013 ampliam para 7 e 8. Pausa e retomada só são despachadas a agentes que anunciaram `CAPABILITY_CONTAINER_PAUSE`/`CAPABILITY_CONTAINER_UNPAUSE`; no Podman, parar um container pausado é feito retomando antes de parar.

A partição `dokpod.agent_commands_default` é uma proteção contra perda de escrita quando a faixa mensal ainda não existe. Linhas nessa partição indicam falha de rollover e exigem correção operacional.

## Sinais

Verifique semanalmente:

- existência de partições para o mês atual e os cinco meses seguintes;
- ausência de comandos na partição `DEFAULT`;
- ausência de comandos não terminais em partições candidatas à retenção;
- uso de pruning em consultas temporais;
- crescimento de `agent_command_keys` e das partições mensais.

```sql
SELECT child.relname AS partition_name,
       pg_size_pretty(pg_total_relation_size(child.oid)) AS total_size
FROM pg_inherits inheritance
JOIN pg_class parent ON parent.oid = inheritance.inhparent
JOIN pg_namespace namespace ON namespace.oid = parent.relnamespace
JOIN pg_class child ON child.oid = inheritance.inhrelid
WHERE namespace.nspname = 'dokpod'
  AND parent.relname = 'agent_commands'
ORDER BY child.relname;

SELECT count(*) AS rows_outside_planned_ranges
FROM dokpod.agent_commands_default;
```

## Rollover

As migrations criam partições mensais de janeiro de 2026 até dezembro de 2036.
Execute a função versionada com a credencial exclusiva de migration antes de o
horizonte restante ficar abaixo de seis meses:

```sql
SELECT dokpod.dokpod_ensure_monthly_partitions(DATE '2038-01-01');
```

Antes de ampliar:

1. confirme que a faixa não sobrepõe outra partição;
2. confirme que `agent_commands_default` não contém linhas da faixa;
3. mova linhas da faixa existentes na `DEFAULT` em uma transação controlada;
4. execute a função em uma janela controlada;
5. valide índices locais e pruning com `EXPLAIN (FORMAT JSON)`;
6. registre a alteração no histórico operacional.

Não anexe uma nova partição enquanto a `DEFAULT` contiver linhas da mesma faixa; o PostgreSQL recusará a operação ou exigirá validação bloqueante.

## Retenção

Política adotada na spec 012 (D-008, proposta de 90 dias aplicada como recomendação; revisão humana pendente antes de release):

- o `AgentCommandExpirationWorker` remove, a cada hora e em lotes de 5.000, comandos `Succeeded` e `Failed` com `created_at_utc` anterior a `Commands:Retention` (padrão 90 dias); `agent_command_parameters` sai junto por `ON DELETE CASCADE`;
- comandos `Pending`, `Dispatched`, `Accepted` e `Indeterminate` nunca são removidos; indeterminados exigem reconciliação manual antes;
- a remoção é por linha, então o gatilho `dokpod_release_agent_command_key` libera a chave em `agent_command_keys`: um replay com a mesma chave após a retenção cria um comando novo. A janela de 90 dias é muito maior que a de retry dos clientes (chaves por sessão do navegador);
- não descarte partições inteiras manualmente: isso deixaria tombstones em `agent_command_keys` e poderia remover indeterminados;
- nunca use retenção para resolver fila acumulada ou comando indeterminado.

Tipo 7 (`pull`, spec 012) tem `container_id` e `expected_container_revision` nulos (`ck_agent_commands_target`) e parâmetros em `dokpod.agent_command_parameters`, particionada por mês com a mesma função de rollover.

## Verificação

A migration deve ser aplicada em PostgreSQL descartável antes da promoção. Execute os testes de integração com `DOKPOD_TEST_POSTGRES_CONNECTION`; o resultado esperado inclui:

- roteamento para a partição mensal e para a `DEFAULT`;
- replay concorrente idempotente, inclusive com `created_at_utc` divergente;
- pruning de partições fora da faixa consultada;
- `dokpod_runtime` sem `INSERT`, `UPDATE` ou `DELETE` em `agent_command_keys`;
- triggers de reserva e liberação executados como `SECURITY DEFINER`;
- lifecycle, expiração e auditoria sem regressão.

## Recuperação

A migration é forward-only. Em falha durante rollout:

1. interrompa novas submissões de comandos;
2. preserve `agent_commands_unpartitioned`, caso a transaction ainda não tenha concluído;
3. reverta a aplicação para a versão compatível sem executar `Down`;
4. restaure em banco separado e compare contagens por estado, ambiente e mês;
5. retome somente após validar identidade global, auditoria e comandos não terminais.

Nunca recrie `agent_command_keys` apenas a partir das partições retidas: isso permitiria reutilizar IDs removidos pela retenção.
