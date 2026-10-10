# Plano de Implementação: Sessão BFF, Relay e Realtime

**Branch**: `chore/adotar-spec-kit` (migração) | **Data**: 2026-10-06 | **Spec**: [spec.md](spec.md)

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Entrada**: [P05](../../docs/plan/p05-bff-keycloak-relay.md) e recortes de P04/P03/MVP.

## Resumo

Validar a diferença entre requisitos e comportamento existente. Não recriar sessão, relay ou hub com base em notas antigas: P05 documenta implementações parciais e o MVP registra jornadas E2E posteriores. Segurança negativa e recuperação ainda precisam de inventário de evidências por cenário.

## Contexto Técnico

**Linguagem/Versão**: .NET 10 e Angular 22.

**Dependências Principais**: BFF OIDC, Keycloak Authorization Services, SignalR e cliente OpenAPI existentes.

**Armazenamento**: Ticket server-side e key ring persistente; PostgreSQL não recebe tokens.

**Testes**: BFF/API, Angular, contratos e Playwright com Keycloak real pelo nginx containerizado.

**Plataforma Alvo**: Containers; browser same-origin pela borda pública.

**Tipo de Projeto**: Web modular com BFF confidencial e API PEP.

**Objetivos de Desempenho**: Limites de transporte revisados antes da validação; carga agregada em 005, sem thresholds inventados.

**Restrições**: Sem tokens no browser, política local, proxy genérico, réplicas sem ADR ou bypass do BFF.

**Escopo**: Sessão, relay REST/SignalR, frontend e cenários negativos; topologia em 004.

## Verificação da Constituição

Gate formal pendente de revisão Spec Kit, não declarado aprovado. Preservar [segurança](../../docs/seguranca.md), [BFF](../../docs/bff.md) e [arquitetura](../../docs/arquitetura.md). A aprovação para implementação narrada em P05-01 (2026-09-13) é histórica, não aprovação destes rascunhos.

## Estrutura do Projeto

### Documentação desta Feature

[spec.md](spec.md), [plan.md](plan.md) e [tasks.md](tasks.md); artefatos adicionais somente após revisão de necessidade.

### Código-fonte

`backend/apps/Dokpod.Bff/`, `backend/apps/Dokpod.ControlPlane.Api/`, `backend/tests/Dokpod.Bff.Tests/`, `frontend/web/src/app/`, `frontend/tests/`, `contracts/openapi/` e `deploy/e2e/` são os módulos existentes a confrontar, não autorização de edição nesta migração.

**Decisão de Estrutura**: Uma única tarefa proprietária por recorte; 001 cuida de ambientes/auditoria, 003 do estado durável e 004 da borda.

## Rastreabilidade da Migração

| Origem | Destino | Tratamento |
| --- | --- | --- |
| P05-01 | T002/T008 | Contrato de confiança, threat model, audience/TTL e instância única |
| P05-02 | T003 | Fundação já existente; refresh, sessão e logs a validar |
| P05-03 | T004 | Relay REST já registrado; conferir limites, headers e egress |
| P05-04 | T005 | Hub/relay já registrados; validação de transporte e isolamento |
| P05-05 | T004/T005 | API PEP e eventos; identidade em 001 e lifecycle em 003 |
| P05-06 | T006 | Angular existente; conferir antiforgery e ausência de credenciais |
| P05-07 | T007 e 004 T004/T005 | Borda, secrets e persistência; ownership físico em 004 |
| P05-08 | T003-T007 | Cenários E2E, falhas, recuperação e inspeção sanitizada |
| P05-09 | T008 e 005 T007 | Documentação e escala; gate de release em 005 |
| P04-05 | T004/T005 | Mesmo relay, sem nova implementação duplicada |
| P04-06 | T006 | Mesma integração Angular |
| P04-07 | T003-T007 | Mesma jornada real, sem suíte concorrente |
| MVP P-03; P03-07 | T003/T005/T007 | Recorte sessão/CSRF/horizontal; identidade em 001 |

## Lacunas e Estratégia

Callback/state/nonce, refresh concorrente/rejeitado, expiração, reinício do BFF, indisponibilidade e acesso direto não são encerrados por login positivo. P05-07/P05-08 `not-started` coexistem com evidências E2E posteriores: preservar, avaliar diferença e obter revisão humana. Não alterar ADR nem habilitar réplicas. Tarefas aprovadas futuramente complementam apenas cenários ausentes e revalidam o residual.
