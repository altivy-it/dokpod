# Especificação da Feature: Visão Geral, Hosts, Eventos e Inventário Enriquecido

**Branch da feature**: `007-console-operacional`

**Criada em**: 2026-10-12

**Estado**: `draft` - aguardando revisão humana.

**Entrada**: Board `01 · Visão geral` ([Main.dc.html](../../docs/design/app/project/Main.dc.html)) e item de menu "Hosts"; dados de hosts e estados usados pelos boards `Containers` e `Detalhe`.

**Depende de**: [007](../007-fundacao-console-design-system/spec.md) (moldura e componentes). **Alimenta**: 009-014.

## Contexto

Hoje o plano de controle conhece ambientes (nome, host, habilitado, scopes) e uma projeção mínima de containers (id, nome, imagem, estado bruto, revisão). Ele não guarda engine, versões, conexão do agente, estágio do ambiente, portas, saúde, reinícios nem eventos. O agente consulta o engine por polling e só suporta o adapter Docker. A Visão geral do mockup exige contagens por situação, distribuição por engine, tabela de hosts com engine/versão/situação e um feed de eventos recentes.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Entender o estado do estágio em uma tela (Prioridade: P1)

Como operador (SRE), quero abrir a Visão geral de um estágio (Produção, Homologação, Desenvolvimento) e ver quantos hosts e containers estão sob gestão, quantos estão em execução, parados, com falha ou reiniciando, a distribuição por engine e quando o estado foi observado.

**Valor desta prioridade**: É a tela inicial do console e a resposta para "está tudo bem?".

**Teste independente**: Com dois agentes reais (Docker e Podman) em estágios diferentes e containers em estados variados, abrir a Visão geral filtrada por estágio e conferir contagens contra o engine.

**Cenários de aceite**:

1. **Dado** estágio Produção com hosts autorizados, **Quando** o operador abre a Visão geral, **Então** vê título com número de hosts e containers, versões de engine por quantidade de hosts e o horário da observação mais antiga entre os hosts exibidos.
2. **Dado** containers em execução, parados, pausados, reiniciando e com falha, **Quando** a tela carrega, **Então** os indicadores "Em execução", "Parados" e "Com falha ou reiniciando" somam o total exibido, conforme o mapeamento de situação desta spec.
3. **Dado** hosts Docker e Podman, **Quando** a tela carrega, **Então** o gráfico "Containers por engine" mostra a quantidade por engine com legenda textual.
4. **Dado** usuário sem `environment:read` em algum host, **Quando** a tela carrega, **Então** esse host e seus containers não entram em nenhuma contagem nem são revelados por diferença de totais.
5. **Dado** mudança de inventário em tempo real, **Quando** chega a invalidação, **Então** os números atualizam sem mover foco nem reordenar a lista de hosts.
6. **Dado** host com inventário desatualizado ou agente desconectado, **Quando** exibido, **Então** a tela sinaliza o host e a idade do estado sem supor dados atuais.

### Jornada do Usuário 2 - Conhecer hosts, engines e conformidade de versão (Prioridade: P1)

Como operador, quero ver cada host com engine (Docker/Podman), versão, situação ("Em dia", "Atualizar", "Desconectado", "Desatualizado"), quantidade de containers e conexão do agente, e abrir a lista completa de hosts.

**Valor desta prioridade**: Mostra onde agir antes de incidentes (engine fora da versão homologada, agente offline).

**Teste independente**: Configurar versão mínima homologada; conectar agente com engine abaixo e acima dela; desligar um agente; conferir situação na Visão geral e na tela Hosts.

**Cenários de aceite**:

1. **Dado** engine abaixo da versão mínima homologada, **Quando** o agente se conecta, **Então** o host aparece como "Atualizar" (alerta) e um evento "Host com engine fora da versão homologada" é registrado uma vez por mudança de versão.
2. **Dado** agente sem heartbeat além do limite, **Quando** a tela carrega, **Então** o host aparece como "Desconectado", com horário do último contato.
3. **Dado** a tela Hosts, **Quando** aberta, **Então** lista todos os hosts autorizados com estágio, engine, versão, sistema operacional, arquitetura, versão do agente, situação, containers e último contato, com busca e filtro por estágio/engine/situação.
4. **Dado** usuário com `environment:manage`, **Quando** cadastra um host pela tela Hosts, **Então** informa identificador, nome, host e estágio no drawer, e o cadastro segue as regras existentes (antiforgery, idempotência, Keycloak).
5. **Dado** usuário com `environment:manage`, **Quando** altera o estágio de um host ou revoga a identidade do agente, **Então** a ação exige confirmação contextual e gera auditoria.

