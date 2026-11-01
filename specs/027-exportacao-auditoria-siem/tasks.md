---

description: "Tarefas da implementação da exportação de auditoria para SIEM"
---

# Tarefas: Exportação de auditoria para SIEM

**Entrada**: Documentos de design em `/specs/027-exportacao-auditoria-siem/`

> Redija todas as tarefas e descrições em português brasileiro (pt-BR), preservando identificadores, comandos e caminhos técnicos.

**Pré-requisitos**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/api.md](contracts/api.md), [contracts/export-record.md](contracts/export-record.md), [quickstart.md](quickstart.md)

**Testes**: obrigatórios (constituição, princípio VI): integração com PostgreSQL real, transportes contra servidores TLS reais, E2E pela borda nginx e canário de segredos.

**Organização**: agrupadas por história da spec para implementação e validação independentes.

**Gate**: nenhuma tarefa de código começa antes de T001 (threat model) aceito pelo revisor humano. `/speckit-implement` exige autorização humana explícita.

## Formato: `[ID] [P?] [Story] Descricao`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência pendente)
- **[Story]**: história da spec (US1, US2, US3)

## Convencoes de Caminhos

- Aplicação: `backend/libs/Dokpod.ControlPlane.Application/AuditExport/`
- Infraestrutura: `backend/libs/Dokpod.ControlPlane.Infrastructure/AuditExport/`
- API (só composição Business): `backend/apps/Dokpod.ControlPlane.Api/AuditExport/`
- Testes: `backend/tests/Dokpod.ControlPlane.Application.Tests/AuditExport/`, `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/`, `backend/tests/Dokpod.Architecture.Tests/`
- Frontend: `frontend/web/src/app/features/settings/`; E2E simulado: `frontend/tests/e2e/mock_ui/` e `frontend/tests/e2e/mock/control_plane_mock.py`

---

## Fase 1: Preparação (Infraestrutura Compartilhada)

**Objetivo**: fronteira de segurança aceita e permissões no Keycloak antes de qualquer código.

