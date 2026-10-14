# Pesquisa: Lista Unificada e Ações em Lote

## D-001 Autorização na listagem multi-host

- **Decisão**: Resolver primeiro o conjunto de hosts com `environment:read` pelo `IEnvironmentAuthorizationDecider` existente (mesmo mecanismo de `listEnvironments`, limite de 500 decisões por requisição, cache por requisição) e filtrar a consulta SQL por esse conjunto. Nunca filtrar depois de paginar.
- **Verificação**: medir com 56 e 100 hosts no teste de SC-001; se o custo das decisões passar do orçamento, abrir decisão separada sobre avaliação em lote.

## D-002 Cursor estável

- Cursor opaco codificando (chave de ordenação, `environment_id`, `container_id`). Mudanças entre páginas podem gerar sobreposição mínima; o frontend deduplica por (`environmentId`, `containerId`).

## D-003 Busca textual

- `ILIKE` sobre nome, imagem, nome do host e portas renderizadas (coluna gerada `ports_text`), com mínimo de 2 caracteres. `pg_trgm` só se SC-001 falhar (extensão exige aprovação operacional).

## D-004 Lote no frontend

- **Decisão**: Lote = N chamadas `submitContainerCommand` com concorrência limitada (5), cada uma com `Idempotency-Key` UUID persistida em `sessionStorage` junto com o lote; acompanhamento por `getContainerCommand` com backoff e invalidação.
- **Alternativa**: Endpoint de lote no servidor (agregaria autorização/auditoria, mas cria semântica parcial e novo contrato; rejeitado nesta fase).

## D-005 Pausa no Podman

- API compatível suporta `pause`/`unpause`; Podman rootless com cgroup v1 não suporta pausa → capability não anunciada nesse modo.

## D-006 Situação exibida persistida

- **Evidência**: a 008 calcula `displayStatus` no domínio (`ContainerDisplayStatus`) e não cria coluna; filtrar, ordenar e resumir por situação no SQL exige o valor persistido.
- **Decisão**: coluna `display_status` em `inventory_containers`, preenchida pela aplicação ao projetar (mesmo mapeamento da 008), com backfill na migration expand e índice.

## D-007 Ordenação por CPU e memória

- **Evidência**: métricas de container ficam só em memória (009 D-002, Opção A′); uma consulta paginada por cursor no PostgreSQL não consegue ordenar por elas.
- **Decisão**: o servidor ordena por nome, situação, host e criação; CPU e memória são ordenadas no navegador apenas sobre as linhas carregadas, com indicação visível.

## D-008 Scope no Keycloak

- `container:pause` é novo: provisionar no resource server `dokpod-api` (realm de desenvolvimento/E2E em `deploy/keycloak/realm/dokpod-realm.json` quando aplicável) e acrescentar à tabela "Scopes iniciais" de `docs/configuracao-keycloak.md`, com a política correspondente.
