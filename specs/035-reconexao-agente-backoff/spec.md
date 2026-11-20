# Especificação da Funcionalidade: Reconexão do agente com espera crescente e aleatória

**Branch da Funcionalidade**: `035-reconexao-agente-backoff`

**Criada em**: 2026-11-20

**Status**: Rascunho

**Origem**: IA assistida; solicitante e revisor humano: Lincoln (`@lzocateli`), que pediu a avaliação e a implementação em 2026-11-20.

**Entrada**: Descrição fornecida: "Reconexão do agente com backoff exponencial e jitter. Hoje o agente espera 5 s fixos entre tentativas quando a sessão com a API cai; após um restart da API todos os agentes reconectam no mesmo instante (spec 005 achado A2: ~131 colisões 40001 com 100 agentes) e, com a API fora por muito tempo, cada agente tenta e loga aviso a cada 5 s. Objetivo: espalhar as reconexões e reduzir tentativas durante quedas longas, mantendo o limite da spec 005 de reconexão de todos os agentes em até 60 s após a API voltar. Escopo: somente o agente (Linux container e Windows Service, mesmo worker); sem mudança de protocolo/contrato, compatível N/N-1. Fora de escopo: estado 'reconectando' no console."

**Depende de**: [003](../003-inventario-lifecycle-reconciliacao/spec.md) (sessão, heartbeat e reconciliação após reconexão), [005](../005-qualificacao-release-docker-linux/spec.md) (limite de reconexão ≤ 60 s e achado A2), [016](../016-governanca-alertas/spec.md) (alerta de agente desconectado com duração).

## Contexto

Quando a sessão com a API cai, o agente espera sempre 5 s e tenta de novo. Isso tem dois efeitos:

| Situação | Hoje | Consequência |
| --- | --- | --- |
| Restart ou atualização da API | Todos os agentes caíram no mesmo instante e, com a mesma espera fixa, voltam juntos | Pico de sessões, snapshots e gravações simultâneas. Na 005 (100 agentes) gerou cerca de 131 conflitos de serialização no restart (achado A2, já mitigado por retentativa no servidor, mas o pico continua) |
| API fora por muito tempo | Cada agente tenta 12 vezes por minuto e registra um aviso em cada tentativa | Ruído no log do host e conexões TLS inúteis contra uma borda que pode estar sob manutenção |

O estado "reconectando" no console, avaliado junto, **fica fora do escopo**: contraria a decisão 008 D-005 (a situação do host não usa `last_seen_at`), exige mudança de contrato e de interface, e o alerta de agente desconectado já espera a duração da regra antes de abrir.

## Cenários de Usuário e Testes *(obrigatório)*

### História de Usuário 1 - Reconexões espalhadas após restart da API (Prioridade: P1)

Como operador que atualiza ou reinicia a API, quero que os agentes voltem espalhados no tempo, e não todos no mesmo instante, para que a volta da API não gere um pico de carga nem erros no log, e todos os hosts voltem a aparecer conectados em até 60 s.

**Motivo da prioridade**: É o problema medido na 005 e acontece em toda atualização.

**Teste independente**: Com a política de espera e uma fonte aleatória controlada, simular 100 agentes que perdem a sessão no mesmo instante e uma API que volta após 35 s; medir a distribuição das tentativas e o tempo até a última reconexão. Com um agente real, reiniciar a API e medir o tempo de volta.

**Cenários de aceitação**:

1. **Dado** 100 agentes conectados, **Quando** a API fica fora por 35 s e volta, **Então** todos reconectam em até 60 s após a volta e as tentativas não se concentram num mesmo segundo.
2. **Dado** um agente que reconectou e manteve a sessão estável, **Quando** a sessão cai de novo, **Então** a espera recomeça do valor inicial, e não do último valor alto.

---

### História de Usuário 2 - Menos tentativas e menos ruído durante quedas longas (Prioridade: P2)

Como operador do host, quero que o agente espace as tentativas enquanto a API estiver fora, até um teto, para que o log do host não seja inundado e a borda não receba conexões inúteis.

