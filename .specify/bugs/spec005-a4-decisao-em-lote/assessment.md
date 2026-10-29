# Avaliação de Bug: leituras do console fazem uma decisão UMA por ambiente (spec 005, A4)

- **Slug**: spec005-a4-decisao-em-lote
- **Criada em**: 2026-10-29
- **Origem**: achado A4 de [specs/005 plan.md](../../../specs/005-qualificacao-release-docker-linux/plan.md#achados-da-vm) e ordem de pendências aprovada pelo solicitante em 2026-10-28 (item 10)
- **Veredito**: valid
- **Severidade**: low

## Relato (literal ou resumido)

Na VM isolada da spec 005, o trace `8c931ccb…` de uma leitura do console mostrou 103 chamadas ao Keycloak com 101 ambientes: uma decisão UMA (`response_mode=decision`) por ambiente. Ficou dentro dos limites, mas o custo cresce com o número de ambientes.

## Sintoma

Cada leitura que filtra os ambientes por um scope dispara `N` chamadas paralelas ao endpoint de token do Keycloak, uma por ambiente legível.

## Reprodução

1. Usuário com acesso a 101 ambientes (VM da spec 005).
2. Abrir o console: `GET /api/v1/settings/access` (carregado em toda página), resumo de alertas ou de auditoria.
3. O trace mostra uma chamada `uma-ticket` com `response_mode=decision` por ambiente.

## Caminhos de Código Suspeitos

Leituras que fazem `Task.WhenAll` de `IEnvironmentAuthorizationDecider.DecideAsync` sobre todos os ambientes:

- `SettingsEndpoints.AuthorizeAsync` (`environment:manage` em algum host: acesso, instalação e parâmetros);
- `AuditQueryService.ReadableAsync` (`audit:read`: resumo, lista, exportação e relacionados);
- `AlertGovernanceService.FilterAsync` nas leituras (resumo, lista, regras, silenciamentos, políticas);
- `RegistryPolicyService.ManagedAsync` na lista.

## Hipótese de Causa Raiz

Confiança: high. Essas leituras são anteriores à spec 023, que criou o `IEnvironmentGrantReader`: uma chamada UMA com `response_mode=permissions` que devolve de uma vez os pares ambiente × scope concedidos. Elas continuaram decidindo ambiente por ambiente.

## Correção Proposta

**Preferencial**: filtrar as leituras com uma só consulta de grants (`EnvironmentGrants.WithScopeAsync`), com indisponível contando como negado, como antes. Ações (silenciar, reconhecer, salvar regra, política ou registries) e o detalhe de um alerta continuam decididos um a um com `response_mode=decision`.

**Alternativas**:

- Uma chamada UMA com vários `permission` (`recurso#scope`) e `response_mode=permissions`: também uma chamada, mas cria um segundo formato de consulta; o leitor de grants já está validado na VM.
- Cache de decisões por requisição/usuário: reduz chamadas repetidas, não o fan-out da primeira leitura, e adiciona invalidação.

**Arquivos Prováveis de Alteração**:

- `backend/libs/Dokpod.ControlPlane.Application/Authorization/EnvironmentGrants.cs`
- `backend/libs/Dokpod.ControlPlane.Application/Auditing/AuditQueryService.cs`
- `backend/libs/Dokpod.ControlPlane.Application/Alerts/AlertGovernanceService.cs`
- `backend/libs/Dokpod.ControlPlane.Application/Registries/RegistryPolicyService.cs`
- `backend/apps/Dokpod.ControlPlane.Api/Environments/SettingsEndpoints.cs`

**Testes a Adicionar ou Atualizar**:

- Unidade do filtro em lote (uma consulta para 101 hosts, provedor indisponível = negado, nenhuma chamada sem candidatos).
- Fakes dos testes de alertas, auditoria e registries passam a responder também como leitor de grants, com as mesmas concessões.

## Riscos e Considerações

- Mesma avaliação de policies do Keycloak nos dois modos; a resposta `permissions` é calculada no momento da requisição, sem cache.
- A métrica `AuthorizationDecided` deixa de contar as leituras em lote (o leitor de grants registra uma linha de log por consulta).

## Perguntas em Aberto

- Nenhuma.
