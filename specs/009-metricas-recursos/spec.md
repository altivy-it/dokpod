# Especificação da Feature: Métricas de Recursos de Hosts e Containers

**Branch da feature**: `007-console-operacional`

**Criada em**: 2026-10-12

**Estado**: `implemented` - aprovada em 2026-10-13 ([specs/README.md](../README.md)); evidências e itens `NOT RUN` em [tasks.md](tasks.md).

**Entrada**: Board `01 · Visão geral` (KPIs "CPU média do cluster" e "Memória alocada", gráfico "Uso de recursos do cluster · últimas 12 horas", coluna CPU dos hosts), board `02 · Containers` (colunas CPU e Memória), board `03 · Detalhe` (card "Recursos agora", aba "Estatísticas · últimos 30 minutos") e board `04 · Novo container` (CPU do host na seleção).

**Depende de**: [008](../008-visao-geral-hosts-eventos/spec.md) (descritor de host, CPUs e memória).

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Uso de recursos do estágio (Prioridade: P1)

Como operador, quero ver a CPU média e a memória em uso de todos os hosts do estágio, agora e nas últimas 12 horas, com a linha de limite operacional de 80%, para saber se há folga antes de criar containers.

**Valor desta prioridade**: Indicadores e gráfico centrais da Visão geral.

**Teste independente**: Gerar carga sintética conhecida em hosts de teste e comparar KPIs e série com as medições do engine.

**Cenários de aceite**:

1. **Dado** hosts conectados com amostras recentes, **Quando** a Visão geral carrega, **Então** "CPU média do cluster" e "Memória alocada" mostram percentuais com uma casa decimal.
2. **Dado** 12 horas de amostras, **Quando** o gráfico é exibido, **Então** mostra séries de CPU e memória por hora (ou granularidade mais fina) com a linha de referência de 80% e nota explicativa.
3. **Dado** intervalo sem amostras (agente offline), **Quando** exibido, **Então** o gráfico mostra lacuna, não zero.
4. **Dado** host não autorizado, **Quando** calculado o agregado, **Então** ele não entra no cálculo.

### Jornada do Usuário 2 - Uso por host (Prioridade: P1)

Como operador, quero ver a CPU atual de cada host com barra e destaque acima de 80%, na Visão geral, na tela Hosts e na escolha de host ao criar container.

**Valor desta prioridade**: Identifica rapidamente o host saturado.

**Teste independente**: Saturar um host e conferir destaque e valores.

**Cenários de aceite**:

1. **Dado** host com CPU ≥ 80%, **Quando** listado, **Então** a barra usa o tom de alerta e o valor é exibido em texto.
2. **Dado** host sem amostra recente, **Quando** listado, **Então** mostra "—" com dica "sem medição recente".

### Jornada do Usuário 3 - Uso por container (Prioridade: P1)

Como operador, quero ver CPU (%) e memória (MiB) de cada container na lista, e no detalhe os recursos atuais (CPU em relação ao limite, memória usada/limite, rede entrada/saída, disco leitura/escrita) e uma série dos últimos 30 minutos.

**Valor desta prioridade**: Diagnóstico de containers lentos ou perto do limite de memória.

**Teste independente**: Container com limites de CPU/memória conhecidos sob carga controlada; comparar com `stats` do engine.

**Cenários de aceite**:

1. **Dado** container com limite de 2 vCPU e 512 MiB, **Quando** o detalhe abre, **Então** "Recursos agora" mostra CPU relativa ao limite, memória "412 MiB de 512 MiB" com percentual e destaque ≥ 80%.
2. **Dado** a aba Estatísticas, **Quando** aberta, **Então** mostra CPU e memória em % do limite nos últimos 30 minutos com pontos a cada minuto ou menos.
3. **Dado** container sem limite definido, **Quando** exibido, **Então** a referência passa a ser a capacidade do host e a interface informa "sem limite".
4. **Dado** container parado, **Quando** exibido, **Então** CPU e memória aparecem como `—` com o estado "parado", sem série ativa; zero só é exibido quando medido.

## Casos de Borda

