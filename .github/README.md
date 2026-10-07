# Engenharia assistida por GitHub Copilot

Este diretório define procedimentos que apoiam o Spec Kit, único processo
autorizado para desenvolver e manter a aplicação Dokpod. Issues são intake;
requisitos, desenho e tarefas pertencem aos artefatos oficiais.

Leia primeiro o [guia do Copilot](COPILOT_GUIDE.md). Para gerenciar trabalho, use os formulários de Issue e o [guia de gestão do GitHub Project](../docs/github-projeto-gestao.md).

## Estrutura

| Diretório/arquivo | Finalidade |
| --- | --- |
| `copilot-instructions.md` | regras globais do Dokpod |
| `instructions/*.instructions.md` | regras carregadas por domínio e caminho |
| `agents/*.agent.md` | especialistas selecionáveis |
| `prompts/*.prompt.md` | tarefas focadas invocáveis por `/` |
| `skills/speckit-*/SKILL.md` | integração oficial Copilot do Spec Kit e Bug Fixing |
| demais `skills/*/SKILL.md` | revisão e qualificação técnica, sem execução concorrente |
| `COPILOT_GUIDE.md` | tutorial de uso |
| `ISSUE_TEMPLATE/` | entrada estruturada para o GitHub Project |
| `COMMIT_CONVENTIONS.md` | formato de commits |
| `PULL_REQUEST_TEMPLATE.md` | evidências mínimas de pull request |
| `SECRET-SCANNING.md` | política de detecção de secrets |
| `CONTRIBUTING.md` | processo de contribuição e Definition of Done |
| `GOVERNANCE.md` | decisões, revisão e releases |
| `SECURITY.md` | reporte de vulnerabilidades |
| `ADR_TEMPLATE.md` | modelo de decisão arquitetural |
| `PLAN_TEMPLATE.md` | modelo obsoleto, somente histórico |
| `../.specify/memory/constitution.md` | princípios duradouros do Dokpod |
| `../.specify/templates/overrides/` | templates pt-BR, resolvidos oficialmente |
| `../specs/<feature>/` | spec, plan e tasks de cada mudança |

## Como escolher

- Use `/speckit-specify`, `/speckit-clarify` quando necessário, `/speckit-plan`, `/speckit-tasks` e `/speckit-analyze` para preparar uma feature.
- Após revisão e autorização humana explícita, use `/speckit-implement`; **Dokpod Delivery Lead** coordena somente tarefas autorizadas. `/speckit-converge` identifica residual, sem aprová-lo.
- Use `/speckit-bug-assess`, `/speckit-bug-fix` e `/speckit-bug-test` para defeitos, com remediação revisada antes da correção.
- Use **Dokpod Engine & Protocol Engineer** para Docker/Podman, gRPC, mTLS, journal e reconciliação.
- Use **Dokpod Angular Engineer** para frontend e UX operacional.
- Use **Dokpod .NET Engineer** para API, BFF, agente, domínio e PostgreSQL.
- Use `/create-adr` apenas para decisão duradoura vinculada ao plan/tarefa; IA mantém `proposed`.
- Use `/review-change` ou `/pull-request-review` para revisão.
- Use `/release-readiness` antes de publicar uma versão.

O GitHub Project organiza demanda, prioridade, risco e bloqueios; `Ready` não
autoriza execução. [Spec Kit](COPILOT_GUIDE.md) define o processo e
[a migração](../specs/README.md) registra seis drafts derivados de cinco planos
congelados em `docs/plan/`. Nenhum draft está aprovado para implementação.
ADRs e evidências históricas não foram aceitos, concluídos ou requalificados
automaticamente. Instalação oficial: Spec Kit 1.0.13 + Bug Fixing 1.0.0, em
container, com saídas pt-BR. Não há commit, push ou publicação automática.
