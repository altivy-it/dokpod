# Modelo de Dados: Janelas de mudança por estágio

**Spec**: [spec.md](spec.md) | **Pesquisa**: [research.md](research.md)

## Janela de mudança (`dokpod.change_windows`)

Uma linha por estágio com janela. Sem linha, o estágio não tem janela e nada muda. A tabela não cresce e tem no máximo uma linha por valor de `EnvironmentStage`, por isso não é particionada.

| Coluna | Tipo | Regra |
| --- | --- | --- |
| `stage` | `text` PK | `production`, `staging` ou `development` (`EnvironmentStages.ToValue`) |
| `time_zone` | `text` não nulo | identificador IANA aceito por `TimeZoneInfo.TryFindSystemTimeZoneById`, até 64 caracteres; um por estágio |
| `ranges` | `jsonb` não nulo | de 1 a 14 faixas (ver abaixo) |
| `version` | `integer` não nulo | começa em 1 e soma 1 a cada `PUT`; vira o `ETag` |
| `updated_by` | `text` não nulo | rótulo do autor, como na política de justificativa |
| `updated_at_utc` | `timestamptz` não nulo | horário da última alteração |

### Faixa (elemento de `ranges`)

| Campo | Tipo | Regra |
| --- | --- | --- |
| `days` | lista de `mon`…`sun` | de 1 a 7 dias, sem repetição; é o dia de início |
| `start` | `HH:mm` | de `00:00` a `23:59` |
| `end` | `HH:mm` | diferente de `start`; menor que `start` indica que a faixa cruza a meia-noite |

Faixas sobrepostas são aceitas e valem como união ([D-004](research.md#d-004-fuso-e-cálculo-da-janela)).

## Estado calculado (não persistido)

`ChangeWindowCalendar.Evaluate(window, instanteUtc)` devolve:

| Campo | Significado |
| --- | --- |
| `state` | `open`, `closed` ou `none` (estágio sem janela, host sem estágio ou CE) |
| `closesAt` | com `open`, o fim da união contígua das faixas abertas |
| `nextOpensAt` | com `closed`, a próxima abertura nos 8 dias seguintes |
| `timeZone` | o fuso do estágio, para exibição |

## Liberação (`ChangeWindowClearance`, em memória)

Só o guard cria a liberação (construtor privado, fábrica no próprio guard); a fila exige uma para enfileirar ([D-001](research.md#d-001-ponto-único-de-aplicação)).

| Campo | Regra |
| --- | --- |
| `EnvironmentId`, `Kind` | precisam ser iguais aos do comando, senão a fila recusa |
| `Mode` | `NoWindow`, `Open`, `Override` ou `Replay` (o `commandId` já existe; a fila só devolve `Duplicate` ou `ConflictingPayload`, nunca cria) |
| `OverrideAuditEvent` | presente só com `Override`; é gravado na mesma transação do comando |

## Exceção de janela (evento de auditoria `change_window.override`)

Não tem tabela própria; fica na trilha da 017, que já é particionada por mês.

| Campo | Origem |
| --- | --- |
| ação (`container.stop`, `image.pull`, …), host, alvo | o comando |
| autor | `actor.Subject` e rótulo |
| justificativa | mascarada pelo `JustificationMasker` |
| `commandId` | o comando liberado (no terminal, a sessão) |
| próxima janela | `nextOpensAt` no instante da exceção |

## Transições

```text
sem janela ──PUT (If-None-Match: *)──> janela v1
janela vN ──PUT (If-Match: vN)──> janela vN+1
janela vN ──DELETE (If-Match: vN)──> sem janela
licença restrita: PUT → 403 license_*; DELETE e aplicação continuam
```