### Jornada do Usuário 3 - Acompanhar eventos que pedem atenção (Prioridade: P1)

Como operador, quero um feed de eventos recentes (reinício automático, falha de health check, OOM, engine fora da versão, imagem atualizada, volume removido, resultado de comandos) com horário, origem e situação, e um contador de eventos que pedem atenção no card, no sino de notificações e no item de menu Alertas.

**Valor desta prioridade**: Explica mudanças sem abrir logs ou o host.

**Teste independente**: Provocar OOM, falha de health check, reinício e remoção de volume em engine real; conferir eventos, textos e contadores.

**Cenários de aceite**:

1. **Dado** container encerrado por falta de memória, **Quando** o agente observa o evento, **Então** aparece um único evento "Limite de memória atingido (OOMKilled)" com container e host, situação "Com falha" (o `die` que o engine emite em seguida é agregado a ele, sem segundo evento de erro).
2. **Dado** eventos de aviso ou erro nas últimas 24 horas, **Quando** a Visão geral carrega, **Então** o card informa "N eventos pedem atenção" e o sino e o item Alertas mostram o mesmo número.
3. **Dado** rajada de eventos repetidos, **Quando** o agente os envia, **Então** eventos idênticos são agregados com contador e o volume por host é limitado sem perder o fato de que houve descarte.
4. **Dado** o sino de notificações, **Quando** acionado, **Então** abre um painel com os eventos que pedem atenção, com link para o container ou host.
5. **Dado** evento com nome de container ou imagem contendo marcação, **Quando** exibido, **Então** o texto é escapado.

### Jornada do Usuário 4 - Situação rica de cada container (Prioridade: P1)

Como operador, quero que o inventário traga, além de nome, imagem e estado, as portas publicadas, o momento de criação e de início, a saúde (health check), o número de reinícios, o código de saída e se houve OOM, para que as telas de lista e detalhe mostrem a situação real.

**Valor desta prioridade**: Base das telas 010 e 011 e do mapeamento de situação da Visão geral.

**Teste independente**: Criar containers reais com portas, health check e falhas controladas; comparar a projeção com o engine após snapshot e após eventos.

**Cenários de aceite**:

1. **Dado** container com portas publicadas, **Quando** projetado, **Então** as portas aparecem no formato `8080→80/tcp`.
2. **Dado** container `exited` com código diferente de zero ou OOM, ou `dead`, ou `running` com health `unhealthy`, **Quando** projetado, **Então** a situação exibida é "Com falha".
3. **Dado** agente de versão anterior (N-1) sem os novos campos, **Quando** publica inventário, **Então** a projeção aceita os dados e os campos novos ficam vazios sem erro.

### Jornada do Usuário 5 - Hosts Podman (Prioridade: P2)

Como operador, quero que hosts com Podman sejam identificados como Podman e tenham inventário, eventos e comandos existentes funcionando pela API compatível do Podman.

**Valor desta prioridade**: O mockup gerencia Docker e Podman lado a lado; hoje só existe adapter Docker.

**Teste independente**: Agente em host Podman real (rootless e rootful) publica engine Podman com versão, inventário e eventos; start/stop/restart/delete funcionam. Pods ficam fora desta spec.

**Cenários de aceite**:

1. **Dado** socket Podman, **Quando** o agente inicia, **Então** anuncia engine Podman, versão Podman, modo rootless/rootful e somente as capabilities comprovadas.
2. **Dado** capability não comprovada para Podman, **Quando** um comando é solicitado, **Então** é recusado explicitamente como não suportado.

## Casos de Borda

