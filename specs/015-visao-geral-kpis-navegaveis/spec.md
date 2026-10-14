# Especificação da Feature: Visão geral com KPIs navegáveis e estatísticas dos containers

**Branch da feature**: `015-visao-geral-kpis-navegaveis`

**Criada em**: 2026-10-14

**Estado**: `approved` - esclarecida e aprovada pelo solicitante em 2026-10-14 ("spec aprovada, siga com o plano e implemente").

**Entrada**: Pedido do solicitante em 2026-10-14, sobre captura da Visão geral na stack real (Homologação, 1 host Podman, 15 containers): "Os cards precisam ser clicáveis como o último, devem abrir a tela de containers, os dados de estatística precisam ser populados, com os dados dos containers". Board `01 · Visão geral` (seis `Altivy.InfoKPI`, gráfico "Uso de recursos do cluster" e donut "Containers por engine") e board `02 · Containers` (filtros, colunas CPU e Memória).

**Depende de**: [008](../008-visao-geral-hosts-eventos/spec.md) (KPIs e contagens por situação), [009](../009-metricas-recursos/spec.md) (amostras de recursos), [010](../010-lista-containers-acoes-lote/spec.md) (lista com filtros na URL), [012](../012-imagens-pull/spec.md) (KPI "Imagens sem uso" já navegável).

