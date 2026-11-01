# Plano de Implementação: Exportação de auditoria para SIEM

**Branch**: `027-exportacao-auditoria-siem` | **Data**: 2026-11-01 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação da funcionalidade em `/specs/027-exportacao-auditoria-siem/spec.md`

**Nota**: Este template é preenchido pelo comando `/speckit-plan`; sua definição descreve o fluxo de execução.

> Redija este plano e todos os artefatos derivados em português brasileiro (pt-BR). Preserve nomes de código, caminhos, comandos e identificadores técnicos.

## Resumo

A edição Business, com a capability `audit-siem-export` na licença, passa a enviar continuamente a trilha de auditoria a até 5 destinos, por syslog RFC 5424 sobre TLS ou por OTLP/HTTP. A fila durável não é uma tabela nova. Cada destino guarda um cursor sobre `audit_events`, que já é particionada por mês, na ordem de confirmação das transações (`xid8` com uma sequência). Isso dá "pelo menos uma vez" sem perder eventos confirmados fora de ordem. Um único remetente por destino é garantido por lease no PostgreSQL. Com a licença restrita, o envio continua e só criar, editar, reativar e testar destino são recusados. A configuração fica em Configurações, sob os scopes `audit-export:read` e `audit-export:manage`, e as credenciais vêm de secret montado.

## Contexto Tecnico

**Linguagem/Versão**: C# / .NET 10 (API, aplicação, infraestrutura); TypeScript / Angular 22 (seção em Configurações).

