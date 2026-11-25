# Plano de Implementação: Janelas de mudança por estágio

**Branch**: `026-janelas-de-mudanca` | **Data**: 2026-11-20 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação da funcionalidade em `/specs/026-janelas-de-mudanca/spec.md`

**Origem**: IA assistida; revisor humano: Lincoln (`@lzocateli`).

> Redija este plano e todos os artefatos derivados em português brasileiro (pt-BR). Preserve nomes de código, caminhos, comandos e identificadores técnicos.

## Resumo

A edição Business, com a capability `change-windows`, permite definir por estágio faixas semanais de horário num fuso IANA, em que as ações que mudam o host são aceitas. Fora delas, a API recusa a ação antes de enfileirar o comando (`409 change_window_closed`, com a próxima abertura), salvo com exceção de emergência: o scope novo `change:override` no host e uma justificativa, válida para aquela ação.

A aplicação da janela fica num único ponto. `AgentCommandQueueService.EnqueueAsync` passa a exigir uma liberação emitida pelo `ChangeWindowGuard`, de modo que uma ação nova não compila sem passar pela janela. O terminal, que não usa a fila, chama o guard diretamente. O guard é da CE, que nunca fecha nada; o módulo Business fornece o calendário guardado no PostgreSQL e as rotas de configuração.

Com a licença restrita, as janelas e a exceção continuam valendo; só editar janela é recusado, e remover continua permitido. As telas recebem o estado da janela nas respostas de host e avisam no botão antes do clique.

## Contexto Tecnico

**Linguagem/Versão**: C# / .NET 10 (API, aplicação, infraestrutura); TypeScript / Angular 22 (vista Políticas e botões de ação); PowerShell 7 (scripts do Keycloak).

