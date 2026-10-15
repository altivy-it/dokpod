# Especificação da Feature: Auditoria

**Branch da feature**: `017-auditoria`

**Criada em**: 2026-10-15

**Estado**: `approved` - aprovada pelo solicitante em 2026-10-15 ("spec aprovada, siga com o plano e implemente").

**Entrada**: Pedido do solicitante em 2026-10-15: "siga para a spec 017 de Auditoria", item "Auditoria" da seção Governança do menu, hoje uma página "Em breve" ("Consulta da trilha de auditoria por ambiente").

**Depende de**: [001](../001-identidade-ambientes-auditoria/spec.md) (trilha de auditoria append-only e scope `audit:read`), [010](../010-lista-containers-acoes-lote/spec.md) (convenções de lista), [011](../011-detalhe-container-logs/spec.md) (detalhe do container), [014](../014-terminal-container/spec.md) (sessões de terminal auditadas), [016](../016-governanca-alertas/spec.md) (ações de governança e justificativas auditadas).

**Design**: não há board de Auditoria em `docs/design/app`. O mockup define o item "Auditoria" na seção Governança. A tela segue os padrões dos boards `02 · Containers` (lista, filtros, chips, exportação) e o drawer de detalhe já usado em Alertas.

## Esclarecimentos

### Sessão 2026-10-15 (recomendações adotadas, sujeitas à aprovação)

- P: Quem vê a auditoria? → R: Quem tem `audit:read` no host; cada usuário vê só os eventos dos hosts em que tem o scope. Não há visão "global" sem o scope em cada host.
- P: A trilha mostra quem fez a ação pelo nome? → R: Sim. Eventos novos passam a guardar também o nome de usuário de quem agiu; eventos anteriores mostram o identificador técnico, como hoje.
- P: Ler a auditoria também é auditado? → R: Consultas na tela não geram evento (evita ruído e crescimento da própria trilha); exportações geram.
- P: Exportação? → R: CSV do resultado filtrado, até 10.000 eventos por arquivo, com fórmulas neutralizadas.
- P: Retenção? → R: Mantém a regra da 001: sem apagamento automático; a tela informa o evento mais antigo disponível.
- P: Busca textual? (pedido do solicitante em 2026-10-15: "certifique que a tabela [...] esteja com particionamento configurado e full-text search") → R: Sim: busca full-text em português, sem diferenciar acentos, com índice próprio em todas as partições.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Consultar a trilha de auditoria (Prioridade: P1)

Um auditor ou administrador abre Governança → Auditoria e vê, do mais recente para o mais antigo, o que aconteceu nos hosts que pode auditar: quem, o quê, onde, quando e com que resultado.

**Valor desta prioridade**: hoje a trilha é gravada, mas só é consultável direto no banco.

**Teste independente**: executar ações variadas (parar com justificativa, abrir terminal, alterar regra de alerta, mudar estágio de host), abrir a Auditoria e encontrar cada ação com ator, host, alvo, horário e resultado.

**Cenários de aceite**:

1. **Dado** um usuário com `audit:read` em dois de três hosts, **Quando** abre a Auditoria, **Então** vê os eventos das últimas 24 horas desses dois hosts, mais recentes primeiro, e nenhum evento do terceiro.
2. **Dado** a lista, **Quando** o usuário rola até o fim da página carregada, **Então** pode carregar mais eventos sem perder a posição nem os filtros.
3. **Dado** uma ação negada por falta de permissão (por exemplo, terminal sem scope), **Quando** consultada, **Então** aparece com resultado "Negado" e o motivo.
4. **Dado** um usuário sem `audit:read` em nenhum host, **Quando** acessa a rota, **Então** vê o estado "sem acesso" e o item de menu não aparece.

---

### Jornada do Usuário 2 - Filtrar e investigar (Prioridade: P1)

Durante uma investigação, o usuário filtra por período, host, quem agiu, tipo de ação, resultado e alvo (container), e compartilha o link com os filtros.

**Valor desta prioridade**: responde rapidamente "quem parou este container ontem?" ou "o que este usuário fez em Produção nesta semana?".

