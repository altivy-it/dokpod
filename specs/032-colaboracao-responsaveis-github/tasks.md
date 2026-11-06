---

description: "Tarefas de responsáveis e colaboração no GitHub"
---

# Tarefas: Responsáveis e colaboração no GitHub

**Entrada**: `specs/032-colaboracao-responsaveis-github/`

**Autorização**: mantenedor, em 2026-11-06 ("etapa 3, ok para seguir"). Commit e PR dependem de pedido próprio.

## Fase 1: História de Usuário 1 - Revisores corretos por área (P1)

- [X] T001 [US1] Distribuir os donos por área em `.github/CODEOWNERS`
- [X] T002 [US1] Conferir por última correspondência os donos de cada área de primeiro nível
- [X] T003 [US1] Confirmar pela API a permissão `write` ou superior das três contas
- [ ] T004 [US1] Confirmar zero erros em `GET /repos/altivy-it/dokpod/codeowners/errors` na branch publicada

## Fase 2: História de Usuário 2 - Responsabilidades documentadas (P2)

- [X] T005 [US2] Registrar responsáveis e independência de revisão em `.github/GOVERNANCE.md`
- [X] T006 [P] [US2] Apontar para a governança em `.github/CONTRIBUTING.md` e `docs/github-projeto-gestao.md`

## Fase 3: História de Usuário 3 - Referências ao repositório atual (P3)

- [X] T007 [P] [US3] Corrigir os links de `.github/ISSUE_TEMPLATE/config.yml`
- [X] T008 [P] [US3] Corrigir as referências de `docs/github-projeto-gestao.md`
- [X] T009 [US3] Confirmar zero ocorrências de `lzocateli/Dokpod` em arquivos ativos

## Fase 4: Fechamento

- [X] T010 Registrar a feature em `specs/README.md`
- [X] T011 Conferir que o diff não toca arquivos gerenciados pelo Groundwork, código de aplicação ou histórico
