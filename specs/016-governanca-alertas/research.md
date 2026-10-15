# Pesquisa: Governança de alertas e parada justificada

## D-001 - Persistência no PostgreSQL, sem particionamento

- **Decisão**: tabelas comuns (`alert_rules`, `alerts`, `alert_timeline`, `alert_silences`, `action_justification_policies`, `container_action_justifications`); retenção de alertas resolvidos por `DELETE` em lote no worker.
- **Justificativa**: volume baixo (deduplicação por regra e alvo); justificativas seguem a retenção da auditoria e são pequenas.
- **Alternativas**: particionamento mensal como `environment_events` (complexidade sem volume que justifique).

## D-002 - Avaliação de eventos no momento da gravação

- **Decisão**: `OperationalEventService.AppendAsync` chama o `AlertEvaluator` para os eventos efetivamente gravados (após a deduplicação por `source_key`), no mesmo escopo, depois da gravação e da notificação; uma falha da avaliação se propaga a quem chamou (que já registra em log, como o `Guard` do `AgentHostActivity`) e não perde o evento, já persistido.
- **Justificativa**: um único ponto por onde passam todos os eventos (agente, varredura de conexão e comandos); atende os 15 s do SC-001 sem fila nova.
- **Alternativas**: worker que lê eventos novos periodicamente (atraso e cursor extra).

## D-003 - Varredura para condições observáveis e limites

- **Decisão**: `AlertSweeper` a cada 30 s: resolve alertas de container cuja condição sumiu (container em execução e saudável, ou removido), de agente reconectado e de limite abaixo do valor pela duração da regra; abre alertas de limite quando a amostra (spec 009) fica acima pela duração; reabre silenciados ao fim da janela; encerra marcações intencionais de containers que voltaram a executar.
- **Justificativa**: resolução automática e limites dependem de estado, não de um evento.
- **Alternativas**: resolver só por evento (`container.recovered` não cobre todos os casos, como remoção ou início fora do Dokpod).

## D-004 - Regras padrão semeadas e regra de encerramento

- **Decisão**: a migration semeia 9 regras com ids fixos; a regra "Container encerrado com erro" usa `container.died` com `exitCode` fora de `{0, 137, 143}`; o avaliador também ignora o encerramento de container com ação justificada vigente.
- **Justificativa**: código 0 e sinais de parada (SIGTERM 143, SIGKILL 137 após timeout) são paradas, não falhas; evita ruído de paradas feitas fora do Dokpod.
- **Alternativas**: regra sem filtro de código (alerta para toda parada).

## D-005 - Justificativa fora do payload do agente, gravada após o enfileiramento

- **Decisão**: `justification` é campo opcional de `POST .../commands`; validado contra a política do estágio do host; mascarado; gravado em `container_action_justifications` com chave única por `command_id` depois de `Created` ou `Duplicate` (`ON CONFLICT DO NOTHING`), com o nome do container no momento.
- **Justificativa**: não muda o hash v1 do comando nem o protocolo do agente; nova tentativa com a mesma `Idempotency-Key` completa o registro se o processo cair entre as escritas.
- **Alternativas**: incluir no hash (quebra a compatibilidade N/N-1) ou gravar antes (registro órfão quando o enfileiramento falha).

## D-006 - Tempo real por invalidação

- **Decisão**: novo `InvalidationKinds.Alerts`; o cliente busca o resumo e a lista pela API, como já ocorre com eventos.
- **Justificativa**: mesmo modelo do hub atual; nenhum dado de alerta trafega pelo hub.

## D-007 - Quem edita a política de justificativa

- **Decisão**: `PUT /api/v1/action-justification-policies/{stage}` exige `environment:manage` em todos os hosts habilitados do estágio (e ao menos um host legível); sem host no estágio, exige `environment:manage` em todos os hosts legíveis.
- **Justificativa**: a API autoriza só por scopes UMA; o grupo `/dokpod/administrators` é quem recebe `environment:manage`, o que equivale ao "administrador" da spec sem mecanismo novo de papéis.
- **Alternativas**: verificar papel do token (mecanismo novo de autorização).

## D-008 - Silenciamento

- **Decisão**: abrangência por estágio, host, padrão de nome de container ou alerta; avaliação marca o alerta como `silenced` com o id do silenciamento; ao fim (ou encerramento antecipado), a varredura devolve a `open` se a condição persistir.
- **Justificativa**: preserva o registro (FR-013) e mantém contador e sino sem ruído.

## D-009 - Scopes `alert:read` e `alert:manage`

- **Decisão**: incluídos em `EnvironmentResourceScopes.Supported`, nos scopes UMA do `configure-keycloak.ps1` e no provisionamento por ambiente; ambientes existentes recebem os scopes pelo provisionamento.
- **Justificativa**: modelo de autorização atual por ambiente.
