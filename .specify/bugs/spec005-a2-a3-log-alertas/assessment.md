# Avaliação de Bug: `fail` no log e alertas enganosos na qualificação em VM (spec 005, A2 e A3)

- **Slug**: spec005-a2-a3-log-alertas
- **Criada em**: 2026-10-28
- **Origem**: texto colado (pedido do solicitante: "corrigir ou melhorar de alguma forma o log e testar uma rc.3") e achados A2/A3 de [specs/005 plan.md](../../../specs/005-qualificacao-release-docker-linux/plan.md#achados-da-vm)
- **Veredito**: valid
- **Severidade**: medium

## Relato (literal ou resumido)

Na repetição da spec 005 em VM isolada com a `v0.1.0-rc.2`:

- **A2**: a reconexão de muitos agentes gerou centenas de linhas `fail:` no log da API, que reprovam o item 7 do runbook de atualização (17 e 4 linhas nas trocas de versão), e `DokpodAuditoriaFalhando` (crítico) disparou sem perda de auditoria.
- **A3**: 2 comandos `indeterminate` não dispararam `DokpodComandosIndeterminados`.

## Sintoma

O log e um alerta crítico indicam falha onde não houve perda de dado, e um alerta de evento raro não dispara nas primeiras ocorrências.

## Reprodução

1. Stack da spec 005 com 100 agentes simulados (`tools/loadtest`) e a API `v0.1.0-rc.2`.
2. Reiniciar a API (gate 7 ou troca de versão do gate 10): o PostgreSQL registra dezenas de `40001` em `inventory_projections` e `inventory_containers`, e a API registra `fail: Microsoft.EntityFrameworkCore.*` para cada tentativa repetida.
3. Matar o simulador com comandos em voo (cenário "resposta perdida"): `dokpod_audit_append_failures_total` sobe (8 na reprodução de 2026-10-28 10:43Z), e o PostgreSQL registra só `canceling statement due to user request`, nenhum erro de gravação.
4. Gerar um comando `indeterminate` numa instância nova da API: a série `dokpod_commands_completed_total{command_state="indeterminate"}` nasce com valor 1 ou 2, e `increase()` não vê o salto.

## Caminhos de Código Suspeitos

- `backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresInventoryProjectionStore.cs`: `ApplyDeltaOnceAsync` e `ReplaceSnapshotOnceAsync` usam `IsolationLevel.Serializable`. Agentes de ambientes diferentes colidem por conflito de predicado (SSI, `OnConflict_CheckForSerializationFailure`); o `RetrySerializableAsync` resolve, mas o EF registra cada tentativa em `Error`.
- `backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresAuditEventWriter.cs:AppendAsync`: o `catch` genérico chama `metrics.RecordFailure()` também para `OperationCanceledException` do chamador.
- `backend/apps/Dokpod.ControlPlane.Api/Agents/AgentControlService.cs:Connect`: o token do stream é cancelado quando o agente cai; a transação de `ApplyStatusAuditedAsync` é desfeita e o agente reentrega o resultado (journal-before-ack).
- `deploy/observability/dokpod-alerts.yml`: `DokpodComandosIndeterminados` e `DokpodAuditoriaFalhando` dependem só de `increase()`.
- `backend/apps/Dokpod.ControlPlane.Api/ApiHost.cs`: `Microsoft.EntityFrameworkCore.Database.Command` em `Information` registra cada SQL (16 mil linhas por instância em minutos, com 100 agentes).

## Hipótese de Causa Raiz

Confiança: high. Três causas independentes:

1. SERIALIZABLE é mais forte que o necessário: a projeção só precisa ordenar escritores do mesmo ambiente, mas o SSI cria dependências entre ambientes vizinhos no índice.
2. A métrica de falha de auditoria conta cancelamento do chamador como falha. Não houve evento perdido: a evidência da VM mostra 400 de 400 comandos auditados.
3. Contadores OpenTelemetry só criam a série no primeiro incremento, e a regra não cobre a série que nasce com valor.

A hipótese inicial do plano ("o `SaveChanges` compartilhado arrasta a gravação de auditoria") foi descartada na reprodução: os incrementos de falha coincidem com cancelamentos, não com `40001`.

## Correção Proposta

**Preferencial**:

- Projeção em `READ COMMITTED` com `pg_advisory_xact_lock(hashtextextended(environment_id, semente))` no início da transação. Mantém o retry para `40001`/`40P01` como rede de segurança.
- `PostgresAuditEventWriter` trata `OperationCanceledException` com o token cancelado à parte, contando em `dokpod.audit.append.canceled`.
- As regras de indeterminados e de falha de auditoria ganham a cláusula `count(x > 0 unless x offset <janela>) > 0`.
- Filtro de log `Microsoft.EntityFrameworkCore.Database.Command` em `Warning` na API.

**Alternativas**:

- `SELECT ... FOR UPDATE` na linha da projeção: não cobre a primeira gravação (linha ainda inexistente).
- Rebaixar os eventos de erro do EF com `ConfigureWarnings`: esconde também erros reais.
- Inicializar contadores com `Add(0)`: inviável para combinações abertas de tags (`command.kind`, `command.failure_code`).

**Arquivos Prováveis de Alteração**:

- `backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresInventoryProjectionStore.cs`
- `backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresAuditEventWriter.cs`, `AuditEventMetrics.cs`
- `backend/apps/Dokpod.ControlPlane.Api/ApiHost.cs`
- `deploy/observability/dokpod-alerts.yml`, `dokpod-alerts.test.yml`
- `docs/runbooks/observabilidade.md`, `docs/seguranca.md`

**Testes a Adicionar ou Atualizar**:

- Integração PostgreSQL: 40 ambientes publicando ao mesmo tempo, e 10 snapshots do mesmo ambiente em corrida.
- Unidade: cancelamento conta em `canceled`, não em `failures`.
- `promtool`: série de indeterminado e de falha de auditoria nascendo com valor.

## Riscos e Considerações

- Mudança de isolamento: só o `PostgresInventoryProjectionStore` escreve em `inventory_projections` e `inventory_containers` (verificado por busca). Leituras são consultas únicas.
- Lock consultivo: colisão de hash entre ambientes só serializa os dois, sem erro.
- A regra nova pode disparar uma vez depois de reiniciar o Prometheus, se uma série antiga tiver valor. Isso está documentado no runbook.
- O EF registra `fail: ...Transaction[20205]` também quando o `BeginTransaction` é cancelado porque o agente caiu: tratado na correção (ver Desvios em `fix.md`).

## Perguntas em Aberto

- Nenhuma.
