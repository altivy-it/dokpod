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
automaticamente. A instalação e a localização do Spec Kit seguem a seção abaixo.

## Spec Kit e extensões

- Spec Kit e integração Copilot: `1.0.13`, PowerShell, skills habilitadas e numeração sequencial.
- Extensões oficiais `assess`, `bug` e `git`: `1.0.1`, com 23 skills materializadas em `.github/skills/speckit-*/`.
- Configurações, comandos e skills localizadas seguem a base Inventory360Api. Os cinco overrides em `.specify/templates/overrides/` mantêm o procedimento da base com exemplos de stack e diretórios do Dokpod.
- As fontes das extensões ficam em `.specify/extensions/<extensão>/commands/`. Não use o modo de links de desenvolvimento nem mantenha caches `.specify-dev` nesta instalação.

### Hooks e autorização

`.specify/extensions.yml` mantém `auto_execute_hooks: true` e os mesmos 18 hooks
Git habilitados da base. Os hooks obrigatórios inicializam Git antes da
constituição e criam a branch antes da especificação. Os hooks de commit são
opcionais e respeitam `.specify/extensions/git/git-config.yml`, que mantém
`auto_commit.default: false` e as sobrescritas por evento desativadas.

Invocar um fluxo Spec Kit autoriza seus hooks configurados, não push, publicação,
deploy nem implementação fora de tarefas revisadas e explicitamente autorizadas.
Mensagens de commit seguem [as convenções do Dokpod](COMMIT_CONVENTIONS.md).

### Localização pt-BR

Use o [prompt localize-speckit](prompts/localize-speckit.prompt.md) após instalações
ou atualizações. Sem modo explícito, ele somente audita:

```text
/localize-speckit extensão=assess modo=auditar
/localize-speckit extensão=assess modo=traduzir
```

Não há tradução automática por configuração de idioma ou hook de instalação.
Preserve aliases, IDs, placeholders, contratos de leitura, segurança e os
artefatos específicos do Dokpod. Os relatórios de Bug Fixing continuam em
`.specify/bugs/<slug>/`; o README original da extensão foi preservado e a
[tradução complementar](../.specify/extensions/bug/README.pt-BR.md) foi adaptada.

### Manutenção e validação

Na raiz do Dokpod, com Docker disponível, consulte a instalação registrada em
um container de ferramentas. O comando abaixo apenas lista as extensões; não
executa workflows nem hooks. `uv` pode acessar a rede para preparar o CLI:

```powershell
docker run --rm --entrypoint uv --volume "${PWD}:/workspace:ro" --volume "dokpod-speckit-uv-cache:/root/.cache/uv" --workdir /workspace lzocateli/devops:cpu-v1 tool run --from git+https://github.com/github/spec-kit.git@v1.0.13 specify extension list
```

Antes de atualizar, confira a ajuda do CLI `1.0.13`, revise o pacote e o diff e
proteja personalizações locais. Não copie registries nem reescreva hashes para
ocultar alterações. Confira versões, YAML/frontmatter, aliases, links, manifests,
skills e scripts, além da paridade das configurações com a base. Compare hashes
da constituição, specs, relatórios de bugs e templates originais.

As skills centrais localizadas podem diferir dos hashes históricos do manifest
de integração; registre essa diferença sem apagar a evidência. Não execute um
fluxo de aplicação apenas para testar o texto de uma skill.
