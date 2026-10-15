# Pesquisa: Visão geral com KPIs navegáveis e estatísticas dos containers

## D-001 - "Parados" passa a contar pausados

- **Decisão**: o card "Parados" soma `stopped` e `paused`, e o link filtra `status=stopped&status=paused`.
- **Justificativa**: o card usa o ícone de pausa, e hoje nenhum card conta pausados; na captura da stack real, 11 + 2 + 1 = 14 de 15 containers. Com a mudança, os cards de situação cobrem todos os containers, exceto os em "Iniciando".
- **Alternativas**: card próprio para pausados (muda o layout de seis cards do mockup); manter pausados fora (soma dos cards não fecha com o total).

## D-002 - Ordenação por consumo na camada de aplicação

- **Decisão**: para `sort` de CPU ou memória, o `ContainerSearchService` lê todo o conjunto filtrado do store em páginas internas (ordem por nome), anexa a amostra atual de cada container (`LatestMetricsCache`) e ordena em memória; depois aplica cursor e limite.
- **Justificativa**: as amostras de containers vivem só em memória na API (spec 009: série de containers em buffer em memória; só o host é persistido, por minuto). Levá-las ao PostgreSQL para ordenar exigiria escrita a cada 15 s por container. O conjunto é limitado a 2.000 containers por estágio (spec 009), o que cabe com folga em memória por requisição.
- **Alternativas**: persistir a amostra atual em `inventory_containers` (escrita contínua de alto volume); ordenar só a página no cliente (viola FR-005).

## D-003 - Cursor por valor, itens sem amostra no fim

- **Decisão**: chave de ordenação composta `(grupo, valor, nome, ambiente, id)`, com grupo 0 para itens com amostra e 1 sem amostra, em qualquer direção; o cursor codifica `grupo|valor|nome` no `SortKey` existente (formato invariante, valor com 6 casas).
- **Justificativa**: reaproveita o formato de cursor da spec 010 e garante desempate estável. Como o consumo muda a cada amostragem, um item pode mudar de posição entre páginas; é o mesmo comportamento aceito em listas ordenadas por valores vivos, registrado como premissa.
- **Alternativas**: cursor por deslocamento (duplica ou pula itens quando a lista muda, e não é estável entre réplicas); snapshot por consulta (estado no servidor por usuário).

## D-004 - Componente único de card navegável

- **Decisão**: os seis cards usam o mesmo link (`a` com `routerLink`, `queryParams` e `queryParamsHandling="merge"`) envolvendo o `dok-info-kpi`, como já faz "Imagens sem uso"; o nome acessível é composto por valor, descrição e destino ("11 containers em execução, abrir lista").
- **Justificativa**: padrão já validado (axe, teclado) na spec 012; a mesma classe de estilo garante cursor, foco e realce iguais (FR-007).
- **Alternativas**: botão com navegação programática (perde semântica de link, abrir em nova aba e leitura do destino).

## D-005 - Agregado atual pelos containers

- **Decisão**: por host habilitado e legível, CPU = soma de `cpuPercentOfHost` dos containers com amostra recente; memória usada = soma de `memoryUsedBytes`; memória total = memória total do host (amostra recente do host ou, na falta, a memória do descritor). Os valores por host alimentam `MetricsPolicy.Aggregate`, que já pondera CPU pelos núcleos; hosts sem nenhum container com amostra recente ficam fora (parcial).
- **Justificativa**: coerente com a lista aberta pelo card (mesmas amostras) e reaproveita a ponderação existente.
- **Alternativas**: média simples entre hosts (distorce hosts de tamanhos diferentes); usar `cpuPercentOfLimit` (não é fração da capacidade do host).

## D-006 - Gráfico mantém a série do host

- **Decisão**: "Uso de recursos do cluster" continua com a série persistida do host (12 h), com subtítulo "% da capacidade dos hosts · últimas 12 horas".
- **Justificativa**: a série de containers fica só 30 minutos em memória (spec 009); recompor 12 h exigiria persistência nova. Decisão do solicitante em 2026-10-14.
- **Alternativas**: série pelos containers (fora do escopo de armazenamento).
