# Gestão do projeto no GitHub

Este documento define como usar GitHub Issues e GitHub Projects para gerenciar o Dokpod.

## Limites da configuração

Os formulários em `.github/ISSUE_TEMPLATE/` são versionados neste repositório. O Project, seus campos e visualizações são recursos da conta ou organização do GitHub e precisam ser criados na interface ou pela API. Este documento é a configuração de referência para evitar divergência.

O Project organiza intake e acompanhamento, não autoriza execução. O Spec Kit
mantém requisitos, desenho, dependências e tarefas em `specs/<feature>/spec.md`,
`plan.md` e `tasks.md`. Os [seis conjuntos migrados](../specs/README.md) são
rascunhos aguardando revisão humana. `docs/plan/` está congelado como histórico;
seus status e evidências não aprovam nem concluem tarefas novas.

README, arquitetura e segurança definem escopo e restrições; ADRs mantêm decisões
arquiteturais. Uma tarefa Spec Kit não aceita um ADR nem modifica o veredito da
[avaliação de release](release-readiness.md), que permanece NO-GO.

## Tipos de demanda

| Tipo | Formulário | Label inicial |
| --- | --- | --- |
| Bug | `bug.yml` | `type:bug` |
| Feature | `feature.yml` | `type:feature` |
| Tarefa | `technical-task.yml` | `type:task` |
| Tarefa Spec Kit | `spec-kit-task.yml` | `type:task` |
| Spike | `architecture-spike.yml` | `type:spike` |

Vulnerabilidades não devem ser registradas nesses formulários. Use o fluxo de segurança do repositório.

Os formulários são portas de entrada. `spec-kit-task.yml` associa trabalho à spec
e tarefa autorizadas, sem reativar planos congelados. Bugs seguem o formulário
próprio e assessment da extensão oficial. Formulários, agentes, prompts e instruções
locais foram migrados nesta entrega; recursos e automações remotos não foram
alterados nem certificados.

## Project recomendado

Crie um Project de organização chamado `Dokpod Delivery` e associe o repositório `lzocateli/Dokpod`.

| Campo | Tipo | Valores sugeridos |
| --- | --- | --- |
| Status | Single select | Inbox, Triage, Ready, In progress, Blocked, In review, Validation, Done, Cancelled |
| Type | Single select | Bug, Feature, Task, Spec Kit task, Spike |
| Priority | Single select | P0, P1, P2, P3 |
| Area | Iteration ou texto | Architecture, Backend, Frontend, Agent, Engine, Contracts, Database, Security, DevOps, Documentation, Tests |
| Risk | Single select | Low, Medium, High |
| Spec | Text | caminho de `specs/<feature>/spec.md` |
| Task | Text | caminho de `specs/<feature>/tasks.md` e ID local `T001` |
| Bug assessment | Text | caminho de `.specify/bugs/<slug>/assessment.md` |
| Decision | Text | ADR ou referência à decisão humana; não checkbox de execução |
| Legacy source | Text | plano/etapa congelados, somente proveniência |
| Iteration | Iteration | sprint ou ciclo de trabalho |
| Target | Single select | MVP, Fase 3, Fase 5, Post-MVP, Unplanned |
| Human approval | Single select | Pending, Approved, Not required |
| Blocked by | Text | Issue, ADR, decisão ou dependência externa |

## Visualizações

Crie views de Backlog, Kanban, Sprint atual, Roadmap, Risco e bloqueios e Entrega
Spec Kit. Filtre itens concluídos/cancelados, agrupe por `Status` e mantenha
`Spec`, `Task`, `Decision`, `Legacy source`, `Risk`, `Priority` e `Blocked by`
visíveis. Se `Plan`/`Slice` já existirem, preserve a proveniência e mapeie os
valores para os novos campos; não apagar histórico nem presumir alteração remota.

## Fluxo operacional

1. A demanda entra pelo Issue Form apropriado e no Project com `Status = Inbox`.
2. A triagem confirma Type, Priority, Area, Risk, responsável e dependências; associa a spec/tarefa ou assessment de bug, ou identifica decisão pendente.
3. Demandas incompletas ou baseadas em rascunho sem revisão ficam em `Triage`/`Blocked`; só passam a `Ready` com artefatos revisados e autorização humana referenciada.
4. Uma pessoa ou o Copilot executa somente tarefa Spec Kit ou remediação Bug Fixing revisada e explicitamente autorizada, primeiro avaliando diferença/evidência para não duplicar comportamento implementado. `Ready` não substitui autorização.
5. O trabalho fica em `In progress`; impedimentos ficam em `Blocked` com causa explícita.
6. Um Pull Request referencia a Issue, por exemplo `Implements #123`, e move a demanda para `In review`.
7. Após os checks, a demanda vai para `Validation`; a revisão humana confirma os critérios e move para `Done`.
8. O fechamento da Issue não substitui evidência e atualização da tarefa Spec Kit ou relatórios `fix.md`/`test.md` do Bug Fixing; checkbox concluído significa execução validada, não decisão arquitetural ou autorização de release.

## Tarefa e Decisão

- Feature/manutenção usa spec, plan e tarefa identificada; bug usa assessment e remediação revisada do Bug Fixing oficial, seguida de fix e test. Intake sozinho não inicia trabalho.
- Decisão de arquitetura, segurança, persistência, engine ou protocolo continua em ADR quando aplicável. A spec referencia a decisão; aprovação humana não é inferida de `Done` ou `[x]`.
- Spike produz evidência para decisão, não aceitação automática. Se faltar decisão, a tarefa fica bloqueada.
- Cada Issue de execução referencia spec/tarefa ou assessment/remediação de bug, origem legada quando existir, critério de validação e autorização humana.
- Implementação já comprovada permanece evidência histórica; criar apenas avaliação, validação e residual, sem segunda lista de requisitos concorrente.

## Governança

- `Done` no Project não autoriza merge, publicação ou deploy por si só.
- Rascunhos `draft`/`proposed` aguardam revisão; tarefas bloqueadas, canceladas ou substituídas não são executadas. Planos legados permanecem congelados mesmo quando dizem `approved`/`completed`.
- Mudanças de arquitetura, contrato, banco, segurança, engine ou protocolo atualizam os documentos correspondentes.
- Dependências são registradas com links para Issues, ADRs e tarefas Spec Kit; etapas legadas são apenas origem histórica.
- NO-GO, HIGH sem correção/aceite formal e gates obrigatórios NOT RUN não são superados por status no Project. Publicação/deploy exigem autorização própria.
- As customizações locais `.github/`/`.specify/` foram migradas nesta entrega. Campos, views, labels e automações remotos exigem solicitação própria; o checklist abaixo permanece pendente.
- Não registre secrets, certificados, dados de infraestrutura ou logs integrais nas Issues.

## Checklist

- [ ] Criar `Dokpod Delivery` e associar `lzocateli/Dokpod`.
- [ ] Criar os campos personalizados e as seis views.
- [ ] Configurar automação para novas Issues e PRs.
- [ ] Padronizar labels `type:*` e `status:triage`.
- [ ] Definir responsáveis pela triagem e revisão humana.
- [ ] Criar uma Issue de teste por tipo e confirmar filtros e views.
