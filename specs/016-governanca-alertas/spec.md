# Especificação da Feature: Governança de alertas e parada justificada

**Branch da feature**: `016-governanca-alertas`

**Criada em**: 2026-10-14

**Estado**: `implemented` - aprovada pelo solicitante em 2026-10-14 com ajuste: a justificativa vale para parar, pausar e remover ("o restante está ok"); implementada e integrada em 2026-10-15 (PR #21); evidências em [tasks.md](tasks.md); revisão do solicitante ainda pendente em [tasks.md](tasks.md).

**Entrada**: Pedido do solicitante em 2026-10-14: "Comece pelos Alertas, inclusive o sininho [...] o menu Governança → Alertas precisa ser construído com todos os detalhes referentes à governança de alertas" e "Quando um usuário for parar um container, se configurado pelo administrador, uma tela deverá ser exibida para que uma justificativa seja informada [...] da conotação que o container foi parado intencionalmente, e não por problema", com persistência em banco e recuperação por API. O defeito visual do sino (painel por baixo dos cards) foi corrigido à parte (PR #20).

**Depende de**: [008](../008-visao-geral-hosts-eventos/spec.md) (eventos operacionais e sino), [009](../009-metricas-recursos/spec.md) (amostras de CPU e memória), [010](../010-lista-containers-acoes-lote/spec.md) (ações e lote), [011](../011-detalhe-container-logs/spec.md) (detalhe do container), [001](../001-identidade-ambientes-auditoria/spec.md) (auditoria e autorização por ambiente).

**Design**: não há board de Alertas em `docs/design/app`. O mockup define o item de menu "Alertas" na seção Governança, com contador de "alertas abertos", e a nota "Limite operacional de 80%". A tela deriva dos padrões dos boards `02 · Containers` (lista, filtros, chips) e `04 · Novo container` (drawer de formulário).

## Esclarecimentos

### Sessão 2026-10-14

- P: Alertas devem notificar fora do console? → R: Não nesta entrega. Alertas aparecem no console (menu, sino, tela Alertas e tempo real); e-mail e webhooks ficam para spec posterior.
- P: Quem gerencia regras e silenciamentos? → R: Scopes UMA por ambiente: `alert:read` (ver) e `alert:manage` (regras, silenciamentos, reconhecer e resolver).
- P: O que entra na primeira entrega? → R: Regras configuráveis, ciclo de vida do alerta, silenciamentos, histórico e auditoria.
- P: Parada com justificativa? → R: Política definida pelo administrador; quando exigida, o usuário informa a justificativa ao parar; ela fica persistida, consultável por API e marca o container como parado intencionalmente, sem gerar alerta de falha.
- P: Quais ações exigem justificativa? → R: Parar, pausar e remover (ajuste do solicitante em 2026-10-14). Reiniciar e iniciar mantêm a confirmação atual.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Ver e acompanhar alertas abertos (Prioridade: P1)

Como operador, quero ver os alertas abertos dos hosts que posso ler, com severidade, origem (host e container), condição, início e há quanto tempo estão abertos, para saber o que exige ação agora.

**Valor desta prioridade**: o menu e o sino passam a contar o que realmente pede ação (alertas abertos), não todos os eventos de atenção.

**Teste independente**: provocar uma condição (container que morre com erro), verificar o alerta aberto na tela, no menu e no sino em até 15 s; remover a condição e verificar a resolução automática.

**Cenários de aceite**:

1. **Dado** um container que encerra com código diferente de zero sem parada intencional, **Quando** o evento chega, **Então** um alerta "Container encerrado com erro" abre com severidade alta, host e container, e aparece em até 15 s na tela Alertas, no contador do menu e no sino.
2. **Dado** alertas abertos em vários estágios, **Quando** o usuário seleciona um estágio, **Então** a tela, o contador e o sino mostram só os alertas dos hosts desse estágio que ele pode ler.
3. **Dado** o sino, **Quando** aberto, **Então** lista os alertas abertos mais recentes (até 10), cada um levando ao alerta na tela Alertas, com o link "Ver todos os alertas".
4. **Dado** a lista de alertas, **Quando** filtrada por severidade, situação (aberto, reconhecido, silenciado), host ou texto, **Então** os filtros ficam na URL e podem ser compartilhados.
5. **Dado** um alerta, **Quando** aberto, **Então** mostra a regra que o gerou, a linha do tempo (aberto, reconhecido, silenciado, resolvido, com autor e horário) e links para o container e o host.

### Jornada do Usuário 2 - Reconhecer e resolver alertas (Prioridade: P1)

Como operador com `alert:manage`, quero reconhecer um alerta (assumindo-o, com comentário) e vê-lo resolvido quando a condição desaparece, para coordenar o atendimento sem trabalho duplicado.

**Valor desta prioridade**: evita que dois operadores atendam o mesmo problema e dá rastreabilidade ao atendimento.

**Teste independente**: reconhecer um alerta com comentário, verificar responsável e linha do tempo; corrigir a condição e verificar a resolução automática.

**Cenários de aceite**:

1. **Dado** um alerta aberto, **Quando** o operador o reconhece com comentário de 10 a 500 caracteres, **Então** o alerta fica "reconhecido", com o operador como responsável, e continua contando como aberto até ser resolvido.
2. **Dado** um alerta cuja condição desaparece (container volta a executar, agente reconecta, uso cai abaixo do limite pela duração da regra), **Quando** a condição é avaliada, **Então** o alerta é resolvido automaticamente, com o motivo na linha do tempo.
3. **Dado** um alerta de condição pontual (sem estado a observar, por exemplo comando com falha), **Quando** o operador o resolve manualmente com comentário, **Então** ele sai dos abertos.
4. **Dado** usuário sem `alert:manage` no host do alerta, **Quando** vê o alerta, **Então** as ações de reconhecer, silenciar e resolver não aparecem e a API as recusa.

### Jornada do Usuário 3 - Parar, pausar e remover com justificativa (Prioridade: P1)

Como administrador, quero exigir uma justificativa ao parar, pausar ou remover containers nos estágios que eu definir; como operador, quero informar o motivo dessas ações, para que todos saibam que foram intencionais e não uma falha.

**Valor desta prioridade**: distingue interrupções planejadas de problemas, evita alertas falsos e responde "quem parou, pausou ou removeu, quando e por quê".

**Teste independente**: com a política ativa em Produção, parar, pausar e remover containers de Produção pela lista, pelo detalhe e em lote; verificar o diálogo, a justificativa persistida, a consulta pela API, os rótulos "Parado intencionalmente" e "Pausado intencionalmente" e a ausência de alerta de falha.

**Cenários de aceite**:

1. **Dado** a política "exigir justificativa" ativa para Produção, **Quando** um operador escolhe "Parar", "Pausar" ou "Remover" num container de Produção, **Então** o diálogo da ação pede a justificativa (10 a 500 caracteres) e o comando só é enviado após informá-la; na remoção, a justificativa entra no diálogo de remoção reforçada existente.
2. **Dado** a política inativa para Homologação, **Quando** o operador para, pausa ou remove um container de Homologação, **Então** a confirmação atual é mantida, com campo de justificativa opcional; "Reiniciar" e "Iniciar" nunca pedem justificativa.
3. **Dado** uma ação em lote (parar, pausar ou remover) com containers de Produção, **Quando** confirmada, **Então** uma única justificativa é exigida e registrada para cada container do lote.
4. **Dado** um container parado ou pausado com justificativa, **Quando** exibido na lista e no detalhe, **Então** aparece "Parado intencionalmente" ou "Pausado intencionalmente", com o autor, o horário e o motivo (no detalhe e em tooltip na lista).
5. **Dado** um container parado ou removido com justificativa, **Quando** o encerramento ou a remoção chega como evento, **Então** nenhum alerta é aberto para essa ação, e alertas abertos do container removido são resolvidos com o motivo "container removido".
6. **Dado** um container parado ou pausado intencionalmente, **Quando** volta a executar (iniciado ou retomado, pelo Dokpod ou fora dele), **Então** a marcação deixa de valer; a justificativa continua no histórico.
7. **Dado** qualquer cliente autorizado (`environment:read`), **Quando** consulta a API, **Então** obtém a justificativa vigente do container e o histórico de ações justificadas (parar, pausar, remover) com ação, autor, horário, motivo e comando associado, inclusive de containers já removidos.
8. **Dado** uma justificativa com segredo aparente ("senha=..."), **Quando** registrada, **Então** o trecho sensível é mascarado antes de persistir, como na justificativa do terminal (spec 014).

### Jornada do Usuário 4 - Configurar regras de alerta (Prioridade: P1)

Como administrador com `alert:manage`, quero ver, criar, editar, ativar e desativar regras, a partir de um conjunto padrão, escolhendo condição, severidade, abrangência e duração mínima, para adaptar os alertas à operação.

**Valor desta prioridade**: sem regras ajustáveis, os alertas viram ruído ou deixam de cobrir o que importa.

**Teste independente**: desativar uma regra padrão e verificar que a condição não abre alerta; criar uma regra de memória acima de 70% por 2 minutos num host e verificar a abertura e a resolução.

**Cenários de aceite**:

1. **Dado** uma instalação nova, **Quando** a tela Regras é aberta, **Então** mostra as regras padrão ativas: container encerrado com erro, container encerrado por falta de memória, container sem saúde, reinícios repetidos (3 ou mais em 10 minutos), agente desconectado por mais de 2 minutos, CPU do host acima de 80% por 5 minutos, memória do host acima de 80% por 5 minutos, comando com falha e comando com resultado indeterminado.
2. **Dado** o formulário de regra, **Quando** preenchido, **Então** aceita condição por tipo de evento ou por limite de CPU ou memória (do host ou do container), severidade (crítica, alta, média, baixa), abrangência (todos os estágios, estágio, host ou containers por nome) e duração mínima para limites.
3. **Dado** uma regra desativada, **Quando** a condição ocorre, **Então** nenhum alerta abre; alertas já abertos pela regra continuam até serem resolvidos.
4. **Dado** uma alteração de regra, **Quando** salva, **Então** vale para avaliações seguintes e fica registrada na auditoria com o estado anterior e o novo.
5. **Dado** a mesma condição ocorrendo de novo no mesmo alvo com alerta aberto, **Quando** avaliada, **Então** não abre um segundo alerta; atualiza o contador de ocorrências e o último horário.

### Jornada do Usuário 5 - Silenciar durante manutenção (Prioridade: P2)

Como administrador, quero criar silenciamentos com início, fim, abrangência e justificativa, para que manutenções planejadas não gerem ruído, sem perder o registro do que aconteceu.

**Valor desta prioridade**: manutenções são frequentes e hoje geram eventos de atenção que poluem o sino.

**Teste independente**: criar um silenciamento de 30 minutos para um host, provocar uma condição, verificar o alerta "silenciado" fora do contador e do sino, e a volta ao normal ao fim da janela.

**Cenários de aceite**:

1. **Dado** um silenciamento ativo para um host, **Quando** uma condição desse host ocorre, **Então** o alerta é registrado como "silenciado", não entra no contador nem no sino, e aparece na tela com o silenciamento indicado.
2. **Dado** o fim da janela, **Quando** a condição ainda existe, **Então** o alerta passa a "aberto" e entra no contador.
3. **Dado** um silenciamento, **Quando** criado, **Então** exige início, fim (até 7 dias depois do início), abrangência e justificativa, e pode ser encerrado antes do fim.
4. **Dado** um alerta aberto, **Quando** o operador o silencia diretamente (por 1 h, 4 h, 24 h ou até um horário), **Então** cria-se um silenciamento restrito a esse alerta.

### Jornada do Usuário 6 - Histórico e auditoria (Prioridade: P2)

Como auditor, quero consultar o histórico de alertas e as alterações de governança (regras, silenciamentos, reconhecimentos e paradas justificadas), para responder o que aconteceu, quando e por quem.

**Valor desta prioridade**: governança exige rastreabilidade.

**Teste independente**: gerar alertas, reconhecer, silenciar e alterar regras; consultar o histórico por período, host e severidade e a auditoria correspondente.

**Cenários de aceite**:

1. **Dado** alertas resolvidos, **Quando** o histórico é consultado por período (até 90 dias), host, severidade ou regra, **Então** a lista mostra duração, responsável e motivo da resolução.
2. **Dado** qualquer ação de governança, **Quando** executada, **Então** gera um evento de auditoria com autor, horário, alvo e detalhes permitidos, sem segredos.
3. **Dado** o fim da retenção (90 dias padrão, configurável), **Quando** atingido, **Então** alertas resolvidos são removidos; paradas justificadas seguem a retenção da auditoria.

## Casos de Borda

- Container parado ou pausado com justificativa que depois falha ao iniciar: o alerta de falha da nova tentativa abre normalmente.
- Remoção justificada de container que não chega a ser removido (comando falha): a justificativa fica registrada com o resultado do comando e o container continua sem marcação.
- Container que morre sozinho enquanto há uma parada justificada em andamento para ele: a parada vence se o comando foi aceito antes do encerramento; caso contrário, abre alerta.
- Parada fora do Dokpod (CLI do engine): não há justificativa; abre alerta de encerramento conforme as regras (código de saída zero e sinal de parada não abrem alerta por padrão).
- Agente desconectado: os alertas dos containers do host ficam como estão; abre o alerta de agente desconectado; métricas sem amostra não resolvem nem abrem alertas de limite.
- Muitos eventos repetidos (container em ciclo de reinício): uma única ocorrência aberta por regra e alvo, com contador.
- Regra excluída com alertas abertos: os alertas são resolvidos com o motivo "regra removida".
- Usuário perde `alert:read` num host: os alertas desse host somem da tela, do contador e do sino.
- Relógios: horários exibidos no fuso do navegador; avaliação em UTC.
- Política de justificativa ativada com um diálogo de parada já aberto: o envio é recusado e o diálogo pede a justificativa.
- Cliente antigo que envia "parar", "pausar" ou "remover" sem justificativa onde ela é exigida: a API recusa com erro legível.

## Requisitos (obrigatório)

### Requisitos Funcionais

**Alertas**

- **FR-001**: O sistema DEVE abrir alertas a partir das regras ativas, avaliando eventos operacionais e amostras de CPU e memória dos hosts que as regras abrangem.
- **FR-002**: Cada alerta DEVE ter regra, severidade, alvo (host e, quando houver, container), condição legível, situação (aberto, reconhecido, silenciado, resolvido), início, última ocorrência, número de ocorrências e linha do tempo.
- **FR-003**: Para a mesma regra e o mesmo alvo DEVE existir no máximo um alerta não resolvido.
- **FR-004**: O sistema DEVE resolver automaticamente alertas de condição observável quando ela desaparece, e permitir resolução manual com comentário.
- **FR-005**: Reconhecer DEVE exigir comentário de 10 a 500 caracteres e registrar o responsável.
- **FR-006**: O contador do menu "Alertas" e o sino DEVEM mostrar os alertas abertos e reconhecidos não silenciados do estágio selecionado, nos hosts com `alert:read`; o sino lista até 10 com link para cada um.
- **FR-007**: A tela Alertas DEVE ter as vistas Abertos, Histórico, Regras e Silenciamentos, com filtros na URL, atualização em tempo real e as mesmas convenções de lista do console.
- **FR-008**: Ver alertas DEVE exigir `alert:read`; reconhecer, resolver, silenciar e gerenciar regras DEVEM exigir `alert:manage` em todos os hosts abrangidos.

**Regras**

- **FR-009**: O sistema DEVE fornecer as regras padrão da Jornada 4, ativas na instalação.
- **FR-010**: Regras DEVEM aceitar condição por tipo de evento operacional ou por limite de CPU ou memória (host ou container) com duração mínima; severidade; abrangência; nome e descrição.
- **FR-011**: Alterações de regra DEVEM valer para avaliações seguintes e ser auditadas com estado anterior e novo.

**Silenciamentos**

- **FR-012**: Silenciamentos DEVEM ter abrangência, início, fim (até 7 dias depois do início), justificativa e autor, e poder ser encerrados antes do fim.
- **FR-013**: Alertas abertos durante um silenciamento DEVEM ser registrados como silenciados e ficar fora do contador e do sino.

**Parada justificada**

- **FR-014**: Administradores do Dokpod DEVEM poder ativar, por estágio, a política "exigir justificativa" para as ações parar, pausar e remover; o padrão é ativa em Produção e inativa nos demais.
- **FR-015**: Com a política ativa, todo comando de parar, pausar ou remover enviado pelo Dokpod (lista, detalhe e lote) DEVE exigir justificativa de 10 a 500 caracteres antes do envio; sem a política, o campo é opcional; iniciar, retomar e reiniciar não pedem justificativa.
- **FR-016**: A justificativa DEVE ser persistida com ação, container (inclusive nome, para containers removidos), host, autor, horário, motivo mascarado e comando associado com seu resultado, e ser recuperável por API: a vigente de um container e o histórico de um container ou host.
- **FR-017**: A API DEVE recusar parar, pausar ou remover sem justificativa onde ela é exigida, com erro legível, inclusive para clientes que não conhecem a política.
- **FR-018**: Um container parado ou pausado com justificativa DEVE ser exibido como "Parado intencionalmente" ou "Pausado intencionalmente" na lista e no detalhe, com autor, horário e motivo; a marcação deixa de valer quando o container volta a executar.
- **FR-019**: Paradas e remoções justificadas NÃO DEVEM abrir alerta; as regras DEVEM ignorar o encerramento que corresponde ao comando justificado, e a remoção resolve os alertas abertos do container.
- **FR-020**: Trechos com aparência de segredo na justificativa DEVEM ser mascarados antes de persistir.

**Histórico e auditoria**

- **FR-021**: Alertas resolvidos DEVEM ser consultáveis por período (até a retenção), host, severidade e regra; a retenção padrão é de 90 dias, configurável.
- **FR-022**: Criação, alteração, ativação e exclusão de regras, silenciamentos, reconhecimentos, resoluções manuais, mudanças da política de justificativa e ações justificadas DEVEM gerar eventos de auditoria.
- **FR-023**: Nenhum dado de alerta, regra, silenciamento ou justificativa DEVE revelar hosts ou containers que o usuário não pode ler.

### Entidades Principais

- **Regra de alerta**: nome, descrição, condição (tipo de evento ou métrica, operador, limite e duração), severidade, abrangência, ativa, autor e datas.
- **Alerta**: regra, alvo, severidade, situação, início, última ocorrência, ocorrências, responsável, linha do tempo e resolução.
- **Silenciamento**: abrangência ou alerta, início, fim, justificativa, autor, encerramento antecipado.
- **Política de ações justificadas**: estágio, exigida (sim/não) para parar, pausar e remover, autor e data da alteração.
- **Ação justificada**: ação (parar, pausar, remover), container e nome, host, comando e resultado, autor, horário, motivo mascarado e situação (vigente ou encerrada quando o container volta a executar).

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Um alerta aparece na tela, no contador e no sino em até 15 segundos após o evento que o origina, em 95% das ocorrências.
- **SC-002**: Com a mesma condição repetida 100 vezes no mesmo alvo, existe um único alerta aberto com contador 100.
- **SC-003**: Em 100% das ações de parar, pausar e remover feitas pelo Dokpod em estágio com a política ativa, existe justificativa persistida recuperável pela API; nenhuma parada ou remoção justificada abre alerta.
- **SC-004**: Alertas de condição observável são resolvidos automaticamente em até 1 minuto após a condição desaparecer (ou após a duração mínima, nas regras de limite).
- **SC-005**: Durante um silenciamento, nenhum alerta abrangido entra no contador ou no sino, e 100% deles ficam registrados como silenciados.
- **SC-006**: As telas e diálogos de alertas e de parada justificada não têm violações sérias ou críticas de acessibilidade e são operáveis só por teclado.
- **SC-007**: Com 2.000 containers e 56 hosts, a lista de alertas abertos responde em até 2 s no p95.

## Premissas

- Notificações externas (e-mail, webhooks, integrações) ficam para spec posterior; esta entrega é só no console.
- "Administrador" para a política de justificativa é o papel Administrator do Dokpod (grupo `/dokpod/administrators`), o mesmo que já administra ambientes; os scopes `alert:read` e `alert:manage` seguem o modelo UMA por ambiente.
- A justificativa segue as regras da justificativa do terminal (spec 014): 10 a 500 caracteres e mascaramento de segredos.
- A política vale para parar, pausar e remover (ajuste do solicitante); iniciar, retomar e reiniciar mantêm a confirmação atual.
- O sino passa a mostrar alertas abertos no lugar de "eventos que pedem atenção"; os eventos continuam na Visão geral.
- Regras de limite usam as amostras da spec 009 (janela de amostra recente) e não abrem nem resolvem alertas sem amostras.
- A retenção de alertas resolvidos é de 90 dias, alinhada à retenção de comandos.
- Não há board de Alertas: a interface segue os padrões existentes do console e do design system.
