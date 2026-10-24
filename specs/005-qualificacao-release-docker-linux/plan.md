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
