# Avaliação de Bug: reconexão do agente disputa a fila de comandos do ambiente

- **Slug**: fencing-reconexao-status-unknown
- **Criada em**: 2026-11-28
- **Origem**: texto colado (achado do Bug Fixing `teste-mtls-intermitente`, `fix.md` e `test.md`)
- **Veredito**: valid
- **Severidade**: medium

## Relato (literal ou resumido)

`TransportBoundaryTests.KestrelSession_ReconnectFencesPreviousStream` falhou de forma intermitente
duas vezes em cerca de 65 rodadas em 2026-11-27:

```text
Assert.Equal() Failure: Values differ
Expected: Aborted
Actual:   Unknown
  at ...TransportBoundaryTests.cs:line 673
```

Ao reconectar, o stream da sessão anterior às vezes termina com `StatusCode.Unknown` em vez de
`Aborted` / `agent_session_fenced`. Autorizado pelo solicitante em 2026-11-28 ("Bug Fixing: sim").

## Sintoma

Quando um agente abre uma sessão nova para o mesmo ambiente, a sessão antiga deveria terminar
sempre com `Aborted` (`agent_session_fenced`), e a nova deveria seguir ativa e receber os
comandos. Às vezes a antiga termina com `Unknown`, porque o handler lançou uma exceção que não é
`RpcException`. A mesma corrida, na ordem inversa, pode derrubar a sessão nova ou descartar um
comando dela.

## Reprodução

1. Uma sessão A do ambiente E entra no laço de `AgentControlService.Connect` e chama
   `commandDeliveryQueue.DequeueAsync(E)`, que fica pendente.
2. Uma sessão B do mesmo E é negociada (A é invalidada), entra no laço e chama
   `DequeueAsync(E)` no mesmo canal.
3. O canal é criado com `SingleReader = true`. Uma nova leitura cancela a leitura pendente: a de A
   termina em `OperationCanceledException`.
4. Se o `Task.WhenAny` de A observa `nextCommand` antes de `invalidated`, `await nextCommand`
   lança `OperationCanceledException`. O gRPC devolve `Unknown`, e a falha observada aparece.

Prova do comportamento do canal, com um programa mínimo no SDK 10.0.403:
`SingleReader=True: primeira leitura -> OperationCanceledException`. Com `SingleReader=False`, a
primeira leitura continua pendente.

Na reprodução do teste, as duas falhas aconteceram em cerca de 65 rodadas. Depois disso, mais
260 rodadas, inclusive sob carga, não falharam. A corrida é rara e depende do escalonamento.

**Ordem inversa**, deduzida do código e não reproduzida:

- A ainda está em `OnSessionStartedAsync` ou em `ClaimDispatchableAsync`, que em produção
  acessam o PostgreSQL, quando B já está no laço.
- A leitura de A, que chega depois, cancela a de B. A sessão válida B termina com `Unknown`, e o
  agente precisa reconectar.
- Se, em vez disso, A recebe um comando enfileirado para B (com o fencing token de B), o laço de A
  cai em `continue` por token divergente. O comando sai da fila em memória sem ser entregue e só
  é recuperado em uma reconexão futura (`ClaimDispatchableAsync`) ou expira.

## Caminhos de Código Suspeitos

- `backend/apps/Dokpod.ControlPlane.Api/Commands/InMemoryAgentCommandDeliveryQueue.cs`
  (`GetQueue`): `SingleReader = true`, mas duas sessões do mesmo ambiente leem ao mesmo tempo
  durante a reconexão.
- `backend/apps/Dokpod.ControlPlane.Api/Agents/AgentControlService.cs` (laço de `Connect`):
  - `nextCommand ??= ... DequeueAsync(...)` é criado mesmo depois de a sessão ter sido cercada;
  - `await nextCommand` não trata o cancelamento vindo do canal;
  - o ramo `command.Command.FencingToken != (long)session.FencingToken` → `continue` descarta um
    comando da sessão mais nova.
