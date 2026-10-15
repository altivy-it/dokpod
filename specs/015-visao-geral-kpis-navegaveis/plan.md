# Plano de Implementação: Visão geral com KPIs navegáveis e estatísticas dos containers

**Branch**: `015-visao-geral-kpis-navegaveis` | **Data**: 2026-10-14 | **Spec**: [spec.md](spec.md)

**Estado**: `implemented` - concluída em 2026-10-14; evidências e itens `NOT RUN` em [tasks.md](tasks.md).

## Resumo

Os seis cards da Visão geral passam a ser links para a tela Containers: os de situação com o filtro correspondente e os de CPU e memória com a lista em execução ordenada por consumo. A API ganha ordenação por CPU e memória na lista de containers, feita na camada de aplicação porque as métricas vivem em memória (spec 009). Os cards de CPU e memória passam a somar o consumo dos containers sobre a capacidade dos hosts, reutilizando a agregação ponderada existente; o gráfico de 12 horas mantém a série do host.

## Contexto Técnico

**Linguagem/Versão**: .NET 10, OpenAPI 3.1, Angular 22 (Signals), TypeScript estrito.

**Dependências Principais**: Existentes. Nenhuma dependência nova.

**Armazenamento**: Sem mudança de schema. Contagens e inventário vêm do PostgreSQL (`inventory_containers`); as métricas atuais de containers vêm do `LatestMetricsCache` em memória da API.

**Testes**: unitários da aplicação (ordenação por consumo, cursor, agregação pelos containers), integração da API em PostgreSQL real (lista ordenada com paginação), Vitest (links e nomes acessíveis dos cards, mapeamento de ordenação), Playwright simulado com axe e Playwright na stack real (Linux, Podman).

**Plataforma-alvo**: console web; API do plano de controle em container Linux.

**Tipo de projeto**: web (backend + frontend), sem mudança no agente nem no protocolo.

**Metas de desempenho**: lista ordenada por consumo em até 2 s no p95 com 2.000 containers (SC-004).

**Restrições**: ordenação sobre todo o conjunto filtrado, não só a página; itens sem amostra sempre no fim; sem revelar hosts não autorizados; sem mudança de protocolo gRPC.

**Escala/Escopo**: até 2.000 containers por estágio (limite da spec 009); 6 cards; 1 parâmetro de ordenação novo.

## Verificação da Constituição

| Princípio | Situação |
| --- | --- |
| I. Documentação em pt-BR | OK: spec, plano e documentos em pt-BR. |
| II. Engine local como fonte | OK: contagens vêm do inventário reconciliado; métricas, das amostras do agente. Nenhuma operação nova no engine. |
| III. Identidade externa e menor privilégio | OK: lista e agregados usam apenas hosts com `environment:read` (FR-015); nenhum scope novo. |
| IV. Operações duráveis | N/A: somente leitura e navegação. |
| V. Arquitetura modular e compatibilidade | OK: ordenação nova é aditiva no contrato (valores novos no enum `sort`); clientes antigos não são afetados. Regra de agregação fica no módulo de métricas da aplicação. |
| VI. Qualidade verificável | OK: testes unitários, PostgreSQL real, Vitest, Playwright simulado com axe e stack real com Podman. Docker real fica `NOT RUN` (sem Docker no host de validação). |

Reavaliação após o desenho: sem violações.

## Estrutura do Projeto

### Documentação (desta feature)

```text
specs/015-visao-geral-kpis-navegaveis/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── containers-sort.md
├── checklists/requirements.md
└── tasks.md
```

### Código-fonte (impactado)

```text
backend/libs/Dokpod.ControlPlane.Application/
├── Inventory/ContainerSearchService.cs        # ordenação por consumo e cursor por valor
├── Inventory/ContainerResourceSort.cs         # novo: regra de ordenação (itens sem amostra no fim)
└── Metrics/
    ├── LatestMetricsCache.cs                  # novo método: containers com amostra recente por host
    └── MetricsQueryService.cs                 # agregado atual pelos containers
backend/apps/Dokpod.ControlPlane.Api/
└── Environments/ConsoleEndpoints.cs           # aceita sort=cpu|-cpu|memory|-memory
contracts/openapi/dokpod-control-plane.v1.yaml # enum de sort ampliado
frontend/web/src/app/
├── core/api/generated/                        # cliente regenerado
├── features/overview/overview.page.{ts,html,css}
└── features/container-list/containers.page.ts # colunas CPU e Memória ordenáveis
frontend/tests/e2e/mock/ e mock_ui/            # cenários 015
```

**Decisão de estrutura**: mudança contida em módulos existentes; sem projeto, tabela ou mensagem gRPC novos.

## Rastreabilidade

| Requisito | Desenho |
| --- | --- |
| FR-001 a FR-003, FR-009, FR-010 | Links dos cards de situação com `status` e estágio preservado; "Parados" conta e filtra `stopped` + `paused` (D-001). |
| FR-004 a FR-006 | `sort=-cpu`/`-memory` com `status=running`; ordenação em memória sobre o conjunto filtrado com cursor por valor (D-002, D-003). |
| FR-007, FR-008 | Componente de link de card único para os seis cards, com nome acessível composto (D-004). |
| FR-011 a FR-014 | Agregado atual pela soma dos containers por host, ponderado por núcleos (D-005); série do host mantida com origem no subtítulo. |
| FR-015 | Hosts filtrados pela autorização antes da busca e da agregação (comportamento atual preservado). |

## Rastreio de Complexidade

Nenhuma violação a justificar.
