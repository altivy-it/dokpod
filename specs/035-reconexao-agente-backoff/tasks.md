---

description: "Tarefas da feature 035: reconexão do agente com espera crescente e aleatória"
---

# Tarefas: Reconexão do agente com espera crescente e aleatória

**Entrada**: Documentos de design em `/specs/035-reconexao-agente-backoff/`

**Pré-requisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md) e [quickstart.md](quickstart.md). Sem `contracts/`: nenhuma interface externa muda.

**Testes**: a mudança altera o comportamento do agente; os testes vêm antes da implementação em cada história.

**Origem**: IA assistida; revisor humano: Lincoln (`@lzocateli`).

## Formato: `[ID] [P?] [Story] Descricao`

- **[P]**: Pode ser executada em paralelo (arquivos diferentes, sem dependências)
- **[Story]**: História de usuário relacionada (US1, US2)

## Fase 1: Preparação

Sem tarefas: os projetos `backend/libs/Dokpod.Agent.Application` e `backend/tests/Dokpod.Agent.Application.Tests` já existem, e não há dependência nova.

---

## Fase 2: Fundação (Pré-requisitos Bloqueadores)

**Objetivo**: a política pura de espera, usada pelas duas histórias.

- [X] T001 Criar `AgentReconnectBackoff` em `backend/libs/Dokpod.Agent.Application/Protocol/AgentReconnectBackoff.cs`, conforme [data-model.md](data-model.md): construtor com `Random` (obrigatório, sem padrão escondido), constantes públicas `InitialDelay = 1 s`, `MaxDelay = 30 s` e `StableSession = 60 s`; método `NextDelay(TimeSpan sessionDuration)` que zera `consecutiveFailures` quando `sessionDuration ≥ StableSession`, depois soma 1 e devolve um `readonly record struct` com o número da tentativa (`int Attempt`, ≥ 1) e a espera (`TimeSpan Delay`), sorteada uniformemente em `[limite/2, limite]` com `limite = min(MaxDelay, InitialDelay × 2^(Attempt − 1))`, sem overflow para `Attempt` grande. Sem I/O, sem relógio, comentário de uma linha referenciando a spec 035 no estilo do arquivo vizinho `AgentCommandProtocolHandler.cs`.

**Checkpoint**: a política compila e pode ser usada pelo worker.

---

## Fase 3: História de Usuário 1 - Reconexões espalhadas após restart da API (Prioridade: P1) MVP

**Objetivo**: agentes que caíram juntos voltam espalhados e em até 60 s após a API voltar; a sequência recomeça só após sessão estável.

**Teste independente**: `dotnet test backend/tests/Dokpod.Agent.Application.Tests --filter "FullyQualifiedName~AgentReconnectBackoff"` (quickstart, seção 1) e restart real da API (quickstart, seção 3).

### Testes da História de Usuário 1

- [X] T002 [P] [US1] Em `backend/tests/Dokpod.Agent.Application.Tests/Protocol/AgentReconnectBackoffTests.cs`, testar com `new Random(seed)`: limites 1, 2, 4, 8, 16, 30, 30 s nas sete primeiras falhas e cada espera em `[limite/2, limite]` (FR-002, FR-003); `Attempt` 1, 2, 3…; `NextDelay(59 s)` não zera e `NextDelay(60 s)` zera a sequência (FR-004); `Attempt` de 1.000 falhas seguidas continua com espera ≤ 30 s (sem overflow).
- [X] T003 [P] [US1] No mesmo arquivo, simulação determinística (semente fixa) de 100 instâncias que perdem a sessão em t = 0, conexão de 0,2 s por tentativa e API de volta em t = 35 s e em t = 600 s: nos dois casos a última reconexão ocorre em ≤ 60 s após a volta (SC-001) e nenhuma janela deslizante de 1 s concentra mais de 20 das 100 reconexões (SC-003), conforme [research.md R6](research.md). Evidência: `AgentReconnectBackoffTests` (casos de 35 s e 600 s) passando.

### Implementação da História de Usuário 1

- [X] T004 [US1] Em `backend/apps/Dokpod.Agent/AgentControlWorker.cs`: injetar `TimeProvider` opcional no construtor primário (padrão `TimeProvider.System`, mantendo os chamadores atuais, inclusive `backend/tests/Dokpod.ControlPlane.Api.Tests/Agents/TransportBoundaryTests.cs`); no laço de `ExecuteAsync`, criar uma `AgentReconnectBackoff(Random.Shared)` antes do `while`, guardar `timeProvider.GetTimestamp()` antes de `RunSessionAsync`, e após qualquer saída da sessão (falha capturada ou fim normal do stream) chamar `NextDelay(timeProvider.GetElapsedTime(início))` e esperar com `Task.Delay(delay, timeProvider, stoppingToken)`. A primeira tentativa continua imediata (FR-001). Renomear `ReconnectDelay` para `EngineEventsReconnectDelay` e mantê-lo só no stream de eventos do engine (R4).
- [X] T005 [US1] Rodar `TransportBoundaryTests` (quickstart, seção 2) e confirmar que as sessões reais continuam estabelecendo e encerrando sem mudança de protocolo (FR-008). Evidência: 18/18 `TransportBoundaryTests` em 2026-11-20.