**Dependências Principais**: nenhum pacote novo. Fuso com `TimeZoneInfo` e o `tzdata` da imagem base ([D-004](research.md#d-004-fuso-e-cálculo-da-janela)).

**Persistência**: PostgreSQL existente, schema `dokpod`, tabela `change_windows` com até três linhas ([data-model.md](data-model.md)). A exceção e as recusas vão para `audit_events`, que já é particionada por mês.

**Testes**:
- unidades em `Dokpod.ControlPlane.Application.Tests`: `ChangeWindowCalendar` com `TimeProvider` falso, incluindo horário de verão, o guard para cada `AgentCommandKind` e a exceção;
- integração com PostgreSQL real em `Dokpod.ControlPlane.Api.Tests`: migration `Up`/`Down`, `If-Match`, gate da licença, exceção na mesma transação do comando;
- `Dokpod.Architecture.Tests`: `ChangeWindowGuardTests` (todo caminho mutável depende do guard) e o `BusinessGateTests` existente;
- E2E Playwright pela borda nginx;
- validação com agentes reais ([quickstart.md](quickstart.md)).

**Plataforma Alvo**: plano de controle em containers Linux atrás do nginx; só a imagem Business registra as rotas de configuração.

**Tipo de Projeto**: plano de controle self-hosted (API, BFF, web).

**Metas de Desempenho**: a decisão soma no máximo 5 ms ao p95 de uma ação na matriz de 100 hosts; sem chamada nova ao Keycloak quando não há exceção ([D-011](research.md#d-011-desempenho)).

**Restrições**:
- a recusa acontece antes de o comando chegar à fila (FR-002, SC-001);
- leitura, logs, inspeção, alertas e auditoria nunca passam pelo guard (FR-003);
- a licença restrita não desliga as janelas (FR-007);
- nenhuma mudança no protocolo do agente;
- campos novos da API opcionais e aditivos.

**Escala/Escopo**: três estágios, até 14 faixas cada; a matriz de 100 hosts do ADR 2026-0003.

## Verificação da Constituição

*GATE: Deve ser aprovado antes da pesquisa da Fase 0 e revisado novamente após o design da Fase 1.*

| Princípio | Como o plano atende | Situação |
| --- | --- | --- |
| I. pt-BR | Artefatos em pt-BR; identificadores preservados | ok |
| II. Engine local e workloads | Não toca agente nem engine; a janela só recusa pedidos novos no plano de controle; comandos já aceitos e workloads seguem iguais | ok |
| III. Identidade externa e menor privilégio | Scope dedicado `change:override` no Keycloak, concedido por ambiente e sem papel padrão; decisão falha fechada; edição exige `environment:manage` em todos os hosts do estágio | ok |
| IV. Operações duráveis | A decisão vale no aceite; a exceção é gravada na mesma transação do comando; edição com `If-Match`; tudo auditado | ok |
| V. Modularidade e compatibilidade | Guard na aplicação, calendário e rotas no módulo Business; sem datastore, broker ou cache; OpenAPI aditivo com `x-dokpod-edition`; migration com `Down`; protocolo do agente inalterado | ok |
| VI. Qualidade por plataforma | PostgreSQL real, E2E pela borda e agentes reais no quickstart | ok |
| Dependências permissivas | Nenhum pacote novo | ok |
| Tabelas de alto crescimento particionadas | `change_windows` tem no máximo três linhas; recusas e exceções vão para a trilha já particionada | ok |
| Threat model para fronteira nova | Um scope novo que contorna um controle de governança: o threat model é a primeira tarefa, antes de código | pendente (primeira tarefa) |
| ADR para decisão duradoura | O guard obrigatório na fila é decisão local desta spec (D-001); sem ADR novo | ok |

Reavaliação após o desenho da Fase 1: sem violações; a tabela de complexidade fica vazia. O threat model segue como entrega obrigatória antes do código.

## Estrutura do Projeto

### Documentação desta Funcionalidade

```text
specs/026-janelas-de-mudanca/
├── spec.md
├── plan.md              # este arquivo
├── research.md          # D-001 a D-011
├── data-model.md
├── quickstart.md
├── contracts/
│   └── api.md           # rotas /api/v1/change-windows, campos novos nas ações e nos hosts
├── checklists/requirements.md
└── tasks.md             # gerado por /speckit-tasks
```

### Código-Fonte (raiz do repositório)

```text
contracts/openapi/dokpod-control-plane.v1.yaml     # rotas Business, changeWindowOverride, changeWindow nos hosts

backend/libs/Dokpod.Domain/ChangeWindows/
└── ChangeWindow.cs                   # faixa, janela, validação

backend/libs/Dokpod.ControlPlane.Application/ChangeWindows/
├── ChangeWindowCalendar.cs           # função pura: estado, closesAt, nextOpensAt
├── ChangeWindowGuard.cs              # decisão e ChangeWindowClearance (CE)
├── ChangeWindowPorts.cs              # IChangeWindowSchedule, NoChangeWindows
└── ChangeWindowSettingsService.cs    # [BusinessCapability("change-windows")] PUT; DELETE sem gate

backend/libs/Dokpod.ControlPlane.Application/Commands/AgentCommandQueueService.cs   # exige a liberação
backend/libs/Dokpod.ControlPlane.Application/{Commands,Containers,Images,Volumes,Networks,Pods,Terminal}/  # pedem a liberação
backend/libs/Dokpod.Domain/Auditing/AuditCategories.cs          # change_window.* em governance
backend/libs/Dokpod.Domain/Environments/EnvironmentRegistration.cs  # change:override em EnvironmentResourceScopes (D-009)

backend/libs/Dokpod.ControlPlane.Infrastructure/ChangeWindows/
└── ChangeWindowStore.cs
backend/libs/Dokpod.ControlPlane.Infrastructure/PostgresAgentCommandStore.cs        # lista de eventos na mesma transação
backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/<data>_ChangeWindows/{Up,Down}/   # tabela e change:override nos ambientes cadastrados

backend/apps/Dokpod.ControlPlane.Api/ChangeWindows/
├── ChangeWindowEndpoints.cs          # rotas, autorização, problem+json
└── ChangeWindowModule.cs             # registrado só pelo ProductEdition da Business
backend/apps/Dokpod.ControlPlane.Api/{Endpoints,Environments,Realtime}/   # 409/403/422 novos; changeWindow nos hosts

frontend/web/src/app/features/settings/
├── change-windows-api.service.ts
└── change-windows-section.component.{ts,html}  # na vista Políticas, só na Business com a capability
frontend/web/src/app/features/change-window/    # aviso nos botões e confirmação da exceção, usados pelas features de ação (lê a API; fica fora do design-system, que é só apresentação)

tools/scripts/configure-keycloak.ps1               # scope change:override
tools/scripts/provision-environment-authorization.ps1   # aceita change:override

backend/tests/
├── Dokpod.ControlPlane.Application.Tests/ChangeWindows/
├── Dokpod.ControlPlane.Api.Tests/ChangeWindows/   # PostgreSQL real
└── Dokpod.Architecture.Tests/ChangeWindowGuardTests.cs

frontend/tests/e2e/   # test_026: aviso, exceção e vista Políticas pela borda nginx

docs/seguranca.md, docs/configuracao-keycloak.md, docs/runbooks/janelas-de-mudanca.md
```

**Decisão de Estrutura**: segue a 027 e a 025. A regra e a porta ficam na aplicação, o PostgreSQL na infraestrutura, e as rotas num módulo que só a Business registra. Diferente das duas, a aplicação da regra é da CE ([D-002](research.md#d-002-composição-entre-ce-e-business)): uma vez configurada, a janela vale independentemente do estado da licença. A CE recebe o calendário vazio, que libera sempre.

## Registro de Complexidade

Sem violações a justificar.

## Itens para a revisão humana

- **Aprovação da spec**: a spec continua `draft`, e as respostas da sessão de 2026-10-27 estão "sujeitas à aprovação". A implementação só começa depois da aprovação explícita do solicitante, incluindo essas respostas.
- **Edição por hosts legíveis**: a spec passou a dizer "todos os hosts do estágio que o usuário enxerga", a mesma regra da política de justificativa (016 D-007). Um host que o administrador não enxerga não impede a edição, mas fica sujeito à janela. Emenda de 2026-11-24 ([D-007a](research.md#d-007a-permissão-de-edição)): é preciso ler ao menos um host do estágio; sem nenhum, o estágio fica só para leitura.

- **Host sem estágio ([D-007a](research.md#d-007a-permissão-de-edição))**: o estágio do host é opcional, e um host sem estágio não fica sujeito a nenhuma janela. A spec não fala desse caso.
- **Catálogo ([D-001](research.md#d-001-ponto-único-de-aplicação))**: "remover imagem" e "criar volume ou rede", citados no esclarecimento de 2026-11-20, ainda não existem no produto. Corrigi o texto do esclarecimento para "quando existirem".
- **Troca de edição ([D-002](research.md#d-002-composição-entre-ce-e-business))**: voltar da imagem Business para a CE desliga as janelas sem aviso. Fica registrado no runbook.
- **Código HTTP**: a recusa por horário usa `409 change_window_closed`, separada do `403` de autorização, para o cliente distinguir "sem permissão" de "fora do horário".
- **Recusas na auditoria**: cada tentativa recusada gera um `change_window.denied`. Um script que repita a ação fora da janela cria um evento por tentativa.
