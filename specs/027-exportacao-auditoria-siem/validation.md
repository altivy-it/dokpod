# Validação: Exportação de auditoria para SIEM

**Spec**: 027, tarefa T039 · **Data**: 2026-11-01 · **Origem**: IA assistida · **Estado**: evidências registradas; aguarda revisão humana

Execução dos seis cenários do [quickstart](quickstart.md). Onde o cenário pede a stack E2E Business com Keycloak, a
evidência vem de outra camada (testes de integração com PostgreSQL 17.6 real, E2E simulado pela borda nginx ou o
script dos coletores) e o passo na stack real fica como `NOT RUN`, com o motivo.

## Por que parte da stack real não rodou

- A stack de `deploy/e2e` deste host é CE e depende da plataforma de identidade externa (redes e secrets próprios).
- A imagem Business só aceita licença assinada pela chave do cofre do fornecedor (trust store `dokpod-2026-1`); chave de
  teste no trust store só num build local, que este ciclo não montou.
- Por isso o script da T033 usa a imagem Business real sem licença (o envio não consulta a licença, FR-008), numa rede
  interna própria, e grava os destinos direto no PostgreSQL como a API faria.

## Ambiente

| Item | Valor |
| --- | --- |
| Commit base | `075ea74` (main) + branch `feat/027-final` |
| PostgreSQL | 17.6 em container, banco limpo |
| Imagem da API | `Business.Dockerfile`, alvo `runtime`, build local |
| Coletores | `rsyslog/rsyslog-collector:2026-10` (rsyslog 8.2610.0, TLS por `ossl`) e `otel/opentelemetry-collector-contrib:0.139.0` (OTLP/HTTP com TLS e token Bearer) |
| Relatório do script | `artifacts/audit-export-collectors/report.json` (fora do Git) |

## Resultado por cenário

| Cenário | Passo | Resultado | Evidência |
| --- | --- | --- | --- |
| 1. syslog (SC-001) | 1. Evento de teste chega ao coletor | Aprovado (integração) / `NOT RUN` na stack real | `AuditExportEndpointsTests`, `SyslogTlsTransportTests` |
| | 2–3. 50 ações; 95% em até 10 s; `eventId` e campos iguais à trilha | **Aprovado** | T033: 50/50 recebidos, 0 duplicados, 0 divergências de campo; latência p50 0,72 s, p95 1,20 s, máx 1,25 s |
| | 4. Desmarcar `containers`: ações não chegam e ficam na trilha | **Aprovado** | T033: 5 ações, 0 recebidas nos dois coletores, 5 na trilha; `AuditExportDeliveryTests` |
| 2. Indisponibilidade e lacuna (SC-002) | 1–2. Destino fora: `failing`, fila; volta: tudo chega uma vez, em ordem | Aprovado (integração) / `NOT RUN` com coletor real | `AuditExportResilienceTests.RejectedDestination_HoldsTheCursor_ThenDeliversEverythingInOrderOnce`, `OneDestinationDown_DoesNotHoldTheOther`, `LongTransaction_HoldsTheCursorWithoutLosingEvents_AndShowsTheLag` |
| | 3. Pendente com mais de 72 h vira lacuna auditada e aviso | Aprovado | `PendingOlderThan72Hours_IsSkippedAsAGap_AndAudited` (relógio injetável); E2E simulado `test_027` (aviso) |
| 3. OpenTelemetry | Cenário 1 por `otlp`, atributos `dokpod.*` | **Aprovado** | T033: 50/50, 0 duplicados, 0 divergências; latência p50 0,48 s, p95 0,97 s, máx 1,01 s; token Bearer validado pelo coletor; `OtlpHttpTransportTests` (mapeamento de campos) |
| 4. Licença (FR-007, FR-008, SC-005) | 1–2. Restrita: envio continua; criar, editar, reativar e testar `403 license_expired`; desativar e excluir funcionam | Aprovado (integração) / `NOT RUN` na stack real | `AuditExportDeliveryTests.RestrictedLicense_KeepsSending`, `AuditExportEndpointsTests.RestrictedLicense_BlocksCreateEditReactivateAndTest_ButNotDeactivateOrDelete` |
| | 3. Sem a capability: `403 license_invalid`; destino existente continua | Aprovado (integração) | `LicenseWithoutTheCapability_IsLicenseInvalid` |
| | 4. Imagem CE: sem seção e sem rotas | Aprovado | `BusinessGateTests`; E2E simulado `test_031` (CE sem seção Business) |
| 5. Autorização e segurança (FR-005, SC-003) | 1. Sem `audit-export:read`/`manage`: recusado; falha do Keycloak nega | Aprovado (integração) / `NOT RUN` com Keycloak real | `Routes_AskTheKeycloakForTheRightScope_AndFailClosed` |
| | 2. Loopback, `169.254.169.254` e nome que resolve para loopback: `422` | Aprovado | `ForbiddenAddressOrSecretRef_Is422`, `DnsDestinationAddressResolverTests` |
| | 3. Certificado do coletor expirado: nada enviado, `tls_error` | Aprovado | `SyslogTlsTransportTests` (`expired`) |
| | 4. Canário: segredo e conteúdo de terminal não chegam | **Aprovado** | T034 `AuditExportCanaryTests` (sessão de terminal real, trilha PostgreSQL, syslog e OTLP) |
| | Extra (T038): host do SIEM fora de métricas e traces da API | **Aprovado** depois da correção A1 | `AuditExportTelemetryTests` |
| 6. Réplicas e lote (SC-004, SC-006) | 1. Duas réplicas: um envio por destino; dono parado é substituído e repete o mesmo `eventId` | Aprovado (integração) / `NOT RUN` com duas réplicas reais | `AuditExportLeaseTests.TwoReplicas_DeliverEachEventOnce`, `DeadOwner_IsReplacedAfterTheLease_AndTheBatchIsResentWithTheSameIds` |
| | 2. Lote de 100, 30 + 30 vezes: p95 ligada no máximo 5% acima | **Aprovado** depois da correção A2 | T035 `AuditExportBatchImpactTests`: com o PostgreSQL limitado a 1 CPU, mediana das razões de 100,7% a 102,6% em 3 execuções; ver as notas abaixo |