**Dependências Principais**: nenhum pacote novo. Syslog com `SslStream` da BCL ([D-004](research.md#d-004-syslog)); OTLP com `Google.Protobuf` e `Grpc.Tools`, já usados no repositório, a partir dos `.proto` oficiais do `opentelemetry-proto` (Apache-2.0), guardados em `contracts/vendor/opentelemetry-proto/` com a versão fixada ([D-005](research.md#d-005-opentelemetry)).

**Persistência**: PostgreSQL existente, schema `dokpod`. Colunas novas em `audit_events` sem reescrever partições; tabelas `audit_export_destinations` e `audit_export_cursors`, que não crescem com o volume ([data-model.md](data-model.md)).

**Testes**:
- unidades em `Dokpod.ControlPlane.Application.Tests`: formatação syslog e OTLP, filtros, estados, gate, janela de 72 h com relógio injetável;
- integração com PostgreSQL real em `Dokpod.ControlPlane.Api.Tests`: cursor sob transações concorrentes, lease entre duas instâncias, migration `Up`/`Down`;
- coletores reais em containers (`rsyslog` com TLS, `otel-collector`);
- `Dokpod.Architecture.Tests` (`BusinessGateTests`);
- E2E Playwright pela borda nginx;
- canário de segredos.

**Plataforma Alvo**: plano de controle em containers Linux atrás do nginx; só a imagem Business registra o módulo.

**Tipo de Projeto**: plano de controle self-hosted (API, BFF, web).

**Metas de Desempenho**: 95% dos eventos no destino em até 10 s (SC-001); lote de até 500 eventos ou 1 s; nenhum custo no caminho da ação além de duas colunas com padrão (SC-004); contagem da fila limitada a 10.000.

**Restrições**:
- TLS obrigatório, sem opção de desligar a verificação;
- endereços internos e de metadados recusados ([D-007](research.md#d-007-tls-e-destinos-permitidos));
- segredos nunca no banco, no console ou na API ([D-006](research.md#d-006-credenciais-do-destino));
- a trilha local nunca depende do envio (FR-004);
- a restrição da licença nunca para o envio (FR-008).

**Escala/Escopo**: até 5 destinos por instalação; trilha com centenas de milhares de eventos por mês na matriz de 100 hosts (ADR 2026-0003); janela de 72 h.

## Verificação da Constituição

*GATE: Deve ser aprovado antes da pesquisa da Fase 0 e revisado novamente após o design da Fase 1.*

| Princípio | Como o plano atende | Situação |
| --- | --- | --- |
| I. pt-BR | Artefatos em pt-BR; identificadores preservados | ok |
| II. Engine local e workloads | Não toca agente nem engine; falha do SIEM não afeta workloads nem a trilha | ok |
| III. Identidade externa e menor privilégio | Scopes `audit-export:read`/`audit-export:manage` no Keycloak, falha fechada; segredos fora do repositório, do banco e da telemetria | ok |
| IV. Operações duráveis | "Pelo menos uma vez" com `eventId` para deduplicação; cursor só avança depois da confirmação; criação idempotente (`Idempotency-Key`) e edição com `If-Match`; mudanças auditadas | ok |
| V. Modularidade e compatibilidade | Sem datastore, broker ou cache novos; OpenAPI versionado com `x-dokpod-edition`; migration expand-contract com `Down`; formato exportado versionado (`formatVersion`) | ok |
| VI. Qualidade por plataforma | Integração com PostgreSQL real e coletores reais; E2E pela borda; lease testado com duas instâncias | ok |
| Dependências permissivas | Nenhum pacote novo; `.proto` do `opentelemetry-proto` Apache-2.0, com versão fixada e licença conferida na tarefa | ok |
| Tabelas de alto crescimento particionadas | A fila é a própria `audit_events` (já mensal); tabelas novas com no máximo 5 linhas | ok |
| Threat model para fronteira nova | Conexões de saída configuráveis (SSRF), credenciais montadas e dados de auditoria fora da instalação: threat model é a primeira tarefa, antes de código | pendente (primeira tarefa) |
| ADR para decisão duradoura | Sem decisão de arquitetura nova além do que a spec 031 e o ADR 2026-0004 cobrem; o cursor por `xid8` fica no research (D-001) | ok |

Reavaliação após o desenho da Fase 1: sem violações; a tabela de complexidade fica vazia. O threat model segue como entrega obrigatória antes do código.

## Estrutura do Projeto

### Documentação desta Funcionalidade

```text
specs/027-exportacao-auditoria-siem/
├── spec.md
├── plan.md              # este arquivo
├── research.md          # D-001 a D-011
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── api.md           # rotas /api/v1/settings/audit-export/*
│   └── export-record.md # registro JSON, syslog e OTLP
├── checklists/requirements.md
└── tasks.md             # gerado por /speckit-tasks
```

### Código-Fonte (raiz do repositório)

```text
contracts/
├── openapi/dokpod-control-plane.v1.yaml           # rotas e schemas novos (x-dokpod-edition: business)
└── vendor/opentelemetry-proto/                     # .proto oficiais de logs, versão fixada (Apache-2.0)

backend/libs/Dokpod.ControlPlane.Application/AuditExport/
├── AuditExportPorts.cs           # destinos, cursor, leitor da trilha, transportes
├── AuditExportDestinationService.cs  # [BusinessCapability("audit-siem-export")] criar/editar/reativar/testar
├── AuditExportSender.cs          # lote, janela de 72 h, lacunas, estados
└── AuditExportRecord.cs          # projeção sanitizada (mesma da spec 017) e formatVersion

backend/libs/Dokpod.ControlPlane.Infrastructure/AuditExport/
├── AuditExportStore.cs           # destinos, cursor e lease
├── AuditTrailExportReader.cs     # leitura por (tx_id, export_seq) abaixo do xmin
├── SyslogTlsTransport.cs         # RFC 5424/5425
├── OtlpHttpTransport.cs          # OTLP/HTTP protobuf
├── DestinationAddressPolicy.cs   # filtros de D-007 com resolução DNS
└── MountedSecretReader.cs        # /run/secrets/dokpod-audit-export/<secretRef>/

backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/<data>_AuditExport/{Up,Down}/

backend/apps/Dokpod.ControlPlane.Api/AuditExport/
├── AuditExportEndpoints.cs       # rotas, scopes, problem+json
├── AuditExportModule.cs          # registrado só por ProductEdition (Business)
└── AuditExportWorker.cs          # BackgroundService com lease por destino

frontend/web/src/app/features/settings/
├── audit-export-api.service.ts
└── audit-export-section.component.{ts,html}  # visível só na Business com a capability

backend/tests/
├── Dokpod.ControlPlane.Application.Tests/AuditExport/
├── Dokpod.ControlPlane.Api.Tests/AuditExport/     # PostgreSQL real e coletores em containers
└── Dokpod.Architecture.Tests/BusinessGateTests.cs # já cobre o novo [BusinessCapability]

frontend/tests/e2e/   # test_027: jornada de configuração pela borda nginx (respostas simuladas)
deploy/e2e/           # coletores de teste rsyslog e otel-collector (perfil siem-test)
```

**Decisão de Estrutura**: segue a 031. Portas e regras ficam na aplicação; PostgreSQL, rede e TLS na infraestrutura; rotas e worker na API, num módulo que só o `ProductEdition` da Business registra, para que a imagem CE não exponha nada. O `BusinessGateTests` já obriga todo caso de uso marcado `[BusinessCapability]` a depender do gate. A categoria nova `audit_export` entra no `AuditCategories` existente.

## Registro de Complexidade

Sem violações a justificar.

## Itens para a revisão humana

- **FR-006 ([D-002](research.md#d-002-janela-de-72-horas-e-lacunas))**: aplicado na spec em 2026-11-01 (`/speckit-analyze`, I2): a lacuna vira evento de auditoria, aviso persistente na tela e métrica, e não regra do motor de alertas da 016.
- **Ordem ([D-009](research.md#d-009-ordem-lote-e-desempenho))**: a spec passou a pedir a ordem de gravação na trilha (I1). Entre transações concorrentes, ela pode diferir por milissegundos da ordem de `occurredAt`.
- **Transação longa ([D-001](research.md#d-001-cursor-na-ordem-de-confirmação-da-trilha))**: segura todos os destinos sem perda; risco aceito, com métrica e tela de atraso.
- **Licença sem a capability ([D-008](research.md#d-008-licença-e-composição))**: tratada como a restrição. O envio continua até o administrador desativar o destino.
- **Filtro de endereços ([D-007](research.md#d-007-tls-e-destinos-permitidos))**: redes privadas comuns são permitidas, e loopback, link-local e a rede do plano de controle são recusados.
- **Spec**: os nomes de implementação que o esclarecimento levou aos requisitos (`audit_events`, `[BusinessCapability]`, lease no PostgreSQL) já estão detalhados aqui. Ao aprovar este plano, a spec pode voltar a ficar só no nível de comportamento, e a checklist recupera os dois itens.
