# Pesquisa: Auditoria

## D-001 - Nome do ator preenchido no writer

- **Decisão**: coluna anulável `actor_name` em `audit_events` e nova sobrecarga de `dokpod_append_audit_event` com o nome. O `PostgresAuditEventWriter` consulta um `IAuditActorNameProvider` opcional: na API, ele lê o `preferred_username` do usuário autenticado da requisição (HTTP ou hub) quando o `sub` é o `ActorId` do evento. Eventos de agente e sistema ficam sem nome.
- **Justificativa**: toda gravação passa pelo writer (inclusive a gravação atômica com comandos), então um ponto cobre todos os casos sem mudar os serviços que geram eventos. O nome entra no hash do payload só quando presente, preservando o hash dos eventos antigos.
- **Alternativas**: propagar o nome por todos os serviços (muitas mudanças, fácil esquecer um); resolver nomes no Keycloak na leitura (dependência e custo por linha, e o nome mudaria retroativamente).

## D-002 - Consulta por período com cursor

- **Decisão**: período obrigatório (padrão 24 h, máximo 90 dias), ordenação `occurred_at_utc DESC, event_id DESC`, cursor por chave (keyset) e página de 50. Filtros: hosts, ator (identificador exato ou nome por prefixo), ações (lista derivada da categoria), resultado e texto (UUID exato de correlação, comando ou evento; senão prefixo do alvo).
- **Justificativa**: o período limita as partições mensais lidas; keyset mantém o custo constante ao paginar.
- **Alternativas**: offset (custo cresce com a página); busca textual nos detalhes (índice caro e risco de expor conteúdo).

## D-003 - Autorização por host

- **Decisão**: hosts candidatos vêm dos ambientes com `environment:read` (mesmo serviço das outras telas) e são filtrados pela decisão `audit:read` no Keycloak, em paralelo. Eventos relacionados e exportação usam o mesmo conjunto; evento de host sem acesso responde 404.
- **Justificativa**: mesmo modelo da spec 016 (D-009) e da 001 (FR-002).

## D-004 - Exportação no servidor

- **Decisão**: `GET /api/v1/audit/export` com os mesmos filtros devolve CSV (UTF-8 com BOM, `;`, cabeçalho em português, horários UTC, células iniciadas por `= + - @` prefixadas com apóstrofo); recusa com 400 (`export_too_large`) acima de 10.000 eventos. Grava `audit.exported` em cada host abrangido com `since`, `until` e `count`.
- **Justificativa**: o arquivo cobre todo o filtro, não só a página carregada (FR-008).

## D-005 - Índice de consulta

- **Decisão**: manter `ix_audit_events_environment_time` e acrescentar `ix_audit_events_time_event (occurred_at_utc DESC, event_id DESC)` para a ordenação global com muitos hosts; validar no teste de carga (SC-003).

## D-006 - Item de menu pela API

- **Decisão**: `GET /api/v1/audit/summary` → `{ readableHosts, oldestEventAt }`; o menu mostra "Auditoria" quando `readableHosts > 0`.
- **Justificativa**: a lista de scopes do registro do ambiente não reflete as concessões do Keycloak (achado da spec 016).

## D-007 - Rótulos e categorias

- **Decisão**: rótulos em português e categorias das ações no console (ambientes, containers, comandos, imagens, logs e inspeção, terminal, alertas e política, auditoria); a API aceita `category` e a converte na lista de ações suportadas.

## D-008 - Busca full-text na trilha particionada

- **Decisão**: migration `202610150002_AuditFullTextSearch` com funções `IMMUTABLE` (`dokpod_fold_text`, `dokpod_audit_search_document`, `dokpod_audit_search_query`) e índice GIN por expressão no pai particionado, que o PostgreSQL cria em cada partição. O documento reúne ação (código e palavras), ator (identificador e nome), alvo, código de falha e valores dos detalhes; dicionário `portuguese` (radicais) e acentos removidos por `translate`, sem depender da extensão `unaccent`. A consulta usa `websearch_to_tsquery` (aspas, `or`, `-exclusão`). Um índice `varchar_pattern_ops` no alvo mantém a busca por prefixo de ids e permite `BitmapOr` com o GIN; a função do documento declara custo alto para o planejador preferir os índices.
- **Justificativa**: índice por expressão não adiciona coluna nem reescreve a trilha append-only. Sem o índice de prefixo e o custo, termos raros em 1 milhão de eventos levavam ~6 s (o planejador avaliava o documento linha a linha); com eles, p95 de 121 ms.
- **Alternativas**: coluna `tsvector` gerada (reescreve todas as partições e duplica dados); extensão `unaccent` (exige privilégio de criação de extensão e não é `IMMUTABLE` sem envoltório); busca por `ILIKE` nos detalhes (sem índice).
- **Particionamento**: confirmado na stack e nos testes: `RANGE (occurred_at_utc)` mensal de 2026-01 a 2036-12, mais `DEFAULT` (vazia); a consulta da tela faz *pruning* (24 h lê 1 partição, 132 descartadas). Teste novo garante as partições dos próximos 12 meses e o índice de busca em todas.
