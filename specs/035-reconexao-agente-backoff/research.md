# Pesquisa: Reconexão do agente com espera crescente e aleatória

## R1. Forma da espera aleatória

- **Decisão**: limite exponencial `min(30 s, 1 s × 2^(n-1))` e espera sorteada uniformemente em `[limite/2, limite]` ("equal jitter").
- **Justificativa**: garante espera mínima (pior caso de 44 tentativas em 10 min: 0,5 + 1 + 2 + 4 + 8 s nas cinco primeiras e 15 s no mínimo depois, logo `1 + 5 + ⌊(600 − 15,5) / 15⌋ = 44`), o que cumpre SC-002 com garantia, e não só em média. No teto, as tentativas se espalham por 15 s, o que dá cerca de 7% por segundo com 100 agentes (SC-003 ≤ 20%).
- **Alternativas consideradas**: espera completa `[0, limite]` (média de cerca de 44 tentativas em 10 min, mas o pior caso não tem limite, o que reprova SC-002 com probabilidade não desprezível); espera "decorrelacionada" (`min(teto, aleatório(base, anterior × 3))`, mais difícil de provar e de testar); espera fixa com jitter de ±20% (como o heartbeat da 003; não reduz tentativas em quedas longas).

## R2. Teto e valor inicial

- **Decisão**: início de 1 s e teto de 30 s.
- **Justificativa**: o limite da 005 é a reconexão de todos os agentes em até 60 s; com teto de 30 s sobra folga para o TLS, o handshake e os snapshots. A indisponibilidade medida numa troca de versão foi de 33 a 36 s (005, gate 10): nessa janela a sequência chega a limites de 16 a 30 s, o que espalha a volta sem estourar SC-001.
- **Alternativas consideradas**: teto de 60 s (estoura SC-001 no pior caso); teto de 15 s (com equal jitter, o pior caso é de cerca de 82 tentativas em 10 min, só 32% de redução).

## R3. Quando recomeçar a sequência

- **Decisão**: zerar a contagem de falhas só depois de uma sessão estabelecida que durou pelo menos 60 s; sessão mais curta, ou falha antes de `SessionEstablished`, conta como falha.
- **Justificativa**: evita martelar uma API que aceita e derruba a sessão (caso de borda da spec), e 60 s cobre quatro heartbeats de 15 s.
- **Alternativas consideradas**: zerar ao receber `SessionEstablished` (recomeça a 1 s numa API instável); zerar após o primeiro heartbeat respondido (o servidor não responde heartbeat; exigiria mudança de protocolo).

## R4. Onde aplicar

- **Decisão**: só no laço da sessão de controle (`ExecuteAsync`). O stream de eventos do engine local mantém 5 s.
- **Justificativa**: o efeito de rebanho é na API compartilhada; o engine é local a cada host.
- **Alternativas consideradas**: aplicar também ao stream do engine (sem ganho; aumentaria a latência de eventos após um restart do engine).

## R5. Testabilidade

- **Decisão**: a política recebe `Random` no construtor (padrão `Random.Shared` no worker; semeado nos testes) e não faz I/O; o worker mede a duração da sessão com `TimeProvider.GetTimestamp`/`GetElapsedTime` e espera com `Task.Delay(espera, timeProvider, token)`.
- **Justificativa**: testes determinísticos sem `Task.Delay` real (FR-009); a simulação de 100 agentes roda em milissegundos.
- **Alternativas consideradas**: `FakeTimeProvider` (pacote novo; desnecessário porque a política não lê relógio).

## R6. Verificação numérica prévia

Simulação em 2026-11-20, com 500 sementes, 100 agentes que caem juntos, conexão de 0,2 s e API de volta em 35 s: última reconexão no máximo 28,4 s após a volta (SC-001 ≤ 60 s) e janela deslizante de 1 s com no máximo 19% das reconexões, mediana de 12% (SC-003 ≤ 20%). Com 10 min de queda, a última reconexão fica em até 30 s após a volta. O teste de simulação da implementação repete esse cálculo com semente fixa.