## Suítes

| Suíte | Resultado |
| --- | --- |
| Domain / Licensing / Architecture / Bff | 166 / 81 / 14 / 29 aprovados |
| Agent.Application / Agent.Infrastructure | 137 / 31 aprovados |
| ControlPlane.Application | 273 aprovados |
| ControlPlane.Api (PostgreSQL real) | 313 aprovados, 2 ignorados (o SC-004, que roda isolado, e um fora da 027); 82 da exportação |
| E2E simulado (`-k "027 or 031"`) | 9 aprovados |
| `tools/scripts/test-audit-export-collectors.ps1` | `pass` (`--help`, `-DryRun` e erro de uso conferidos) |

## Notas

- **SC-004**: só com a exportação desligada, o p95 de 30 amostras varia uns 8% entre execuções (183 a 198 ms), mais
  que a margem de 5%. Numa comparação isolada o teste falhou 1 vez em 4 (108%) com medianas iguais. O teste roda a
  comparação de 30 + 30 cinco vezes e usa a mediana das cinco razões; o critério continua sendo 105%. Na suíte completa,
  com o banco carregado pelos outros testes, uma execução deu 118,9%; por isso a medição roda só isolada
  (`DOKPOD_TEST_SC004=1 dotnet test backend/tests/Dokpod.ControlPlane.Api.Tests --filter AuditExportBatchImpactTests`)
  e o CI a ignora. **Decisão pendente de revisão humana**: aceitar a medição isolada como evidência do SC-004 ou definir
  um ambiente dedicado para ela.
- **A2 (desempenho, corrigido)**: no CI (runner de 2 vCPU com o PostgreSQL no mesmo host), o teste do SC-004 deu
  132%. As leituras do remetente em `audit_events` não podam partições (o cursor é por transação, não por data), e com
  as 133 partições mensais o planejamento custava de 10 a 40 ms por consulta, várias por ciclo, a cada segundo e por
  destino. Duas correções: as leituras agora são preparadas (depois de cinco execuções o PostgreSQL usa o plano
  genérico e não replaneja), e a busca de lacuna só roda quando o primeiro evento lido passou de 72 h, em vez de em todo
  ciclo. Com o banco carregado pela suíte completa, a mediana de um ciclo caiu de 37 ms para 21,6 ms (p95 de 65 para
  48 ms). O custo que resta (cerca de 13 ms por leitura) é abrir e bloquear as 133 partições e seus índices: limitar a
  busca por data o eliminaria, mas perderia eventos com relógio adiantado, porque a escrita na trilha não recusa datas
  futuras. Fica registrado para avaliação, junto com a criação de partições até 2037.
- **Migrations por `psql`**: aplicar o SQL das migrations com a sessão no fuso do servidor (`America/Sao_Paulo`) falha
  por sobreposição de partições; com `PGTZ=UTC` funciona. A API e o `dotnet ef` aplicam no mesmo servidor sem erro. O
  achado é anterior à 027 e fica registrado para avaliação; o script fixa `PGTZ=UTC`.
- **rsyslog em host Ubuntu**: o perfil AppArmor `rsyslogd` do host se aplica ao binário do container pelo caminho e
  recusa sinais do runtime (o container não para). O script inicia o rsyslogd pelo loader dinâmico.
