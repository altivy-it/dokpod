# Verificação de Bug: reconexão do agente disputa a fila de comandos do ambiente

- **Slug**: fencing-reconexao-status-unknown
- **Testado em**: 2026-11-28
- **Avaliação**: ./assessment.md
- **Correção**: ./fix.md
- **Resultado**: verified

## Resumo

Os testes novos reproduzem, de forma determinística, as duas causas da avaliação, e falham sem a
correção:

- a leitura nova cancela a pendente;
- o comando da sessão nova é descartado pela sessão cercada.

Com a correção, os dois passam e não houve regressão. O `Unknown` exato do teste original depende
de timing e não foi forçado. A causa dele, o cancelamento entre leitores, foi reproduzida e
eliminada.

## Verificações Executadas

| Verificação | Comando / Ação | Resultado | Notas |
|-------------|----------------|-----------|-------|
| Reprodução da causa (fila) | `InMemoryAgentCommandDeliveryQueueTests`, com e sem o patch | pass | Sem a correção: `Expected: False, Actual: True` (leitura cancelada). Com a correção: passa. |
| Reprodução da perda de comando | `KestrelSession_CommandOfANewerSession_…`, com e sem o patch | pass | Sem a correção: `DeadlineExceeded` após 90 s. Com a correção: `Aborted`, e o comando volta à fila. |
| Teste original | `TransportBoundaryTests` 25 vezes | pass | 25 de 25 rodadas com 19 de 19 aprovados. |
| Suíte de regressão | `dotnet test Dokpod.slnx -c Release --filter 'Category!=Integration'` | pass | Todas as suítes verdes; API com 179 aprovados e 62 ignorados. |
| Lint / formatação | `dotnet format whitespace --verify-no-changes` nos arquivos alterados | pass | exit 0. |
| Agente real em reconexão | quality ou E2E | skipped | Exige deploy. Pode ser coberto pela próxima validação com agentes reais. |

## Trechos de Saída

```text
== sem a correção
  Failed ...KestrelSession_CommandOfANewerSession_IsReturnedToTheQueue_AndTheStaleSessionIsFenced [1 m 30 s]
  Failed ...InMemoryAgentCommandDeliveryQueueTests.ConcurrentReadersOfTheSameEnvironment_... [119 ms]
== com a correção
Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3
     25 Passed!  - Failed:     0, Passed:    19, Skipped:     0, Total:    19
```

## Riscos Residuais

- Os testes de integração com PostgreSQL não rodaram. A mudança não toca persistência: a fila é
  em memória, e o store de comandos não mudou.
- Durante a reconexão, a ordem entre comandos de um ambiente pode mudar, porque o comando
  reenfileirado vai para o fim da fila (pergunta aberta na avaliação).

## Recomendação

Encerre o bug: a causa foi reproduzida e eliminada, com testes que impedem a regressão. Confirme
no CI e na próxima validação com agentes reais.