- Estágio não definido em hosts já cadastrados (migração): aparecem como "Sem estágio" e só entram na Visão geral sem filtro.
- Zero hosts autorizados: estado vazio com orientação para cadastrar host (se `environment:manage`) ou solicitar acesso.
- Host desabilitado: aparece na tela Hosts com situação "Desabilitado", fora das contagens da Visão geral.
- Agente que reconecta com outra engine/versão: atualiza descritor e gera evento de mudança.
- Eventos fora de ordem ou duplicados após reconexão: deduplicados por identificador do evento no engine e horário.
- Relógio do host divergente: horário do evento é o do engine, com horário de recebimento registrado; exibição usa o do engine e sinaliza divergência acima de 5 minutos.
- Volume de eventos acima do limite por host: agregação e evento `events.dropped` com a quantidade descartada.
- Plano de controle reiniciado ou instância encerrada: conexões que não têm sessão ativa são marcadas como desconectadas na inicialização e por varredura periódica.
- Agente N conectado a plano de controle N-1: o agente não envia mensagens novas, porque o servidor não confirma as capabilities correspondentes.
- Versão do engine não reconhecida (string fora do padrão): situação "Desconhecida", sem falha.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: Cada ambiente (host) DEVE ter estágio opcional entre Produção, Homologação e Desenvolvimento, definido no cadastro e alterável por quem tem `environment:manage`, com auditoria.
- **FR-002**: O plano de controle DEVE registrar, por host, o descritor anunciado pelo agente: engine, versão do engine, versão da API do engine, modo rootless/rootful, sistema operacional e versão, arquitetura, versão do agente, capabilities, nome do host, número de CPUs e memória total.
- **FR-003**: O plano de controle DEVE registrar a conexão do agente (conectado/desconectado) no momento em que a sessão abre e fecha, e o início da sessão. O último contato é mantido em memória pela instância que detém a sessão e persistido no máximo uma vez por minuto apenas para exibição, sem histórico individual de heartbeat. Conexões sem sessão ativa DEVEM ser marcadas como desconectadas na inicialização do plano de controle e por varredura periódica.
- **FR-004**: A situação do host DEVE ser derivada, nesta ordem: host desabilitado → "Desabilitado"; sem sessão ativa, ou sessão ativa sem heartbeat por 3 intervalos negociados → "Desconectado"; `inventory_revision` do heartbeat diferente da revisão projetada por mais de 2 minutos sem reconciliação concluída → "Desatualizado"; versão do engine abaixo da mínima homologada configurada → "Atualizar"; versão não interpretável → "Desconhecida"; caso contrário "Em dia". Host estável, sem mudanças de inventário, permanece "Em dia".
- **FR-005**: A projeção de container DEVE incluir portas publicadas, criação, início, saúde, contagem de reinícios, código de saída, indicação de OOM; campos novos são opcionais para compatibilidade N-1. Pods ficam fora desta spec.
- **FR-006**: A situação exibida do container DEVE seguir o mapeamento: `running` saudável ou sem health → "Em execução"; `running` com health `starting` → "Iniciando"; `running` com health `unhealthy`, `dead`, ou `exited` com código ≠ 0 ou OOM → "Com falha"; `restarting` → "Reiniciando"; `paused` → "Pausado"; `created` ou `exited` com código 0 → "Parado".
- **FR-007**: A API DEVE fornecer um resumo agregado por estágio (opcional) contendo hosts por situação, containers por situação exibida, containers e hosts por engine e versões, horário de observação mais antigo e quantidade de eventos que pedem atenção nas últimas 24 horas, considerando somente hosts autorizados por `environment:read`.
- **FR-008**: O agente DEVE observar eventos do engine em tempo real, filtrar por lista permitida (morte com código, OOM, mudança de saúde, reinício automático, criação/remoção de container, pull/remoção de imagem, remoção de volume) e publicá-los ao plano de controle com deduplicação, agregação de repetidos (incluindo `die` após `oom` do mesmo container) e limite por host, informando a quantidade descartada.
- **FR-009**: O plano de controle DEVE gerar eventos próprios: engine fora da versão homologada, agente conectado/desconectado, resultado de comandos (concluído, falhou, indeterminado) e `events.dropped` a partir da quantidade descartada informada pelo agente.
- **FR-010**: Eventos DEVEM ter tipo, severidade (sucesso, informativo, aviso, erro, neutro), horário do engine, horário de recebimento, host, sujeito (container, imagem, volume, host, comando) e parâmetros sanitizados; nunca incluem variáveis de ambiente, conteúdo de logs ou secrets. Quando o horário do engine diverge do horário de recebimento em mais de 5 minutos, o evento é exibido com sinalização de divergência.
- **FR-011**: A API DEVE permitir consultar eventos por estágio, host, severidade e período, com paginação por cursor, limitada a hosts autorizados.
- **FR-012**: Eventos DEVEM ser retidos por período limitado configurável (padrão proposto: 30 dias), com partições mensais criadas antecipadamente e uma rotina de retenção nova que remove partições inteiramente fora do período e apaga em lotes as linhas vencidas da partição parcialmente fora dele, sem bloquear escrita, conforme a constituição.
- **FR-013**: Mudanças de descritor e conexão (`kind=host`), de inventário (`kind=inventory`) e novos eventos (`kind=events`) DEVEM emitir invalidação segmentada por host pelo canal em tempo real existente, respeitando o limite de uma emissão por segundo por host e tipo.
- **FR-014**: A Visão geral DEVE exibir, conforme o mockup: cabeçalho do estágio com título e subtítulo, ações "Atualizar", "Baixar imagem" (visível quando a spec 012 estiver entregue e houver `image:pull`) e "Novo container" (visível quando a spec 013 estiver entregue e houver `container:create`), indicadores, gráfico por engine, card "Hosts e engines" com ação "Ver todos os hosts" e card "Eventos recentes" com contador de atenção.
- **FR-015**: Os indicadores de CPU, memória e imagens sem uso e o gráfico de recursos DEVEM aparecer como "indisponível" até que as specs 009 e 012 sejam entregues, sem quebrar layout.
- **FR-016**: A tela Hosts DEVE substituir a tela atual de catálogo/cadastro de ambientes, preservando cadastro, revogação de identidade do agente e acesso ao inventário.
- **FR-017**: O agente DEVE suportar hosts Podman pela API compatível, anunciando engine Podman e somente capabilities comprovadas por testes em engine real.
- **FR-018**: Contrato do agente DEVE evoluir de forma compatível N/N-1 nos dois sentidos: novos campos e mensagens com tags novas; servidor N aceita agente N-1 sem eventos e sem campos enriquecidos; o servidor confirma em `SessionEstablished.accepted_capabilities` as capabilities que aceita, e o agente só envia mensagens novas cujas capabilities foram confirmadas (lista ausente = servidor N-1, nenhuma mensagem nova).

