# CI/CD do Dokpod

Arquivo gerenciado pelo Altivy.Groundwork (componente `ci`). Não edite: mudanças no
processo são feitas no catálogo do Groundwork e chegam ao projeto por `groundwork update`.

## Fluxo

1. Todo PR para `main` executa `.github/workflows/ci.yml`, que chama
   `gw-pr-hygiene.yml` (título Conventional Commit, actionlint e arquivos gerenciados),
   `gw-gitleaks.yml` (histórico completo) e os jobs do projeto.
2. O job `Result` do `ci.yml` agrega todos os jobs obrigatórios e é o **único check
   obrigatório** da branch padrão.
3. O merge é sempre squash: o título do PR vira a mensagem do commit e decide se a mudança
   é releasable.

## Workflows

| Workflow | Tipo | Função |
| --- | --- | --- |
| `ci.yml` | semente | CI do projeto e job `Result` |
| `gw-pr-hygiene.yml` | gerenciado | título do PR, impacto na release, actionlint e `Test-ManagedFiles.ps1` |
| `gw-gitleaks.yml` | gerenciado | varredura de secrets no histórico completo |
| `gw-dotnet.yml` | gerenciado | restore, build, test e pack .NET na imagem `dotnet.sdkImage` |
| `gw-node.yml` | gerenciado | install, test e build Node em imagem fixada |

Arquivos **gerenciados** (`gw-*.yml`, `tools/scripts/ci/*`, este documento e os runbooks
padrão) têm SHA-256 registrado em `.groundwork/manifest.json`; `Test-ManagedFiles.ps1`
reprova o PR que os altera. A regra vale para os componentes de CI/CD; arquivos de `core`,
`secret-scanning` e `speckit` admitem personalização, reconciliada pelo `groundwork update`.
Arquivos **semente** são criados uma vez e pertencem ao projeto.

## Contratos das sementes

- `.github/ci-cd.json`: `defaultBranch`, `commitScopes`, `dotnet.sdkImage`, `dotnet.solution`,
  `versionFiles`, `publishSnapshots`, `deploy.environments`, `deploy.autoDeployEnvironment`,
  `attestations` e `rollbackMinimumVersion`. É lido pelos workflows e scripts gerenciados.
- `attestations`: padrão `true`. Use `false` somente quando o plano do GitHub não oferece atestação
  de artefatos (repositório privado fora do Enterprise Cloud). Com `false`, manifesto, imagens e
  assets não são atestados, o manifesto registra `"attestation": "none"` e o guard de deploy recusa
  a release. Campo ausente equivale a `true`; qualquer valor que não seja booleano falha o workflow.
- `ci.yml`: todo job do projeto entra em `needs` do job `Result`; o CI funciona sem secrets,
  pois PRs do Dependabot não os recebem.

## Pré-requisitos
- Environments e rulesets descritos em `.github/repository-policy.json`, aplicados com
  `groundwork github --sync` (ver [REPOSITORY-POLICY.md](REPOSITORY-POLICY.md)).

## Runbooks
- Atualização do pipeline: `groundwork update --dry-run`, `groundwork update` e
  `groundwork github` a cada nova versão do Groundwork.