**Contexto**: na captura, "CPU média do cluster" e "Memória alocada" mostravam "indisponível" e o gráfico informava que nenhum host publicava amostras. A causa era um defeito do agente da spec 009 (amostragem concorrente que parava no primeiro ciclo), corrigido em `fix(agent)` (PR #18), fora desta spec. Depois da correção, os valores aparecem, calculados a partir da amostra do **host**. Esta spec trata da navegação dos cards e da origem dos números.

## Esclarecimentos

### Sessão 2026-10-14

- P: Os cards "CPU média do cluster" e "Memória alocada" devem vir da soma dos containers ou da amostra do host (spec 009)? → R: Soma dos containers em execução sobre a capacidade dos hosts com amostra. O gráfico "Uso de recursos do cluster" continua com a série do host, com a origem indicada no subtítulo.

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Abrir a lista de containers a partir de um card de situação (Prioridade: P1)

Como operador, ao ver "11 Em execução", "2 Parados" ou "1 Com falha ou reiniciando" na Visão geral, quero clicar no card e chegar à tela Containers já filtrada por essa situação, no mesmo estágio, para agir sem refazer os filtros.

**Valor desta prioridade**: o card "Imagens sem uso" já leva à sua lista; os cards de situação são os mais usados na triagem e hoje não levam a lugar nenhum.

**Teste independente**: com containers em cada situação, clicar em cada card de situação e comparar o número do card com a quantidade de linhas da lista aberta.

**Cenários de aceite**:

1. **Dado** a Visão geral com o estágio Homologação selecionado, **Quando** o operador clica em "Em execução", **Então** a tela Containers abre com o filtro de situação "Em execução", o estágio Homologação mantido e o chip de filtro visível.
2. **Dado** o card "Parados", **Quando** clicado, **Então** a lista mostra os containers parados e pausados, e o total da lista é igual ao número do card.
3. **Dado** o card "Com falha ou reiniciando", **Quando** clicado, **Então** a lista mostra os containers com falha e os em reinício.
4. **Dado** um card de situação com valor 0, **Quando** clicado, **Então** a lista abre filtrada e mostra o estado vazio "Nenhum container nesta situação".
5. **Dado** foco no card pelo teclado, **Quando** o operador pressiona Enter, **Então** a navegação é a mesma do clique, e o leitor de tela anuncia o card como link com o número e o destino ("11 containers em execução, abrir lista").

### Jornada do Usuário 2 - Abrir os containers que mais consomem a partir de CPU e memória (Prioridade: P1)

Como operador, ao ver "CPU média do cluster" ou "Memória alocada", quero clicar no card e ver a lista de containers ordenada pelo maior consumo do recurso, para identificar quem pesa no cluster.

**Valor desta prioridade**: transforma um número agregado em ação: o próximo passo natural ao ver CPU ou memória alta é saber quais containers consomem.

**Teste independente**: com containers de consumo diferente, clicar em cada card e verificar que a primeira linha é o container de maior consumo do recurso.

**Cenários de aceite**:

1. **Dado** o card "CPU média do cluster", **Quando** clicado, **Então** a tela Containers abre com o filtro "Em execução" e ordenada por CPU, do maior para o menor.
2. **Dado** o card "Memória alocada", **Quando** clicado, **Então** a lista abre com o filtro "Em execução" e ordenada por memória usada, do maior para o menor.
3. **Dado** a lista aberta por um desses cards, **Quando** o operador clica no cabeçalho da coluna CPU ou Memória, **Então** a ordenação alterna entre crescente e decrescente e a URL reflete a ordenação.
4. **Dado** containers sem amostra recente, **Quando** a lista é ordenada por CPU ou memória, **Então** eles aparecem depois dos que têm amostra, com o valor "—".
5. **Dado** o card com valor "indisponível", **Quando** clicado, **Então** a lista abre normalmente; a indisponibilidade é do agregado, não impede consultar os containers.

### Jornada do Usuário 3 - Estatísticas do cluster calculadas a partir dos containers (Prioridade: P2)

Como operador, quero que "CPU média do cluster" e "Memória alocada" reflitam o consumo dos containers sob gestão, de forma coerente com a lista aberta pelo card, para que o número do card e a lista contem a mesma história.

**Valor desta prioridade**: hoje os cards usam a amostra do host, que inclui processos fora dos containers (o próprio sistema, o agente e outros serviços); a lista mostra apenas containers. Os cards passam a somar o consumo dos containers sobre a capacidade dos hosts (decisão do solicitante em 2026-10-14; ver Esclarecimentos).

**Teste independente**: com um processo pesado fora dos containers no host, comparar o card com a soma das colunas CPU e Memória da lista aberta pelo card.

**Cenários de aceite**:

1. **Dado** containers em execução com amostras recentes, **Quando** a Visão geral carrega, **Então** "Memória alocada" mostra a soma da memória usada pelos containers do estágio dividida pela memória total dos hosts com amostra, com uma casa decimal.
2. **Dado** containers em execução com amostras recentes, **Quando** a Visão geral carrega, **Então** "CPU média do cluster" mostra a soma do CPU dos containers (percentual do host) dividida pela capacidade dos hosts com amostra.
3. **Dado** um host sem amostras recentes, **Quando** o agregado é calculado, **Então** ele não entra no numerador nem no denominador, e o card indica "parcial: N de M hosts".
4. **Dado** nenhum container com amostra recente, **Quando** a Visão geral carrega, **Então** o card mostra "indisponível", nunca 0%.
5. **Dado** o gráfico "Uso de recursos do cluster", **Quando** exibido, **Então** continua mostrando a série do host (12 horas persistidas pela spec 009), com subtítulo que deixe clara a origem ("host") para não ser confundido com os cards.

## Casos de Borda

- Usuário sem `environment:read` em algum host: o host não entra nas contagens nem nos agregados, e a lista aberta pelo card também não o mostra; os números continuam coerentes.
- Estágio "Todos os estágios": o card leva à lista sem filtro de estágio.
- Container que muda de situação entre o carregamento da Visão geral e a abertura da lista: a lista reflete o estado atual; o card não é recalculado retroativamente. Diferença momentânea é aceitável e não é erro.
- Card em carregamento (esqueleto): não é navegável até ter valor, para evitar clique em número que ainda vai mudar.
- Containers pausados: entram em "Parados" (o ícone do card é pausa), e o card passa a contar parados mais pausados.
- Container com falha e reiniciando ao mesmo tempo: conta uma única vez em "Com falha ou reiniciando".
- Muitos containers (até 2.000, limite da spec 009): a ordenação por CPU e memória ocorre no servidor, com paginação estável.
- Agente desconectado: seus containers não têm amostra recente; ficam no fim da ordenação por consumo, com "—".

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: Os cards "Em execução", "Parados" e "Com falha ou reiniciando" DEVEM ser links para a tela Containers com o filtro de situação correspondente, preservando o estágio selecionado.
- **FR-002**: O card "Parados" DEVE contar containers parados e pausados, e seu link DEVE filtrar as duas situações.
- **FR-003**: O card "Com falha ou reiniciando" DEVE filtrar as situações "Com falha" e "Reiniciando".
- **FR-004**: Os cards "CPU média do cluster" e "Memória alocada" DEVEM ser links para a tela Containers filtrada por "Em execução" e ordenada, respectivamente, por CPU e por memória usada, em ordem decrescente.
- **FR-005**: A tela Containers DEVE aceitar ordenação por CPU e por memória usada, crescente e decrescente, pela URL e pelo cabeçalho das colunas; a ordenação DEVE ser feita no servidor sobre todo o conjunto filtrado, e não só sobre a página visível.
- **FR-006**: Containers sem amostra recente DEVEM ficar depois dos que têm amostra em qualquer direção de ordenação por consumo, com desempate estável por nome.
- **FR-007**: O card "Imagens sem uso" DEVE manter o comportamento atual (spec 012); os seis cards DEVEM ter a mesma aparência de interação (cursor, foco visível, realce ao passar o mouse).
- **FR-008**: Cada card navegável DEVE ser um link acessível, operável por teclado, com nome acessível que inclua o valor, a descrição e o destino; o foco visível DEVE atender WCAG 2.2 AA.
- **FR-009**: Cards em carregamento NÃO DEVEM ser navegáveis; cards com valor "indisponível" DEVEM permanecer navegáveis.
- **FR-010**: A quantidade mostrada em cada card de situação DEVE ser igual ao total da lista aberta por ele, considerando o mesmo estágio, as mesmas permissões e o mesmo instante.
- **FR-011**: "CPU média do cluster" e "Memória alocada" DEVEM ser calculados a partir das amostras recentes dos containers em execução sobre a capacidade dos hosts com amostra excluindo hosts sem permissão de leitura ou sem amostra recente.
- **FR-012**: Quando parte dos hosts do estágio não tiver amostra recente, os cards de CPU e memória DEVEM indicar "parcial: N de M hosts", como já ocorre em "Imagens sem uso".
- **FR-013**: Sem nenhuma amostra recente de container, os cards de CPU e memória DEVEM mostrar "indisponível", nunca 0.
- **FR-014**: O gráfico "Uso de recursos do cluster" DEVE manter a série do host da spec 009 e identificar a origem no subtítulo.
- **FR-015**: Os números e os links NÃO DEVEM revelar a existência de hosts ou containers que o usuário não pode ler.

### Entidades Principais

- **Card de KPI navegável**: valor, descrição, ícone, cor, estado (carregando, valor, indisponível, parcial) e destino (rota, filtros e ordenação).
- **Agregado de consumo do estágio**: soma do consumo dos containers com amostra recente, capacidade dos hosts com amostra, número de hosts com amostra e total de hosts legíveis.
- **Ordenação por consumo**: campo (CPU ou memória usada), direção e regra de posicionamento dos itens sem amostra.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: A partir da Visão geral, o operador chega à lista de containers filtrada pela situação de interesse com 1 interação (antes: abrir Containers e aplicar o filtro, no mínimo 3 interações).
- **SC-002**: Em 100% das verificações com estado estável, o número de cada card de situação é igual ao total da lista aberta por ele.
- **SC-003**: Ao abrir a lista pelo card de CPU ou de memória, o container de maior consumo aparece na primeira linha em 100% das verificações com amostras recentes.
- **SC-004**: Com até 2.000 containers, a lista ordenada por consumo responde em até 2 segundos no percentil 95.
- **SC-005**: Os seis cards são operáveis só por teclado e não geram violações sérias ou críticas na verificação automática de acessibilidade.
- **SC-006**: Com amostras recentes, os cards de CPU e memória não mostram "indisponível"; com um processo fora dos containers consumindo recursos do host, o card de memória fica dentro de 1 ponto percentual da soma da coluna Memória da lista aberta por ele.

## Premissas

- "Clicáveis como o último" refere-se ao card "Imagens sem uso", que já é link (spec 012); os demais adotam o mesmo padrão.
- O destino de todos os cards é a tela Containers, inclusive os de CPU e memória, conforme o pedido ("devem abrir a tela de containers"); para esses dois, a ordenação por consumo é o recorte que dá sentido ao clique.
- Pausados entram em "Parados" porque o card usa o ícone de pausa e hoje nenhum card os conta, o que faz a soma dos cards diferir do total de containers.
- "Amostra recente" usa a janela da spec 009 (até 2 intervalos de amostragem).
- O mockup não define navegação nos cards; a navegação é extensão pedida pelo solicitante, coerente com o padrão já implementado em "Imagens sem uso".
- O defeito de amostragem do agente que deixava os cards "indisponível" é corrigido fora desta spec (PR #18); esta spec assume essa correção.