### Entidades Principais

- **Ambiente (host)**: existente; ganha estágio. Nesta spec, "host" (termo da interface) e "ambiente"/`environment` (termo do contrato e do banco) designam a mesma entidade.
- **Descritor do agente/engine**: atributos anunciados pelo agente na sessão, último conhecido por host.
- **Conexão do agente**: estado, início da sessão, último contato.
- **Container observado**: existente; ganha portas, criação, início, saúde, reinícios, código de saída, OOM.
- **Evento operacional**: tipo, severidade, horários, host, sujeito, parâmetros, contagem de agregação.
- **Matriz de versão homologada**: versão mínima por engine, configurada pelo operador.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Contagens da Visão geral coincidem 100% com o engine real em cenário de teste com pelo menos 2 hosts (Docker e Podman) e 30 containers em 6 situações.
- **SC-002**: Evento de OOM, falha de health check ou reinício aparece no feed em até 5 segundos após ocorrer no engine, com agente conectado.
- **SC-003**: Visão geral carrega em até 2 segundos com 56 hosts e 1.120 containers (baseline de capacidade) na stack de teste.
- **SC-004**: Nenhum host ou container não autorizado é revelado em contagens, eventos ou gráficos (testes negativos de autorização).
- **SC-005**: Agente N-1 continua publicando inventário e executando comandos contra servidor N sem erro, e agente N mantém a sessão com servidor N-1 sem enviar mensagens novas.
- **SC-006**: Retenção remove eventos além do período configurado sem bloquear escrita, verificado em PostgreSQL real.

## Premissas

- Limites padrão propostos: agente desconectado após 3 heartbeats perdidos (≈ 45 s com o intervalo padrão de 15 s); inventário desatualizado após 2 minutos de revisão divergente sem reconciliação; eventos de atenção = aviso ou erro nas últimas 24 h; até 120 eventos por minuto por host; varredura de conexões a cada 60 s.
- Versão mínima homologada vem de configuração do plano de controle, documentada em `docs/distribuicao.md`; não é consultada em fonte externa.
- O texto dos eventos é montado no frontend a partir de tipo e parâmetros, em pt-BR.
- Reconhecimento ("ack") de eventos e a tela Alertas ficam fora do escopo; o contador é apenas por janela de 24 horas.
- Pods (Podman) ficam fora do escopo, inclusive como atributo e evento: a API compatível do Podman não os expõe, e exibi-los exige um adapter Libpod futuro.
- "Cluster" no mockup significa o conjunto de hosts de um estágio; não há orquestrador.