**Teste independente**: aplicar cada filtro, isolado e combinado, conferir o resultado com os eventos gerados e reabrir o link copiado com os mesmos filtros.

**Cenários de aceite**:

1. **Dado** os filtros período, estágio, host, ator, categoria de ação, ação, resultado e texto (container, alvo ou correlação), **Quando** aplicados, **Então** a lista mostra só os eventos que atendem a todos, e os filtros ficam na URL.
2. **Dado** um período maior que 90 dias, **Quando** aplicado, **Então** a tela pede um período menor.
3. **Dado** um container, **Quando** o usuário escolhe "Ver auditoria" no detalhe do container, **Então** abre a Auditoria filtrada por aquele container e host.

---

### Jornada do Usuário 3 - Ver o detalhe e os eventos relacionados (Prioridade: P1)

Ao abrir um evento, o usuário vê todos os campos e os eventos relacionados da mesma operação (mesma correlação ou mesmo comando), por exemplo o pedido de parada, o resultado do comando e a justificativa.

**Valor desta prioridade**: uma ação gera vários eventos; vê-los juntos explica o que aconteceu.

**Teste independente**: parar um container com justificativa, abrir o evento "container.stop" e ver a justificativa, o resultado do comando e o link para o container.

**Cenários de aceite**:

1. **Dado** um evento, **Quando** aberto, **Então** o detalhe mostra ação em linguagem do operador, ator (nome e identificador), tipo de ator (usuário, agente, sistema), host, estágio, alvo, horário, resultado, motivo da falha, correlação, comando e os detalhes registrados (como justificativa já mascarada, shell e bytes do terminal, regra ou silenciamento).
2. **Dado** eventos com a mesma correlação ou o mesmo comando, **Quando** o detalhe é aberto, **Então** lista os relacionados em ordem cronológica, cada um navegável.
3. **Dado** um alvo que ainda existe (container, alerta, regra), **Quando** o detalhe é aberto, **Então** oferece o link para ele; se não existe mais, mostra o nome registrado sem link.

---

### Jornada do Usuário 4 - Exportar para evidência (Prioridade: P2)

Para uma auditoria externa ou incidente, o usuário exporta o resultado filtrado em CSV.

**Valor desta prioridade**: evidência fora do console, sem acesso ao banco.

**Teste independente**: filtrar um período, exportar, abrir o CSV e conferir colunas, quantidade e a neutralização de fórmulas; verificar o evento de exportação na própria trilha.

**Cenários de aceite**:

1. **Dado** um filtro com até 10.000 eventos, **Quando** exportado, **Então** o arquivo contém todos os eventos do filtro (não só a página carregada), com cabeçalho em português.
2. **Dado** um filtro com mais de 10.000 eventos, **Quando** o usuário tenta exportar, **Então** é orientado a reduzir o período.
3. **Dado** uma exportação, **Quando** concluída, **Então** gera um evento de auditoria "auditoria exportada" com o filtro usado e a quantidade, em cada host abrangido.

---

## Casos de Borda

- Evento de host removido ou desabilitado: continua visível para quem ainda tem `audit:read` no recurso; o nome registrado do host é usado se o cadastro não existir mais.
- Usuário perde `audit:read` num host: os eventos desse host somem da lista e do detalhe (indistinguível de inexistente).
- Eventos gravados por agente ou sistema (por exemplo, resultado de comando, varredura de alertas): ator mostrado como "Agente" ou "Sistema".
- Detalhes com chaves desconhecidas pela versão do console: exibidos como pares chave e valor, sem quebrar a tela.
- Volume alto (milhões de eventos): a consulta é sempre limitada por período e paginada; nunca carrega a trilha inteira.
- Relógio: horários exibidos no fuso do navegador, com o UTC disponível no detalhe.
- Trilha imutável: a tela não oferece edição nem exclusão.

## Requisitos (obrigatório)

### Requisitos Funcionais

**Consulta**

