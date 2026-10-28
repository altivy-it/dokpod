# Plano de Implementação: Qualificação de Release Docker Linux

**Branch**: `chore/adotar-spec-kit` (migração) | **Data**: 2026-10-06 | **Spec**: [spec.md](spec.md)

**Estado**: `approved` - execução autorizada em 2026-10-17 com as decisões da [spec](spec.md#decisões-de-execução-2026-10-17-solicitante).

**Entrada**: [Desbloqueio](../../docs/plan/desbloqueio-release.md), [MVP](../../docs/plan/mvp.md) e [release-readiness](../../docs/release-readiness.md), preservados.

## Resumo

Consolidar gates existentes em tarefas de qualificação, sem refazer núcleo Docker Linux nem transformar PASS de laboratório em aprovação de produção. O NO-GO e a necessidade de revisão humana permanecem. Publicar/assinar/deploy não são efeitos desta migração; sua eventual execução precisa de autorização específica.

## Contexto Técnico

**Linguagem/Versão**: .NET 10/Angular 22 e ferramentas containerizadas versionadas do projeto.

**Dependências Principais**: CI existente, Trivy/SBOM/Gitleaks, k6, Playwright, Docker, PostgreSQL e Keycloak reais.

**Armazenamento**: Relatórios/artifacts sanitizados e durabilidade existente; sem nova infraestrutura de produto.

**Testes**: Qualificação de segurança, carga/recuperação, N/N-1 e operação; aplicação HTTP apenas via nginx containerizado.

**Plataforma Alvo**: Candidata Docker Linux em VM isolada; outras capabilities via 006 ou adiamento humano formal.

**Tipo de Projeto**: Gate de release, não nova feature de domínio.

**Objetivos de Desempenho**: Nominal 56 agentes/1.120 containers/30 usuários por >=30min; margem >=100/2.000/50, thresholds prévios humanos.

**Restrições**: NO-GO, gate HIGH obrigatório, N/N-1, licença, evidência por digest e nenhuma publicação automática.

**Escopo**: Qualificar CI, vulnerabilidades, desempenho, supply chain, operação e suporte; não mudar veredito por IA.

## Verificação da Constituição

Revisão formal pendente, sem aprovação declarada. [Arquitetura](../../docs/arquitetura.md), [segurança](../../docs/seguranca.md) e [critérios de desempenho](../../.github/PERFORMANCE_TESTING_CRITERIA.md) são fontes restritivas somente consultáveis nesta migração. Critérios não são reduzidos pela frase histórica sobre ausência de CRITICAL corrigível.

## Estrutura do Projeto

### Documentação desta Feature

[spec.md](spec.md), [plan.md](plan.md) e [tasks.md](tasks.md); relatórios futuros com evidência real, não gerados como PASS nesta migração.

### Superfícies Existentes

`backend/tests/`, `frontend/tests/`, `deploy/`, `tools/scripts/` e workflows existentes são referências de qualificação. Não editar `.github/`, `docs/adr/` ou avaliação de release nesta migração.

**Decisão de Estrutura**: 001-004 mantêm seus testes proprietários; este conjunto agrega evidência e executa apenas gates integrados faltantes quando autorizado.

## Rastreabilidade da Migração

| Origem | Destino | Tratamento |
| --- | --- | --- |
| Desbloqueio P-01 | T003 | CI remoto e artifacts; sem refazer workflow existente |
| Desbloqueio P-02 | T004 | Triagem HIGH e scans finais por digest |
| Desbloqueio P-03 | T005/T006 | Nominal >=30min e margem exigida pela arquitetura/release-readiness |
| Desbloqueio P-04 | T007 | Threat model/assinatura/provenance verificadas |
| Desbloqueio P-05 | T008 e 006 T003-T006 | Docker Linux aqui; Windows/Podman em 006 |
| Desbloqueio P-06 | T008/T009 | Operação, N/N-1, licença e decisão humana GO/NO-GO |
| MVP P-04/P-05 | T005/T006 | Só carga/recuperação integradas, implementação em 003 |
| MVP P-06 | T003-T009 | Hardening/release candidata residual |
| P03-07; P05-09 | T005-T009 | Carga/recuperação/gate global, sem duplicar testes locais |

## Lacunas e Estratégia

34 HIGH históricos não são aceitos por falta de CRITICAL; T004 bloqueia o gate. Carga nominal/margem e N/N-1 seguem `NOT RUN`. Backup/restore histórico não certifica rollback/observabilidade. A avaliação menciona horizontal pendente em uma linha e PASS em outra: catalogar cenário/data sem editar sua inconsistência. Revisão -> inventário de evidências -> gates autorizados -> residual -> decisão humana. Adiamento de 006 precisa constar explicitamente na matriz para não confundir requisito por capability com gate universal da candidata Docker Linux.

## Execução (2026-10-17)

1. **Cadeia de fornecimento (T003, T004, T007)**: job Images do CI passa a gerar SBOM CycloneDX e relatório Trivy por imagem, falhando com CRITICAL corrigível ou HIGH/CRITICAL fora da lista de aceites vigente; aceites por CVE em `docs/seguranca-excecoes.md` com expiração verificada; assinatura keyless (cosign) e attestation de proveniência só em tags `v*`, sem execução nesta etapa; testes negativos locais (digest alterado, chave/identidade errada) com registro local descartável.
2. **Harness de carga (T005)**: `tools/loadtest/` com simulador de agentes (.NET, contrato v1, mTLS com certificados emitidos pela CA do laboratório fora do repositório), cada um com inventário sintético, heartbeat, eventos, métricas e resposta a comandos; usuários virtuais lendo as rotas do console pela borda nginx/BFF; coleta de latência, erros, convergência, reconexão e recursos (CPU/memória dos containers).
3. **Recuperação (T006)**: resposta perdida, reconnect storm, comandos concorrentes em ambientes distintos, restart da API com comandos não terminais e verificação de dedup/fencing (sem efeito duplicado).
4. **Operação (T008)**: N/N-1 (evidência da 023), backup/restore com integridade da auditoria, atualização e rollback da stack, licença e avisos de terceiros.
5. **Matriz e decisão (T009)**: matriz de gates com evidência, `NOT RUN` explícitos e destino; decisão GO/NO-GO pelo solicitante.

## Evidências de carga

### Carga nominal — laboratório local, 2026-10-24 (T015)

**Cenário**: 56 agentes simulados (`Dokpod.LoadSim`, protocolo e mTLS reais) × 20 containers = 1.120 containers, com reinícios espontâneos (média de 120 s por host); 30 usuários virtuais pela borda (gateway + BFF, login real no Keycloak) por 31 min (16:11:54Z–16:42:59Z). Stack no commit `2af719f`: API, BFF e web reconstruídas do HEAD; Podman 5.7.0 rootless; estação de trabalho com 12 vCPU e 30 GB compartilhada com Keycloak e PostgreSQL. **Não é VM isolada**: vale como evidência de laboratório. O FR-004 continua pedindo repetição em VM isolada.

| Limite aprovado | Medido | Resultado |
| --- | --- | --- |
| Leituras p95 ≤ 500 ms / p99 ≤ 1,5 s | p95 51,5 ms / p99 63,3 ms (27.363 leituras; máx. 475,9 ms) | PASS |
| Comando aceito → concluído p95 ≤ 3 s | p95 310,1 ms (916 comandos; máx. 568,5 ms) | PASS |
| Convergência do inventário ≤ 30 s | p95 32,6 ms / p99 44,8 ms / máx. 551,8 ms | PASS |
| Erros < 0,1% | 0,004% (1 de 27.363): um comando `stale_target`, porque um reinício espontâneo mudou a revisão antes da execução, e a rejeição esperada pela concorrência otimista | PASS |
| API ≤ 2 vCPU e 1,5 GB | CPU média 0,17 vCPU (máx. 0,19); memória máx. 345 MiB | PASS |
| Sem perda nem efeito duplicado | 915 comandos `succeeded` = 915 execuções registradas no engine simulado (`LOADSIM_EXEC_LOG`) | PASS |
| Reconexão de todos os agentes ≤ 60 s | fora do perfil nominal; medir na margem e na recuperação (T016) | NOT RUN |

Demais componentes: BFF 0,04 vCPU / 108 MiB; Keycloak 0,41 vCPU / 1.024 MiB; PostgreSQL 0,14 vCPU / 957 MiB; web 13 MiB. Os 56 agentes ficaram conectados durante toda a rodada.

Reprodução: `tools/loadtest/README.md`, com `provision-load-hosts.sh 100 <pasta>` e os 56 primeiros hosts. Os resultados brutos (`nominal.json` e `nominal-recursos.csv`) ficam na pasta de carga, fora do repositório.

### Carga de margem — laboratório local, 2026-10-24 (T015)

**Cenário**: 100 agentes simulados × 20 containers = 2.000 containers; 50 usuários virtuais por 30 min (16:50:30Z–17:20:35Z). A stack, o host e a ressalva de VM isolada são os mesmos da nominal.

| Limite aprovado | Medido | Resultado |
| --- | --- | --- |
| Leituras p95 ≤ 500 ms / p99 ≤ 1,5 s | p95 59,0 ms / p99 82,9 ms (44.024 leituras; máx. 601 ms). Em 2026-10-17 o p95 foi 569 ms, antes de ea0653b | PASS |
| Comando aceito → concluído p95 ≤ 3 s | p95 323,9 ms (1.466 comandos) | PASS |
| Convergência do inventário ≤ 30 s | p95 46,9 ms / p99 59,1 ms | PASS |
| Erros < 0,1% | 0,007% (3 comandos `stale_target` por reinício espontâneo concorrente) | PASS |
| API ≤ 2 vCPU e 1,5 GB | CPU média 0,20 vCPU (máx. 0,23); memória máx. 381 MiB | PASS |
| Sem perda nem efeito duplicado | 1.463 `succeeded` = 1.463 execuções no engine | PASS |
| Reconexão de todos os agentes ≤ 60 s | partida simultânea dos 100 agentes: todos conectados em 5,7 s | PASS |

### Recuperação — laboratório local, 2026-10-24 (T016)

Cada cenário envia um restart por host de carga, os 100 em paralelo (`recovery.py`), e confere o log de execuções do engine.

**Restart da API com backlog**, primeira execução (`2af719f`): **reprovado**. Três defeitos:

1. A API não encerrava os streams dos agentes no SIGTERM e só saía por SIGKILL após 20 s. Correção: as sessões terminam em `ApplicationStopping` com `Unavailable`, e o agente reconecta à nova instância.
2. O pool padrão do Npgsql (100) esgotou o `max_connections = 100` do PostgreSQL compartilhado (`53300 too many clients`) com 100 reconexões simultâneas: 32 de 100 comandos foram recusados no envio (500/503). Correção no laboratório: `Maximum Pool Size=40` no `manage-e2e-stack.ps1`, com orientação de dimensionamento em `deploy/e2e/README.md`.
3. O registro de início de sessão que falhou nesse momento foi engolido, e 9 hosts ficaram **desconectados no console com o agente ativo** até a próxima reconexão. Correção: o registro falho é refeito a cada heartbeat até gravar, e o contexto descarta as entidades pendentes da gravação que falhou.

Mesmo assim, nenhum comando foi perdido ou duplicado: 68 aceitos, 68 `succeeded` e 68 execuções.

**Restart da API com backlog**, depois das correções: **PASS**. 100/100 aceitos; parada e partida em 3,6 s, sem SIGKILL; as 100 sessões foram restabelecidas em até 4,7 s após a partida; 99 `succeeded` = 99 execuções, 1 `stale_target` por reinício espontâneo, nenhuma duplicada; 0 `too many clients`; 100 hosts conectados no console.

**Resposta perdida** (SIGKILL no simulador logo após o envio, com nova subida usando o mesmo journal), três execuções. Nenhuma teve efeito duplicado:

- **Primeira**: 97 executados antes do kill; 96 `succeeded`; reconexão em 5,2 s. Um comando foi executado 66 ms antes do kill, sem resultado gravado, e na reentrega foi reportado como `failed/stale_target`. Isso viola a spec 003 SC-003, porque o efeito ocorreu. Correção no agente: um comando reentregue (`Duplicate`) sem resultado no journal, com alvo mudado ou removido, passa a `Indeterminate` (`engine_result_unknown`). Coberto por `AgentCommandProcessorTests`.
- **Segunda e terceira** (agente corrigido): 100/100 `succeeded` = 100 execuções. 88 e 100 comandos executados antes do kill; os restantes foram retomados pelo journal. Nenhuma das duas acertou a janela entre a execução e a gravação do resultado, então o novo `Indeterminate` está comprovado por teste, não no laboratório.

Ainda não executados: inventário degradado e comandos concorrentes no mesmo container.

## Evidências de operação

### Backup, restore, atualização e rollback — laboratório local, 2026-10-24 (T017)

Procedimento e comandos em [backup, restore e atualização](../../docs/runbooks/backup-restore-e-atualizacao.md).

| Verificação | Resultado |
| --- | --- |
| Backup completo (`pg_dump -Fc`, 4,5 GB → 325 MB), com API e BFF parados por 31 s | PASS (27 s) |
| Restore numa database temporária (`pg_restore --exit-on-error`) | PASS (80 s) |
| Impressão digital origem × cópia: 31 tabelas e 125.193 linhas do schema `dokpod`; 27.200 eventos de auditoria com o mesmo SHA-256; 133 partições; 27 migrations; trigger append-only; grants de `dokpod_runtime`; 2 realms e 4 usuários do Keycloak | PASS (41/41 itens iguais) |
| `UPDATE` e `DELETE` na auditoria da cópia | PASS (bloqueados pelo trigger) |
| API sobre a cópia: `/health/ready`, `/ready/database` e `/ready/keycloak` | PASS (200) |
| Rollback N (`dd9a645`) → N-1 (`cd674fc`) com o agente Podman real em N | PASS: stack em 33 s, agente reconectado em 36 s, leituras sem erro, auditoria idêntica, ambientes e identidade preservados |
| Atualização N-1 → N | PASS: mesmos tempos e verificações |
| Licença e avisos de terceiros nas imagens e no console | PASS (1de46ef) |

Ressalvas: não há migration entre N-1 e N, então o rollback com migration nova não foi exercitado (o runbook manda restaurar o backup nesse caso). A stack tem uma instância só, então o console fica indisponível cerca de 33 s em cada troca. A cópia de exercício, o dump e os arquivos com credenciais foram removidos.

## Matriz de gates (T018) — 2026-10-24

Candidata: **Docker Linux**, commit `33684ff` (branch `005-qualificacao-release`, PR #30), CI remoto `40123301221` verde. Esta matriz não altera `docs/release-readiness.md` (FR-001) e não autoriza publicação nem deploy (FR-009).

**Legenda**: **PASS** = comprovado; **PASS (decisão)** / **(com aceite)** = aceito pelo solicitante, com o residual registrado na linha; **PASS (lab)** = comprovado no laboratório local (estação compartilhada, não VM isolada); **PARCIAL** = parte comprovada, com o restante indicado; **NOT RUN** = sem evidência; **ADIAMENTO PROPOSTO** = depende de decisão humana. **PASS (VM)** = comprovado na VM isolada de 2026-10-28, com as imagens assinadas da `v0.1.0-rc.2`.

| # | Gate | Origem | Estado | Evidência | Pendência e destino |
| --- | --- | --- | --- | --- | --- |
| 1 | CI remoto com PostgreSQL real e imagens | P-01, FR-002 | PASS (decisão) | Run `40123301221` (`33684ff`): Backend, Frontend e Images verdes; 681 testes backend, sem skips (inclui os de PostgreSQL); SBOM e Trivy das quatro imagens. As imagens partem sempre das bases do repositório `containers` (`lzocateli/dotnet-aspnet`, `lzocateli/nginx`, `lzocateli/angular-cli`), onde usuário, portas e hardening são mantidos | Aceito pelo solicitante em 2026-10-24: a inspeção de usuário, portas, mounts e redes fica coberta pelo repositório `containers` |
| 2 | Vulnerabilidades HIGH/CRITICAL | P-02, FR-003 | PASS | A política do CI reprova HIGH/CRITICAL corrigível ou sem aceite. Web: 0 corrigíveis após `ac985ea`, 93 aceites por CVE vigentes até 2027-01-15. API, BFF e agente sem HIGH/CRITICAL | Renovar ou revisar os aceites antes de 2027-01-15 |
| 3 | Assinatura e provenance | P-04, FR-006 | PASS | Job `release-images` só em tag `v*`: cosign keyless, attestation e verificação de identidade e emissor (`13ff9bd`, `6265505`); tag `v0.1.0-rc.1` (`e528b71`, run 40138370444) | Exercício autorizado em 2026-10-24. Local: 11/11 casos com `tools/security/exercise-signing-local.sh` (chave errada, chave retirada da confiança, atestado de outro tipo, tag movida, imagem sem assinatura e assinatura de outro digest recusados). Keyless, verificado fora do workflow em 2026-10-25 (cosign 3.1.3 e `gh attestation verify`): as 4 imagens do GHCR aceitas pelo digest com a identidade `…/ci.yml@refs/tags/v0.1.0-rc.1` e o emissor do GitHub Actions, e os atestados de proveniência aceitos com repositório, ref e workflow do signatário. Recusados: identidade de outra tag, da branch `main`, de outro repositório e de outro workflow; outro emissor; outro tipo de predicado; e a assinatura da API copiada para o digest do agente num registro local descartável (`provided artifact digest does not match any digest in statement`) |
| 4 | Threat model revisado | P-04, FR-006 | PASS (decisão) | [threat-model-consolidado.md](threat-model-consolidado.md): 18 fronteiras, telemetria OTLP e backup modelados. Achado A1 corrigido (o agente não validava o nome do servidor com CA própria). L1 corrigida (bundle de raízes e [runbook de troca da CA](../../docs/runbooks/troca-da-ca-dos-agentes.md)), L5 corrigida (`retire-load-hosts.sh`), L4 confirmada | Aprovado pelo solicitante em 2026-10-24, com R1–R10 aceitos. Revisar de novo a cada fronteira nova |
| 5 | Carga nominal 56/1.120/30 por ≥ 30 min | P-03, FR-004 | PASS (VM) | Todos os limites atendidos: leituras com p95 de 51,5 ms, comandos com p95 de 310 ms, 0,004% de erros, API com 0,17 vCPU | VM 2026-10-28: todos os limites atendidos (ver repetição em VM) |
| 6 | Carga de margem 100/2.000/50 | P-03, FR-004 | PASS (VM) | Leituras com p95 de 59 ms, comandos com p95 de 324 ms, 0,007% de erros, reconexão em 5,7 s | VM 2026-10-28: todos os limites atendidos; 100 agentes em 5,0 s |
| 7 | Recuperação | FR-005 | PASS (VM, com aceite) | Reconnect storm (100 agentes em 5,7 s), restart da API com backlog (após `b4be2b7`, `a86058c` e `4ff3623`) e resposta perdida sem efeito duplicado (`Indeterminate` em `745b2fc`, coberto por teste) | VM 2026-10-28: restart da API e resposta perdida sem duplicidade; `Indeterminate` observado. Residual aceito em 2026-10-24 (inventário degradado e comandos concorrentes no mesmo container) segue sem execução |
| 8 | Compatibilidade N/N-1 | FR-007 | PASS | Spec 023 T003: agente N-1 ↔ servidor N por 10 min, sem erros; o sentido inverso falhou e foi corrigido para versões futuras. Em 2026-10-24, o agente N (`dd9a645`) operou com o servidor N-1 (`cd674fc`) | VM 2026-10-28: agentes N (rc.2) operaram com servidor N-1 (rc.1) no rollback; repetir com as versões publicadas |
| 9 | Backup e restore com auditoria íntegra | FR-007 | PASS (VM) | 41/41 itens idênticos à origem, incluindo o SHA-256 dos 27.200 eventos; trigger append-only ativo e API pronta sobre a cópia | VM 2026-10-28: 40/40 itens iguais, append-only e API sobre a cópia |
| 10 | Atualização e rollback | FR-007 | PARCIAL (VM) | N → N-1 e N-1 → N com o agente Podman real: 33 s de indisponibilidade, auditoria, ambientes e identidades preservados; runbook `docs/runbooks/backup-restore-e-atualizacao.md` | Troca funcional nos dois sentidos; item 7 do runbook (sem `fail` no log) reprovado pelo achado A2; rollback com migration nova não exercitado |
| 11 | Observabilidade de release | FR-007 | PASS (VM, com achado) | OpenTelemetry (OTLP) na API e no BFF, com métricas dos ADRs 0001, 0002 e 0004, traces BFF → API → Keycloak → PostgreSQL e 13 regras de alerta testadas com `promtool`. Três falhas reais dispararam os alertas certos. Runbook `docs/runbooks/observabilidade.md` | Três falhas reais com alerta; achado A3 (alerta de `indeterminate` não disparou) |
| 12 | Licença, avisos e código-fonte | FR-008 | PASS | `1de46ef`: LICENSE e avisos de terceiros nas quatro imagens, rótulo OCI `AGPL-3.0-only`, oferta do código-fonte no console. Nenhuma chave, serviço de licenciamento ou telemetria comercial no código | — |
| 13 | Autenticação, autorização e revogação | readiness | PASS | Autorização horizontal e revogação PASS (`docs/release-readiness.md`); permissões do Keycloak por scope (spec 023) | — |
| 14 | E2E com dependências indisponíveis | readiness | PASS (com aceite) | Jornada autenticada, inventário, lifecycle e revogação PASS | Residual aceito pelo solicitante em 2026-10-24: sessão expirada e indisponibilidade do Keycloak e do PostgreSQL com recuperação da UI ficam sem E2E dedicado |
| 15 | Windows (Service real, ACL, atualização e rollback) | P-05, 006 | PASS (decisão); Podman Windows ADIADO | Docker no Windows com Docker Desktop testado manualmente pelo solicitante em 2026-10-24, que declarou o funcionamento. Script do Windows Service e DryRun no repositório | Decisão do solicitante em 2026-10-24: Docker Windows (Docker Desktop) aprovado; Podman Windows **adiado**. O teste manual não tem evidência registrada aqui, e Service real, ACL, atualização e rollback do agente Windows seguem sem prova no repositório (spec 006) |
| 16 | Podman Linux rootless/rootful | P-05, 006 | PASS (decisão) | Teste manual do solicitante (2026-10-24), que declarou o funcionamento. Laboratório: stack completa com agente Podman rootless (specs 008 e 023) e rollback N ↔ N-1 com o agente Podman real (gate 10) | Decisão do solicitante em 2026-10-24: Podman Linux aprovado. O modo rootful não tem evidência registrada no repositório |

### Atualização de 2026-10-25

O PR #30 entrou na `main` em andamento (`966a4ae`). Depois dele, a `main` recebeu mudanças que afetam a candidata, todas com CI e Gitleaks verdes (`e5b7eba`):

| PR | Mudança | Efeito na matriz |
| --- | --- | --- |
| #32 | Alertas do Dependabot do frontend (`vite` 8.3.5, `vitest` 4.1.12, `undici` 7.30.1) e imagem de build `lzocateli/angular-cli:22.2.4-node24.22.1-bookworm-v1`, sobre a base `lzocateli/node:24.22.1-bookworm-v1` (containers #142 e #143) | Gates 1 e 2: imagens reconstruídas sobre as bases novas, Trivy sem HIGH/CRITICAL corrigível; 0 alertas abertos no Dependabot |
| #36 | Tema de login do Keycloak no design system do console e realm em pt-BR | Gate 13: login validado com axe (WCAG 2.2 AA) no laboratório |
| #37 | **Defeito**: o Sair do console não navegava (logout por XHR seguia os redirecionamentos dentro da chamada). Corrigido com POST de formulário e retorno a `/dokpod/` | Gate 14: o E2E não cobria o Sair pela interface; validado no laboratório com usuário temporário. Fica como lacuna de E2E |
| #38 | Símbolo D na cor da marca no console | Sem efeito em gate |

Consequência para a repetição em VM: a candidata passa a ser a `main` atual, não `33684ff`. A assinatura do gate 3 vale para os digests da `v0.1.0-rc.1` (`e528b71`); a VM deve usar uma tag nova (`v0.1.0-rc.2` ou posterior), para que as imagens testadas sejam as mesmas assinadas pelo job `release-images`.

### Repetição em VM isolada — 2026-10-28 (gates 5, 6, 7, 9, 10 e 11)

**Ambiente**: VPS dedicado (KVM, 8 vCPU, 32 GB, 400 GB, Ubuntu 26.04), sem outra carga; Podman 6.1.3 rootless, usuário sem sudo; todas as portas da stack só em `127.0.0.1`. Plataforma de identidade (PostgreSQL 18.4, Keycloak 26.7.0 e gateway), PKI de laboratório, segredos e realm `dokpod` gerados na própria VM; nada copiado do laboratório local.

**Candidata**: tag `v0.1.0-rc.2` (`24592a5`), pipeline `40623891639` verde. As quatro imagens da rc.2 e as quatro da rc.1 (N-1) tiveram a assinatura verificada por digest com `cosign verify` (identidade `ci.yml@refs/tags/<tag>`, emissor do GitHub Actions) e foram transferidas para a VM com o mesmo ID de imagem (8/8). Migrations: 27, com 133 partições em `audit_events` e 133 em `agent_commands`.

| Gate | Resultado na VM | Laboratório (2026-10-24) |
| --- | --- | --- |
| 5 — nominal 56/1.120/30 por 30 min | **PASS**: leituras p95 88 ms / p99 136 ms (26.133); comando p95 350 ms (868); convergência máx. 738 ms; erros 0,004% (1 `stale_target`); API 0,21 vCPU, 334 MiB; 867 `succeeded` = 867 execuções; host 74% ocioso, steal 1,4% | p95 51,5 / 310 ms; 0,004% |
| 6 — margem 100/2.000/50 por 30 min | **PASS**: leituras p95 113 ms / p99 233 ms (43.241); comando p95 375 ms (1.455); convergência máx. 929 ms; erros 0,009% (4 `stale_target`); API 0,26 vCPU, 351 MiB; 1.451 `succeeded` = 1.451 execuções; partida simultânea dos 100 agentes em 5,0 s | p95 59 / 324 ms; 5,7 s |
| 7 — recuperação | **PASS**: restart da API com 100 comandos em voo: parada limpa em 4,8 s (sem SIGKILL), 100 agentes de volta em 5,8 s, 100 `succeeded` = 100 execuções, 0 `too many clients`. Resposta perdida (SIGKILL no simulador), 3 rodadas: 0 execução duplicada; numa rodada, 2 comandos terminaram `indeterminate` (`engine_result_unknown`) sem execução no engine — o caminho de `745b2fc`, antes provado só por teste, agora observado | PASS (lab, com aceite) |
| 9 — backup e restore | **PASS**: API e BFF parados 17,9 s; dump de 48 MB em 5,1 s (banco de 676 MB); `pg_restore --exit-on-error` em 15,3 s; impressão digital 40/40 itens iguais (10.979 eventos de auditoria, mesmo SHA-256, 133 partições, 27 migrations, trigger, grants, realms, usuários e policies do Keycloak, linhas de cada tabela); `UPDATE` e `DELETE` na auditoria da cópia barrados; API temporária sobre a cópia com 200 em `/health/ready`, `/ready/database` e `/ready/keycloak`; cópia, API e dump removidos | 41/41 itens |
| 10 — rollback e atualização | **PASS funcional, item 7 reprovado** (achado A2): rc.2 → rc.1 e rc.1 → rc.2 com os 100 agentes simulados e o agente Podman real em N: 36,1 s e 35,8 s de indisponibilidade, agentes de volta em 7 s, leituras e comandos sem erro, auditoria até a troca idêntica (10.979 e 10.994 eventos), 101 ambientes e identidades preservados. O log da API teve 4 e 17 linhas `fail` (`40001`). Sem migration entre rc.1 e rc.2: o rollback com migration nova continua sem exercício | 33 s; sem `fail` (só 1 agente conectado) |
| 11 — observabilidade | **PASS, com achado A3**: 1.133 séries da API e 820 do BFF, 10/10 métricas-chave; leituras p95 de 126 ms no Prometheus contra 113 ms externos; trace BFF → API → Keycloak → PostgreSQL; 0 dado sensível em 50 traces; `promtool check rules` (13) e `test rules` OK. Falhas reais: Keycloak parado → `DokpodKeycloakIndisponivel` em 146 s (apagou 151 s após a volta); 100 agentes derrubados → `DokpodAgentesDesconectando` em 317 s (25 s); API parada → `DokpodApiSemTelemetria` em 136 s (30 s). `DokpodComandosIndeterminados` **não disparou** para os 2 `indeterminate` do gate 7 | PASS (lab) |

**Preparo da VM** (não afeta o produto, registrado para reprodução): o cadastro por API usa por padrão só `environment:read` e `environment:manage`, e os ambientes de carga precisam de todos os scopes (corrigido direto no banco, como dado sintético, depois de uma primeira rodada nominal que só leu); o `agent-podman` do compose tem `depends_on` da API, que no Podman impede trocar a API (removido no compose da VM, porque em produção o agente fica em outro host); o script idempotente do `dotnet ef` não executa a migration `202609220002`, que chama funções dentro do bloco `DO` (aplicado o script normal, como o `database update`); o compose de laboratório publica a porta gRPC da API em todas as interfaces (presa em `127.0.0.1` na VM).

#### Achados da VM

| Achado | Gravidade | Evidência | Destino proposto |
| --- | --- | --- | --- |
| **A1** `provision-environment-authorization.ps1` lista as policies com `GET .../policy` sem filtro: o Keycloak devolve só 100, e a partir daí o script não acha a policy que criou e falha | Média (ferramenta de operação) | 1.253 de 1.717 concessões falharam; com filtro por nome e paginação explícita, 0 falhas | `/speckit-bug-assess` |
| **A2** Tempestade de reconexão gera `40001` (serialização) na projeção de inventário; o `SaveChanges` compartilhado arrasta a gravação de auditoria junto, o evento é refeito na reentrega, mas o log registra `fail` e `DokpodAuditoriaFalhando` dispara | Média (sem perda: 400/400 comandos com auditoria completa e inventário convergido em 100/100 hosts) | ~131 colisões no restart da API com 100 agentes; 2 e 7 por troca de versão; 1 a cada poucos minutos sob carga | `/speckit-bug-assess`: retentativa de `40001` e separar a gravação de auditoria da projeção |
| **A3** Contadores criados só no primeiro incremento: a série nasce com o valor do primeiro lote e o `increase()` não o enxerga; alertas de evento raro perdem as primeiras ocorrências | Média (observabilidade de evento crítico) | 2 `indeterminate` sem `DokpodComandosIndeterminados` | `/speckit-bug-assess`: inicializar as séries com zero e teste de regra com série nascendo em valor |
| **A4** Leitura do console faz uma decisão UMA por ambiente (103 chamadas ao Keycloak num trace com 101 ambientes) | Baixa (dentro dos limites; custo cresce com o número de ambientes) | Trace `8c931ccb…` | Avaliar decisão em lote antes de matrizes maiores |

### Repetição dos gates 10 e 11 com a `v0.1.0-rc.3` — 2026-10-28

**Candidata**: tag `v0.1.0-rc.3` (`6c84722`, correção de A2 e A3 do PR #53, bug `.specify/bugs/spec005-a2-a3-log-alertas`), pipeline `40768626305` verde. As quatro imagens tiveram a assinatura verificada por digest com `cosign verify` v3.1.3 (identidade `ci.yml@refs/tags/v0.1.0-rc.3`, emissor do GitHub Actions); api, bff e web foram transferidas para a VM com o mesmo ID de imagem (3/3). Mesma VM, mesmos 100 agentes simulados e o agente Podman real em rc.2.

| Verificação | Resultado |
| --- | --- |
| Gate 10 — atualização rc.2 → rc.3 | **PASS**: 37,4 s de indisponibilidade, 100 agentes de volta em 13,3 s, 136 leituras sem erro, auditoria até a troca idêntica (13.826 eventos), 101 ambientes e identidades preservados; **item 7: 0 linhas `fail`/`crit`** (rc.1 → rc.2: 17) |
| Gate 10 — rollback rc.3 → rc.2 | **PASS** nos itens 1 a 6 (35,5 s, agentes em 7,5 s, 16.644 eventos idênticos). As 307 linhas `fail` do item 7 são da própria rc.2 (552 `40001`), o defeito que a rc.3 corrige |
| Gate 10 — nova atualização rc.2 → rc.3 | **PASS**: 34,9 s, agentes em 7,5 s, 16.663 eventos idênticos, item 7 com 0 linhas |
| Gate 7 com rc.3 | Restart da API com 100 comandos em voo: 100/100 `succeeded` = 100 execuções. Resposta perdida, 13 rodadas: 0 execução duplicada; 1 `stale_target` (recusa correta do agente por revisão divergente, sem execução, como nos gates 5 e 6 da rc.2). Em toda a instância: 0 `fail`/`crit` e 0 `40001` |
| Gate 11 — alertas | Regras novas carregadas e saudáveis; `DokpodAuditoriaFalhando` inativo, com 0 falhas de auditoria e 39 cancelamentos em `dokpod_audit_append_canceled_total`. Nenhum `indeterminate` nas 13 rodadas da rc.3, então `DokpodComandosIndeterminados` não teve evento para disparar; a prova ao vivo da regra nova foi feita com a imagem de pré-teste do mesmo código (disparo às 11:09:57) e nos dados reais da rc.2 (05:20 a 05:28), registrada no bug |

**Efeito nos achados**: A2 resolvido (item 7 do gate 10 aprovado, 0 `40001`, auditoria sem falha falsa). A3 resolvido nas regras de alerta, com prova ao vivo na imagem de pré-teste e `promtool test rules`; sem nova ocorrência espontânea na rc.3.

### Veredito recomendado: **GO** (atualizado em 2026-10-28, rc.3)

Com a `v0.1.0-rc.3`, os dois achados que sustentavam o NO-GO estão corrigidos e verificados na VM. Fica como residual: A1 (ferramenta de operação), A4 (dentro dos limites), rollback com migration nova sem exercício e a prova ao vivo do alerta de `indeterminate` feita em imagem de pré-teste do mesmo código, não na imagem assinada. A decisão continua sendo do solicitante.

#### Veredito anterior: **NO-GO** (2026-10-28, rc.2)

Não há gate `NOT RUN`. A repetição em VM isolada (FR-004) comprovou carga nominal, margem, recuperação, restore e observabilidade com as imagens assinadas da `v0.1.0-rc.2`. O NO-GO recomendado se deve aos achados da VM, não a falta de evidência:

- **A2** reprova o item 7 do runbook de atualização (gate 10) e faz `DokpodAuditoriaFalhando` disparar sem perda de auditoria, o que confunde a operação;
- **A3** deixa o alerta de comando `indeterminate`, um evento crítico pela spec 003, cego para as primeiras ocorrências.

Caminhos para GO, à escolha do solicitante:

1. Corrigir A2 e A3 pelo fluxo de bugs (`/speckit-bug-assess`), gerar `v0.1.0-rc.3` e repetir na VM só o gate 10 e a prova do alerta de `indeterminate`; ou
2. Aceitar A2 e A3 como residual, com responsável e prazo registrados abaixo, e decidir GO na `v0.1.0-rc.2`.

A1 e A4 não bloqueiam: A1 é ferramenta de operação, e A4 está dentro dos limites aprovados.

### Decisão humana

| Campo | Valor |
| --- | --- |
| Decisão | **GO** na `v0.1.0-rc.3` |
| Responsável | lzocateli (solicitante) |
| Data | 2026-10-28 |
| Adiamentos aceitos (gates 15 e 16) | Podman Windows adiado (2026-10-24). Docker Windows (Docker Desktop) e Podman Linux aprovados por teste manual do solicitante |
| Decisões parciais (2026-10-24) | Gate 1 aceito: as imagens usam sempre as bases do repositório `containers`. Gates 7 e 14 aceitos com o residual registrado nas linhas. Gate 4 aprovado, com as recomendações das lacunas e os riscos R1–R10 aceitos |
| Observações | Decisão do solicitante em 2026-10-28 ("GO"), depois da repetição dos gates 10 e 11 com a `v0.1.0-rc.3` na VM isolada. Residuais aceitos com o GO: A1 (script de autorização sem paginação), A4 (decisão UMA por ambiente), rollback com migration nova sem exercício e disparo ao vivo de `DokpodComandosIndeterminados` provado só na imagem de pré-teste do mesmo código. `docs/release-readiness.md` não foi alterado (FR-001) |

### Observabilidade — laboratório local, 2026-10-24 (gate 11)

Stack N com exportação OTLP para Prometheus 3.5.0 (receptor OTLP) e Jaeger 2.5.0, do perfil `observability`. Carga: 10 agentes simulados e 5 usuários virtuais.

| Verificação | Resultado |
| --- | --- |
| Métricas recebidas da API e do BFF (`job` = `dokpod-api` / `dokpod-bff`) | PASS: agentes por engine e versão, idade do heartbeat e do inventário, comandos e duração, decisões do Keycloak, auditoria, HTTP por rota, pool e operações do Npgsql e runtime |
| Valores conferidos com a medição externa | PASS: comando p95 de 0,375 s no Prometheus contra 343 ms nos usuários virtuais; leituras p95 de 62 ms contra 68 ms |
| Trace ponta a ponta | PASS: BFF → API → Keycloak → PostgreSQL num trace só, pelo `traceparent` |
| Dados sensíveis | PASS: valores de query string redigidos, SQL parametrizado, sem tokens, cookies ou senhas nos spans |
| Regras de alerta | PASS: 13 regras, `promtool check rules` e 8 grupos de `promtool test rules` |
| Falhas reais | PASS: Keycloak parado → `DokpodKeycloakIndisponivel` em 2,5 min (e `DokpodErrosDoServidor` pendente); agentes derrubados → `DokpodAgentesDesconectando` em 5 min; API parada → `DokpodApiSemTelemetria` em 2,5 min, apagando na volta |

Três defeitos foram achados e corrigidos durante a prova:

1. O `AddOtlpExporter` por sinal ignorava o endpoint e o protocolo específicos do sinal e caía em gRPC em `localhost:4317`; agora eles são aplicados explicitamente.
2. Os buckets padrão dos histogramas são de milissegundos, e com unidade em segundos o p95 dos comandos saía como 4,75 s; agora há buckets em segundos.
3. `DokpodAgentesDesconectando` somava as séries de instâncias antigas da API depois de um restart; agora a regra usa uma subconsulta do total, e há teste com troca de instância.

Limitação: `DokpodKeycloakIndisponivel` depende de tráfego; sem nenhum uso, a queda aparece só no `/health/ready/keycloak`.
