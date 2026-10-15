# Tarefas: Visão geral com KPIs navegáveis e estatísticas dos containers

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/containers-sort.md), [quickstart.md](quickstart.md).

**Estado**: `implemented` - concluída em 2026-10-14. `NOT RUN`: Docker real e laboratório de carga com 2.000 containers reais (SC-004 medido em PostgreSQL real com amostras simuladas).

**Pré-requisitos**: spec aprovada (2026-10-14); correção de amostragem do agente (PR #18) incorporada à branch.

## Fase 1: Contrato

- [x] T001 [P] OpenAPI: ampliar o enum `sort` de `listContainers` com `cpu`, `-cpu`, `memory`, `-memory` e documentar a semântica de `current` em `getStageMetrics` (contracts/containers-sort.md); `OpenApiContractTests`; regenerar o cliente do frontend. — OpenAPI com `cpu`, `-cpu`, `memory`, `-memory` e semântica de `current` em `getStageMetrics`; `OpenApiContractTests`; cliente regenerado.

## Fase 2: Jornada 2 - ordenação por consumo (P1)

- [x] T002 [US2] `ContainerSort` com `CpuAsc/CpuDesc/MemoryAsc/MemoryDesc`; regra em `ContainerResourceSort` (grupo com/sem amostra, valor, desempate por nome, ambiente e id; cursor `grupo|valor|nome`); testes unitários de ordem, itens sem amostra no fim nas duas direções e paginação por cursor sem repetição nem lacuna. — `ContainerResourceSort` e `ContainerSort` ampliado; `ContainerResourceSortTests` (ordem nas duas direções, sem amostra no fim, regra da coluna de CPU, paginação por cursor sem repetição nem lacuna).
- [x] T003 [US2] `ContainerSearchService`: com sort por consumo, ler todo o conjunto filtrado do store em páginas internas, anexar a amostra atual (`LatestMetricsCache`), ordenar e paginar; resumo por situação inalterado; testes unitários com store e cache falsos. — `ContainerSearchService` lê o conjunto filtrado em páginas de 500 (teto de 5.000) e ordena na aplicação; teste com 1.200 containers (maior consumo além da primeira página interna).
- [x] T004 [US2] API: `TryParseSort` aceita os valores novos; teste de integração em PostgreSQL real com containers de consumo diferente (ordem, cursor e `400` para valor inválido). — `TryParseSort` com os valores novos; PostgreSQL real: dois hosts com cursor (`SearchService_SortsByCurrentConsumptionAcrossHostsWithStableCursor`) e SC-004 com 2.000 containers abaixo de 2 s no p95.
- [x] T005 [P] [US2] Frontend: colunas CPU e Memória ordenáveis com mapeamento `cpu`/`-cpu` e `memory`/`-memory` em `SERVER_SORT`; o cabeçalho reflete o `sort` da URL; Vitest. — Colunas CPU e Memória ordenáveis; `gridSortFromServer` reflete a URL no cabeçalho. Correção encontrada: a ordenação da URL era trocada por `name` ao abrir a lista (valia também para `-createdAt`); o grid passou a manter números ausentes no fim nas duas direções.

## Fase 3: Jornada 1 - cards de situação navegáveis (P1)

- [x] T006 [US1] Overview: "Parados" passa a somar `stopped` + `paused`; cards "Em execução", "Parados" e "Com falha ou reiniciando" viram links para `/containers` com `status` e estágio preservado; não navegáveis durante o carregamento; nome acessível com valor, descrição e destino; Vitest. — Links dos cards de situação com `status` e estágio preservado; "Parados" soma parados e pausados; não navegáveis durante o carregamento; nome acessível = texto visível + destino em `.sr-only` (o axe recusou `aria-label` por WCAG 2.5.3).
- [x] T007 [US1] Lista de containers: estado vazio "Nenhum container nesta situação" quando aberta com filtro de situação sem resultados; Vitest. — Estado vazio "Nenhum container nesta situação." quando só o filtro de situação está ativo.

## Fase 4: Jornadas 2 e 3 - cards de CPU e memória (P1/P2)

- [x] T008 [US2] Overview: cards "CPU média do cluster" e "Memória alocada" viram links para `/containers?status=running&sort=-cpu|-memory`; navegáveis também com "indisponível"; indicação "parcial: N de M hosts"; mesma aparência de interação dos seis cards; Vitest. — Cards de CPU e memória para `status=running&sort=-cpu|-memory`, navegáveis com "indisponível" e com "parcial: N de M hosts".
- [x] T009 [US3] `LatestMetricsCache.GetContainers(environmentId)` e `MetricsQueryService.GetStageAsync`: `current` pela soma dos containers por host (CPU ponderada por núcleos, memória total do host ou do descritor), hosts sem amostra de container fora do cálculo; série do host inalterada; testes unitários (parcial, indisponível, ponderação, coerência com a soma da lista). — `LatestMetricsCache.GetContainers` e agregado atual pela soma dos containers (CPU ponderada por núcleos; capacidade do host ou do descritor); `Stage_CurrentFollowsContainersNotTheWholeHost` e teste anterior atualizado.
- [x] T010 [P] [US3] Gráfico "Uso de recursos do cluster": subtítulo "% da capacidade dos hosts · últimas 12 horas". — Subtítulo "% da capacidade dos hosts · últimas 12 horas".

## Fase 5: Verificação

- [x] T011 E2E simulado (`frontend/tests/e2e/mock_ui/test_015_overview_kpis.py`): os seis cards por clique e teclado, filtros e ordenação na URL, total da lista igual ao card, axe sem violações sérias/críticas; mock com `sort` por consumo. — `test_015_overview_kpis.py`: 5 testes verdes com axe (situação com totais, consumo, teclado e estágio, nomes acessíveis, estado vazio); suíte simulada completa com 61 verdes; Vitest com 120 verdes.
- [x] T012 Stack real (Linux, Podman): quickstart completo; SC-002, SC-003 e SC-006 medidos; SC-004 com 2.000 containers `NOT RUN` se não houver laboratório de carga. — Stack real em 2026-10-14 (Linux, Podman 5.7.0 rootless): SC-002 OK (card = lista em Em execução 12, Parados 3, Com falha 1), SC-003 OK (CPU e memória em ordem decrescente, primeiro container igual à primeira linha), SC-006 OK (card de memória 4,9% contra 4,86% da soma da lista, diferença de 0,04 pp). SC-004 medido em PostgreSQL real (T004); laboratório de carga com 2.000 containers reais: **NOT RUN**. Docker real: **NOT RUN**.
- [x] T013 Documentação: `docs/frontend.md` (cards navegáveis) e evidências nesta lista; estado da spec. — `docs/frontend.md` (cards de KPI navegáveis) e evidências nesta lista.