- **FR-001**: O sistema DEVE oferecer a tela Governança → Auditoria com a trilha dos hosts em que o usuário tem `audit:read`, do evento mais recente ao mais antigo, paginada, sem total global exato quando custoso.
- **FR-002**: A consulta DEVE ser sempre limitada a um período (padrão: últimas 24 horas; máximo: 90 dias por consulta).
- **FR-003**: Os filtros DEVEM incluir período, estágio, host, ator, categoria de ação (ambientes, containers, comandos, imagens, logs e inspeção, terminal, alertas e política), ação, resultado (sucesso, negado, falha) e busca textual (palavras da justificativa e de outros detalhes, ator, ação, alvo e código de falha, sem diferenciar acentos; correlação, comando ou evento exatos; prefixo do alvo), combináveis e mantidos na URL.
- **FR-004**: Cada linha DEVE mostrar horário, ator, ação em linguagem do operador, host, alvo e resultado; o detalhe DEVE mostrar todos os campos registrados e os eventos relacionados pela mesma correlação ou comando.
- **FR-005**: Os detalhes registrados DEVEM ser exibidos como foram gravados (já mascarados na origem); a tela NÃO DEVE reconstruir ou revelar dado mascarado.
- **FR-006**: O detalhe do container DEVE oferecer "Ver auditoria", abrindo a Auditoria filtrada pelo container e host.

**Ator legível**

- **FR-007**: Eventos novos DEVEM registrar, além do identificador do ator, o nome de usuário exibível no momento da ação; eventos anteriores mostram o identificador.

**Exportação**

- **FR-008**: O usuário DEVE poder exportar em CSV o resultado do filtro, até 10.000 eventos, com fórmulas neutralizadas e horários em UTC.
- **FR-009**: Toda exportação DEVE gerar evento de auditoria com o filtro e a quantidade, em cada host abrangido.

**Segurança e integridade**

- **FR-010**: A consulta e a exportação DEVEM exigir `audit:read` por host; nenhum dado DEVE revelar hosts sem o scope, nem a existência de eventos deles.
- **FR-011**: A trilha DEVE permanecer append-only; a interface e a API NÃO DEVEM permitir alterar ou excluir eventos.
- **FR-012**: O item de menu "Auditoria" DEVE aparecer só quando o usuário tem `audit:read` em algum host.
- **FR-013**: A trilha DEVE continuar particionada por mês (spec 001 FR-006) e a busca textual DEVE usar índice próprio presente em todas as partições, sem reescrever eventos gravados.

## Entidades Principais

- **Evento de auditoria** (existente, spec 001): identificador, horário, correlação, tipo de ator, ator, ação, host, resultado, código de falha, alvo, comando e detalhes; passa a ter o nome exibível do ator.
- **Filtro de auditoria**: período, estágio, hosts, ator, categoria, ação, resultado e texto.
- **Exportação**: filtro, quantidade, autor e horário (registrada como evento de auditoria).

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: 100% das ações de governança e operação geradas no teste (ambientes, containers, comandos, imagens, terminal, logs, alertas e política) são encontradas na tela com ator, host, alvo, horário e resultado.
- **SC-002**: Nenhum evento de host sem `audit:read` aparece na lista, no detalhe, nos relacionados ou na exportação (verificado com usuário de acesso parcial).
- **SC-003**: Com 1 milhão de eventos em 56 hosts nos últimos 30 dias, a primeira página de qualquer combinação de filtros responde em até 2 s no p95.
- **SC-004**: Um operador encontra "quem parou o container X ontem e por quê" em até 3 interações a partir do detalhe do container.
- **SC-005**: A exportação de 10.000 eventos termina em até 10 s e o arquivo abre em planilha sem execução de fórmulas.
- **SC-006**: A tela e o detalhe não têm violações sérias ou críticas de acessibilidade e são operáveis só por teclado.

## Premissas

- O scope `audit:read` já existe (spec 001) e é concedido por host pelo provisionamento; esta spec não cria scopes.
- A trilha atual (spec 001 e posteriores) é a fonte única; não há importação de fontes externas.
- Retenção e descarte continuam decisão humana (spec 001 FR-006); esta spec não apaga eventos.
- O nome exibível vem do token do usuário no momento da ação; mudanças posteriores de nome não alteram eventos já gravados.
- Fora do escopo: alertas sobre eventos de auditoria, envio a SIEM externo, assinatura criptográfica da trilha e consulta da auditoria do próprio Keycloak.