- Contadores reiniciados (container reiniciado) não podem produzir valores negativos ou picos irreais.
- Host com cgroups v1 e v2; Podman rootless sem acesso a algumas métricas: campos ausentes são "indisponível", não zero.
- Relógio do host divergente: amostras ordenadas pelo horário de recebimento quando a divergência exceder 5 minutos.
- Reinício da API: o último valor e a série de 30 minutos dos containers ficam em memória e se recompõem a partir das próximas amostras; a série de hosts persiste.
- Host com lxcfs ou outra virtualização de `/proc`: CPU e memória do host lidas de dentro do container não representam o host; o agente detecta e informa "indisponível".
- Mais de 2.000 containers (margem de capacidade): amostragem não deve degradar o canal de controle.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: O agente DEVE coletar, em intervalo configurável (padrão 15 s, alinhado ao heartbeat; máximo 30 s; `DOKPOD_AGENT_METRICS_INTERVAL_SECONDS`), para o host, sem montar diretórios do host além do socket do engine (CPU e memória lidas de `/proc/stat` e `/proc/meminfo` do próprio container, que refletem o host, e capacidade de `/info`): CPU em uso (%), memória usada e total; para cada container em execução: CPU (% do host, 0-100, e % do limite), memória usada e limite, bytes de rede recebidos/enviados por segundo e bytes de disco lidos/escritos por segundo.
- **FR-002**: O agente DEVE enviar uma amostra por intervalo, sem média prévia, em lotes de no máximo 500 containers por mensagem e 4 mensagens por intervalo, informando a quantidade descartada, sem bloquear comandos nem inventário. Acima da capacidade do intervalo, amostra uma parte dos containers por ciclo, em rodízio.
- **FR-003**: O plano de controle DEVE manter o valor mais recente por host e por container para consulta imediata.
- **FR-004**: O plano de controle DEVE manter séries com resolução de 1 minuto: por host no PostgreSQL, por pelo menos 12 horas, com retenção configurável (padrão 48 h, `Metrics:HostRetention`); por container somente em memória, nos últimos 30 minutos, sem persistência.
- **FR-005**: A API DEVE fornecer: agregado do estágio (CPU média ponderada pela capacidade e memória em uso sobre memória total) atual e série; uso atual por host; uso atual por container (inclusive na listagem de containers); série por container.
- **FR-006**: Todas as consultas DEVEM considerar somente hosts autorizados por `environment:read`.
- **FR-007**: Limite operacional de 80% DEVE ser configurável e retornado pela API para que a interface desenhe a referência e o destaque.
- **FR-008**: Lacunas de amostra DEVEM ser representadas como ausência, nunca como zero.
- **FR-009**: As métricas não DEVEM incluir nomes de processos, argumentos, variáveis, labels ou qualquer conteúdo do container; o agente não lê `/proc/<pid>` de nenhum processo.
- **FR-010**: A capability de métricas DEVE ser anunciada pelo agente e só é usada se confirmada em `SessionEstablished.accepted_capabilities` (008 D-009); agente sem a capability resulta em "indisponível" na interface. Agentes Windows não anunciam a capability nesta spec.
- **FR-012**: A interface DEVE atualizar métricas por consulta periódica a cada 30 segundos, somente com a aba visível; métricas não geram invalidação pelo canal em tempo real.
- **FR-011**: A interface DEVE preencher os placeholders deixados pela spec 008 (KPIs de CPU e memória, gráfico de recursos, coluna CPU de hosts) e fornecer os dados de recursos para as specs 010, 011 e 013.

### Entidades Principais

- **Amostra de host**: host, minuto, CPU %, memória usada, memória total (persistida).
- **Amostra de container**: host, container, minuto (somente em memória), CPU % do limite, CPU % do host, memória usada, limite de memória, rede rx/tx por segundo, disco leitura/escrita por segundo.
- **Política de métricas**: intervalo, limite operacional, retenções.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Diferença entre o valor exibido e o medido pelo engine ≤ 5 pontos percentuais em carga estável.
- **SC-002**: Valor atual reflete mudança de carga em até 30 segundos.
- **SC-003**: Com 56 hosts e 1.120 containers, a coleta adiciona no máximo 5% de CPU ao host do agente e não aumenta a latência de comandos acima do baseline em mais de 10%.
- **SC-004**: Consulta da série de 12 horas do estágio responde em até 500 ms p95.
- **SC-005**: Armazenamento de métricas de host permanece limitado pela retenção configurada (sem crescimento após o período) e nenhuma métrica de container é gravada no PostgreSQL.
- **SC-006**: Nenhum payload de métricas contém nome de processo, argumento, variável ou label (teste de contrato e de payload).

## Premissas

- A arquitetura determina que métricas de container não sejam armazenadas como série temporal no banco transacional e que memória local seja cache efêmero de instância única. Por isso, séries de container ficam só em memória (30 min) e só a série de host, pequena e com retenção, vai ao PostgreSQL; decisão sujeita à revisão humana (D-002).
- Agentes Windows (containers Windows não usam cgroups e têm `stats` diferente) ficam fora desta spec; a capability não é anunciada até a qualificação da 006.
- "Memória alocada" do mockup é interpretada como memória em uso sobre memória total dos hosts do estágio.
- Métricas de rede/disco por host ficam fora do escopo; apenas por container.
- Alertas automáticos por limiar e escalonamento automático (citado na nota do mockup) ficam fora do escopo; a nota é apenas informativa.
