# Tarefas: Pipeline único de CI/CD pelo Altivy.Groundwork

**Entrada**: [spec.md](spec.md) e [plan.md](plan.md).

**Estado**: `in-progress` - histórias 1 a 3 autorizadas em 2026-10-25; história 4 bloqueada pelo GO da spec 005.

## Fase 1: Integração (US1)

- [X] T001 [US1] Aplicar o Groundwork 0.5.0 com `ci`, `dependency-updates` e `repository-policy` e registrar os parâmetros no manifesto. *(2026-10-25: 9 arquivos gerenciados íntegros, `groundwork status` código 0.)*
- [X] T002 [US1] Integrar os jobs locais ao `ci.yml` semente: `Hygiene` e `Gitleaks` gerenciados, `Result` com `toJSON(needs)` falhando fechado, gatilho `development` removido, concorrência sem cancelar PR em andamento. *(actionlint sem achados.)*
- [X] T003 [US1] Remover `gitleaks.yml`, preservando `.gitleaks.toml` e `tools/scripts/test-gitleaks.ps1`, executados pelo `gw-gitleaks.yml`.
- [ ] T004 [US1] Comprovar o `Result` verde no PR desta mudança e conferir que um título fora de Conventional Commits reprova o `Hygiene`.

## Fase 2: Políticas (US2)

- [ ] T005 [US2] `groundwork github --sync --dry-run`, `--sync` e, com T004 comprovado, `--sync --replace-legacy`; registrar o resultado e confirmar `groundwork github` com código 0.
- [ ] T006 [US2] Confirmar que o merge com `Result` vermelho é recusado e que o PR verde do admin é integrado por squash sem aprovação de terceiros.

## Fase 3: Dependências (US3)

- [X] T007 [US3] Semente `dependabot.yml` do catálogo: actions, NuGet, npm e os quatro Dockerfiles, prefixos compatíveis com o release-please e `exclude-paths` dos `gw-*.yml`.
- [ ] T008 [US3] Conferir o primeiro lote de PRs do Dependabot depois do merge: títulos aceitos pelo `Hygiene` e nenhum arquivo gerenciado alterado (SC-004).

## Fase 4: Documentação

- [X] T009 Atualizar CONTRIBUTING, GOVERNANCE, template de PR e SECRET-SCANNING para o `Result` único e o contrato de políticas (FR-010, SC-005).

## Fase 5: Release, publicação e deploy (US4, bloqueada)

- [ ] T010 [US4] Após o GO da 005: componentes `release`, `publish-containers`, `publish-github-assets` e `deploy`; `<Version>` → `<VersionPrefix>`; migrar o `release-images` para `gw-release`/`publish.yml` atualizando a identidade de verificação em `docs/seguranca.md`; Environments `quality` e `production` só com tags `v*`; deploy, redeploy e rollback em `quality` (SC-006).
