# Tarefas: Qualificação de Release Docker Linux

**Entrada**: [spec.md](spec.md) e [plan.md](plan.md).

**Estado**: `in-progress` - execução iniciada em 2026-10-17 (decisões na [spec](spec.md#decisões-de-execução-2026-10-17-solicitante)).

**Pré-requisitos**: Revisão humana, autorização específica por gate, build identificado e ambiente isolado.

**Testes**: Obrigatórios; resultados históricos não são reexecuções desta branch.

**Organização**: IDs locais e jornadas `[US1]` a `[US3]`; T010+ detalham a execução de 2026-10-17.

## Fase 1: Inventário de Gates

- [X] T001 Submeter os três artefatos deste diretório à revisão humana e definir escopo autorizado, sem tratar a migração como autorização de CI, carga, assinatura ou publicação. *(Spec aprovada e execução autorizada pelo solicitante em 2026-10-17; escopo por gate nas [decisões de execução](spec.md#decisões-de-execução-2026-10-17-solicitante).)*
- [X] T002 Confrontar [MVP](../../docs/plan/mvp.md), [desbloqueio](../../docs/plan/desbloqueio-release.md), [release-readiness](../../docs/release-readiness.md) e evidências de 001-004; registrar commit/digest/data/cenário, divergências e gaps em [plan.md](plan.md), sem editar veredito. *(Confronto em [plan.md](plan.md#rastreabilidade-da-migração) e na [matriz de gates](plan.md#matriz-de-gates-t018--2026-10-24), que cita commit, run e cenário de cada evidência.)*

## Fase 2: CI e Segurança (US1)

**Teste independente**: Rastrear candidata por execução/artifacts e testar rejeição de origem inválida.

- [X] T003 [US1] Após autorização externa específica, verificar execução remota do CI existente e artifacts de backend/migrations/PostgreSQL sem skips, frontend, quatro imagens, SBOM/scan e inspeções de usuário/portas/mounts/redes; registrar URL/ID/commit em [plan.md](plan.md), referenciando `deploy/` e `backend/tests/` sem criar workflow concorrente. *(Gate 1: run `40123301221`, 681 testes de backend sem skips, SBOM e Trivy das quatro imagens; detalhado em T010.)*
- [X] T004 [US1] Avaliar os 34 HIGH históricos e scans atuais das quatro imagens em `artifacts/`; registrar correções/re-scan ou classificação/aceite humano com responsável, impacto e expiração em [plan.md](plan.md). Adicionar somente residual demonstrado para autorização; não liberar HIGH por ausência de CRITICAL. *(Gate 2: correções e aceites por CVE em T010 e T011, com expiração em 2027-01-15.)*
- [X] T007 [US1] Confrontar threat model, licenças e artefatos identificados de `deploy/` com [segurança](../../docs/seguranca.md); verificar assinatura/provenance/emissor/digest/revogação em ambiente limpo, incluindo testes negativos. Obter autorização separada se geração/assinatura for necessária; não publicar. *Concluída (2026-10-25)*: threat model consolidado em [threat-model-consolidado.md](threat-model-consolidado.md) e aprovado (gate 4 PASS); licenças PASS (gate 12); assinatura PASS no gate 3 (2026-10-25).

## Fase 3: Carga e Recuperação (US2)

**Teste independente**: Dois perfis aprovados previamente com backlog recuperado.

- [X] T005 [US2] Avaliar harness existente em `tools/scripts/` e `deploy/`, aprovar baseline/thresholds humanos e validar nominal 56/1.120/30 por >=30min e margem >=100/2.000/50 em VM isolada; registrar CPU/memória/rede/PostgreSQL/conexões/filas/idade/p95/p99/erro/convergência e resultados sanitizados em [plan.md](plan.md). *(Concluída em 2026-10-28 com a repetição em VM isolada: ver T015 e a [repetição em VM](plan.md#repetição-em-vm-isolada--2026-10-28-gates-5-6-7-9-10-e-11).)*
- [X] T006 [US2] Validar snapshots/deltas/leituras, replay, resposta perdida, reconnect storm, inventário degradado, comandos concorrentes por ambiente e restart/recuperação do control plane, usando `backend/tests/`, `frontend/tests/` e garantias do [003](../003-inventario-lifecycle-reconciliacao/tasks.md); comprovar ausência de perda/efeito duplicado/acesso indevido e retorno do backlog ao baseline. *(Concluída em 2026-10-28 com a repetição em VM isolada: ver T016. Inventário degradado e comandos concorrentes no mesmo container seguem como residual aceito em 2026-10-24.)*

## Fase 4: Operação e Decisão (US3)

**Teste independente**: Matriz obrigatória sem NOT RUN nem exceção vencida.

- [X] T008 [US3] Confrontar evidências de N/N-1 do [003](../003-inventario-lifecycle-reconciliacao/tasks.md), backup/restore/auditoria, atualização/rollback, observabilidade/alertas, runbooks em `docs/runbooks/`, licença/avisos/código correspondente e CE sem chave/licenciamento externo; registrar matriz Docker Linux e decisões de adiamento do [006](../006-qualificacao-windows-podman/tasks.md) em [plan.md](plan.md). *Decisões de 2026-10-24 registradas nos gates 15 e 16: Podman Windows adiado; Docker Windows (Docker Desktop) e Podman Linux aprovados por teste manual do solicitante.*
- [x] T009 [US3] Registrar gaps específicos restantes em [tasks.md](tasks.md), obter revisão para concluir/revalidar somente residual e submeter matriz a decisão humana GO/NO-GO referenciada em [plan.md](plan.md); obrigatório NOT RUN ou HIGH/CRITICAL sem tratamento mantém NO-GO. Não alterar `docs/release-readiness.md`, publicar, fazer deploy, commit ou push sob esta migração. *(Mesmo escopo do T018: repetição em VM feita em 2026-10-28 (rc.2 e rc.3); decisão do solicitante em 2026-10-28: **GO** na `v0.1.0-rc.3`.)*

## Dependências e Ordem de Execução

T001 -> T002 -> T003 -> T004; T005 exige baseline e laboratório de 004, T006 usa 003 e T005; T007 usa artifacts finais de T004; T008 agrega T003-T007 e decisões de 006; T009 depende de todos. A posição numérica de T007 não implica execução antes da carga: os gates podem ser agendados somente por autorização explícita. Sem evidência, registrar bloqueio, não PASS.

## Execução de 2026-10-17

- [x] T010 [US1] CI: SBOM CycloneDX e Trivy por imagem no job Images, com falha em CRITICAL corrigível e em HIGH/CRITICAL sem aceite vigente. *(13ff9bd, a39c6e5; em 2026-10-24 a política reprovou três HIGH corrigíveis da imagem web, corrigidos com `apt-get upgrade` na base.)*
- [x] T011 [US1] `docs/seguranca-excecoes.md`: aceite por CVE da imagem web (pacote, severidade, impacto no nginx, responsável, expiração) e verificação de expiração no CI. *(13ff9bd; revarredura de 2026-10-24: 93 aceites vigentes.)*
- [X] T012 [US1] CI: assinatura keyless e attestation de proveniência só em tags `v*`; teste local positivo e negativos (digest alterado, chave errada) com registro descartável. *(Concluída: job `release-images` com cosign e verificação de identidade e emissor em 13ff9bd e 6265505. Teste local em 2026-10-24 com `tools/security/exercise-signing-local.sh` (registro descartável em rede interna, sem Rekor): 11/11 casos conferem — assinatura e atestado aceitos pelo digest; chave errada, chave retirada da confiança, atestado de outro tipo, tag movida, imagem sem assinatura e assinatura de outro digest recusados. Keyless em 2026-10-25 na tag `v0.1.0-rc.1` (run 40138370444): as 4 imagens e os atestados aceitos pelo digest com a identidade do workflow e o emissor do GitHub Actions; outra tag, `main`, outro repositório, outro workflow, outro emissor, outro tipo de predicado e assinatura copiada para outro digest recusados. Gate 3 PASS.)*
- [x] T013 [US2] Harness `tools/loadtest/`: simulador de agentes e usuários virtuais; validação em pequena escala. *(2d674af: 3 hosts e 3 usuários, 0 erros, leituras p95 31,9 ms.)*
- [X] T014 [US2] Ambientes e identidades de carga (100 hosts) provisionados e removíveis. *(100 ambientes `Carga NNN`; certificados e identidades por `tools/loadtest/provision-load-hosts.sh`; aposentados por `tools/loadtest/retire-load-hosts.sh` em 2026-10-24: identidades revogadas, ambientes desabilitados e autorização removida do Keycloak. Os registros ficam, porque a auditoria é append-only.)*
- [X] T015 [US2] Carga nominal 56/1.120/30 por 30 min e margem 100/2.000/50, contra os limites aprovados. *(Nominal: PASS no laboratório local em 2026-10-24, com todos os limites atendidos (ver [plan.md](plan.md#evidências-de-carga)); falta repetir em VM isolada. Margem: PASS no laboratório local em 2026-10-24 (p95 59 ms; em 2026-10-17 tinha sido 569 ms, antes de ea0653b). VM isolada em 2026-10-28, rc.2 assinada: nominal PASS (p95 88 ms, comandos p95 350 ms, 867 = 867) e margem PASS (p95 113 ms, comandos p95 375 ms, 1.451 = 1.451, 100 agentes em 5,0 s); ver [plan.md](plan.md#repetição-em-vm-isolada--2026-10-28-gates-5-6-7-9-10-e-11).)*
- [X] T016 [US2] Recuperação: resposta perdida, reconnect storm, comandos concorrentes, restart da API com backlog. *(Executado no laboratório local em 2026-10-24 ([plan.md](plan.md#recuperação--laboratório-local-2026-10-24-t016)). Reconnect storm e restart da API com backlog: PASS depois de três correções. Resposta perdida: sem efeito duplicado; o `Indeterminate` corrigido está coberto por teste. VM isolada em 2026-10-28: restart da API com backlog (parada limpa em 4,8 s, 100 = 100) e resposta perdida em 3 rodadas, sem duplicidade e com `Indeterminate` observado. Inventário degradado e comandos concorrentes no mesmo container seguem como residual aceito em 2026-10-24.)*
- [x] T017 [US3] Operação: backup/restore com auditoria íntegra, atualização e rollback, licença e avisos de terceiros. *(Laboratório local em 2026-10-24: restore com 41/41 itens idênticos à origem, incluindo o SHA-256 da auditoria; rollback N → N-1 e atualização N-1 → N com o agente Podman real; runbook em `docs/runbooks/backup-restore-e-atualizacao.md`. VM isolada em 2026-10-28: restore 40/40 e troca rc.2 ↔ rc.1 funcional, com o item 7 do runbook reprovado pelo achado A2; rollback com migration nova ainda sem exercício, porque não há migration entre as candidatas. Com a `v0.1.0-rc.3`: rc.2 → rc.3 → rc.2 → rc.3 funcional e item 7 com 0 linhas `fail`.)*
- [x] T018 [US3] Matriz de gates e submissão à decisão GO/NO-GO. *(Matriz montada em 2026-10-24 ([plan.md](plan.md#matriz-de-gates-t018--2026-10-24)), com veredito recomendado NO-GO. Atualizada em 2026-10-28 com a repetição em VM e os achados A1 a A4, veredito recomendado NO-GO até a decisão sobre A2 e A3. A2 e A3 corrigidos na `v0.1.0-rc.3` e repetidos na VM (gate 10 com item 7 em 0 linhas; alertas novos), veredito recomendado GO ([plan.md](plan.md#veredito-recomendado-go-atualizado-em-2026-10-28-rc3)). Decisão do solicitante em 2026-10-28: **GO** na `v0.1.0-rc.3` ([plan.md](plan.md#decisão-humana)).)*
