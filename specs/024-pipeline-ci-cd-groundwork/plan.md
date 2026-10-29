# Plano de Implementação: Pipeline único de CI/CD pelo Altivy.Groundwork

**Branch**: `feat/024-groundwork-ci` | **Data**: 2026-10-25 | **Spec**: [spec.md](spec.md)

**Estado**: `in-progress` - histórias 1 a 3; a história 4 depende do GO da [spec 005](../005-qualificacao-release-docker-linux/plan.md).

## Resumo

Adotar o Altivy.Groundwork 0.5.0 com os componentes `ci`, `dependency-updates` e `repository-policy`, sem `core` (FR-001): os documentos de contribuição, templates e Spec Kit do Dokpod já existem e seguem a constituição do projeto. Os jobs atuais (Backend, Frontend, Images) continuam locais no `ci.yml` e entram no `Result` junto com os jobs gerenciados de higiene do PR e Gitleaks.

## Contexto Técnico

- **Ferramenta**: Altivy.Groundwork 0.5.0 (tag `v0.5.0` de `altivy-it/altivy-groundwork`). O pacote é privado no GitHub Packages; nesta execução a ferramenta foi compilada da tag no container `lzocateli/dotnet-sdk:10.0.402-noble-v1`, sem alterar o catálogo.
- **Parâmetros** (registrados em `.groundwork/manifest.json`): `github_owner=altivy-it`, `github_repo=dokpod`, `require_signed_commits=true`, `legacy_rulesets=Protecao exclusiva da main`, `release_app_id=5441343`, `publish_snapshots=false`, `dotnet_solution=Dokpod.slnx`, `version_files=backend/Directory.Build.props,frontend/web/package.json` e `dependabot_ecosystems` com `github-actions`, `nuget:/backend`, `npm:/frontend/web` e `docker` nos quatro Dockerfiles.
- **Gerenciados** (não editar; mudanças vão para o catálogo): `gw-pr-hygiene.yml`, `gw-gitleaks.yml`, `gw-dotnet.yml`, `gw-node.yml`, `tools/scripts/ci/Test-ManagedFiles.ps1`, `tools/scripts/ci/Test-PullRequestTitle.ps1`, `.github/CI-CD.md`, `.github/repository-policy.json`, `.github/REPOSITORY-POLICY.md`.
- **Sementes do projeto**: `ci.yml`, `.github/ci-cd.json` e `.github/dependabot.yml`.

## Decisões

| # | Decisão | Motivo |
| --- | --- | --- |
| D1 | Sem o componente `core` | Os documentos e templates do Dokpod já cobrem o conteúdo e têm regras próprias (constituição, Spec Kit pt-BR); evita conflitos `.groundwork-new` sem ganho |
| D2 | Backend, Frontend e Images continuam jobs locais, sem `gw-dotnet.yml`/`gw-node.yml` | O backend precisa do PostgreSQL de serviço e das migrations; o frontend gera e confere o cliente da API; Images roda a política de vulnerabilidades e os SBOMs. Os reutilizáveis ficam disponíveis, sem uso |
| D3 | O gatilho de tags `v*` e o job `release-images` ficam no `ci.yml` até a história 4 (exceção ao FR-004); **encerrada em 2026-10-28**: migrados para `gw-release.yml`/`publish.yml` | A identidade da assinatura keyless é `.github/workflows/ci.yml@refs/tags/<tag>`, verificada por quem instala ([segurança](../../docs/seguranca.md)) e no gate 3 da 005. Mover o job mudaria a identidade antes de o `gw-release` assumir a publicação |
| D4 | O contrato não tem `gw-release-tags` nem Environments de deploy | Vêm com os componentes `release` e `deploy` (história 4). As tags `v*` seguem protegidas contra alteração e deleção pelo ruleset `Protect release tags`, que não é legado |
| D5 | `.github/dependabot.yml` substituído pela semente do catálogo | Prefixos compatíveis com o título de PR e o release-please (`fix`/`build`, `ci(actions)`, `build(deps)`), `exclude-paths` dos `gw-*.yml` e as mesmas pastas de antes |
| D6 | Proteções legadas removidas só depois do `Result` verde no PR desta mudança (FR-008) | A proteção clássica exige o contexto `Full History`, que deixa de existir com o `gw-gitleaks.yml` (o check passa a `Gitleaks / Full History`, agregado no `Result`). A ordem é: PR verde, `groundwork github --sync`, `--sync --replace-legacy`, merge |
| D7 | Environments `ci`, `copilot`, `production` e `quality` mantidos | `ci` guarda os secrets do PostgreSQL do CI em push; `quality`/`production` serão reconciliados pelo componente `deploy` na história 4; `copilot` é do agente de código do GitHub |

## Verificação

- `groundwork status` com código 0 e `Test-ManagedFiles.ps1` verde.
- actionlint sem achados nos workflows.
- PR desta mudança com `Result` verde, incluindo `Hygiene` (título, actionlint, arquivos gerenciados) e `Gitleaks / Full History`.
- `groundwork github` com código 0 depois da reconciliação (SC-002).
