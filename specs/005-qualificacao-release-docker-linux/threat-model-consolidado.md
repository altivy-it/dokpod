# Threat model consolidado — candidata Docker Linux (gate 4)

**Spec**: 005, gate 4 (P-04, FR-006) · **Data**: 2026-10-24 · **Candidata**: branch `005-qualificacao-release`, PR #30
**Estado**: aguardando revisão humana. Este documento junta o que está espalhado em [segurança](../../docs/seguranca.md) para uma revisão única; não substitui aquele documento e não é aprovação.

## Como revisar

1. Leia o [modelo do sistema](#modelo-do-sistema) e confira se as fronteiras correspondem ao que será instalado.
2. Percorra o [mapa de cobertura](#mapa-de-cobertura): cada fronteira aponta para o threat model detalhado e para a evidência.
3. Decida cada item das [lacunas encontradas](#lacunas-encontradas-na-consolidação) (aceitar, corrigir antes do GO, ou adiar com destino).
4. Aceite ou recuse cada [risco residual](#registro-de-riscos-residuais).
5. Preencha a [decisão](#decisão-do-revisor). Gate 4 passa a `PASS` só com a decisão registrada.

## Modelo do sistema

```text
Navegador ──HTTPS──▶ web (nginx) ──▶ BFF ──HTTP + token──▶ API (PEP) ──▶ PostgreSQL
   │ cookie __Host- HttpOnly  │ OIDC + PKCE        │ UMA (deny-by-default)
   │ Lax + antiforgery Strict └──────▶ Keycloak ◀──┘
   │ SignalR/WebSocket (Origin)
                                                   API ◀──gRPC mTLS (por ambiente)── agente ──socket──▶ Docker/Podman
                                                                                      │
                                                              registros de imagens ◀──┘ (pull)
API/BFF ──OTLP──▶ coletor de métricas e traces (opcional, operação)
PostgreSQL ──pg_dump──▶ backup (operação)
```

**Ativos**: controle dos hosts (o socket do engine equivale a root), identidades de usuário e de agente, decisões de autorização, valores sensíveis de criação de container, credenciais de registro (ficam no host), trilha de auditoria append-only, inventário e backups.

**Premissas de confiança**:
- Keycloak é a autoridade de identidade e autorização. Falha dele = negado.
- O agente é confiável para o próprio host e não confiável para o plano de controle: tudo que ele envia é validado por schema e por limite.
- Quem administra o host do agente já tem o engine. O Dokpod não protege o host contra o próprio administrador.
- As imagens partem das bases do repositório `containers` (gate 1, aceito em 2026-10-24).

## Mapa de cobertura

| # | Fronteira | Threat model | Evidência principal | Situação |
| --- | --- | --- | --- | --- |
| F1 | Navegador → BFF (sessão, CSRF, Origin, login) | [Identidade](../../docs/seguranca.md#identidade), [BFF](../../docs/bff.md) | Cookie de sessão `__Host-Dokpod.Session` `HttpOnly`/`SameSite=Lax`/`Secure` fora de Development (`ConfigureCookieOptions.cs`), antiforgery `SameSite=Strict`, `OriginValidationMiddleware`, rate limit no login (`BffHost.cs`); readiness gate 13 | Coberto, sem seção STRIDE própria (ver L2) |
| F2 | BFF → API → Keycloak (PEP, UMA) | [Identidade](../../docs/seguranca.md#identidade), [spec 023](../../docs/seguranca.md#qualificação-na-stack-real-spec-023) | Permissões por scope, leitor sem ações, revogação (gate 13) | Coberto |
| F3 | Agente ↔ API (mTLS, sessão, fencing, comandos) | [Ameaças prioritárias](../../docs/seguranca.md#ameaças-prioritárias), [Identidade](../../docs/seguranca.md#identidade) | EKU `clientAuth` + cadeia da CA própria + identidade por fingerprint com `revoked_at` (`ApiHost.cs`, `PostgresAgentIdentityRegistry.cs`); journal e `Indeterminate` (gate 7) | Coberto; ver L1 |
| F4 | Agente → engine local | [Engine local](../../docs/seguranca.md#engine-local) | Allowlist de comandos e DTOs; sem proxy genérico | Coberto |
| F5 | Eventos operacionais (008) | [Eventos](../../docs/seguranca.md#eventos-operacionais-spec-008) | Allowlist de tipos e parâmetros | Coberto |
| F6 | Métricas de recursos (009) | [plan 009](../009-metricas-recursos/plan.md) | Só `/proc/stat` e `/proc/meminfo` do próprio container | Coberto |
| F7 | Ações em lote (010) | — | Não há rota de lote na API: o console envia um comando por container pela rota normal, com autorização e `Idempotency-Key` próprias (`container-actions.service.ts`) | Sem seção própria (ver L2) |
| F8 | Inspeção e logs (011) | [011](../../docs/seguranca.md#threat-model-inspeção-e-logs-sob-demanda-spec-011) | Canários, limites, monitor de revogação | Coberto |
| F9 | Pull de imagens (012) | [012](../../docs/seguranca.md#threat-model-inventário-de-imagens-e-pull-spec-012) | Gramática OCI na API e no agente | Coberto, residual R2 |
| F10 | Criação de container (013) | [013](../../docs/seguranca.md#threat-model-criação-de-container-spec-013) | Allowlist fechada, valores cifrados e purgados | Coberto, residual R3 |
| F11 | Terminal (014) | [014](../../docs/seguranca.md#threat-model-terminal-interativo-spec-014), ADR 2026-0006 | Limites, revogação em ≤ 60 s, fencing | Coberto, residual R4 |
| F12 | Alertas e justificativas (016) | [016](../../docs/seguranca.md#threat-model-governança-de-alertas-e-ações-justificadas-spec-016) | Escopo em todos os hosts, auditoria | Coberto |
| F13 | Consulta da auditoria (017) | [017](../../docs/seguranca.md#threat-model-consulta-da-auditoria-spec-017) | 404 indistinguível, limites, CSV seguro | Coberto, residual R6 |
| F14 | Volumes, redes, registros, pods (018–021) | [018](../../docs/seguranca.md#threat-model-volumes-spec-018) a [021](../../docs/seguranca.md#threat-model-pods-spec-021) | Recusa de recurso em uso, pod do agente protegido | Coberto, residual R7 |
| F15 | Configurações (022) | [022](../../docs/seguranca.md#threat-model-configurações-spec-022) | Lista fechada de parâmetros, canários | Coberto |
| F16 | Cadeia de fornecimento | [Cadeia](../../docs/seguranca.md#cadeia-de-fornecimento-spec-005), [exceções](../../docs/seguranca-excecoes.md) | SBOM + Trivy no CI; cosign keyless só em tag | Parcial: gate 3 |
| F17 | **Telemetria OTLP** (nova na 005) | Seção [T1](#t1-telemetria-otlp) abaixo | Redação de query string, SQL parametrizado, verificação no laboratório (gate 11) | Novo nesta consolidação |
| F18 | **Backup, restore e rollback** (novo na 005) | Seção [T2](#t2-backup-restore-e-rollback) abaixo | 41/41 itens, trigger append-only na cópia (gate 9) | Novo nesta consolidação |

## Fronteiras novas da spec 005

### T1. Telemetria OTLP

**Fronteira**: API e BFF → coletor OTLP escolhido pela operação. Desligada por padrão; liga só com `OTEL_EXPORTER_OTLP_*`.

| Ameaça | Mitigação | Evidência |
| --- | --- | --- |
| Segredo em span ou métrica | Query string redigida pela instrumentação; SQL parametrizado; nenhum header, cookie ou corpo instrumentado; health checks fora dos traces | Laboratório 2026-10-24: varredura dos traces e métricas sem token, cookie ou senha |
| Reconhecimento pelas métricas | O label do pool do Npgsql traz host e usuário do banco (sem senha) | Registrado no [runbook](../../docs/runbooks/observabilidade.md); residual R9 |
| Interceptação ou injeção no caminho OTLP | Endpoint, protocolo e TLS são da operação; o produto não abre porta de entrada para telemetria | Residual R9: no laboratório, OTLP em HTTP na rede interna |
| Alerta silenciado por falta de dado | `absent_over_time` para API e BFF; regras testadas com `promtool` | `dokpod-alerts.test.yml`; prova real de queda da API |
| Prometheus e Jaeger expostos | São ferramentas do laboratório, publicadas só em `127.0.0.1`, fora da distribuição | `deploy/e2e/docker-compose-dokpod.yaml` (perfil `observability`) |

### T2. Backup, restore e rollback

**Fronteira**: PostgreSQL → `pg_dump` → armazenamento da operação → `pg_restore`.

| Ameaça | Mitigação | Evidência |
| --- | --- | --- |
| Vazamento do backup | O dump contém dados pessoais, auditoria e o realm do Keycloak; o runbook manda guardá-lo fora do repositório, com acesso restrito, e apagar as cópias de exercício | [Runbook](../../docs/runbooks/backup-restore-e-atualizacao.md); residual R10 (sem cifragem própria) |
| Valores de criação de container no dump | Ficam cifrados com o key ring da API e são purgados ao terminar o comando | Threat model 013 |
| Restore que adultera a auditoria | Fingerprint de origem × cópia, incluindo o SHA-256 dos eventos; trigger append-only ativo na cópia | Gate 9: 41/41 itens iguais |
| Rollback que apaga evidência | Migrations forward-only; o rollback troca imagens e nunca executa `Down` | Gate 10 |

## Lacunas encontradas na consolidação

| # | Lacuna | Recomendação | Decisão do revisor |
| --- | --- | --- | --- |
| L1 | A revogação do agente é feita no cadastro de identidades (`revoked_at` por fingerprint), não por CRL/OCSP: a cadeia é validada com `RevocationMode.NoCheck`. Funciona por certificado, mas um comprometimento da CA exige reemitir todas as identidades e trocar a raiz confiável | Aceitar o modelo atual e acrescentar ao runbook o procedimento de troca da CA antes do GO | |
| L2 | Specs 001–004, 010 e 015 não têm seção STRIDE própria em `docs/seguranca.md`; estão cobertas pelas seções gerais (ameaças prioritárias, identidade, engine local) e pelos gates 13 e 14 | Aceitar a cobertura geral, ou pedir as seções de BFF (002) e ações em lote (010) antes do GO | |
| L3 | O agente roda como `0:0` com o socket do engine; posse do socket equivale a root no host | Já é premissa do produto ([Engine local](../../docs/seguranca.md#engine-local)); aceitar | |
| L4 | O compose de laboratório (`deploy/e2e`) usa `ASPNETCORE_ENVIRONMENT=Development` e publica o gRPC em todas as interfaces | É laboratório, não distribuição; confirmar que o pacote instalável não herda esses valores | |
| L5 | Os 100 ambientes "Carga NNN" do laboratório continuam no banco de laboratório | Não afeta a distribuição; remover com o script pendente do T014 | |

## Registro de riscos residuais

| # | Risco | Origem | Mitigação atual | Aceite |
| --- | --- | --- | --- | --- |
| R1 | Segredos sem padrão reconhecível em campos fora da denylist podem aparecer na inspeção | 011 | Mascaramento best-effort com aviso na interface | |
| R2 | Sem verificação de assinatura de imagens de workload, um registro comprometido entrega imagem maliciosa | 012, 013 | Registros por estágio, digest, auditoria | |
| R3 | Operador com `container:create` executa qualquer imagem permitida com as permissões não privilegiadas do engine | 013 | Scope dedicado, allowlist fechada, auditoria | |
| R4 | Processos em segundo plano criados no terminal sobrevivem ao fim da sessão; a revogação leva até 60 s mais a vida do token (≤ 5 min) | 014 | Limites de sessão, alerta de root, auditoria | |
| R5 | Estado das condições de duração dos alertas fica em memória da réplica única | 016 | Recontagem ao reiniciar | |
| R6 | A consulta da auditoria não é auditada, só a exportação | 017 | Decisão da spec | |
| R7 | Pods de infraestrutura que não contêm o agente podem ser parados por quem tem os scopes; janelas de milissegundos em volume e rede | 018, 019, 021 | Proteção do pod do agente; o engine recusa recurso em uso | |
| R8 | Componente adulterado pode informar versão falsa em Configurações | 022 | Assinatura das imagens (gate 3) | |
| R9 | Métricas mostram host e usuário do banco; o transporte OTLP depende da operação | T1 | Telemetria desligada por padrão; métricas tratadas como internas | |
| R10 | O backup não tem cifragem própria | T2 | Acesso restrito pela operação | |
| R11 | Assinatura e proveniência nunca executadas (sem tag) | Cadeia | Testes negativos locais com chaves de teste em 2026-10-17 | Fica com o gate 3, não com este |

## Controles transversais verificados

- **Autorização**: deny-by-default no Keycloak por host e scope; falha do Keycloak = negado; 404 indistinguível onde o recurso é de outro host (gate 13, spec 023).
- **Segredos**: canários em variáveis, labels, comando de saúde, opção de log, linhas de log, termo de busca, senha de registro e `.env` → zero ocorrências fora do mascaramento em respostas, PostgreSQL e logs (specs 011, 013, 020, 022, 023).
- **Auditoria**: append-only por trigger, sem `UPDATE`/`DELETE` para a role da aplicação, íntegra após restore (gate 9).
- **Comandos**: idempotência por `Idempotency-Key`, fencing por sessão, journal antes do ack e `Indeterminate` quando o efeito é incerto (gate 7).
- **Cadeia de fornecimento**: HIGH/CRITICAL corrigível reprova o CI; 93 aceites com validade até 2027-01-15 (gate 2).

## Decisão do revisor

| Campo | Valor |
| --- | --- |
| Revisor | *pendente* |
| Data | *pendente* |
| Lacunas L1–L5 | *pendente* |
| Riscos R1–R10 aceitos | *pendente* |
| Condições para o GO | *pendente* |
| Resultado do gate 4 | *pendente* (`PASS` ou `FAIL`) |
