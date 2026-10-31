---

description: "Tarefas da spec 031: licenciamento Business offline"
---

# Tarefas: Licenciamento Business offline

**Entrada**: Documentos de design em `/specs/031-licenciamento-business-offline/`

**Pré-requisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md) (D-001 a D-012), [data-model.md](data-model.md), [contracts/](contracts/) e [quickstart.md](quickstart.md)

**Gates cumpridos**: FR-030 (revisão jurídica declarada em 2026-10-30) e FR-031 (ADRs 2026-0007 e 2026-0008 aceitos em 2026-10-30). Continuam como gates: o threat model (T002) antes de qualquer código, a criação do repositório da CLI (T056) com autorização explícita da fase F2 e o GO humano de F7 antes de publicar a imagem Business.

**Testes**: obrigatórios. Toda mudança de comportamento tem teste escrito antes e falhando (constituição). Relógio sempre injetável (`TimeProvider`); nada de `DateTime.UtcNow` no licenciamento.

**Organização**: tarefas agrupadas por história. A coluna "Fase do plano" de cada seção liga as tarefas às fases F1 a F7 de [plan.md](plan.md#fases-de-entrega-do-plano-de-origem).

## Formato: `[ID] [P?] [Story] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história de usuário de spec.md (US1 a US6)

## Convenções de Caminhos

- Verificador: `backend/libs/Dokpod.Licensing/` e `backend/tests/Dokpod.Licensing.Tests/`
- Portas e regras: `backend/libs/Dokpod.ControlPlane.Application/Licensing/`
- Adaptadores: `backend/libs/Dokpod.ControlPlane.Infrastructure/Licensing/`
- Migrations: `backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/<yyyyMMddNNNN>_Licensing/{Up,Down}/`
- Composição Business e endpoints: `backend/apps/Dokpod.ControlPlane.Api/Licensing/`
- Console: `frontend/web/src/app/features/settings/`; E2E em `frontend/tests/e2e/`
- CLI de emissão: repositório separado `dokpod-license-admin` (nome proposto), fora deste repositório

---

## Fase 1: Preparação (F1: contratos e segurança)

**Objetivo**: fechar o que a constituição exige antes de código e reservar contratos.

- [ ] T001 Reservar a tag 14 de `AgentHello` para `cpu_count_source` e o enum `CpuCountSource` na tabela "Registro de alocação de contratos" de `specs/README.md` (coluna 031; scopes Keycloak `licensing:read` e `licensing:manage` no recurso `urn:dokpod:installation`), e trocar `<tag a reservar>` por `14` em `specs/031-licenciamento-business-offline/contracts/agent-cpu-source.md`
- [x] T002 Escrever o threat model da fronteira nova (upload de licença e de revogação, trust store embarcado, relógio local, restauração de backup, composição CE/Business, CLI e chave privada do fornecedor) em `specs/031-licenciamento-business-offline/threat-model.md`, no formato de `specs/005-qualificacao-release-docker-linux/threat-model-consolidado.md`, com mitigação ligada a FR/D e revisão humana registrada; **gate**: nenhuma tarefa de código começa antes da revisão
- [ ] T003 [P] Criar a biblioteca `backend/libs/Dokpod.Licensing/Dokpod.Licensing.csproj` (.NET 10, sem `PackageReference` nem `ProjectReference`, cabeçalho e arquivo `LICENSE` MIT conforme D-002) e o projeto `backend/tests/Dokpod.Licensing.Tests/Dokpod.Licensing.Tests.csproj` (xUnit, sem I/O), e registrar os dois em `Dokpod.slnx`
- [ ] T004 [P] Incorporar o fragmento `specs/031-licenciamento-business-offline/contracts/licensing-api.yaml` em `contracts/openapi/dokpod-control-plane.v1.yaml`, com os paths `/api/v1/licensing/*` marcados como exclusivos da composição Business (`x-dokpod-edition: business`)

---

## Fase 2: Fundação (bloqueia todas as histórias)

**Objetivo**: separação de edições no build, persistência, autorização e trilha global prontas.

**CRÍTICO**: nenhuma história começa antes desta fase.

### Testes da fundação

- [ ] T005 [P] Testes de arquitetura em `backend/tests/Dokpod.Architecture.Tests/LicensingArchitectureTests.cs`: `Dokpod.Licensing` não referencia nenhum assembly `Dokpod.*` nem pacote; nenhum caso de uso CE de `Dokpod.ControlPlane.Application` depende de `IBusinessCapabilityGate`; nenhum tipo do licenciamento usa `DateTime.UtcNow`, `DateTimeOffset.UtcNow`, `HttpClient`, `Dns` ou `Socket` (FR-002, D-009)
- [ ] T006 [P] Teste de integração PostgreSQL em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/LicensingMigrationTests.cs`: migration aplica e reverte; partições mensais de `licensing_capacity_observations` e `licensing_events`; índice único de `(license_id, revision)` só para `outcome = 'accepted'`; `licensing_events` recusa `UPDATE` e `DELETE`; uma linha com data fora das partições mensais vai para `*_default`; `dokpod_ensure_monthly_partitions` cria os meses seguintes das duas tabelas; `dokpod_apply_temporal_retention` aceita `licensing_capacity_observations`/`observed_at_utc` e recusa `licensing_events`; `dokpod_retain_licensing_capacity` grava as âncoras em `licensing_capacity_anchors` antes de remover observações; `license_revisions.content` aceita `null`; `licensing_state` tem `installation_id`, `chain_head_seq` e `chain_head_hash`; `licensing_events` tem `chain_seq`, `prev_hash` e `record_hash` não nulos e `actor_ref` ao lado de `actor_id`

### Implementação da fundação

- [ ] T007 Criar a migration `backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/<ts>_Licensing/Up/01-Licensing.sql` e o `Down` correspondente, expand-contract, schema `dokpod`, exatamente como [data-model.md](data-model.md): `license_revisions` (`revision_id uuid` PK, `document_type` em `license|revocation`, `received_at_utc timestamptz`, `received_by text`, `content_sha256 bytea` de 32 bytes, `content bytea null` ≤ 65536 bytes (só aceitos), `content_sha256` sobre `header.payload`, `license_id text`, `revision int`, `outcome` em `accepted|rejected`, `failure_code text null`); `licensing_state` com uma única linha (`installation_id uuid` aleatório, `actor_hmac_key bytea` de 32 bytes aleatórios, `chain_head_seq bigint`, `chain_head_hash bytea`, `installation_slot_id`, `active_revision_id uuid null` FK, `revocation_sequence bigint`, `expiry_grace_until_utc`, `excess_started_at_utc`, `excess_grace_until_utc` nulos, `last_trusted_instant_utc`, `version bigint`); `licensing_hosts`, `licensing_host_environments` (um ambiente em no máximo um host ativo); `licensing_capacity_observations` (`vcpus int null`, `source` em `engine|process_fallback|unknown`) e `licensing_events` particionadas por mês em `observed_at_utc` e `occurred_at_utc`, no padrão de `202609120001_CreateAuditEvents`: partições `licensing_capacity_observations_default` e `licensing_events_default`, e índices criados na tabela-mãe para que cada partição os herde; `licensing_capacity_anchors` não particionada (`host_id uuid` PK, `observed_at_utc`, `vcpus int null`, `source`, `environment_id`) e a função `dokpod.dokpod_retain_licensing_capacity(p_retention interval)`, que grava a âncora de cada host e depois chama a retenção genérica (a retenção genérica nunca é chamada direto para essa tabela). A mesma migration recria `dokpod.dokpod_ensure_monthly_partitions` incluindo as duas tabelas na lista, o que garante partições criadas com antecedência e o rollover pelo agendamento de `202609220002_ScheduleTemporalPartitions`, e recria `dokpod.dokpod_apply_temporal_retention` acrescentando `licensing_capacity_observations` às tabelas e `observed_at_utc` às colunas permitidas. `licensing_events` não entra: é trilha de auditoria e não é purgada, como `audit_events`. O `Down` restaura as duas funções na versão anterior
- [ ] T008 [P] Definir as portas em `backend/libs/Dokpod.ControlPlane.Application/Licensing/`: `ILicenseStateReader`, `IBusinessCapabilityGate` (`EnsureAsync(capability, ct)` devolvendo decisão tipada, nunca exceção de autenticação), `ILicenseStore`, `ICapacityLedger`, `ILicensingAuditWriter` e `LicensingEventAction` com as ações de data-model.md (`license.imported`, `license.rejected`, `license.state_changed`, `license.grace_started`, `license.grace_ended`, `license.expiry_warning`, `license.report_exported`, `revocation.imported`, `host.linked`, `host.unlinked`, `host.removed`)
- [ ] T009 Implementar `LicensingAuditWriter` (depende de T007 e T008) em `backend/libs/Dokpod.ControlPlane.Infrastructure/Licensing/LicensingAuditWriter.cs` gravando em `licensing_events` com `details` em allowlist por ação (IDs, revisões, códigos; nunca conteúdo da licença, FR-026) e encadeando cada evento conforme [report-format.md](contracts/report-format.md#cadeia-de-licenciamento): `chain_seq` sem lacunas, `actor_ref` = HMAC-SHA256 do sujeito com `actor_hmac_key`, `prev_hash`, `record_hash` sobre JSON canônico (com `actor_ref`, nunca `actor_id`) e avanço de `chain_head_seq`/`chain_head_hash` com `version` otimista na mesma transação (FR-035); testes de gravação concorrente sem `chain_seq` repetido e de cadeia recalculada igual à gravada em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/LicensingAuditWriterTests.cs`
- [ ] T010 Criar o recurso `urn:dokpod:installation` e os scopes `licensing:read` e `licensing:manage` em `tools/scripts/configure-keycloak.ps1` (idempotente, sem roles locais, D-008) e documentar em `docs/configuracao-keycloak.md`
- [ ] T011 Criar a composição Business em `backend/apps/Dokpod.ControlPlane.Api/Licensing/LicensingModule.cs` (`AddDokpodBusinessLicensing`, `MapLicensingEndpoints`), registrada em `backend/apps/Dokpod.ControlPlane.Api/Program.cs` só quando a constante de build `DOKPOD_BUSINESS` estiver definida (`<DefineConstants>` por `-p:DokpodEdition=Business` em `Dokpod.ControlPlane.Api.csproj`); sem variável de ambiente que ligue a edição (D-009)
- [ ] T012 Criar o problem details tipado de licença em `backend/apps/Dokpod.ControlPlane.Api/Licensing/LicensingProblems.cs` com os códigos de `licensing-api.yaml` (`license_invalid`, `license_obsolete`, `license_expired`, `license_capacity_exceeded`, `license_revoked`, `license_state_indeterminate`, `license_revision_conflict`, `payload_too_large`): recusa por licença é `403`, upload inválido `422`, conflito `412`, tamanho `413`, nunca `401` (FR-024)

**Checkpoint**: a imagem CE compila sem o módulo; a Business compila com ele, ainda sem endpoints funcionais.

---

## Fase 3: História 1 - CE continua completa sem licença (P1) MVP

**Fase do plano**: F5/F6 (prova contínua). **Objetivo**: a CE não muda, não comunica nada para fora e não mostra licença.

**Teste independente**: composição CE sem licença e sem egress; suítes existentes verdes; captura de rede vazia (quickstart, cenário 1).

### Testes da História 1

- [ ] T013 [P] [US1] Teste em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/CeCompositionTests.cs`: na composição CE, `GET /api/v1/licensing/status` responde `404`, nenhum serviço `ILicenseStateReader` ou `IBusinessCapabilityGate` está registrado e o startup não toca tabelas `licensing_*`
- [ ] T014 [P] [US1] Teste E2E em `frontend/tests/e2e/test_licensing_ce.py`: na CE, Configurações não tem seção Licença nem chamada a `/api/v1/licensing/*`
- [ ] T015 [US1] Script de captura de rede sem egress em `tools/scripts/test-licensing-no-egress.ps1` (D-012): sobe a stack de `deploy/e2e/docker-compose-dokpod.yaml` em rede interna (só PostgreSQL e Keycloak), captura DNS/TCP/UDP do container da API durante início, operação e reinício, e falha com qualquer destino externo; também procura canários de licença em logs, métricas e traces exportados

### Implementação da História 1

- [ ] T016 [US1] Garantir no frontend que a seção Licença só existe quando o BFF informa a edição Business (campo `edition` lido de `frontend/web/src/app/features/settings/settings-api.service.ts`), sem chamada na CE
- [ ] T017 [US1] Incluir T013, T014 e T015 no CI da CE em `.github/workflows/ci.yml`, rodando sobre a imagem CE

**Checkpoint**: História 1 validável sozinha; vale para cada versão Business daqui em diante.

---

## Fase 4: História 2 - Importar e verificar a licença offline (P1)

**Fase do plano**: F3 (verificador) e F5 (importação, API). **Objetivo**: o administrador importa a licença e vê o estado; arquivo inválido nunca substitui a licença íntegra.

**Teste independente**: quickstart, cenário 2, com vetores sintéticos numa instalação Business isolada.

### Testes da História 2

- [ ] T018 [P] [US2] Vetores de conformidade em `backend/tests/Dokpod.Licensing.Tests/Vectors/` (chave P-256 sintética de teste, nunca no trust store de produção) e `LicenseVerifierVectorTests.cs`, com um caso por falha de [license-format.md](contracts/license-format.md#falhas-tipadas-do-verificador): `malformed` (ID fora de `[A-Za-z0-9_-]{1,128}`, Base64URL com padding, UTF-8 inválido, JSON com propriedade duplicada ou desconhecida, profundidade > 8, arquivo > 65536 bytes, inteiro fora de `int32`, string > 128), `unsupported_profile` (`alg: none`, `HS256`, `jwk`/`jku`/`x5u`/`x5c`/`crit` no header, `typ` trocado, `schemaVersion` ≠ 1, `meteringVersion` ≠ 1), `unknown_key`, `bad_signature` (um bit alterado), `wrong_product`, `inconsistent_dates` (`issuedAt ≤ notBefore < expiresAt` violado), `invalid_capacity` (`nodeEquivalents` ≤ 0, 0 ou > 2 slots, soma de cotas habilitadas acima da capacidade, `vcpuBand` ≤ 0) e `unknown_capability` (> 64 itens ou fora da allowlist)
- [ ] T019 [P] [US2] Teste de desempenho em `backend/tests/Dokpod.Licensing.Tests/LicenseVerifierPerformanceTests.cs`: licença de 64 KiB verificada em menos de 50 ms
- [ ] T020 [P] [US2] Testes de aplicação em `backend/tests/Dokpod.ControlPlane.Application.Tests/Licensing/LicenseImportServiceTests.cs`: aceite atualiza estado e grava `license.imported`; recusa grava `license.rejected` e mantém a revisão ativa; revisão menor que a ativa → `license_obsolete`; mesma revisão com o mesmo `content_sha256` (hash de `header.payload`; a mesma licença com assinatura maleada também conta como idêntica) → `200` sem efeito, sem nova linha em `license_revisions` e sem evento (FR-009); mesma revisão com outro hash → `license_invalid`, com o motivo `revision_reused` gravado em `license.rejected`; slot inexistente na licença → `license_slot_unknown`; slot com cota 0 → `license_slot_disabled`; troca de slot grava `license.slot_changed`; `If-Match` divergente é `license_revision_conflict`; licença persistida é reverificada a cada leitura (FR-010)
- [ ] T021 [P] [US2] Testes de API com PostgreSQL real em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/LicensingEndpointsTests.cs`: `PUT /api/v1/licensing/license` exige `If-Match` e `X-Dokpod-Antiforgery`; corpo > 65536 bytes → `413`; usuário sem `licensing:manage` → `403 authorization_denied` mesmo com licença válida; sem `licensing:read` o status é negado; duas importações concorrentes → uma `200` e uma `412`; sem `slotId` → `400`; acima de 10 uploads por minuto por sujeito em `PUT` ou `POST /revocations` → `429 rate_limited`

### Implementação da História 2

- [ ] T022 [P] [US2] Leitor JWS compacto em `backend/libs/Dokpod.Licensing/JwsCompactReader.cs`: limites de D-003, Base64URL sem padding, header exatamente `alg`/`kid`/`typ`, assinatura ES256 IEEE P1363 de 64 bytes verificada com `ECDsa` sobre os segmentos recebidos antes de interpretar o payload, sem reserializar
- [ ] T023 [P] [US2] Parser estrito do payload em `backend/libs/Dokpod.Licensing/LicensePayloadParser.cs` com `Utf8JsonReader` (profundidade 8, recusa de duplicadas e desconhecidas) e o modelo imutável `LicenseDocument` com os campos e regras de data-model.md
- [ ] T024 [US2] `TrustStore` (várias chaves por `kid`, para rotação) e `LicenseVerifier.Verify(ReadOnlySpan<byte>, TrustStore, DateTimeOffset)` em `backend/libs/Dokpod.Licensing/`, devolvendo `LicenseVerificationResult` com falha tipada; depende de T022 e T023
- [ ] T025 [US2] `LicenseStore` em `backend/libs/Dokpod.ControlPlane.Infrastructure/Licensing/LicenseStore.cs`: grava a revisão recebida e troca `active_revision_id` numa única transação com `version` otimista; recusa nunca toca o estado e grava só hash, tamanho e `failure_code`, com `content = null`
- [ ] T026 [US2] `LicenseImportService` e `LicenseStateEvaluator` (eixo autenticidade e vigência; apuração entra na US3) em `backend/libs/Dokpod.ControlPlane.Application/Licensing/`, reverificando o conteúdo persistido a cada avaliação
- [ ] T027 [US2] Trust store embarcado da composição Business em `backend/apps/Dokpod.ControlPlane.Api/Licensing/TrustStore/` (chaves públicas como recurso embutido; nenhuma chave privada no repositório) e endpoints `GET /api/v1/licensing/status` (com `ETag`) e `PUT /api/v1/licensing/license?slotId=` em `backend/apps/Dokpod.ControlPlane.Api/Licensing/LicensingEndpoints.cs`, na ordem autenticar → autorizar no Keycloak → avaliar → persistir (FR-022), com rate limit de 10 uploads por minuto por sujeito
- [ ] T028 [US2] Seção Licença em `frontend/web/src/app/features/settings/` (`licensing-api.service.ts`, `licensing-section.component.ts` e `.html`): estado, organização, plano, vigência, capacidade, cota da instalação, capabilities e upload com `If-Match`; a até 2 cliques de Configurações (SC-007); o conteúdo do arquivo nunca aparece em lista, notificação ou erro (FR-027)
- [ ] T029 [US2] Teste E2E em `frontend/tests/e2e/test_licensing_import.py`: importar licença válida e um vetor adulterado pela borda nginx; estado e mensagem de recusa conferem

**Checkpoint**: a Business aceita licença legítima e recusa as inválidas, sem apuração ainda.

---

## Fase 5: História 3 - Apuração de nodes equivalentes e cotas (P2)

**Fase do plano**: F4. **Objetivo**: consumo atual e pico simultâneo de 30 dias, com hosts e origem da CPU.

**Teste independente**: quickstart, cenário 3, com hosts sintéticos e relógio injetável.

### Testes da História 3

- [ ] T030 [P] [US3] Testes do agente em `backend/tests/Dokpod.Agent.Application.Tests/CpuCountSourceTests.cs`: `ENGINE` quando `EngineHostInfo.CpuCount > 0`, `PROCESS_FALLBACK` quando usa `Environment.ProcessorCount`
- [ ] T031 [P] [US3] Teste N/N-1 em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/AgentCpuSourceCompatibilityTests.cs`: agente N-1 sem o campo → observação `unknown`, último valor confiável mantido; sessão do agente nunca cai por falha ao gravar observação
- [ ] T032 [P] [US3] Testes de apuração em `backend/tests/Dokpod.ControlPlane.Application.Tests/Licensing/CapacityMeteringTests.cs` com as linhas da tabela do cenário 3 do quickstart: faixa 16 com 16/17/32 → `current = 5`; picos em horários diferentes → `windowPeak = 4`, não 5; host com Docker e Podman de 32 e 32 → 2, uma vez; host N-1 → `unknownHosts = 1`; host removido há 10 dias ainda no pico; troca para faixa 32 reapura dos dados brutos; casos de 33 e 64 vCPUs (SC-004)
- [ ] T033 [P] [US3] Teste de integração PostgreSQL em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/CapacityLedgerTests.cs`: observação gravada só na mudança ou na conexão; retenção ≥ 35 dias; a retenção grava a âncora em `licensing_capacity_anchors` antes de remover as observações; um host sem mudança há 60 dias mantém a capacidade correta; nova execução da retenção não regride a âncora
- [ ] T066 [P] [US3] Teste de integração com Docker real em `backend/tests/Dokpod.Agent.Docker.IntegrationTests/CpuCountSourceDockerTests.cs`: `EngineHostInfo.CpuCount` vem do `/info` do engine e resulta em `ENGINE`; o limite `--cpus` do container do agente não altera o valor reportado
- [ ] T067 [P] [US3] Teste de integração com Podman real em `backend/tests/Dokpod.Agent.Podman.IntegrationTests/CpuCountSourcePodmanTests.cs`: rootful e rootless reportam `ENGINE` com o `NCPU` do host; se o engine não informar, resulta em `PROCESS_FALLBACK`
- [ ] T068 [US3] Passo na qualificação Windows em `docs/runbooks/qualificacao-windows.md`: o Windows Service, num host sem runtime .NET, reporta a origem e a quantidade de vCPU, e o valor confere com `Get-CimInstance Win32_Processor`
- [ ] T034 [P] [US3] Testes de associação de hosts em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/LicensingHostEndpointsTests.cs`: criar, associar, desassociar e remover exigem `licensing:manage`, geram `host.linked`/`host.unlinked`/`host.removed` e um ambiente não fica em dois hosts ativos

### Implementação da História 3

- [ ] T035 [US3] Adicionar `enum CpuCountSource` e `CpuCountSource cpu_count_source = 14;` em `contracts/agent/v1/agent.proto`, sem mudar `cpu_count = 10` (FR-017), e regenerar os tipos de `backend/libs/Dokpod.Agent.Contracts`
- [ ] T036 [US3] Preencher `CpuCountSource` em `CreateHello` de `backend/apps/Dokpod.Agent/AgentControlWorker.cs`
- [ ] T037 [US3] `CapacityLedger` em `backend/libs/Dokpod.ControlPlane.Infrastructure/Licensing/CapacityLedger.cs` e gravação da observação no início da sessão em `backend/apps/Dokpod.ControlPlane.Api/Agents/AgentHostActivity.cs`, só na composição Business, com falha isolada da sessão; `ENGINE` grava `vcpus` confiável, `PROCESS_FALLBACK` e `UNSPECIFIED` gravam `source = process_fallback|unknown` (FR-013)
- [ ] T038 [US3] `CapacityMeter` em `backend/libs/Dokpod.ControlPlane.Application/Licensing/CapacityMeter.cs`: `U(t) = Σ teto(vCPU_h(t) / B)` avaliado só nos instantes de mudança; `P = max U(s)` em 30 dias; host desconectado mantém a última capacidade; maior capacidade confiável entre os ambientes do host (D-005); lê a âncora de `licensing_capacity_anchors` quando não há observação no início da janela
- [ ] T039 [US3] Serviço e endpoints de hosts de licenciamento em `backend/libs/Dokpod.ControlPlane.Application/Licensing/LicensingHostService.cs` e `backend/apps/Dokpod.ControlPlane.Api/Licensing/LicensingHostEndpoints.cs`, e os paths correspondentes em `contracts/openapi/dokpod-control-plane.v1.yaml` (D-004)
- [ ] T040 [US3] Preencher `consumption` e `installation` em `GET /api/v1/licensing/status` e mostrar na seção Licença: consumo atual, pico e horário, faixa, cota e hosts com medição desconhecida destacados

**Checkpoint**: a apuração coincide com o esperado em todos os cenários de SC-004.

---

## Fase 6: História 4 - Expiração e excesso com tolerância (P2)

**Fase do plano**: F5. **Objetivo**: avisos e prazos auditáveis; depois da tolerância, só novas execuções Business param.

**Teste independente**: quickstart, cenário 4, com relógio injetável.

### Testes da História 4

- [ ] T041 [P] [US4] Testes de estado em `backend/tests/Dokpod.ControlPlane.Application.Tests/Licensing/LicenseStateEvaluatorTests.cs`: expirada há 10 dias → `granted_in_grace` com `grace.until = expiresAt + 30d`; após o prazo → `restricted`; expiração e excesso juntos → vale o prazo mais cedo (FR-020); `notBefore` no futuro → `not_started`, sem direitos; relógio abaixo de `last_trusted_instant_utc` por mais de 5 minutos → `indeterminate` sem tolerância nova; recuo de até 5 minutos → estado mantido e instante não avança; cota 0 → slot desabilitado; capacidade `unlimited` nunca vem de zero; pico 5 com cota 4 → `excess_in_grace`; pico 5 com cota 6 e capacidade total 10 → `regular`; `quota = null` → nunca excesso
- [ ] T042 [P] [US4] Testes de persistência dos prazos em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/GracePersistenceTests.cs`: reinício da API, reimportação da mesma licença e restauração de backup antigo não estendem nenhum prazo (SC-006)
- [ ] T043 [P] [US4] Testes do gate em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/BusinessCapabilityGateTests.cs`: em `restricted`, nova execução Business → `403 license_expired`; operação já aceita conclui; leitura, backup, exportação, renovação e endpoints CE seguem `200`; containers não são parados (SC-005)
- [ ] T044 [P] [US4] Testes de aviso em `backend/tests/Dokpod.ControlPlane.Application.Tests/Licensing/ExpiryWarningTests.cs`: `license.expiry_warning` uma vez em cada marco de 60, 30 e 7 dias, sem repetir após reinício (FR-032)
- [ ] T070 [P] [US4] Testes do resumo diário em `backend/tests/Dokpod.ControlPlane.Application.Tests/Licensing/CapacityDailySummaryTests.cs`: um `capacity.daily_summary` por dia UTC encerrado, com pico, faixa, hosts e origem; reinício não duplica; dias com a API parada gravados depois com `late: true` (FR-035)

### Implementação da História 4

- [ ] T045 [US4] Completar `LicenseStateEvaluator` com os eixos de apuração e direitos, prazos persistidos em `licensing_state` (`expiry_grace_until_utc`, `excess_started_at_utc`, `excess_grace_until_utc` gravados na primeira detecção e nunca recomeçados) e avanço monotônico de `last_trusted_instant_utc` (D-006); o excesso compara `windowPeak` com a cota do slot ativo
- [ ] T046 [US4] `BusinessCapabilityGate` em `backend/libs/Dokpod.ControlPlane.Infrastructure/Licensing/BusinessCapabilityGate.cs`, reavaliado a cada nova operação exclusiva e a cada execução de job, sem cache que estenda prazo; registrado só na composição Business
- [ ] T047 [US4] Job de avaliação periódica em `backend/apps/Dokpod.ControlPlane.Api/Licensing/LicensingEvaluationService.cs` (`BackgroundService` com `TimeProvider`) que grava `license.state_changed`, `license.grace_started`, `license.grace_ended`, `license.expiry_warning` e, uma vez por dia UTC encerrado, `capacity.daily_summary` ([report-format.md](contracts/report-format.md#resumo-diário-de-capacidade)), preenchendo dias perdidos com `late: true`
- [ ] T048 [US4] Mostrar na seção Licença os motivos (`reasons`), o prazo de tolerância e o aviso de vencimento, com a mesma informação disponível por leitura da trilha de licenciamento
- [ ] T069 [US4] Saída de `indeterminate` (L2 do threat model): `POST /api/v1/licensing/trusted-instant` em `backend/apps/Dokpod.ControlPlane.Api/Licensing/LicensingEndpoints.cs` com `licensing:manage`, `If-Match`, antiforgery e `reason` de 10 a 500 caracteres; regrava `last_trusted_instant_utc` com o relógio atual, nunca cria nem estende tolerância, responde `409` fora de `indeterminate` por relógio e grava `license.trusted_instant_reset` com instante anterior e novo; testes em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/TrustedInstantResetTests.cs`

**Checkpoint**: enforcement validado sem nenhum efeito sobre workloads, dados ou CE.

---

## Fase 7: História 6 - Rotação de chave e revogação offline (P3)

**Fase do plano**: F3 e F5. Vem antes da História 5 porque só depende do verificador deste repositório. **Objetivo**: duas chaves confiáveis ao mesmo tempo e revogação importada à mão.

**Teste independente**: quickstart, cenário 6.

### Testes da História 6

- [ ] T049 [P] [US6] Vetores de revogação em `backend/tests/Dokpod.Licensing.Tests/Vectors/` e `RevocationVerifierTests.cs`: `typ: dokpod-revocation+jws` aceito só como revogação e licença nunca aceita como revogação; até 64 entradas; trust store com duas chaves aceita licenças de ambas
- [ ] T050 [P] [US6] Testes de API em `backend/tests/Dokpod.ControlPlane.Api.Tests/Licensing/RevocationEndpointsTests.cs`: pacote válido → `restricted` e `revocation.imported`; sequência menor ou igual → `422`; IDs de outra organização → recusa; CE e workloads intactos

### Implementação da História 6

- [ ] T051 [US6] `RevocationDocument` e `RevocationVerifier` em `backend/libs/Dokpod.Licensing/` (payload de [license-format.md](contracts/license-format.md#payload-do-pacote-de-revogação), sequência monotônica)
- [ ] T052 [US6] `POST /api/v1/licensing/revocations` em `backend/apps/Dokpod.ControlPlane.Api/Licensing/LicensingEndpoints.cs`, atualizando `revocation_sequence` na mesma transação do estado, e upload do pacote na seção Licença
- [ ] T053 [US6] Procedimento de rotação de chave (inclusão da chave nova no trust store, sobreposição, retirada da antiga) e de comprometimento (chave nova, release com a comprometida removida, reemissão das licenças assinadas por ela, comunicação aos clientes) em `docs/runbooks/licenciamento-rotacao-e-revogacao.md`

**Checkpoint**: rotação e revogação funcionam sem rede.

---

## Fase 8: História 5 - Emissão, renovação e aditivo pelo fornecedor (P3)

**Fase do plano**: F2 e F3. **Objetivo**: CLI separada, idempotente e com conferência dupla.

**Teste independente**: quickstart, cenário 5, no repositório da CLI.

**Gate**: T056 exige autorização explícita do solicitante para criar o repositório (nome, licença, visibilidade, responsáveis, armazenamento e provedor de chave).

- [ ] T054 [US5] Empacotar `Dokpod.Licensing` como pacote NuGet com versão exata e os vetores de conformidade publicados junto, por `backend/libs/Dokpod.Licensing/Dokpod.Licensing.csproj` e um job em `.github/workflows/`, sem publicar antes do GO de F7
- [ ] T055 [US5] Registrar em `specs/031-licenciamento-business-offline/research.md` as decisões pendentes de D-010 (armazenamento do registro de emissões com versão e licença, provedor de chave) para decisão do solicitante
- [ ] T056 [US5] Criar o repositório `dokpod-license-admin` (após autorização) com `README.md`, licença decidida e CI própria; nada dele entra em `backend/apps/` deste repositório (FR-028)
- [ ] T057 [US5] No repositório da CLI (depende de T056): testes de `license issue|renew|amend` e `revocation create` com `--dry-run` (nada assinado, gravado ou numerado), idempotência por `requestId` + hash (código 0 na repetição, 3 com conteúdo diferente), soma de cotas acima da capacidade recusada antes de assinar (código 2), aprovação ausente (código 4) e interrupção em cada etapa resultando numa única revisão (SC-009)
- [ ] T058 [US5] No repositório da CLI: implementar os comandos de [cli-commands.md](contracts/cli-commands.md) consumindo `Dokpod.Licensing`, com segredo nunca por argumento, saída atômica e verificação do artefato antes de entregar
- [ ] T059 [US5] Teste cruzado: arquivo emitido pela CLI importado pelo cenário 2 do quickstart, registrado em `specs/031-licenciamento-business-offline/quickstart.md`
- [ ] T072 [P] [US5] No repositório da CLI (depende de T056 e T071): testes de `report verify` com os vetores de T071 e de `license issue|renew` sem `--report` ou com relatório não `ok` → código 6, nada assinado; `--accept-report-findings` só com segunda aprovação; registro guarda `installationId`, `headSeq` e `headHash` aceitos (FR-034, SC-010)
- [ ] T073 [US5] No repositório da CLI: implementar `report verify` e a exigência de `--report` em `license issue|renew` conforme [cli-commands.md](contracts/cli-commands.md), usando `LicensingReportVerifier` e o `challenge` do pedido

**Checkpoint**: emissão verificável pela instalação, com uma única revisão por pedido.

---

## Fase 9: Acabamento e Aspectos Transversais (F6 e F7)

- [ ] T060 [P] `GET /api/v1/licensing/report` em `backend/apps/Dokpod.ControlPlane.Api/Licensing/LicensingEndpoints.cs` (`licensing:read`, `challenge` obrigatório, grava `license.report_exported` com o desafio antes de montar o documento, cadeia completa no formato de [report-format.md](contracts/report-format.md), funciona sem licença importada, sem envio automático; o teste confirma que nenhum sujeito do Keycloak nem `actor_hmac_key` aparece no documento, só `actorRef`) e botão de exportação local na seção Licença com campo para o desafio, com teste em `LicensingEndpointsTests.cs` (FR-033, FR-035)
- [ ] T071 [P] `LicensingReportVerifier` em `backend/libs/Dokpod.Licensing/` (BCL, sem I/O): recalcula a cadeia, confere continuidade com `headSeq`/`headHash` anteriores, `installationId`, desafio e lista de emissões informada, devolvendo `ok|tampered|discontinuous|mismatch|stale` mais `gaps`; vetores em `backend/tests/Dokpod.Licensing.Tests/ReportVectors/` com um relatório íntegro e um por adulteração de SC-010, gerados a partir de uma instalação real de teste e publicados junto do pacote (T054)
- [ ] T061 Imagens CE e Business: build da API com `-p:DokpodEdition=Business` só na imagem Business em `backend/apps/Dokpod.ControlPlane.Api/Dockerfile` e no `publish.yml`, sem mudar a identidade cosign da CE (`docs/seguranca.md`)
- [ ] T062 [P] Inspeção das camadas e do histórico das imagens CE e Business em `tools/scripts/test-licensing-image-contents.ps1`: nenhum emissor, nenhuma chave privada, CE sem o módulo registrado, nenhum `kid` da chave sintética dos vetores no trust store da imagem Business (SC-008)
- [ ] T074 [P] Teste em `backend/tests/Dokpod.Architecture.Tests/BusinessGateTests.cs`: todo handler marcado como exclusivo Business depende de `IBusinessCapabilityGate`; nenhum tipo da CE referencia o módulo de licenciamento (L7 do threat model)
- [ ] T063 [P] Atualizar `docs/licenciamento.md` (matriz reconciliada da spec: ações em lote e governança de alertas na CE), `docs/arquitetura.md` e `docs/runbooks/licenciamento-importacao.md` (importação, estados, prazos, recuperação de `indeterminate` por `POST /api/v1/licensing/trusted-instant`, exportação do relatório com o desafio, e reimportação da revogação após restauração de backup)
- [ ] T064 Revisão de segurança e privacidade dos fluxos novos contra o threat model de T002 (telemetria sem conteúdo de licença, logs redigidos, FR-026), registrada em `specs/031-licenciamento-business-offline/threat-model.md`
- [ ] T065 Executar os sete cenários de [quickstart.md](quickstart.md), inclusive T015 nas duas edições, e registrar evidências para o GO humano de F7; publicação da imagem Business só com pedido separado

---

## Dependências e Ordem de Execução

### Dependências entre Fases

- **Preparação (Fase 1)**: T002 é gate de todo código; T001, T003 e T004 podem andar juntos.
- **Fundação (Fase 2)**: depende da Fase 1 e bloqueia as histórias.
- **US1 (Fase 3)**: depende da fundação; serve de regressão para as demais.
- **US2 (Fase 4)**: depende da fundação. Os testes do verificador (T018, T019) e T022 a T024 são F3 e podem começar logo após T003 e T002.
- **US3 (Fase 5)**: depende da fundação; pode andar em paralelo com a US2 (F3 e F4 em paralelo, conforme plan.md). T040 depende de T027.
- **US4 (Fase 6)**: depende de US2 e US3 (F5 depende de F3 e F4).
- **US6 (Fase 7)**: depende de T024 e T027.
- **US5 (Fase 8)**: depende de T024, de T071 (verificador do relatório) e da autorização de F2.
- **Acabamento (Fase 9)**: depende das histórias selecionadas.

### Dependências externas

As specs 025 a 029 consomem `IBusinessCapabilityGate` (T046) e só começam depois da US4.

### Dentro de Cada História

Testes escritos e falhando antes da implementação; verificador → persistência → serviço → endpoint → console.

---

## Exemplo de Paralelismo

```bash
# Depois de T002 e T003, em paralelo:
Task: "Vetores de conformidade em backend/tests/Dokpod.Licensing.Tests/Vectors/"   # T018
Task: "Leitor JWS em backend/libs/Dokpod.Licensing/JwsCompactReader.cs"            # T022
Task: "Parser estrito em backend/libs/Dokpod.Licensing/LicensePayloadParser.cs"    # T023
Task: "Testes do agente em backend/tests/Dokpod.Agent.Application.Tests/CpuCountSourceTests.cs"  # T030
Task: "Teste de integração com Docker real em backend/tests/Dokpod.Agent.Docker.IntegrationTests/CpuCountSourceDockerTests.cs"  # T066
Task: "Teste de integração com Podman real em backend/tests/Dokpod.Agent.Podman.IntegrationTests/CpuCountSourcePodmanTests.cs"  # T067
Task: "Testes de apuração em backend/tests/Dokpod.ControlPlane.Application.Tests/Licensing/CapacityMeteringTests.cs"  # T032
```

---

## Estratégia de Implementação

### MVP

1. Fases 1 e 2 (threat model revisado, composição por edição, migrations, scopes).
2. US1: CE comprovadamente intacta.
3. US2: licença importada e verificada offline.
4. **PARE E VALIDE** com os cenários 1 e 2 do quickstart.

O MVP ainda não libera nenhum recurso Business para venda: isso exige US3 e US4 (apuração e enforcement) e a emissão real (US5).

### Entrega Incremental

1. MVP (US1 + US2).
2. US3 e US4: apuração e tolerância; libera o gate para as specs 025 a 029.
3. US6: rotação e revogação, obrigatória antes da primeira venda.
4. US5: CLI, após autorização de F2.
5. Fase 9 e GO de F7.

---

## Observações

- Nenhuma tarefa cria repositório, publica imagem ou pacote, ou faz release sem pedido explícito.
- O trust store de produção nunca recebe a chave sintética dos vetores.
- Tarefas [P] atuam em arquivos distintos.
