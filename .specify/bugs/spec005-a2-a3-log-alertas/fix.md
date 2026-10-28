# Correção de Bug: `fail` no log e alertas enganosos (spec 005, A2 e A3)

- **Slug**: spec005-a2-a3-log-alertas
- **Corrigido em**: 2026-10-28
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

A projeção de inventário deixa de usar SERIALIZABLE e serializa só os escritores do mesmo ambiente. O cancelamento pelo chamador deixa de contar como falha de auditoria. As regras de alerta passam a enxergar séries que nascem com valor. A API para de registrar cada SQL em `Information`. O erro de transação do EF, que inclui o `BeginTransaction` cancelado pela queda do agente, passa a ser aviso.

## Alterações

| Arquivo | Alteração | Notas |
| --- | --- | --- |
| `backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresInventoryProjectionStore.cs` | modificado | `READ COMMITTED` + `pg_advisory_xact_lock` por ambiente; retry mantido como rede de segurança |
| `backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresAuditEventWriter.cs` | modificado | `OperationCanceledException` do chamador vai para `RecordCanceled` |
| `backend/libs/Dokpod.ControlPlane.Infrastructure/AuditEventMetrics.cs` | modificado | contador `dokpod.audit.append.canceled` e `Meter` exposto para teste |
| `backend/apps/Dokpod.ControlPlane.Api/ApiHost.cs` | modificado | `Microsoft.EntityFrameworkCore.Database.Command` em `Warning` |
| `backend/libs/Dokpod.ControlPlane.Infrastructure/ControlPlaneInfrastructureServiceCollectionExtensions.cs` | modificado | `RelationalEventId.TransactionError` em `Warning`; a exceção continua subindo e é registrada por quem a trata |
| `deploy/observability/dokpod-alerts.yml` | modificado | cláusula de série nova em `DokpodComandosIndeterminados` e `DokpodAuditoriaFalhando` |
| `deploy/observability/dokpod-alerts.test.yml` | testes adicionados | 3 casos (série nascendo com valor e série existente crescendo) |
| `backend/tests/Dokpod.ControlPlane.Api.Tests/InventoryProjectionConcurrencyIntegrationTests.cs` | teste adicionado | 40 ambientes concorrentes; corrida no mesmo ambiente |
| `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditEventWriterTests.cs` | teste adicionado | cancelamento não conta como falha |
| `docs/runbooks/observabilidade.md`, `docs/seguranca.md` | modificado | métrica nova, comportamento das regras e o novo isolamento |

## Testes Adicionados ou Atualizados

- `InventoryProjectionConcurrencyIntegrationTests.ManyAgentsPublishingAtOnce_AllSnapshotsAndDeltasAreProjected`: 40 ambientes, snapshot e delta em paralelo; tudo aceito, revisão 2 e 800 containers.
- `InventoryProjectionConcurrencyIntegrationTests.SameEnvironmentRacingSnapshots_KeepsTheNewestRevision`: 10 snapshots do mesmo ambiente em corrida; fica a revisão 10.
- `AuditEventWriterTests.AppendAsync_WhenCallerCancels_CountsCancellationNotFailure`.
- `dokpod-alerts.test.yml`: "Indeterminado em série que nasce com valor alerta", "Indeterminado que cresce em série existente alerta", "Falha de auditoria em série que nasce com valor alerta".

## Verificação Local

- `dotnet test Dokpod.slnx --configuration Release` (imagem `lzocateli/dotnet-sdk:10.0.402-noble-v1`, PostgreSQL 17.6 efêmero, migrations aplicadas) → 700 testes, 0 falhas, 0 ignorados.
- Mesmo teste de concorrência contra o código anterior da projeção → passa, mas com **128** `40001` no log do PostgreSQL; com a correção, **0**.
- `promtool test rules dokpod-alerts.test.yml` → SUCCESS; `promtool check rules` → 13 regras.

## Desvios da Avaliação

- Ampliação: a primeira imagem testada na VM ainda gerava `fail: ...Transaction[20205]` quando o agente caía durante o `BeginTransaction` (18 em 7 rodadas). O EF não separa cancelamento de erro em transações, então o evento passou a `Warning` por `ConfigureWarnings`. A alternativa de filtrar por exceção exigiria um provider de log próprio.

## Acompanhamento

- Validar na `v0.1.0-rc.3` assinada (ver `test.md`).
- Erros reais de transação aparecem como `warn` na categoria do EF e como `fail` em quem trata a exceção (gRPC, ASP.NET).