- [ ] T001 Threat model em `specs/027-exportacao-auditoria-siem/threat-model.md`, no formato do de `specs/031-licenciamento-business-offline/threat-model.md`. Cobrir:
  - conexões de saída configuráveis e SSRF (filtros de [D-007](research.md#d-007-tls-e-destinos-permitidos), inclusive DNS que muda entre validação e conexão);
  - secret montado e travessia de path ([D-006](research.md#d-006-credenciais-do-destino));
  - dados de auditoria fora da instalação;
  - repetição e deduplicação;
  - lease entre réplicas;
  - recusa de TLS inválido;
  - comportamento sob licença restrita (FR-008).

  Registrar lacunas, riscos residuais e a decisão do revisor humano. Bloqueia todas as tarefas de código.
- [ ] T002 [P] Scopes `audit-export:read` e `audit-export:manage` no recurso `urn:dokpod:installation` em `tools/scripts/configure-keycloak.ps1`, seguindo `licensing:read`/`licensing:manage` (linhas 234 e 498–517), com permissão para o grupo de administradores e teste do script com `-DryRun`.

---

## Fase 2: Fundação (Pré-requisitos Bloqueadores)

**Objetivo**: cursor transacional sobre a trilha, tabelas, regras compartilhadas e composição Business.

**CRÍTICO**: nenhuma história começa antes desta fase.

- [ ] T003 Migration `backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/<AAAAMMDDNNNN>_AuditExport/Up/01-AuditExport.sql` e `Down/01-AuditExport.sql` conforme [data-model.md](data-model.md):
  - **Colunas em `audit_events`:** `tx_id xid8` e `export_seq bigint`, adicionadas sem padrão e depois com `SET DEFAULT pg_current_xact_id()` e `SET DEFAULT nextval('dokpod.audit_export_seq')`, sem reescrever partições.
  - **Índice:** `ix_audit_events_export_order (tx_id, export_seq) WHERE tx_id IS NOT NULL` em cada partição existente, e a rotina de partições da migration `202609220002_ScheduleTemporalPartitions` ajustada para criá-lo nas futuras.
  - **`audit_export_destinations`:**
    - `name varchar(64)` obrigatório e único sem diferenciar maiúsculas;
    - `protocol varchar(16)` com `CHECK IN ('syslog','otlp')`;
    - `host varchar(253)`;
    - `port integer` com `CHECK BETWEEN 1 AND 65535`;
    - `path varchar(256)` só em `otlp`, começando com `/`;
    - `secret_ref varchar(64)` com `CHECK ~ '^[a-z0-9-]{1,64}$'`;
    - `categories text[]` não vazio;
    - `active boolean`, `version bigint`, `created_at_utc`, `updated_at_utc`;
    - `idempotency_key varchar(128)` e `idempotency_expires_at_utc` (criação + 24 h).
  - **`audit_export_cursors`:**
    - `destination_id` PK, com FK `ON DELETE CASCADE`;
    - `last_tx_id xid8`, `last_seq bigint`;
    - `last_sent_at_utc`, `last_error_code varchar(64)`, `last_error_at_utc`, `last_gap_at_utc`;
    - `lease_owner varchar(128)`, `lease_until_utc`.
  - **`Down`:** remove índices, padrões, colunas, sequência e tabelas.
- [ ] T004 [P] Teste de integração da migration em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/AuditExportMigrationTests.cs`, com PostgreSQL real:
  - o `relfilenode` das partições existentes não muda;
  - linhas novas recebem `tx_id` e `export_seq`, e as antigas ficam `NULL`;
  - uma partição criada pela rotina recebe o índice;
  - o `Down` validado por psql dentro de transação desfeita, como na 031.
- [ ] T005 [P] Categoria `audit_export` em `backend/libs/Dokpod.Domain/Auditing/AuditCategories.cs`, com as ações `audit_export.destination_created`, `audit_export.destination_updated`, `audit_export.destination_deleted`, `audit_export.test_sent` e `audit_export.gap`, e teste em `backend/tests/Dokpod.ControlPlane.Application.Tests/AuditExport/AuditExportCategoriesTests.cs`.
- [ ] T006 Portas e tipos em `backend/libs/Dokpod.ControlPlane.Application/AuditExport/AuditExportPorts.cs`:
  - `AuditExportDestination`, com as mesmas restrições do T003 validadas na aplicação;
  - `AuditExportCursor`;
  - `IAuditTrailExportReader`, que lê o lote após `(tx_id, seq)` abaixo do `xmin`, filtrado por categorias, com no máximo 500 itens;
  - `IAuditExportStore`, com CRUD, `If-Match`, limite de 5, lease e cursor;
  - `IAuditExportTransport`, que envia o lote e devolve confirmação ou código de erro (`tls_error`, `connection_refused`, `timeout`, `secret_missing`, `rejected`, `address_blocked`);
  - estados `disabled`, `ok`, `catching_up`, `failing` e `gap`.
- [ ] T007 Projeção `AuditExportRecord` (`formatVersion` 1) em `backend/libs/Dokpod.ControlPlane.Application/AuditExport/AuditExportRecord.cs`. Usa a mesma visão sanitizada que o CSV de `backend/apps/Dokpod.ControlPlane.Api/Auditing/AuditEndpoints.cs` (linhas 78–93), com os campos de [export-record.md](contracts/export-record.md); se a sanitização estiver no endpoint, extrair para a aplicação e usar nos dois. Teste em `backend/tests/Dokpod.ControlPlane.Application.Tests/AuditExport/AuditExportRecordTests.cs`: campos iguais aos do CSV e nenhum segredo ou conteúdo de terminal.
- [ ] T008 Leitor `AuditTrailExportReader` em `backend/libs/Dokpod.ControlPlane.Infrastructure/AuditExport/AuditTrailExportReader.cs`, com a consulta de [D-001](research.md#d-001-cursor-na-ordem-de-confirmação-da-trilha): `(tx_id, export_seq) > cursor AND tx_id < pg_snapshot_xmin(pg_current_snapshot())`, ordenado por `(tx_id, export_seq)`. Teste com PostgreSQL real em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/AuditTrailExportReaderTests.cs`: duas transações confirmando fora da ordem de `occurred_at_utc` não perdem nenhum evento, e uma transação aberta segura o cursor sem pular.
- [ ] T009 Store `AuditExportStore` em `backend/libs/Dokpod.ControlPlane.Infrastructure/AuditExport/AuditExportStore.cs`:
  - criação idempotente por `Idempotency-Key` e limite de 5 sob lock da tabela;
  - edição e exclusão com `version`;
  - o cursor nasce no `xmin` atual, para que o destino comece no fim da trilha;
  - lease com `UPDATE ... WHERE lease_until_utc < now()`, renovação e liberação; exclusão do destino durante o envio faz a renovação falhar e o remetente parar sem erro;
  - `Idempotency-Key` válida por 24 h;
  - contagem da fila limitada a 10.000.

  Testes com PostgreSQL real em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/AuditExportStoreTests.cs`.
- [ ] T010 [P] Política de endereços `DestinationAddressPolicy` em `backend/libs/Dokpod.ControlPlane.Infrastructure/AuditExport/DestinationAddressPolicy.cs`. Recusa loopback, link-local (inclusive `169.254.169.254`), `0.0.0.0`/`::` e as sub-redes do plano de controle (configuráveis), conferindo de novo depois da resolução DNS a cada conexão; redes 10/8, 172.16/12 e 192.168/16 são aceitas. Testes em `backend/tests/Dokpod.ControlPlane.Application.Tests/AuditExport/DestinationAddressPolicyTests.cs`, inclusive um nome que resolve para loopback.
- [ ] T011 [P] Leitor de secret `MountedSecretReader` em `backend/libs/Dokpod.ControlPlane.Infrastructure/AuditExport/MountedSecretReader.cs`. Lê de `/run/secrets/dokpod-audit-export/<secretRef>/` (`ca.pem`, `tls.crt`, `tls.key`, `token`), com `secretRef` validado por `^[a-z0-9-]{1,64}$` e caminho normalizado e conferido dentro da raiz antes de qualquer acesso. Expõe só a presença de cada arquivo para a API. Testes com filesystem real em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/MountedSecretReaderTests.cs`, inclusive tentativas de travessia e symlink para fora da raiz.
- [ ] T012 Módulo `AuditExportModule` em `backend/apps/Dokpod.ControlPlane.Api/AuditExport/AuditExportModule.cs`, registrado só em `backend/apps/Dokpod.ControlPlane.Api/ProductEdition.cs` sob `DOKPOD_BUSINESS`. Estender `backend/tests/Dokpod.Architecture.Tests/BusinessGateTests.cs` para que o código da CE também não referencie `Dokpod.ControlPlane.*.AuditExport`.
- [ ] T013 Contrato OpenAPI em `contracts/openapi/dokpod-control-plane.v1.yaml`:
  - as rotas `/api/v1/settings/audit-export/destinations`, `/{id}` e `/{id}/test`, com `x-dokpod-edition: business`;
  - os schemas `AuditExportDestinationInput`, `AuditExportDestination`, `AuditExportPending` e `AuditExportTestResult`;
  - os códigos de [contracts/api.md](contracts/api.md#códigos-de-problema).

  Validar com `openapi-spec-validator==0.7.1` e regenerar o cliente em `frontend/web/src/app/data-access/generated/control-plane/` (`npm run generate:api` no container `angular-cli`).

**Checkpoint**: trilha com cursor transacional, tabelas, políticas e contrato prontos; a CE continua sem o módulo.

---

## Fase 3: História de Usuário 1 - Enviar a auditoria ao SIEM (Prioridade: P1) MVP

**Objetivo**: o administrador configura um destino syslog com TLS, testa e passa a ver os eventos no SIEM, com filtros por categoria e o comportamento de licença de FR-007 e FR-008.

**Teste independente**: [quickstart, cenário 1](quickstart.md#cenário-1-envio-contínuo-por-syslog-jornada-1-sc-001) e [cenário 4](quickstart.md#cenário-4-licença-restrita-e-sem-capability-fr-007-fr-008-sc-005).

### Testes da História de Usuário 1

> Escreva os testes antes da implementação e confirme que falham para o comportamento ausente.

- [ ] T014 [P] [US1] Testes do formatador syslog em `backend/tests/Dokpod.ControlPlane.Application.Tests/AuditExport/SyslogFormatterTests.cs`, conforme [export-record.md](contracts/export-record.md#syslog-rfc-5424-sobre-tls-rfc-5425):
  - PRI com facility 10 e severidade 5 para sucesso e 4 para negado ou falha;
  - `TIMESTAMP` com microssegundos;
  - `APP-NAME` `dokpod`;
  - `MSGID` truncado em 32 caracteres ASCII;
  - `STRUCTURED-DATA` `-`;
  - `MSG` com BOM UTF-8 e o JSON;
  - enquadramento `LEN SP MSG`.
- [ ] T015 [P] [US1] Testes do `SyslogTlsTransport` contra um servidor TLS real em processo (`TcpListener` + `SslStream`, CA e certificados gerados na execução) em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/SyslogTlsTransportTests.cs`:
  - certificado válido entrega;
  - expirado, CA desconhecida e nome errado dão `tls_error`;
  - mTLS com `tls.crt`/`tls.key` funciona;
  - `secret_missing` quando o `ca.pem` exigido falta;
  - nada é enviado sem TLS.
- [ ] T016 [P] [US1] Testes das rotas em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/AuditExportEndpointsTests.cs`, com PostgreSQL real:
  - scopes `audit-export:read`/`audit-export:manage` e falha do Keycloak dando `503`;
  - `400` para propriedade desconhecida ou categoria inexistente;
  - `409 audit_export_limit_reached` no sexto destino e `409 audit_export_name_taken`;
  - `412` com `If-Match` velho;
  - `Idempotency-Key` repetida devolve o mesmo destino;
  - `422 audit_export_address_blocked` e `422 audit_export_invalid_secret_ref`;
  - com licença restrita: criar, editar, reativar e testar dão `403 license_expired`, e desativar e excluir funcionam;
  - licença sem a capability dá `403 license_invalid`;
  - auditoria das mudanças sem segredos.
- [ ] T017 [P] [US1] Teste ponta a ponta do envio em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/AuditExportDeliveryTests.cs`, com PostgreSQL real e servidor syslog TLS em processo:
  - eventos da trilha chegam com os mesmos campos;
  - categoria desmarcada não é enviada e continua na trilha;
  - mudar filtros não reenvia o passado;
  - destino novo começa no fim da trilha;
  - com a licença restrita, o envio continua (FR-008, SC-005).

### Implementação da História de Usuário 1

- [ ] T018 [US1] `SyslogTlsTransport` e formatador em `backend/libs/Dokpod.ControlPlane.Infrastructure/AuditExport/SyslogTlsTransport.cs`, conforme [D-004](research.md#d-004-syslog):
  - TLS 1.2/1.3 com validação de cadeia e nome, sem opção de desligar;
  - mTLS opcional;
  - `DestinationAddressPolicy` aplicada à conexão;
  - um lote por conexão, confirmado quando a escrita termina sem erro.
- [ ] T019 [US1] `AuditExportDestinationService` em `backend/libs/Dokpod.ControlPlane.Application/AuditExport/AuditExportDestinationService.cs`:
  - criar, editar, reativar e testar marcados `[BusinessCapability("audit-siem-export")]` e dependentes de `IBusinessCapabilityGate`;
  - desativar (editar só `active: false`) e excluir sem gate;
  - eventos `audit_export.destination_*` e `audit_export.test_sent` sem segredos;
  - o evento de teste (`audit_export.test`, `test: true`) não entra na trilha.
- [ ] T020 [US1] Rotas em `backend/apps/Dokpod.ControlPlane.Api/AuditExport/AuditExportEndpoints.cs`, seguindo `backend/apps/Dokpod.ControlPlane.Api/Licensing/LicensingEndpoints.cs`:
  - autorização pelo Keycloak antes de revelar destinos;
  - `ETag`/`If-Match`, `Idempotency-Key` e `problem+json`;
  - códigos de licença vindos de `BusinessCapabilityGate.DenialCode`;
  - a resposta traz `secretFiles` só como presença.
- [ ] T021 [US1] `AuditExportSender` em `backend/libs/Dokpod.ControlPlane.Application/AuditExport/AuditExportSender.cs` e `AuditExportWorker` (`BackgroundService`) em `backend/apps/Dokpod.ControlPlane.Api/AuditExport/AuditExportWorker.cs`. A cada ciclo, por destino ativo:
  - assume ou renova o lease (30 s);
  - lê até 500 eventos ou espera 1 s;
  - envia;
  - avança o cursor só depois da confirmação.

  O remetente não consulta o gate (FR-008).
- [ ] T022 [US1] Seção de exportação em Configurações:
  - `frontend/web/src/app/features/settings/audit-export-api.service.ts`;
  - `audit-export-section.component.ts` e `.html`, com lista, criação e edição (nome, protocolo `syslog`, host, porta, `secretRef`, categorias de `AuditCategories`), ativo/inativo, excluir e "Enviar evento de teste" com sucesso ou erro;
  - rótulos em `settings-labels.ts`;
  - visível só com `edition = business` e a capability na licença;
  - com a licença restrita, ações de criação, edição e teste desabilitadas com a explicação.

  Testes em `frontend/web/src/app/features/settings/audit-export-section.spec.ts`.
- [ ] T023 [US1] E2E pela borda nginx com respostas simuladas em `frontend/tests/e2e/mock_ui/test_027_audit_export.py`, com as rotas de [contracts/api.md](contracts/api.md) em `frontend/tests/e2e/mock/control_plane_mock.py`: criar destino, testar, desativar, limite de 5, licença restrita com ações bloqueadas e a seção ausente na CE.

**Checkpoint**: syslog com TLS funcionando, testável e entregável sozinho (MVP).

---

## Fase 4: História de Usuário 2 - Não perder eventos quando o destino cai (Prioridade: P1)

**Objetivo**: a fila durável pelo cursor, estados, lacunas além de 72 h e um único remetente entre réplicas.

**Teste independente**: [quickstart, cenário 2](quickstart.md#cenário-2-indisponibilidade-e-lacuna-jornada-2-sc-002) e [cenário 6](quickstart.md#cenário-6-várias-réplicas-e-lote-sc-004-sc-006).

### Testes da História de Usuário 2

- [ ] T024 [P] [US2] Testes de resiliência em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/AuditExportResilienceTests.cs`, com PostgreSQL real e relógio injetável:
  - destino fora: o cursor para, o estado vira `failing` com `last_error_code` e a fila conta até 10.000 e marca `capped`;
  - destino de volta: todos os eventos chegam na ordem de `(tx_id, export_seq)`, sem duplicar a trilha (SC-002);
  - pendente com mais de 72 h: o cursor pula, grava `audit_export.gap` com `fromUtc`, `toUtc` e `count`, e `last_gap_at_utc` é preenchido;
  - resposta `rejected` não avança o cursor;
  - espera exponencial até 60 s;
  - dois destinos, um fora e outro ativo: o ativo continua entregando;
  - uma transação longa aberta em outra conexão segura o cursor sem perder eventos, e o atraso do pendente mais antigo aparece na tela e na métrica (D-001).
- [ ] T025 [P] [US2] Teste de réplicas em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/AuditExportLeaseTests.cs`: duas instâncias do remetente no mesmo banco dão um só remetente por destino; matar a dona do lease faz a outra assumir em até 30 s; o lote não confirmado é reenviado com os mesmos `eventId` (SC-006).

### Implementação da História de Usuário 2

- [ ] T026 [US2] No `AuditExportSender`:
  - a janela de 72 h e as lacunas de [D-002](research.md#d-002-janela-de-72-horas-e-lacunas);
  - os estados de [data-model.md](data-model.md#estado-exibido-de-um-destino-calculado);
  - a espera exponencial até 60 s;
  - `rejected` (4xx que não seja 408 ou 429) sem avançar o cursor.

  Lease e cursor ficam no `AuditExportStore`.
- [ ] T027 [P] [US2] Métricas `dokpod.audit_export.pending`, `dokpod.audit_export.lag_seconds` (atraso do pendente mais antigo), `dokpod.audit_export.failures` e `dokpod.audit_export.gaps`, com rótulo do destino por ID, sem host nem segredo, em `backend/libs/Dokpod.ControlPlane.Application/Hosts/ControlPlaneMetrics.cs`. Logs só com código de erro e ID do destino.
- [ ] T028 [US2] Na seção de Configurações (`audit-export-section.component.*`): estado, último envio, tamanho da fila ("10.000+" quando `capped`), idade do mais antigo e aviso persistente de lacuna. Teste no `audit-export-section.spec.ts` e caso de destino indisponível com lacuna no `test_027_audit_export.py`.

**Checkpoint**: indisponibilidade e lacunas tratadas; sem perda com o destino fora até 72 h.

---

## Fase 5: História de Usuário 3 - Enviar por OpenTelemetry (Prioridade: P2)

**Objetivo**: destino OTLP/HTTP com protobuf, com os mesmos campos.

**Teste independente**: [quickstart, cenário 3](quickstart.md#cenário-3-opentelemetry-jornada-3).

### Testes da História de Usuário 3

- [ ] T029 [P] [US3] Testes do `OtlpHttpTransport` contra um receptor HTTPS em processo (Kestrel com certificado gerado na execução) em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/OtlpHttpTransportTests.cs`, conforme [export-record.md](contracts/export-record.md#otlphttp-protobuf-post-path-padrão-v1logs):
  - `Resource` e `ScopeLogs`;
  - `time_unix_nano`;
  - severidade `INFO`/`WARN`;
  - atributos `dokpod.*`;
  - cabeçalho `Authorization: Bearer` só com `token`;
  - 2xx confirma, 400 vira `rejected`, 429 e 503 tentam de novo;
  - TLS inválido dá `tls_error`.

### Implementação da História de Usuário 3

- [ ] T030 [US3] `.proto` de logs do `opentelemetry-proto`, numa versão fixada com licença Apache-2.0 conferida e registrada, com o `LICENSE` em `contracts/vendor/opentelemetry-proto/`. Compilar com `Grpc.Tools` em `backend/libs/Dokpod.ControlPlane.Infrastructure/Dokpod.ControlPlane.Infrastructure.csproj`, sem pacote novo.
- [ ] T031 [US3] `OtlpHttpTransport` em `backend/libs/Dokpod.ControlPlane.Infrastructure/AuditExport/OtlpHttpTransport.cs` (`HttpClient` com `SocketsHttpHandler`, validação TLS, `DestinationAddressPolicy` no `ConnectCallback` e `path` padrão `/v1/logs`) e seleção do transporte por `protocol` no `AuditExportSender`.
- [ ] T032 [US3] Protocolo `otlp` na seção de Configurações (campo `path` e texto do token no secret) em `audit-export-section.component.*`, com teste no `.spec.ts` e caso OTLP no `test_027_audit_export.py`.

**Checkpoint**: as três histórias funcionando de forma independente.

---

## Fase Final: Refinamento e Aspectos Transversais

- [ ] T033 [P] Script `tools/scripts/test-audit-export-collectors.ps1`, com `--help`, `-DryRun` e as regras de `.github/instructions/script-authoring.instructions.md`. Ele sobe em containers `rsyslog` com TLS e `otel-collector` com receptor OTLP/HTTP e saída em arquivo, em versões fixadas e com licença conferida, usando CA e certificados gerados na execução e descartados ao fim. Depois executa os cenários 1 e 3 do quickstart pela borda nginx da stack E2E Business, compara os `eventId` recebidos com a trilha e mede o tempo entre `occurredAt` e a chegada de cada registro, falhando se o p95 passar de 10 s (SC-001) e registrando a distribuição.
- [ ] T034 [P] Canário de SC-003 em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/AuditExportCanaryTests.cs`: um segredo e conteúdo de terminal plantados na origem de um evento não aparecem no registro enviado pelos dois transportes.
- [ ] T035 [P] Medição de SC-004 em `backend/tests/Dokpod.ControlPlane.Api.Tests/AuditExport/AuditExportBatchImpactTests.cs`: p95 de 30 execuções de uma ação em lote de 100 containers com a exportação ligada e desligada; falha se o p95 ligado passar de 105% do desligado (SC-004), com os dois valores na saída do teste.
- [ ] T036 Documentação:
  - novo `docs/runbooks/auditoria-exportacao-siem.md`, com configuração, secret montado, estados, lacuna, troca de certificado e desativação sob licença restrita;
  - linha da capability `audit-siem-export` em `docs/licenciamento.md`;
  - conexões de saída e filtro de endereços em `docs/seguranca.md`;
  - montagem de `/run/secrets/dokpod-audit-export/` em `deploy/e2e/docker-compose-dokpod.yaml` como exemplo comentado, sem valores.
- [ ] T037 Spec de volta ao nível de comportamento (incluindo o "lease no PostgreSQL" de FR-009): tirar de FR-003, FR-007 e FR-009 de `specs/027-exportacao-auditoria-siem/spec.md` os nomes de implementação que estão em [plan.md](plan.md) e [research.md](research.md), mantendo o comportamento, e remarcar os dois itens de `checklists/requirements.md`.
- [ ] T038 Revisão de segurança e privacidade contra o T001 (logs, métricas, traces sem host sensível nem segredo; filtros de endereço; TLS), registrada em `specs/027-exportacao-auditoria-siem/threat-model.md`.
- [ ] T039 Executar os seis cenários de [quickstart.md](quickstart.md) e registrar as evidências em `specs/027-exportacao-auditoria-siem/validation.md`, com `NOT RUN` explícito no que faltar.

---

## Dependências e Ordem de Execução

- **T001** bloqueia todo o código. **T002** é independente e só precisa estar pronta antes dos testes de rotas (T016).
- **Fundação (T003–T013)** depende de T001; bloqueia as histórias. T003 vem antes de T004, T008 e T009; T006 vem antes de T007 a T011.
- **US1 (T014–T023)** depende da fundação. É o MVP.
- **US2 (T024–T028)** depende de T021 (o remetente da US1), mas é testável sozinha sobre o syslog.
- **US3 (T029–T032)** depende da fundação e de T021; não depende da US2.
- **Refinamento** depende das histórias que forem entregues; T037 pode rodar a qualquer momento depois da aprovação do plano.

## Exemplos de Execução Paralela

- **Fundação**: T004, T005, T010 e T011 em paralelo, depois de T003 e T006.
- **US1**: T014, T015, T016 e T017 (testes) em paralelo; depois T018 e T019 em paralelo, e T020 a T023 em sequência.
- **US2**: T024 e T025 em paralelo; T027 em paralelo com T026.
- **US3**: T029 e T030 em paralelo; depois T031 e T032.
- **Refinamento**: T033, T034 e T035 em paralelo.

## Estratégia de Implementação

1. **MVP**: T001 → fundação → US1. Syslog com TLS, configuração, teste e comportamento de licença. Validar com o cenário 1 e o cenário 4 do quickstart.
2. **Incremento 2**: US2 (resiliência e lacunas), sem mudar contratos.
3. **Incremento 3**: US3 (OTLP), só um transporte novo e um campo na tela.
4. **Fechamento**: coletores reais, canário, medição de lote, documentação, revisão de segurança e evidências.
