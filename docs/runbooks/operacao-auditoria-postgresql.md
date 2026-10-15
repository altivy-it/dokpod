# Runbook: operação da auditoria PostgreSQL

## Escopo

A migration `202609120001_CreateAuditEvents` cria a tabela `audit_events` particionada, o registro global de chaves `audit_event_keys` e a role sem login `dokpod_runtime`. A migration é forward-only: rollback destrutivo não é permitido porque apagaria evidência.

O usuário de migration deve ser diferente do usuário de runtime. O usuário de runtime precisa receber membership em `dokpod_runtime`; essa role possui somente `SELECT` nas tabelas e `EXECUTE` na função controlada `dokpod_append_audit_event`. A role não recebe `INSERT` direto nas tabelas. No laboratório compartilhado atual, a conexão usa a instância local em `localhost:5432` e `Search Path=dokpod`; tabelas, partições, funções e histórico EF ficam no schema `dokpod`.

## Aplicação

No diretório raiz do Dokpod, injete uma connection string de laboratório por processo e execute a migration com `dotnet-ef`:

```powershell
$env:DOKPOD_CONTROLPLANE_CONNECTION = 'Host=127.0.0.1;Port=5432;Database=keycloak;Username=migration_user;Search Path=dokpod'
dotnet ef database update `
  --project backend/libs/Dokpod.ControlPlane.Infrastructure/Dokpod.ControlPlane.Infrastructure.csproj `
  --startup-project backend/apps/Dokpod.ControlPlane.Api/Dokpod.ControlPlane.Api.csproj `
  --configuration Release
Remove-Item Env:DOKPOD_CONTROLPLANE_CONNECTION
```

O valor acima é fictício. Não registre connection strings, senhas ou secrets em arquivos, logs ou argumentos persistidos.

## Rollover

A migration `202609220002_ScheduleTemporalPartitions` cria partições mensais de
`audit_events` e `agent_commands` de janeiro de 2026 até dezembro de 2036, além
das partições `DEFAULT` de segurança. A função versionada
`dokpod.dokpod_ensure_monthly_partitions(date)` permite ampliar esse horizonte
sem reproduzir DDL manual.

As conexões do plano de controle (API e `dotnet ef`) fixam a sessão em UTC
(`Timezone=UTC`, `ControlPlaneConnectionString`): os limites das partições
mensais são instantes UTC, e a migration `202609220002` converte datas com o
fuso da sessão, o que num servidor fora de UTC criava partições sobrepostas
(`42P17`). Em sessões manuais (`psql`), execute `SET TIME ZONE 'UTC';` antes de
chamar funções de partição ou de retenção.

Antes de o horizonte ficar abaixo de seis meses, o operador deve executar com a
credencial de migration:

```sql
SELECT dokpod.dokpod_ensure_monthly_partitions(DATE '2038-01-01');
```

Antes da chamada, o operador deve:

1. confirmar que o horizonte solicitado não ultrapassa o limite aceito pela função;
2. confirmar que as partições `DEFAULT` não contêm linhas das novas faixas;
3. verificar os índices locais e o pruning com `EXPLAIN` em consulta autorizada;
4. registrar a alteração no histórico operacional.

A partição `DEFAULT` evita perda silenciosa, mas não substitui o rollover. Linhas nela exigem diagnóstico e criação da faixa correta antes de qualquer movimentação controlada.

## Consulta pelo console (spec 017)

A migration `202610150001_AuditConsole` acrescenta a coluna `actor_name` (nome exibível do usuário nos eventos novos), uma sobrecarga de `dokpod_append_audit_event` com o nome e o índice `ix_audit_events_time_event (occurred_at_utc DESC, event_id DESC)`. O Up é idempotente na coluna e no índice; o Down remove só a sobrecarga e o índice, mantendo a coluna para não apagar evidência.

- A consulta é sempre limitada a 90 dias e paginada por cursor; com 1 milhão de eventos em 56 hosts, a primeira página respondeu com p95 de 14 ms no teste de carga.
- Exportações aparecem como `audit.exported` (sujeito `audit`, detalhes `since`, `until`, `count`) em cada host abrangido.
- A migration `202610150002_AuditFullTextSearch` cria a busca textual: índice GIN por expressão (`ix_audit_events_search`) e índice de prefixo do alvo (`ix_audit_events_subject_pattern`), no pai e em cada partição. A criação trava as gravações de auditoria enquanto os índices são construídos; em instalação com muitos eventos, aplique em janela de manutenção. Partições novas criadas por `dokpod_ensure_monthly_partitions` herdam os índices automaticamente.
- Verificação: `SELECT count(*) FROM pg_indexes WHERE schemaname = 'dokpod' AND indexdef LIKE '%dokpod_audit_search_document%';` deve ser o número de partições mais 1.
- O EF não localiza migrations destes ids (12 dígitos) por nome no `database update <alvo>`: para validar o Down, execute os scripts `Down`/`Up` da migration num banco descartável.

## Verificação

Consultar a estrutura sem conteúdo sensível:

```sql
SELECT tablename
FROM pg_tables
WHERE schemaname = 'dokpod'
  AND tablename LIKE 'audit_events%'
ORDER BY tablename;

SELECT grantee, privilege_type
FROM information_schema.role_table_grants
WHERE table_schema = 'dokpod'
  AND table_name IN ('audit_events', 'audit_event_keys')
ORDER BY grantee, table_name, privilege_type;

SELECT routine_schema, routine_name
FROM information_schema.routines
WHERE routine_schema = 'dokpod'
  AND routine_name IN ('dokpod_append_audit_event', 'dokpod_reject_audit_mutation')
ORDER BY routine_name;
```

Resultado esperado: 132 partições mensais por tabela até `*_2036_12`, partições
`audit_events_default` e `agent_commands_default`, funções no schema `dokpod` e
somente `SELECT` nas tabelas de auditoria para `dokpod_runtime`; escrita runtime
ocorre por `EXECUTE` em `dokpod.dokpod_append_audit_event`, não por `INSERT`
direto.

Os testes `PostgresAuditSchemaIntegrationTests` usam uma conexão PostgreSQL efêmera somente quando
`DOKPOD_TEST_POSTGRES_CONNECTION` está injetada no processo. Sem essa variável, os testes são pulados
explicitamente. A migration deve ser aplicada antes da execução.

No laboratório compartilhado, use `Search Path=dokpod` na connection string de teste para validar o mesmo namespace lógico usado pela aplicação.

`UPDATE` e `DELETE` devem falhar por privilégio ou pelo trigger append-only. Falha de migration, ausência de partição planejada ou linhas inesperadas na partição `DEFAULT` bloqueiam a promoção e devem ser escaladas para o responsável pela operação do banco.

## Recuperação

Não execute `Down` para remover auditoria. Em caso de rollback da aplicação, preserve as tabelas e desabilite apenas o fluxo novo até corrigir a versão. Restauração deve ocorrer em uma cópia verificada, com backup e teste de integridade antes de qualquer decisão operacional.
