# Plano de Implementação: Licenciamento Business offline

**Branch**: `031-licenciamento-business-offline` | **Data**: 2026-10-29 | **Spec**: [spec.md](spec.md)

**Entrada**: Especificação da funcionalidade em `/specs/031-licenciamento-business-offline/spec.md`

**Nota**: Este template é preenchido pelo comando `/speckit-plan`; sua definição descreve o fluxo de execução.

> Redija este plano e todos os artefatos derivados em português brasileiro (pt-BR). Preserve nomes de código, caminhos, comandos e identificadores técnicos.

## Resumo

A edição Business passa a aceitar uma licença assinada (JWS `ES256`, perfil fechado) verificada na própria instalação por uma biblioteca .NET sem I/O (`Dokpod.Licensing`, MIT proposta). O plano de controle guarda revisões, estado efetivo com prazos de tolerância e observações de capacidade por host, e apura nodes equivalentes pelo maior total simultâneo em 30 dias. Recursos exclusivos Business consultam `IBusinessCapabilityGate` depois da autorização do Keycloak; nenhum estado de licença afeta workloads, dados ou a CE. A emissão fica numa CLI em repositório separado, que consome o mesmo verificador. A edição é fixada no build (D-009): a imagem CE não registra o módulo.

Este plano não autoriza código: FR-030 (revisão jurídica) e FR-031 (aceite do ADR 2026-0007) são gates antes de `/speckit-implement`.

## Contexto Tecnico

**Linguagem/Versão**: C# / .NET 10 (backend, agente, verificador e CLI); TypeScript / Angular 22 (tela de licença em Configurações).

**Dependências Principais**: só BCL no verificador (`System.Security.Cryptography.ECDsa`, `System.Text.Json`); dependências existentes no plano de controle; nenhuma dependência nova no produto. CLI: armazenamento transacional local a selecionar em F2 (SQLite proposto), sujeito à política de licenças.

**Persistência**: PostgreSQL existente, schema `dokpod`, migrations SQL em `backend/libs/Dokpod.ControlPlane.Infrastructure/Migrations/Script/`; observações de capacidade e eventos de licenciamento particionados por mês ([data-model.md](data-model.md)).

**Testes**: vetores de conformidade do verificador (xUnit, sem I/O); `Dokpod.ControlPlane.Application.Tests` (estados, prazos, apuração com relógio injetável); integração PostgreSQL real em `Dokpod.ControlPlane.Api.Tests`; `Dokpod.Architecture.Tests` (CE não referencia o módulo; verificador sem dependências do produto); E2E Playwright pela borda nginx; captura de rede sem egress (D-012); inspeção de imagens.

**Plataforma Alvo**: plano de controle em containers Linux atrás do nginx; agentes Docker/Podman Linux e Windows (campo novo do `AgentHello`).

**Tipo de Projeto**: plano de controle self-hosted (API, BFF, web), biblioteca independente e CLI externa.

**Metas de Desempenho**: verificação de uma licença ≤ 64 KiB abaixo de 50 ms; status em até 2 cliques (SC-007); apuração proporcional ao número de mudanças de capacidade na janela, não ao tempo.

**Restrições**: zero comunicação externa do licenciamento (FR-002); nenhum efeito sobre workloads, dados e CE (FR-003); `cpu_count` tag 10 inalterada e N/N-1 (FR-017); recusa de licença nunca é `401` (FR-024); nenhum emissor ou chave privada em imagem do cliente (FR-028).

**Escala/Escopo**: até 100 hosts por instalação na matriz atual (ADR 2026-0003), 2 slots, ≤ 64 capabilities; observações de capacidade gravadas só na mudança, retenção ≥ 35 dias.

## Verificação da Constituição

*GATE: Deve ser aprovado antes da pesquisa da Fase 0 e revisado novamente após o design da Fase 1.*

| Princípio | Como o plano atende | Situação |
| --- | --- | --- |
| I. pt-BR | Todos os artefatos em pt-BR; identificadores preservados | ok |
| II. Engine local e workloads preservados | Licença nunca para containers nem agentes; observação de CPU é leitura do `Hello`; falha de medição não derruba sessão | ok |
| III. Identidade externa e menor privilégio | Scopes `licensing:read`/`licensing:manage` no Keycloak (D-008); licença não supre permissão (FR-022); nada de roles locais; telemetria sem conteúdo de licença (FR-026) | ok |
| IV. Operações duráveis | Importação atômica e idempotente com `If-Match`; operações já aceitas concluem após restrição (FR-021); emissão idempotente na CLI | ok |
| V. Modularidade e compatibilidade | Verificador como biblioteca sem dependências do produto; OpenAPI versionado; campo protobuf aditivo; migrations expand-contract; sem datastore novo no produto | ok |
| VI. Qualidade por plataforma | Integração PostgreSQL real, E2E pela borda, prova de rede, Docker/Podman/Windows testados separadamente para a origem da CPU | ok |
| Dependências permissivas | Nenhuma nova no produto; CLI decide em F2 com verificação de licença | ok, pendente F2 |
| Threat model para novas fronteiras | Importação de arquivo e trust store são fronteira nova: threat model é entrega da fase F1, antes de código | pendente F1 |
| ADR para decisão duradoura | Formato de licença e verificação offline viram ADR `proposed` a partir de D-001, D-002 e D-009 | a criar |

