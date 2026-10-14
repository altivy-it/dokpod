# Runbook: partições e retenção de eventos operacionais

## Escopo

`dokpod.environment_events` (spec 008) é particionada por mês em `occurred_at`, com partição `DEFAULT` como proteção. As partições mensais são criadas antecipadamente por `dokpod.dokpod_ensure_monthly_partitions`, a mesma função usada por `agent_commands`, e a retenção é aplicada por `dokpod.dokpod_apply_temporal_retention` com o período `EventRetention:Period`.

## Sinais

- existência das partições do mês atual e dos meses seguintes;
- linhas na partição `environment_events_default` (indica rollover atrasado);
- crescimento diário de eventos por host e de `events.dropped` (agente descartando por volume);
- duração da execução de retenção e linhas removidas.

```sql
SELECT child.relname AS partition_name,
       pg_get_expr(child.relpartbound, child.oid) AS bound
FROM pg_inherits
JOIN pg_class parent ON parent.oid = pg_inherits.inhparent
JOIN pg_class child ON child.oid = pg_inherits.inhrelid
WHERE parent.oid = 'dokpod.environment_events'::regclass
ORDER BY child.relname;

SELECT count(*) FROM dokpod.environment_events_default;
```

## Rollover

Use o mesmo procedimento de `operacao-comandos-postgresql.md`: execute `SELECT dokpod.dokpod_ensure_monthly_partitions(DATE '<horizonte>');` antes do fim da faixa criada. Se a `DEFAULT` tiver linhas da faixa a criar, mova-as antes de anexar a nova partição.

## Retenção

- A API aplica a retenção periodicamente; execução manual:

```sql
SELECT dokpod.dokpod_apply_temporal_retention('dokpod.environment_events'::regclass, 'occurred_at', INTERVAL '30 days');
```

- A função recusa períodos menores que 1 hora, lotes fora de 1-50.000, tabelas fora da allowlist e colunas não temporais. Ela remove partições inteiramente vencidas e apaga em lotes as linhas vencidas da partição de corte, com `lock_timeout` de 2 s para não bloquear inserções.
- Reduzir o período é imediato e irreversível para os eventos removidos; aumente-o antes de investigações longas.
- Desde a migration `202610130008_FixRetentionPartitionDelete`, a exclusão em lote compara `(tableoid, ctid)`. Versões anteriores comparavam só `ctid`, que se repete entre partições, e podiam apagar eventos recentes de outra partição junto com os vencidos; instalações que executaram retenção antes dessa migration devem considerar possível perda de eventos recentes (a auditoria não é afetada).

## Verificação

- `ConsoleOverviewIntegrationTests` cobre deduplicação, paginação, retenção e recusa de tabelas não elegíveis em PostgreSQL real (`DOKPOD_TEST_POSTGRES_CONNECTION`).
