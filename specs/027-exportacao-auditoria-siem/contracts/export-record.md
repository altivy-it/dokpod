# Contrato: registro exportado ao SIEM

Mesmos campos e mesma sanitização da trilha que o console e o CSV da spec 017 usam ([D-010](../research.md#d-010-conteúdo-exportado)). Nunca contém conteúdo de terminal, valores de variáveis, tokens, certificados nem segredos.

## Registro JSON (`formatVersion` 1)

```json
{
  "formatVersion": 1,
  "eventId": "6f1d…",
  "occurredAt": "2026-11-01T12:00:00.123456Z",
  "installationId": "3f0c…",
  "correlationId": "…",
  "commandId": null,
  "actor": { "kind": "user", "id": "…", "name": "Maria" },
  "action": "container.restart",
  "category": "containers",
  "environmentId": "…",
  "subjectId": "web-1",
  "outcome": "success",
  "failureCode": null,
  "details": { },
  "test": false
}
```

- `eventId` é o identificador único para deduplicação no destino. Uma reentrega leva o mesmo `eventId`.
- O evento de teste tem `action` `audit_export.test`, `test: true` e `eventId` novo; ele não entra na trilha local, só o registro `audit_export.test_sent`.
- Campos novos podem ser acrescentados no mesmo `formatVersion`; remover ou mudar o significado de um campo exige `formatVersion` 2.

## Syslog (RFC 5424 sobre TLS, RFC 5425)

`<PRI>1 TIMESTAMP HOSTNAME dokpod - MSGID - BOM+JSON`, com enquadramento por contagem de octetos (`LEN SP MSG`).

| Campo | Valor |
| --- | --- |
| `PRI` | facility `security/authorization` (10, *authpriv*); severidade `notice` (5) para sucesso e `warning` (4) para negado ou falha |
| `TIMESTAMP` | `occurredAt` com microssegundos |
| `HOSTNAME` | nome configurado da instalação ou `-` |
| `APP-NAME` | `dokpod` |
| `PROCID` | `-` |
| `MSGID` | `action`, truncada em 32 caracteres ASCII |
| `STRUCTURED-DATA` | `-` (ver [D-004](../research.md#d-004-syslog)) |
| `MSG` | BOM UTF-8 seguido do registro JSON |

## OTLP/HTTP (protobuf, `POST {path}`, padrão `/v1/logs`)

| Campo OTLP | Valor |
| --- | --- |
| `Resource.attributes` | `service.name=dokpod`, `dokpod.installation.id` |
| `ScopeLogs.scope.name` | `dokpod.audit` |
| `LogRecord.time_unix_nano` | `occurredAt` |
| `LogRecord.severity_number` / `severity_text` | `INFO` (9) para sucesso; `WARN` (13) para negado ou falha |
| `LogRecord.body` | `action` |
| `LogRecord.attributes` | `dokpod.event.id`, `dokpod.correlation.id`, `dokpod.command.id`, `dokpod.actor.kind`, `dokpod.actor.id`, `dokpod.actor.name`, `dokpod.action.category`, `dokpod.environment.id`, `dokpod.subject.id`, `dokpod.outcome`, `dokpod.failure.code`, `dokpod.details` (JSON), `dokpod.test` |
| Cabeçalho | `Authorization: Bearer <token>` quando o secret traz `token` |

Uma resposta 2xx confirma o lote inteiro e avança o cursor. Redirecionamentos (3xx) não são seguidos e viram `rejected` (L1). Uma resposta 4xx que não seja 408 ou 429 vira `rejected` e não avança o cursor. As demais falhas tentam de novo com espera exponencial até 60 s. Conexão em até 10 s, envio em até 30 s, corpo da resposta lido até 64 KiB e descartado (L2).
