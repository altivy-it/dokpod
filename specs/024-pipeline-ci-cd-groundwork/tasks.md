# Tarefas: Pipeline único de CI/CD pelo Altivy.Groundwork

**Entrada**: [spec.md](spec.md) e [plan.md](plan.md).

**Estado**: `in-progress` - histórias 1 a 3 autorizadas em 2026-10-25; história 4 bloqueada pelo GO da spec 005.

## Fase 1: Integração (US1)

- [X] T001 [US1] Aplicar o Groundwork 0.5.0 com `ci`, `dependency-updates` e `repository-policy` e registrar os parâmetros no manifesto. *(2026-10-25: 9 arquivos gerenciados íntegros, `groundwork status` código 0.)*
- [X] T002 [US1] Integrar os jobs locais ao `ci.yml` semente: `Hygiene` e `Gitleaks` gerenciados, `Result` com `toJSON(needs)` falhando fechado, gatilho `development` removido, concorrência sem cancelar PR em andamento. *(actionlint sem achados.)*
- [X] T003 [US1] Remover `gitleaks.yml`, preservando `.gitleaks.toml` e `tools/scripts/test-gitleaks.ps1`, executados pelo `gw-gitleaks.yml`.
- [X] T004 [US1] Comprovar o `Result` verde no PR desta mudança e conferir que um título fora de Conventional Commits reprova o `Hygiene`. *(2026-10-25: PR #40, run `40242666302`, com `Result` verde incluindo `Hygiene / Conventional title`, `Hygiene / Actionlint`, `Hygiene / Managed files` e `Gitleaks / Full History`. Caso negativo no PR descartável #44 (run `40243848863`): título "Teste de título inválido" reprovou `Hygiene / Conventional title` e o `Result`; fechado sem merge.)*

## Fase 2: Políticas (US2)

- [ ] T005 [US2] `groundwork github --sync --dry-run`, `--sync` e, com T004 comprovado, `--sync --replace-legacy`; registrar o resultado e confirmar `groundwork github` com código 0. *(2026-10-25: `--sync` aplicado; rulesets `gw-main-integrity` e `gw-main-review`, secret scanning, push protection, `allow_rebase_merge=false` e `allow_update_branch=true` conformes. Falta `--sync --replace-legacy`, executado pelo mantenedor: remove a proteção clássica e o ruleset `Protecao exclusiva da main`.)*
- [x] T006 [US2] Confirmar que o merge com `Result` vermelho é recusado e que o PR verde do admin é integrado por squash sem aprovação de terceiros. *(2026-10-28: caso positivo nos PRs #53 e #54, integrados por squash pelo admin com 0 revisões; no #54 o `Result` ficou verde às 21:36:27Z e o merge entrou às 21:37:55Z, 88 s depois (SC-003), com o commit assinado pelo GitHub. Caso negativo no PR descartável #55 (título fora de Conventional Commits): `Hygiene / Conventional title` e `Result` vermelhos, `mergeStateStatus` `BLOCKED` e `viewerCanMergeAsAdmin` `false` para o admin. A tentativa de `gh pr merge 55 --squash --admin` pelo mantenedor, em 2026-10-28, foi recusada: o PR continuou aberto e a `main` não mudou; #55 fechado sem merge.)*

## Fase 3: Dependências (US3)

- [X] T007 [US3] Semente `dependabot.yml` do catálogo: actions, NuGet, npm e os quatro Dockerfiles, prefixos compatíveis com o release-please e `exclude-paths` dos `gw-*.yml`.
- [x] T008 [US3] Conferir o primeiro lote de PRs do Dependabot depois do merge: títulos aceitos pelo `Hygiene` e nenhum arquivo gerenciado alterado (SC-004). *(2026-10-28: primeiro lote, todos de npm: #45 `fix(deps)` e #48 `build(deps-dev)` integrados e #46 `build(deps-dev)` fechado pelo próprio Dependabot depois de `Result` vermelho por build, substituído pelo #48. Os três títulos passaram em `Hygiene / Conventional title`, e `Hygiene / Managed files` passou em todos. Nenhum dos 9 arquivos do `.groundwork/manifest.json` foi alterado: o #45 e o #46 mudam só `frontend/web/package*.json`, e o #48 também tem o cliente gerado, regerado pelo mantenedor (`f84a40e`). Os ecossistemas NuGet, GitHub Actions e Docker ainda não abriram PR.)*

## Fase 4: Documentação

- [X] T009 Atualizar CONTRIBUTING, GOVERNANCE, template de PR e SECRET-SCANNING para o `Result` único e o contrato de políticas (FR-010, SC-005).

## Fase 5: Release, publicação e deploy (US4, bloqueada)

- [ ] T010 [US4] Após o GO da 005: componentes `release`, `publish-containers`, `publish-github-assets` e `deploy`; `<Version>` → `<VersionPrefix>`; migrar o `release-images` para `gw-release`/`publish.yml` atualizando a identidade de verificação em `docs/seguranca.md`; Environments `quality` e `production` só com tags `v*`; deploy, redeploy e rollback em `quality` (SC-006). *(2026-10-28, GO da 005 na `v0.1.0-rc.3`: componentes `release`, `publish-containers`, `publish-github-assets` e `deploy` aplicados pelo groundwork 0.5.0; `<VersionPrefix>`; `release-images` e o gatilho de tags saíram do `ci.yml`; o `publish.yml` publica as 4 imagens por digest, assina com cosign (identidade `publish.yml@refs/heads/main` ou `@refs/tags/vX.Y.Z`, em `docs/seguranca.md`) e anexa o pacote do agente Windows; `deploy.yml` por SSH com hook do host (`deploy/hooks/compose-deploy-hook.sh`), `quality` = VM da spec 005, hook testado na VM com os digests da rc.3: aplicação, reaplicação sem recriar nada e checagem. Primeira release forçada em 0.1.0 (`release-as`, a remover depois). 2026-10-29: o primeiro `Release` deu `startup_failure` porque o repositório só permitia actions do GitHub e o `gw-release.yml` usa a release-please; o padrão fixado por SHA foi liberado e o groundwork 0.5.1 passou a conferir essa política (`allowedActions`, PR #63). `groundwork github --sync` aplicado (Environment `release`, `quality`/`production` só com `tag:v*` e revisor, ruleset `gw-release-tags`). Release PR #64 → `v0.1.0` publicada com manifesto atestado (`gh attestation verify` OK) e as 4 imagens verificadas com cosign na identidade `publish.yml@refs/heads/main`. Deploy em `quality` (run 40970435980): guard OK, recriou agent-podman, api, bff e web, smoke OK nos 4 (healthy, digest do manifesto, versão 0.1.0). Redeploy (run 40970741054): nada recriado, smoke OK. `release-as` removido. Falta só o rollback real, que precisa de uma segunda release.)*
