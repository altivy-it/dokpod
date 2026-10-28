# Especificação da Feature: Volumes

**Branch da feature**: `018-volumes`

**Criada em**: 2026-10-15

**Estado**: `implemented` - aprovada pelo solicitante em 2026-10-15 ("Aprovado, siga com o plano"); implementada e integrada em 2026-10-15 (PR #23); evidências e itens `NOT RUN` em [tasks.md](tasks.md).

**Entrada**: Pedido do solicitante em 2026-10-15: "Siga com os Volumes", item "Volumes" da seção Operação do menu, hoje uma página "Em breve" ("Inventário e limpeza de volumes por host").

**Depende de**: [003](../003-inventario-lifecycle-reconciliacao/spec.md) (inventário e comandos duráveis), [008](../008-visao-geral-hosts-eventos/spec.md) (eventos, inclusive "volume removido"), [010](../010-lista-containers-acoes-lote/spec.md) (convenções de lista e lote), [011](../011-detalhe-container-logs/spec.md) (volumes no detalhe do container), [012](../012-imagens-pull/spec.md) (inventário por capability e projeção "em uso"), [016](../016-governanca-alertas/spec.md) (política de justificativa por estágio), [017](../017-auditoria/spec.md) (trilha consultável).

**Design**: não há board de Volumes em `docs/design/app`. O mockup define o item "Volumes" na seção Operação, a seção "Volumes" no detalhe do container e o evento "Volume removido". A tela segue o board `02 · Containers` e a tela Imagens (spec 012): lista, filtros, chips de resumo, ações por linha e em lote.

## Esclarecimentos

### Sessão 2026-10-15 (recomendações adotadas, sujeitas à aprovação)

- P: O que conta como volume? → R: Volumes gerenciados pelo engine (nomeados e anônimos), no Docker e no Podman. Bind mounts (pastas do host) não são volumes e ficam fora.
- P: Pode remover volume em uso? → R: Não. Só volumes sem nenhum container (inclusive parado) que os use; o agente confere de novo antes de remover e nunca força.
- P: Quem remove? → R: Scope novo `volume:delete` por host, com confirmação reforçada (digitar o nome do volume em Produção) e justificativa conforme a política do estágio da spec 016.
- P: Tamanho do volume? → R: Exibido quando o engine informa; o cálculo pode ser caro, então é feito sob demanda no detalhe do volume, não na lista.
- P: Limpeza automática por política de retenção (evento de exemplo do mockup)? → R: Fora desta entrega; exige decisão humana sobre perda de dados. Esta spec entrega a limpeza manual, em lote, dos volumes sem uso.
- P: Caminho do volume no disco do host (mountpoint)? → R: Não é exibido nem trafega para o plano de controle (reduz exposição da estrutura do host).

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Ver os volumes dos hosts e os sem uso (Prioridade: P1)

Um operador abre Operação → Volumes e vê os volumes de todos os hosts que pode ler, quais containers usam cada um e quais estão sem uso.

**Valor desta prioridade**: volumes esquecidos ocupam disco e guardam dados sem dono; hoje não há como vê-los pelo console.

**Teste independente**: criar volumes com e sem container, abrir a tela e conferir host, nome, driver, criação, containers que usam e o rótulo "sem uso", comparando com o engine.

**Cenários de aceite**:

1. **Dado** hosts autorizados com volumes, **Quando** o usuário abre Volumes, **Então** vê nome, host, engine, driver, criação, quantidade e nomes dos containers que o usam (inclusive parados) e o rótulo "Sem uso" quando não há nenhum.
2. **Dado** o resumo, **Quando** exibido, **Então** mostra total de volumes e quantos estão sem uso, e o chip "sem uso" filtra a lista.
3. **Dado** um volume anônimo (nome gerado pelo engine), **Quando** listado, **Então** aparece com o rótulo "Anônimo" e o identificador curto.
4. **Dado** um host com agente sem suporte a inventário de volumes, **Quando** listado, **Então** a tela informa que o inventário daquele host está indisponível, sem esconder os demais.

---

### Jornada do Usuário 2 - Investigar um volume (Prioridade: P1)

O operador abre um volume para ver detalhes e quem o usa, com acesso direto aos containers.

**Valor desta prioridade**: antes de limpar, é preciso saber o que o volume guarda e de quem é.

**Teste independente**: abrir o detalhe de um volume em uso e de um sem uso; conferir labels, driver, escopo, containers vinculados (com link) e o tamanho quando o engine informa.

**Cenários de aceite**:

1. **Dado** um volume, **Quando** aberto, **Então** o detalhe mostra nome, host, driver, escopo, criação, labels, containers que o usam (com link para o detalhe do container e o destino de montagem) e o tamanho quando disponível.
2. **Dado** o detalhe do container (spec 011), **Quando** a seção "Volumes" lista um volume nomeado, **Então** o nome leva ao detalhe do volume.

---

### Jornada do Usuário 3 - Remover volumes sem uso (Prioridade: P1)

O operador remove um ou vários volumes sem uso, com confirmação e justificativa quando exigida, e acompanha o resultado por volume.

**Valor desta prioridade**: libera disco e elimina dados órfãos com rastreabilidade.

**Teste independente**: selecionar volumes sem uso de hosts diferentes, remover em lote, acompanhar cada resultado, conferir a auditoria e o evento "volume removido"; tentar remover um volume em uso e ver a recusa.

**Cenários de aceite**:

1. **Dado** volumes sem uso selecionados, **Quando** o usuário escolhe "Remover", **Então** a confirmação lista os afetados e os ignorados (em uso, sem permissão, host desconectado), avisa que os dados serão apagados de forma definitiva e pede justificativa conforme a política do estágio.
2. **Dado** mais de um volume ou qualquer volume de Produção, **Quando** confirma, **Então** a confirmação exige digitar a quantidade (lote) ou o nome do volume (um volume em Produção).
3. **Dado** o envio, **Quando** cada comando termina, **Então** o resultado aparece por volume (removido, recusado por estar em uso, falha) e o volume some da lista.
4. **Dado** um volume que passou a ser usado entre a listagem e a remoção, **Quando** o agente recebe o comando, **Então** recusa sem remover e informa "em uso".
5. **Dado** um usuário sem `volume:delete` no host, **Quando** vê a lista, **Então** não tem a ação de remover para aquele host.

---

## Casos de Borda

- Volume usado só por container parado: conta como "em uso" e não pode ser removido.
- Volume criado ou removido fora do Dokpod: aparece ou some pelo inventário (eventos do engine e snapshots), sem ação do usuário.
- Mesmo nome de volume em hosts diferentes: são volumes distintos; a chave é host + nome.
- Driver de volume remoto (NFS, plugin): listado; a remoção segue o engine (o driver pode recusar).
- Agente desconectado: inventário mostrado como desatualizado (idade do inventário), sem ações.
- Volume com labels de sistema (por exemplo, compose): labels exibidas; a remoção segue as mesmas regras.
- Host com milhares de volumes anônimos: lista paginada no servidor; resumo correto.

## Requisitos (obrigatório)

### Requisitos Funcionais

**Inventário**

- **FR-001**: O agente DEVE publicar o inventário de volumes do host (nome, driver, escopo, criação, labels e containers que o montam), por snapshot e atualizações, com capability própria; o caminho no disco do host NÃO DEVE ser publicado.
- **FR-002**: O plano de controle DEVE manter a projeção de volumes reconstruível por host e marcar "em uso" quando qualquer container do host, em qualquer situação, monta o volume.
- **FR-003**: A API DEVE listar volumes dos hosts autorizados (`environment:read`) com filtros (estágio, host, engine, sem uso, anônimo), busca, ordenação, paginação e resumo (total, sem uso).
- **FR-004**: O detalhe do volume DEVE mostrar labels, escopo, containers vinculados com destino de montagem e o tamanho quando o engine informar, consultado sob demanda.

**Remoção**

- **FR-005**: A remoção DEVE exigir o scope `volume:delete` no host, seguir o pipeline de comandos duráveis (idempotência, prazo, auditoria, capability) e valer só para volume sem uso; o agente DEVE conferir o uso imediatamente antes e NUNCA forçar a remoção.
- **FR-006**: A confirmação DEVE avisar que os dados serão apagados de forma definitiva, listar afetados e ignorados, exigir digitar o nome (um volume em Produção) ou a quantidade (lote), e pedir justificativa conforme a política do estágio (spec 016), registrada na auditoria.
- **FR-007**: O lote DEVE enviar um comando por volume (até 50), acompanhar o resultado de cada um e permitir retomar após recarregar sem reenviar automaticamente, como o lote de containers (spec 010).
- **FR-008**: O resultado DEVE informar códigos padronizados (removido, em uso, não encontrado, recusado pelo driver, falha) e gerar o evento "volume removido" quando removido.

**Navegação**

- **FR-009**: A tela Volumes DEVE substituir a página "Em breve"; o detalhe do container DEVE ligar os volumes nomeados ao detalhe do volume.
- **FR-010**: Hosts sem a capability de inventário de volumes DEVEM aparecer como "inventário indisponível" na tela, sem impedir os demais.

## Entidades Principais

- **Volume**: host, nome, anônimo (sim/não), driver, escopo, criação, labels, containers que o montam (container, destino, somente leitura) e tamanho (quando informado).
- **Comando de remoção de volume**: host, volume, autor, justificativa (quando exigida), prazo, resultado e código de falha.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: A lista de volumes e a contagem "sem uso" coincidem com o engine em 100% dos hosts de teste (Docker e Podman).
- **SC-002**: Um volume criado ou removido fora do Dokpod aparece ou some da tela em até 30 segundos com o agente conectado.
- **SC-003**: Nenhum volume em uso (inclusive por container parado) é removido nos testes, inclusive quando passa a ser usado entre a listagem e o comando.
- **SC-004**: Remover 20 volumes sem uso em lote leva no máximo 4 interações a partir da tela (filtrar sem uso, selecionar, remover, confirmar), mais a justificativa quando exigida.
- **SC-005**: Com 56 hosts e 5.000 volumes, a primeira página responde em até 2 s no p95.
- **SC-006**: A tela e a confirmação não têm violações sérias ou críticas de acessibilidade e são operáveis só por teclado.

## Premissas

- Docker e Podman expõem volumes pela API compatível (`/volumes`); o agente usa a lista de containers do próprio inventário para saber quem monta cada volume.
- O tamanho do volume depende do engine (no Docker, `system df`); quando não disponível, a tela mostra "não informado".
- A remoção usa a política de justificativa por estágio da spec 016 sem nova configuração.
- O scope `volume:delete` é novo e concedido por host pelo provisionamento, como os demais.
- Fora do escopo: criação de volume pela tela de Volumes (já existe na criação de container, spec 013), limpeza automática por retenção, backup ou cópia de dados, bind mounts e volumes de pods.
