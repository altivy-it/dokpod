# Correção de Bug: reconexão do agente disputa a fila de comandos do ambiente

- **Slug**: fencing-reconexao-status-unknown
- **Corrigido em**: 2026-11-28
- **Avaliação**: ./assessment.md
- **Estado**: applied

## Resumo

A fila em memória de entrega de comandos passou a aceitar vários leitores por ambiente. Assim, na
reconexão, a leitura da sessão nova não cancela mais a da sessão cercada, e vice-versa. Um comando
de uma sessão mais nova lido pela sessão cercada volta para a fila, e a sessão cercada termina com
`Aborted` / `agent_session_fenced`, em vez de descartar o comando.

## Alterações

| Arquivo | Alteração | Notas |
|---------|-----------|-------|
| `backend/apps/Dokpod.ControlPlane.Api/Commands/InMemoryAgentCommandDeliveryQueue.cs` | modificado | `SingleReader = false`, com comentário |
| `backend/apps/Dokpod.ControlPlane.Api/Agents/AgentControlService.cs` | modificado | comando com fencing token maior que o da sessão: reenfileira e encerra com `agent_session_fenced` |
| `backend/tests/Dokpod.ControlPlane.Api.Tests/Commands/InMemoryAgentCommandDeliveryQueueTests.cs` | teste adicionado | leitores concorrentes não se cancelam, e o comando vai para um só |
| `backend/tests/Dokpod.ControlPlane.Api.Tests/Agents/TransportBoundaryTests.cs` | teste adicionado | comando de sessão mais nova volta à fila, e a sessão cercada termina com `Aborted` |

## Destaques do Diff

```csharp
if (command.Command.EnvironmentId == session.EnvironmentId &&
    command.Command.FencingToken > (long)session.FencingToken)
{
    await commandDeliveryQueue.EnqueueAsync(command, CancellationToken.None);
    throw new RpcException(new Status(StatusCode.Aborted, "agent_session_fenced"));
}
```

Comandos com token menor que o da sessão (obsoletos) continuam descartados, como antes.

## Testes Adicionados ou Atualizados

- `InMemoryAgentCommandDeliveryQueueTests.ConcurrentReadersOfTheSameEnvironment_DoNotCancelEachOther_AndACommandGoesToExactlyOne`:
  protege contra o cancelamento entre leitores, que é a causa do `Unknown`.
- `TransportBoundaryTests.KestrelSession_CommandOfANewerSession_IsReturnedToTheQueue_AndTheStaleSessionIsFenced`:
  protege contra a perda de comando na reconexão e garante o status `Aborted`.

## Verificação Local

Todos os comandos rodaram no container `lzocateli/dotnet-sdk:10.0.403-noble-v1`.

- Sem a correção (patch de produção revertido), os dois testes novos falham:
  - o da fila com `Expected: False, Actual: True`, porque a primeira leitura foi cancelada;
  - o da sessão com `Expected: Aborted, Actual: DeadlineExceeded`, depois de 90 s: o comando foi
    descartado e a sessão só caiu pelo heartbeat.
- Com a correção, os dois passam, junto com `KestrelSession_ReconnectFencesPreviousStream`.
- Regressão: `dotnet test Dokpod.slnx -c Release --filter 'Category!=Integration'`, com todas as
  suítes verdes. Na API foram 179 aprovados e 62 ignorados (dependem de ambiente), sem falhas.
- `TransportBoundaryTests` em laço: 25 de 25 rodadas com 19 de 19 aprovados.
- `dotnet format whitespace --verify-no-changes` nos quatro arquivos: exit 0.

## Desvios da Avaliação

Nenhum. O teste de sessão usa um comando com o token seguinte ao da sessão, o que simula de forma
determinística a sessão cercada lendo o comando da nova, sem depender da corrida.

## Acompanhamento

- A pergunta aberta sobre ordem estrita entre comandos de um ambiente continua registrada na
  avaliação. Nenhuma spec encontrada exige essa ordem.
