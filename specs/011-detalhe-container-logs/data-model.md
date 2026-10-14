# Modelo de Dados: Detalhe, Inspeção e Logs

Nenhuma tabela nova. Conteúdo é efêmero. `audit_events` recebe colunas novas (D-006).

## Auditoria

Colunas novas em `audit_events` (migration expand, D-006):

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `subject_id` | varchar(256) null | ID do container consultado. |
| `details` | jsonb null | ≤ 2 KiB; chaves permitidas por ação; somente números, booleanos e enums. |

Nova sobrecarga de `dokpod_append_audit_event(..., p_subject_id, p_details)`; a assinatura atual é mantida.

Tipos novos:

| Tipo | Dados |
| --- | --- |
| `container.inspected` | ator, ambiente, container, tipo (`summary`\|`inspect`), correlação |
| `container.logs.read` | ator, ambiente, container, `tail`/`since`, filtro presente (bool, nunca o termo), linhas e bytes retornados |
| `container.logs.stream_started` / `stream_ended` | ator, ambiente, container, sessão, motivo de fim, duração, linhas |

## Efêmeros

- `ContainerConfigSummary`: `command`, `user`, `workingDir`, `healthcheck{test, intervalSeconds, retries, timeoutSeconds}`, `labels{}` (≤ 50, valores ≤ 256), `logging{driver, options{}}`, `ports[]`, `networks[]{name, driver, ipAddress}`, `volumes[]{type, name, destination, readOnly}`, `restartPolicy{name, maximumRetryCount}`, `cpuLimitCores`, `memoryLimitBytes`, `environmentVariableCount`, `secretCount`.
- `LogLine`: `timestamp`, `stream` (`stdout`\|`stderr`), `level?` (inferido no agente), `text` (mascarado, ≤ 16 KiB).
- `LogSession` (memória da API): `sessionId`, `actor`, `environmentId`, `containerId`, `startedAt`, `expiresAt`, `lastAuthorizedAt`, `linesDelivered`, `bytesDelivered`, `connectionId`.
- Buffer do navegador: no máximo 10.000 linhas ou 5 MiB, descartando as mais antigas.

## Configuração

`Logs:DefaultTail` 500, `Logs:MaxTail` 5000, `Logs:MaxResponseBytes` 1 MiB, `Logs:LiveMaxDuration` 15 min, `Logs:LiveMaxLinesPerSecond` 200, `Logs:LiveMaxBytesPerSecond` 256 KiB, `Logs:MaxLiveSessionsPerAgent` 10, `Logs:MaxLiveSessionsPerUser` 3, `Logs:AuthorizationRecheckInterval` 60 s, `Inspect:MaxBytes` 256 KiB, `Agent:Queries:Timeout` 10 s, `Agent:Queries:MaxConcurrentPerAgent` 4, `Agent:Queries:MaxMessageBytes` 256 KiB (abaixo do limite gRPC de 1 MiB).