- `backend/apps/Dokpod.ControlPlane.Api/ApiHost.cs:180`: a fila em memória é a registrada também
  em produção.

## Hipótese de Causa Raiz

A fila em memória declara um único leitor por ambiente, mas o protocolo de fencing admite, por um
instante, duas sessões do mesmo ambiente no laço (a antiga ainda não observou a invalidação). O
`Channel` de consumidor único cancela a leitura anterior quando chega uma nova. Por isso, conforme
a ordem, cai a sessão antiga com `Unknown` (o caso observado) ou a nova. Um comando lido pela
sessão errada é descartado em vez de devolvido. Confiança: high. O comportamento do canal foi
comprovado, e o caminho no código é direto.

## Correção Proposta

**Preferencial**:

1. Em `InMemoryAgentCommandDeliveryQueue`, criar o canal com `SingleReader = false`. Leitores
   concorrentes passam a esperar sem se cancelar, e cada comando vai para um único leitor.
2. No laço de `Connect`, quando o comando lido tem fencing token maior que o da sessão (é de uma
   sessão mais nova), reenfileirar o comando com `EnqueueAsync` em vez de descartá-lo, e encerrar
   a sessão antiga com `Aborted` / `agent_session_fenced`. Comandos com token menor (obsoletos)
   continuam descartados, como hoje.
3. A leitura pendente da sessão antiga já é cancelada no `finally` (`sessionCancellation`), e um
   canal multi-leitor ignora leitores cancelados.

**Alternativas**:
- Um canal por sessão, e não por ambiente: isola as leituras, mas muda o contrato de
  `IAgentCommandDeliveryQueue` e o roteamento do enfileiramento. A mudança é maior.
- Só tratar `OperationCanceledException` de `nextCommand` como fencing: corrige o status da
  sessão antiga, mas não impede que a antiga derrube a nova nem que um comando se perca.

**Arquivos Prováveis de Alteração**:
- `backend/apps/Dokpod.ControlPlane.Api/Commands/InMemoryAgentCommandDeliveryQueue.cs`
- `backend/apps/Dokpod.ControlPlane.Api/Agents/AgentControlService.cs`
- teste novo da fila em `backend/tests/Dokpod.ControlPlane.Api.Tests/Commands/`
- `backend/tests/Dokpod.ControlPlane.Api.Tests/Agents/TransportBoundaryTests.cs`

**Testes a Adicionar ou Atualizar**:
- Fila: duas leituras concorrentes do mesmo ambiente não se cancelam, e um comando enfileirado
  vai para exatamente um leitor.
- Sessão: com uma sessão antiga cercada e uma nova ativa, um comando enfileirado com o token novo
  chega à sessão nova (mesmo que a antiga o leia antes), e a antiga termina com `Aborted`.
- Manter `KestrelSession_ReconnectFencesPreviousStream` e validá-lo em laço.

## Riscos e Considerações

- Com vários leitores, a ordem de entrega entre sessões não é garantida. Só a sessão ativa
  processa, e as cercadas devolvem o comando.
- O reenfileiramento põe o comando no fim da fila do ambiente. Durante a reconexão, a ordem entre
  comandos pode mudar. Os comandos são idempotentes e independentes (ID idempotente, expiração).
  [NEEDS CLARIFICATION: confirmar que nenhuma spec exige ordem estrita entre comandos de um
  ambiente. Não encontrei essa exigência.]
- Não há mudança de protocolo gRPC, contrato público, schema ou migração. O status
  `agent_session_fenced` passa a ser consistente.
- O CE e o Business usam a mesma fila em memória, e os dois são afetados.

## Perguntas em Aberto

- [NEEDS CLARIFICATION: exigência de ordem estrita entre comandos de um ambiente. Não bloqueia: a
  ordem já não é garantida entre a recuperação (`ClaimDispatchableAsync`) e a fila.]
