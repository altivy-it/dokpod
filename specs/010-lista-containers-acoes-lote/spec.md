# Especificação da Feature: Lista Unificada de Containers, Pausa e Ações em Lote

**Branch da feature**: `007-console-operacional`

**Criada em**: 2026-10-12

**Estado**: `draft` - aguardando revisão humana.

**Entrada**: Board `02 · Containers` ([Containers.dc.html](../../docs/design/app/project/Containers.dc.html)).

**Depende de**: [007](../007-fundacao-console-design-system/spec.md), [008](../008-visao-geral-hosts-eventos/spec.md) (situação exibida, portas, criação, estágio). **Usa quando disponível**: [009](../009-metricas-recursos/spec.md) (CPU/memória).

## Contexto

Hoje a lista de containers é por ambiente, em cartões, com ações individuais (iniciar, parar, reiniciar, excluir). O mockup mostra uma única lista com containers Docker e Podman de todos os hosts do estágio, filtros, resumo por situação, grid com seleção múltipla, ações em lote, ações por linha (logs, terminal, inspecionar, reiniciar, parar, remover), estado "Pausado" e exportação.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Encontrar containers em todos os hosts (Prioridade: P1)

Como operador, quero uma lista única dos containers de todos os hosts autorizados do estágio, com nome, situação, engine, host, imagem, CPU, memória, portas e data de criação, filtrável e ordenável, com busca por nome, imagem, host ou porta.

**Valor desta prioridade**: Substitui navegar host por host.

**Teste independente**: Dois hosts (Docker e Podman) com 40 containers; buscar, filtrar por engine/host/situação, ordenar e paginar; comparar com engines.

**Cenários de aceite**:

1. **Dado** estágio Produção, **Quando** a tela abre, **Então** lista containers de todos os hosts autorizados do estágio, com chips de filtro ativos (Engine, Estágio) e resumo com as seis situações exibidas da 008 (em execução, iniciando, pausados, reiniciando, com falha, parados), ocultando as de valor zero, cuja soma é igual ao total.
2. **Dado** busca "8080", **Quando** digitada, **Então** retorna containers cuja porta, nome, imagem ou host contenha o termo.
3. **Dado** filtro de situação "Com falha", **Quando** aplicado, **Então** a lista e o resumo refletem o filtro e o chip pode ser removido por teclado.
4. **Dado** mais containers que uma página, **Quando** o usuário rola até o fim ou aciona "Carregar mais", **Então** a próxima página chega sem perder seleção, filtros ou posição.
5. **Dado** atualização em tempo real, **Quando** um container muda de situação, **Então** a linha é atualizada no lugar sem reordenar a lista nem mover foco; novos containers são anunciados ("2 novos containers - atualizar") em vez de inseridos de surpresa.
6. **Dado** container de host não autorizado, **Quando** a lista é montada, **Então** ele não aparece em linhas, contagens ou busca.

### Jornada do Usuário 2 - Agir em vários containers de uma vez (Prioridade: P1)

Como operador, quero selecionar linhas e iniciar, parar, reiniciar ou remover todos os selecionados, vendo o progresso e o resultado de cada um.

**Valor desta prioridade**: Operações repetitivas (ex.: parar workers de batch) em um passo.

**Teste independente**: Selecionar containers de hosts e engines diferentes, executar cada ação em lote e verificar resultado individual, auditoria e idempotência após recarregar.

**Cenários de aceite**:

1. **Dado** 2 containers selecionados, **Quando** aciona "Parar", **Então** a confirmação lista os containers afetados e, após confirmar, cada um recebe seu próprio comando idempotente e auditado.
2. **Dado** ação em lote em andamento, **Quando** os comandos progridem, **Então** cada linha mostra pendente/concluído/falhou/indeterminado e um resumo final é anunciado ("3 concluídos, 1 falhou").
3. **Dado** container selecionado para o qual a ação não se aplica (ex.: iniciar um já em execução) ou sem permissão, **Quando** o lote é montado, **Então** ele é listado como "ignorado" com motivo, sem envio de comando.
4. **Dado** "Remover" em lote, **Quando** confirmado, **Então** a confirmação informa explicitamente que volumes não são removidos e, quando houver mais de um container em estágio Produção, exige digitar a quantidade exata de containers afetados (ex.: "3") para habilitar o botão.
5. **Dado** perda de conexão durante o lote, **Quando** a página é recarregada na mesma aba, **Então** o estado dos comandos já enviados é recuperado e nada é reenviado sem nova confirmação. Em outra aba ou após fechar o navegador, o acompanhamento do lote não é recuperado; os comandos seguem no servidor e o resultado aparece na lista e no feed de eventos.

### Jornada do Usuário 3 - Pausar e retomar (Prioridade: P2)

Como operador, quero pausar e retomar containers, e ver o estado "Pausado".

**Valor desta prioridade**: Estado presente no mockup; útil para depuração (ex.: `debug-rede`).

**Teste independente**: Pausar e retomar em Docker e Podman reais, com idempotência e auditoria.

**Cenários de aceite**:

1. **Dado** container em execução e permissão `container:pause`, **Quando** aciona "Pausar", **Então** o container fica "Pausado" e o resultado do comando é registrado como evento `command.succeeded` (008 FR-009), sem tipo de evento novo.
2. **Dado** container pausado, **Quando** aciona "Retomar", **Então** volta a "Em execução".
3. **Dado** engine/host sem a capability de pausa, **Quando** o operador tenta pausar, **Então** a ação aparece desabilitada com motivo.

### Jornada do Usuário 4 - Ações por linha e exportação (Prioridade: P2)

Como operador, quero ações rápidas por linha (Ver logs, Abrir terminal, Inspecionar, Reiniciar, Parar, Remover) e exportar a lista filtrada.