**Motivo da prioridade**: Reduz ruído e carga, mas não afeta a disponibilidade.

**Teste independente**: Simular uma API indisponível por 10 minutos e contar as tentativas e os avisos registrados por um agente.

**Cenários de aceitação**:

1. **Dado** a API indisponível por 10 minutos, **Quando** o agente tenta reconectar, **Então** faz no máximo 45 tentativas (hoje são 120), e cada aviso informa o número da tentativa e a próxima espera.
2. **Dado** o agente esperando a próxima tentativa, **Quando** o serviço do agente é parado, **Então** ele encerra imediatamente, sem aguardar o fim da espera.

### Casos de Borda

- **Queda logo após a sessão ser estabelecida** (API aceita e derruba em seguida, repetidamente): a espera não volta ao valor inicial, para o agente não martelar uma API instável.
- **Certificado revogado ou sessão recusada pela API**: o agente continua tentando com a mesma política, até o teto; a revogação continua bloqueando a sessão como hoje (001 SC-003).
- **Agente iniciado com a API fora**: a primeira tentativa é imediata; as seguintes seguem a política.
- **Stream de eventos do engine local**: fora do escopo; continua com a espera atual, porque o engine é local e não sofre o efeito de rebanho.
- **Agentes N-1**: continuam com a espera fixa; convivem com agentes N sem mudança no servidor.

## Requisitos *(obrigatório)*

### Requisitos Funcionais

- **FR-001**: A primeira tentativa de conexão ao iniciar o agente DEVE ser imediata.
- **FR-002**: Após uma falha, a espera antes da próxima tentativa DEVE crescer exponencialmente a partir de 1 s (1, 2, 4, 8, 16 s…), limitada a um teto de 30 s.
- **FR-003**: Cada espera DEVE ser sorteada uniformemente entre a metade e o total do limite da tentativa, para que agentes que caíram juntos não voltem juntos e a espera nunca fique abaixo da metade do limite.
- **FR-004**: A sequência de esperas DEVE recomeçar do valor inicial somente depois de uma sessão que durou pelo menos 60 s; uma sessão mais curta conta como falha para a progressão.
- **FR-005**: A espera DEVE ser cancelada imediatamente quando o serviço do agente for parado.
- **FR-006**: Cada falha DEVE gerar um único aviso no log com o número da tentativa consecutiva, o status da falha e a espera escolhida, sem certificados, tokens ou outros segredos.
- **FR-007**: O comportamento DEVE ser o mesmo no agente Linux (container) e no agente Windows (Windows Service).
- **FR-008**: A mudança NÃO DEVE alterar o protocolo, as mensagens ou o servidor; agentes N e N-1 continuam compatíveis com servidores N e N-1.
- **FR-009**: A política DEVE ser testável de forma determinística, com fonte aleatória e relógio controláveis nos testes.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: Após uma indisponibilidade da API de até 10 minutos, todos os agentes voltam a ficar conectados em até 60 s depois que a API volta (limite da 005).
- **SC-002**: Com a API fora por 10 minutos, cada agente faz no máximo 45 tentativas de conexão (redução de pelo menos 62% em relação às 120 atuais).
- **SC-003**: Com 100 agentes que perdem a sessão no mesmo instante, nenhuma janela de 1 s concentra mais de 20% das tentativas de reconexão depois que a API volta (hoje, praticamente 100% caem na mesma janela).
- **SC-004**: Um restart da API não abre alerta de agente desconectado com a duração padrão da regra (2 minutos).

## Premissas

- O teto de 30 s e o início de 1 s são suficientes para cumprir o limite de 60 s da 005 com folga para o handshake TLS e o envio dos snapshots.
- O tempo médio de volta após um restart curto pode subir em relação aos 5 a 7 s medidos na 005; isso é aceito, desde que respeite SC-001 e SC-004.
- Não há configuração exposta ao operador para a política nesta entrega; os valores são fixos no agente.
- O simulador de carga da 005 (`tools/loadtest/`) tem política própria e não faz parte desta entrega; a validação de distribuição usa a política do agente com fonte aleatória controlada.
