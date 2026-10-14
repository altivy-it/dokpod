# Especificação da Feature: Sessão BFF, Relay e Realtime

**Branch da migração**: `chore/adotar-spec-kit`; branch de implementação a definir após autorização.

**Criada em**: 2026-10-06

**Estado**: `draft` - rascunho de migração, aguardando revisão humana.

**Entrada**: [P05](../../docs/plan/p05-bff-keycloak-relay.md), recortes do [P04](../../docs/plan/p04-padronizacao-dockerfiles-compose.md), [P03](../../docs/plan/p03-auditoria-persistente.md) e [MVP](../../docs/plan/mvp.md).

## Cenários do Usuário e Testes (obrigatório)

### Jornada do Usuário 1 - Manter sessão sem expor tokens (Prioridade: P1)

Como usuário, quero login, renovação e logout seguros, sem credenciais no browser.

**Valor desta prioridade**: Tokens expostos comprometem a fronteira de confiança.

**Teste independente**: Inspecionar sessão e rede de usuário sintético durante refresh concorrente, expiração, reinício do BFF e logout.

**Cenários de aceite**:

1. **Dado** login OIDC, **Quando** a sessão é consultada, **Então** só há projeção sanitizada e cookie técnico `Secure`, `HttpOnly`, host-only, com `SameSite` explícito.
2. **Dado** refresh rejeitado ou Keycloak indisponível, **Quando** a sessão é usada, **Então** há falha fechada, sem fallback local ou credenciais na resposta.
3. **Dado** callback, nonce/state ou returnUrl inválido, **Quando** o fluxo retorna, **Então** é rejeitado sem redirect aberto.

### Jornada do Usuário 2 - Operar pela fronteira autorizada (Prioridade: P1)

Como usuário, quero consultar e operar ambientes somente pelo relay do BFF.

**Valor desta prioridade**: A API deve aplicar a decisão externa antes de revelar ou alterar dados.

**Teste independente**: Capturar requisição downstream e exercitar mutação, erro, timeout e tentativa de contornar o BFF.

**Cenários de aceite**:

1. **Dado** Authorization/Cookie fornecidos pelo browser, **Quando** ocorre relay, **Então** são descartados; o Bearer é inserido somente no salto BFF -> API e `Set-Cookie` downstream não retorna.
2. **Dado** mutação sem antiforgery ou Origin permitida, **Quando** enviada, **Então** é rejeitada antes do downstream.
3. **Dado** ambiente negado, **Quando** consultado, **Então** a API PEP não revela metadados; 401/403 de AJAX não se tornam login silencioso.

### Jornada do Usuário 3 - Receber invalidações autorizadas (Prioridade: P1)

Como operador, quero realtime por ambiente, com reconexão e fallback REST limitado.

**Valor desta prioridade**: Eventos não podem criar um canal de acesso horizontal.

**Teste independente**: Negotiate, upgrade, encerramento e reconexão com ambientes permitidos/negados.

**Cenários de aceite**:

1. **Dado** acesso negado, **Quando** ocorre inscrição SignalR, **Então** a conexão não ingressa no grupo do ambiente.
2. **Dado** invalidação, **Quando** recebida, **Então** o browser busca estado durável autorizado por REST; nenhuma credencial é enviada em query ou `accessTokenFactory`.
3. **Dado** desconexão/timeout, **Quando** o hub falha, **Então** ambos os pumps encerram e o usuário mantém fluxo REST sem acesso direto à API.

## Casos de Borda

- Refresh concorrente, revogação/logout, ticket expirado, reinício e key ring persistente.
- Origin ausente/negada, CSRF, XSS, SSRF, path traversal, redirect, header smuggling e WebSocket hijacking.
- Corpos chunked/acima do limite, subprotocol inválido, resposta lenta, cancelamento e backpressure.
- Base path `/dokpod`, recuperação de Keycloak/API/PostgreSQL e ambiente negado após reconexão.

## Requisitos (obrigatório)

### Requisitos Funcionais

- **FR-001**: BFF DEVE usar OIDC confidencial Authorization Code + PKCE, ticket server-side, refresh coordenado e Data Protection persistente, com certificado fora de Development.
- **FR-002**: Access/refresh/id tokens e client secrets NÃO DEVEM aparecer no frontend, cookies acessíveis, Web Storage, URL, DOM, HAR sanitizado, logs, traces ou artefatos.
- **FR-003**: Login/sessão/antiforgery/logout DEVEM manter as rotas `/bff/*`; login é rate limited, logout mutável protegido e returnUrl somente local.
- **FR-004**: Relay DEVE permitir apenas rotas `/api/v1/*` e `/hubs/*`, métodos/headers contratados, com limites de corpo/resposta/timeout, cancelamento e tratamento hop-by-hop, cookies e redirects.
- **FR-005**: API DEVE validar issuer, audience, assinatura, expiração e decisão Keycloak por recurso/scope; o BFF não decide permissões de ambiente.
- **FR-006**: Mutação/logout DEVEM exigir antiforgery e Origin; upgrade WebSocket DEVE validar Origin e manter Bearer somente server-side, sem token em query por conveniência.
- **FR-007**: Hub DEVE autorizar grupos por ambiente e emitir invalidações mínimas; REST permanece fonte durável, com polling limitado quando necessário.
- **FR-008**: Angular DEVE usar rotas relativas same-origin e tratar sessão expirada, forbidden e indisponibilidade sem expor respostas sensíveis.
- **FR-009**: Deployment padrão DEVE impedir rota alternativa do browser para API; configuração de forwarded headers, mounts, egress e portas depende do [004](../004-distribuicao-containerizada/spec.md).
- **FR-010**: Instância única DEVE permanecer limite explícito; réplicas exigem ADR sobre ticket store, refresh/fencing, rate limit e invalidação, sem nova infraestrutura presumida.

### Entidades Principais

- **Sessão BFF**: ticket e expiração server-side, cookie técnico sem token.
- **Grupo de ambiente/invalidação**: associação autorizada, sem snapshot sensível no evento.

## Critérios de Sucesso (obrigatório)

### Resultados Mensuráveis

- **SC-001**: Todos os cenários exercitados demonstram ausência de credenciais no browser e nos artefatos sanitizados.
- **SC-002**: Testes negativos de Origin/CSRF/SSRF/redirect/headers e autorização horizontal são bloqueados.
- **SC-003**: Login, REST, hub e logout funcionam pela mesma borda nginx, sem bypass, inclusive após falha e recuperação.

## Premissas e Proveniência

P05 registra fundação, relay, UMA, hub e cliente Angular implementados; MVP registra E2E posterior. Pendências antigas não são prova de ausência atual. Migrar P05-01 a P05-09, P04-05 a P04-07 e sessão de MVP P-03/P03-07 para avaliação e validação residual, com gates de release em 005. Não executar nem certificar a aplicação nesta migração.