**Valor desta prioridade**: Atalhos para o detalhe e para relatórios.

**Teste independente**: Acionar cada ação por mouse e teclado; exportar CSV e conferir colunas e escape.

**Cenários de aceite**:

1. **Dado** linha, **Quando** escolhe "Ver logs", "Abrir terminal" ou "Inspecionar", **Então** abre o detalhe do container na aba correspondente (specs 011/014), ou a ação aparece desabilitada se a spec ou a permissão não estiverem disponíveis.
2. **Dado** clique ou Enter no nome, **Quando** acionado, **Então** abre o detalhe do container.
3. **Dado** "Exportar", **Quando** acionado, **Então** baixa CSV com as linhas carregadas e filtros aplicados, com nome `containers-<estagio>-<data>.csv`, valores escapados contra injeção de fórmula.

## Casos de Borda

- Container removido entre seleção e envio: comando retorna "não encontrado" e a linha some após atualização, sem afetar os demais.
- Revisão do container mudou entre seleção e envio: comando é rejeitado por revisão divergente; usuário pode reaplicar com a revisão nova.
- Lote acima do limite (proposto: 50 containers): bloqueado com mensagem para refinar a seleção.
- Host desconectado: ações sobre seus containers ficam desabilitadas com motivo; leitura continua mostrando idade do estado.
- Nomes iguais em hosts diferentes: identificados por host na confirmação.
- Ordenação por CPU/memória: feita só nas linhas já carregadas, com a indicação "ordenado na página carregada", porque as métricas ficam em memória (009 D-002); sem a 009, as colunas ficam ocultas.
- Pausa ou retomada pedida com o container no estado errado (ex.: retomar um container em execução): o comando falha com `not_paused`/`not_running`; revisão divergente segue a regra existente de `expectedContainerRevision`.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: A API DEVE oferecer listagem de containers de múltiplos hosts autorizados, filtrável por estágio, host, engine, situação exibida e texto (nome, imagem, host, porta), ordenável no servidor por nome, situação, host e criação, com paginação por cursor estável. CPU e memória são ordenáveis apenas nas linhas carregadas, no navegador.
- **FR-002**: A listagem DEVE retornar, por container: identificador, host (id e nome), engine, nome, imagem, situação exibida, estado bruto, portas formatáveis, criação, revisão, observação e, quando disponível, CPU e memória; e um resumo por situação para os filtros aplicados.
- **FR-003**: A API DEVE aceitar as ações `pause` e `unpause` com os mesmos requisitos dos comandos existentes (scope `container:pause`, idempotência, revisão esperada, prazo, auditoria, capability do agente confirmada na sessão, journal-before-ack). O scope `container:pause` DEVE ser provisionado no Keycloak (resource server `dokpod-api`) e documentado em `docs/configuracao-keycloak.md`.
- **FR-004**: O agente DEVE executar pausa e retomada em Docker e Podman e anunciar as capabilities correspondentes.
- **FR-005**: Ações em lote DEVEM ser executadas como comandos individuais por container, cada um com sua chave de idempotência, autorização e auditoria; não existe comando de lote no agente.
- **FR-006**: A interface DEVE impedir lote acima do limite configurado e omitir com motivo os containers não elegíveis (situação, permissão, capability, host desconectado).
- **FR-007**: Remoção (individual ou em lote) DEVE exigir confirmação contextual que liste os containers e declare que volumes não são removidos; em Produção, remoção de mais de um container exige digitar a quantidade exata de containers afetados.
- **FR-008**: A interface DEVE persistir em `sessionStorage` (escopo da aba) as chaves de idempotência e os IDs de comando do lote em andamento para recuperar o acompanhamento após recarga na mesma aba, sem reenviar automaticamente; a situação de cada comando é sempre consultada no servidor.
- **FR-009**: Atualizações em tempo real DEVEM atualizar linhas no lugar, preservar seleção, filtros, ordenação e foco, e sinalizar novas linhas sem inseri-las abruptamente.
- **FR-010**: A tela DEVE seguir o mockup: cabeçalho com eyebrow "Operação · {estágio}", título, descrição, botões de lote (Iniciar, Parar, Reiniciar, Remover) e "Novo container" (quando 013 entregue), chips de filtro, resumo por situação e grid com colunas, ações por linha, densidade, busca, "atualizado em" e exportação.
- **FR-011**: A rota `/environments/:environmentId/containers` atual DEVE redirecionar para a lista unificada filtrada por host, preservando links existentes.

### Entidades Principais

- **Linha de container**: projeção enriquecida (008) + host + engine + recursos (009).
- **Lote de ações**: identificador local, ação, itens (container, host, chave de idempotência, comando, estado), criado em; existe só no navegador.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Lista com 1.120 containers em 56 hosts retorna a primeira página em até 500 ms p95 e a busca responde em até 1 s.
- **SC-002**: Lote de 20 containers mostra resultado individual de todos em até 30 s com engines saudáveis.
- **SC-003**: Zero efeitos duplicados em testes de recarga, retry e perda de resposta durante lotes.
- **SC-004**: Nenhum container não autorizado aparece em linhas, resumo, busca ou exportação.
- **SC-005**: Operador completa "parar 2 containers com falha selecionados" em até 4 interações após abrir a tela.

## Premissas

- Limite de lote proposto: 50 containers; confirmação reforçada para remoção múltipla em Produção.
- Agrupamento por coluna do grid é desejável (P3) e pode ficar para depois sem afetar o aceite.
- O chip "Ambiente" do mockup é exibido como "Estágio", termo da spec 008, para não confundir com o host (`environment` no contrato).
- A autorização por host reutiliza o `IEnvironmentAuthorizationDecider` existente (limite de 500 decisões por requisição, acima da margem de 100 hosts).