Reavaliação após o desenho da Fase 1: sem violações; a tabela de complexidade fica vazia. Dois itens dependem de entregas futuras (threat model em F1, dependências da CLI em F2).

## Estrutura do Projeto

### Documentação desta Funcionalidade

```text
specs/031-licenciamento-business-offline/
├── spec.md                       # Requisitos, histórias e matriz CE/Business
├── plan.md                       # Este arquivo
├── research.md                   # Decisões D-001 a D-012
├── data-model.md                 # Licença, tabelas, estados e transições
├── quickstart.md                 # Cenários de validação ponta a ponta
├── contracts/
│   ├── license-format.md         # Perfil JWS, payloads e falhas tipadas
│   ├── licensing-api.yaml        # Fragmento OpenAPI (composição Business)
│   ├── agent-cpu-source.md       # Campo aditivo do AgentHello
│   └── cli-commands.md           # CLI de emissão (repositório separado)
├── checklists/requirements.md
└── tasks.md                      # Gerado por /speckit-tasks
```

### Código-Fonte (raiz do repositório)

```text
backend/
├── libs/
│   ├── Dokpod.Licensing/                          # novo: verificador BCL-only, MIT proposta (D-002)
│   ├── Dokpod.ControlPlane.Application/Licensing/ # portas: ILicenseStateReader, IBusinessCapabilityGate,
│   │                                              #   ILicenseStore, ICapacityLedger, ILicensingAuditWriter
│   └── Dokpod.ControlPlane.Infrastructure/
│       ├── Licensing/                             # adaptadores, apuração, relógio confiável
│       └── Migrations/Script/<ts>_Licensing/      # tabelas e partições de data-model.md
├── apps/
│   ├── Dokpod.ControlPlane.Api/                   # composição Business, endpoints de licensing,
│   │   └── Agents/AgentHostActivity.cs            #   observação de capacidade no início da sessão
│   └── Dokpod.Agent/AgentControlWorker.cs         # preencher cpu_count_source
└── tests/                                         # vetores, aplicação, integração, arquitetura
contracts/
├── agent/v1/agent.proto                           # CpuCountSource (aditivo)
└── openapi/dokpod-control-plane.v1.yaml           # paths /api/v1/licensing/*
frontend/web/src/app/features/settings/            # seção Licença (só Business)
tools/scripts/configure-keycloak.ps1               # recurso urn:dokpod:installation e scopes licensing:*
deploy/                                            # composição e imagem Business com trust store
```

Fora deste repositório: `dokpod-license-admin` (CLI), criado só na fase F2 autorizada.

**Decisão de Estrutura**: o verificador fica isolado em `backend/libs/Dokpod.Licensing` para ser consumido pela CLI sem código AGPL. A Application define as portas e a regra de direitos; a Infrastructure implementa persistência e apuração; a API compõe apenas na imagem Business (D-009). Casos de uso CE não recebem dependência nova.

## Fases de entrega (do plano de origem)

| Fase | Entrega | Gate de saída |
| --- | --- | --- |
| F0 | Esta spec, matriz CE/Business, este plano e o ADR `proposed` | revisão humana da spec e do ADR |
| F1 | Contratos (este diretório), threat model, revisão de segurança e privacidade | aceite do ADR; revisão jurídica (FR-030) |
| F2 | Repositório da CLI (nome, licença, visibilidade, responsáveis, armazenamento, provedor de chave) | autorização explícita de criação |
| F3 | Verificador, vetores e emissão na CLI | vetores independentes, recuperação e separação da chave |
| F4 | Hosts, origem da CPU, observações, janela | PostgreSQL real, N/N-1, cálculo simultâneo comprovado |
| F5 | Estados, direitos, API, Keycloak, auditoria global | nenhum bypass por API direta; baseline CE preservado |
| F6 | Tela de Licença, composições e imagens CE/Business | E2E, inspeção de artefatos |
| F7 | Runbooks, rede isolada, recuperação, evidências | GO humano específico; publicação exige pedido separado |

F3 e F4 podem andar em paralelo após F1. F5 depende de F3 e F4. As specs 025 a 029 consomem `IBusinessCapabilityGate` de F5.

## Registro de Complexidade

Sem violações da constituição a justificar.