**Checkpoint**: US1 entrega o espalhamento e o limite de 60 s.

---

## Fase 4: História de Usuário 2 - Menos tentativas e menos ruído durante quedas longas (Prioridade: P2)

**Objetivo**: no máximo 45 tentativas em 10 min de queda, aviso informativo e parada imediata.

**Teste independente**: testes da política (quickstart, seção 1) e log do agente real durante a queda (quickstart, seção 3).

### Testes da História de Usuário 2

- [X] T006 [P] [US2] Em `backend/tests/Dokpod.Agent.Application.Tests/Protocol/AgentReconnectBackoffTests.cs`, com um `Random` cujo `NextDouble()` devolve sempre 0 (pior caso, subclasse de teste): somar 1 tentativa imediata mais as esperas até 600 s e verificar ≤ 45 tentativas (SC-002, R1).

### Implementação da História de Usuário 2

- [X] T007 [US2] Em `backend/apps/Dokpod.Agent/AgentControlWorker.cs`, trocar o aviso atual por um único `LogWarning` por falha com o status (`GetStatus(exception)`), o número da tentativa e a espera em segundos, sem certificado, senha nem metadados (FR-006); para o fim normal do stream sem exceção, registrar o mesmo aviso com status `stream_ended`. Confirmar que `Task.Delay(..., stoppingToken)` lança `OperationCanceledException` tratada pelo laço, encerrando sem aguardar a espera (FR-005).

**Checkpoint**: US1 e US2 completas.

---

## Fase 5: Refinamento e Aspectos Transversais

- [X] T008 Rodar `dotnet format --verify-no-changes` e `dotnet build` de `Dokpod.slnx` e os testes de `backend/tests/Dokpod.Agent.Application.Tests` em container `lzocateli/dotnet-sdk:10.0.403-noble-v1`; registrar o resultado. Evidência (2026-11-20): `dotnet build Dokpod.slnx -c Release` sem warnings, `dotnet format --verify-no-changes` nos 3 arquivos, 164/164 em `Dokpod.Agent.Application.Tests` (6 novos).
- [X] T009 [P] Atualizar a descrição da reconexão do agente em `docs/arquitetura.md`, na seção do contrato de sessão `Connect` com a política: imediata, 1 s dobrando até 30 s, espera entre metade e o limite, recomeço após sessão de 60 s.
- [X] T010 Validar com agente real (quickstart, seção 3): parar a API por cerca de 35 s, medir o tempo até `Agent control session established` (≤ 60 s), conferir os avisos e a ausência de alerta; registrar a evidência em `specs/035-reconexao-agente-backoff/quickstart.md` ou como `NOT RUN` com o motivo; registrar que o agente Windows não foi validado em host Windows (qualificação da 006 não substituída). Evidência (2026-11-20, VPS de homologação, Docker rootless, agente e API na `v0.10.0-rc.1` após `upgrade --answers` a partir da 0.9.2-rc.1): API parada às 15:38:50 e iniciada às 15:39:26 (36 s); seis avisos `Agent control session disconnected with status Unavailable; attempt N, reconnecting in X s`, com esperas de 1,0, 2,0, 3,4, 4,1, 10,7 e 17,2 s, dentro das faixas [0,5–1], [1–2], [2–4], [4–8], [8–16] e [16–30] s, e sem segredos; `Agent control session established` às 15:39:29, 3,7 s após o start da API (≤ 60 s, SC-001); sem alerta (indisponibilidade de 39 s, abaixo dos 2 min da regra padrão, SC-004); todos os containers healthy e borda com HTTP 200. O agente Windows não foi validado em host Windows (qualificação da 006 não substituída).

---

## Dependências e Ordem de Execução

- **Fundação (T001)** bloqueia todas as histórias.
- **US1 (T002–T005)**: T002 e T003 podem ser escritos em paralelo, antes de T001 terminar (devem falhar sem a política); T004 depende de T001; T005 depende de T004.
- **US2 (T006–T007)**: T006 depende de T001; T007 depende de T004 (mesmo arquivo).
- **Refinamento**: T008 depois de T007; T009 em paralelo a qualquer momento após T001; T010 depois de T008 e de uma imagem do agente com a mudança.

## Exemplos de Paralelismo

```text
US1: T002 e T003 (mesmo arquivo de teste, seções distintas; escreva juntos)
US2: T006 junto com T004
Refinamento: T009 em paralelo a T008
```

## Estratégia de Implementação

1. MVP: T001 a T005 (US1). Já reduz o pico de reconexão e cumpre SC-001/SC-003.
2. Incremento: T006 e T007 (US2), aviso informativo e limite de tentativas.
3. Fechamento: T008 a T010.
