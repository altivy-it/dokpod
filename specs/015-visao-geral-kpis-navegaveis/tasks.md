# Tarefas: Visão geral com KPIs navegáveis e estatísticas dos containers

**Entrada**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/containers-sort.md), [quickstart.md](quickstart.md).

**Estado**: `in-progress` - implementação iniciada em 2026-10-14.

**Pré-requisitos**: spec aprovada (2026-10-14); correção de amostragem do agente (PR #18) incorporada à branch.

## Fase 1: Contrato

- [ ] T001 [P] OpenAPI: ampliar o enum `sort` de `listContainers` com `cpu`, `-cpu`, `memory`, `-memory` e documentar a semântica de `current` em `getStageMetrics` (contracts/containers-sort.md); `OpenApiContractTests`; regenerar o cliente do frontend.

## Fase 2: Jornada 2 - ordenação por consumo (P1)

- [ ] T002 [US2] `ContainerSort` com `CpuAsc/CpuDesc/MemoryAsc/MemoryDesc`; regra em `ContainerResourceSort` (grupo com/sem amostra, valor, desempate por nome, ambiente e id; cursor `grupo|valor|nome`); testes unitários de ordem, itens sem amostra no fim nas duas direções e paginação por cursor sem repetição nem lacuna.
- [ ] T003 [US2] `ContainerSearchService`: com sort por consumo, ler todo o conjunto filtrado do store em páginas internas, anexar a amostra atual (`LatestMetricsCache`), ordenar e paginar; resumo por situação inalterado; testes unitários com store e cache falsos.
- [ ] T004 [US2] API: `TryParseSort` aceita os valores novos; teste de integração em PostgreSQL real com containers de consumo diferente (ordem, cursor e `400` para valor inválido).
- [ ] T005 [P] [US2] Frontend: colunas CPU e Memória ordenáveis com mapeamento `cpu`/`-cpu` e `memory`/`-memory` em `SERVER_SORT`; o cabeçalho reflete o `sort` da URL; Vitest.

## Fase 3: Jornada 1 - cards de situação navegáveis (P1)

- [ ] T006 [US1] Overview: "Parados" passa a somar `stopped` + `paused`; cards "Em execução", "Parados" e "Com falha ou reiniciando" viram links para `/containers` com `status` e estágio preservado; não navegáveis durante o carregamento; nome acessível com valor, descrição e destino; Vitest.
- [ ] T007 [US1] Lista de containers: estado vazio "Nenhum container nesta situação" quando aberta com filtro de situação sem resultados; Vitest.

## Fase 4: Jornadas 2 e 3 - cards de CPU e memória (P1/P2)

- [ ] T008 [US2] Overview: cards "CPU média do cluster" e "Memória alocada" viram links para `/containers?status=running&sort=-cpu|-memory`; navegáveis também com "indisponível"; indicação "parcial: N de M hosts"; mesma aparência de interação dos seis cards; Vitest.
- [ ] T009 [US3] `LatestMetricsCache.GetContainers(environmentId)` e `MetricsQueryService.GetStageAsync`: `current` pela soma dos containers por host (CPU ponderada por núcleos, memória total do host ou do descritor), hosts sem amostra de container fora do cálculo; série do host inalterada; testes unitários (parcial, indisponível, ponderação, coerência com a soma da lista).
- [ ] T010 [P] [US3] Gráfico "Uso de recursos do cluster": subtítulo "% da capacidade dos hosts · últimas 12 horas".

## Fase 5: Verificação

- [ ] T011 E2E simulado (`frontend/tests/e2e/mock_ui/test_015_overview_kpis.py`): os seis cards por clique e teclado, filtros e ordenação na URL, total da lista igual ao card, axe sem violações sérias/críticas; mock com `sort` por consumo.
- [ ] T012 Stack real (Linux, Podman): quickstart completo; SC-002, SC-003 e SC-006 medidos; SC-004 com 2.000 containers `NOT RUN` se não houver laboratório de carga.
- [ ] T013 Documentação: `docs/frontend.md` (cards navegáveis) e evidências nesta lista; estado da spec.
