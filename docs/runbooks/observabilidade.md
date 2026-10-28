# Runbook: observabilidade do Dokpod

Métricas e traces da API e do BFF por OpenTelemetry (decisão de [Backend e agente](../backend.md); sinais dos ADRs [2026-0001](../adr/2026-0001-arquitetura-inicial.md), [2026-0002](../adr/2026-0002-distribuicao-e-identidade.md) e [2026-0004](../adr/2026-0004-auditoria-persistente.md)). Validado no laboratório local em 2026-10-24 (spec 005, gate 11).

## Configuração

A exportação é OTLP e só liga com as variáveis padrão definidas. Sem nenhuma delas, nada é exportado e não há tentativa de conexão.

| Variável | Uso |
| --- | --- |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint geral (métricas e traces) |
| `OTEL_EXPORTER_OTLP_METRICS_ENDPOINT` / `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT` | Endpoint por sinal (URL completa). Cada sinal liga sozinho |
| `OTEL_EXPORTER_OTLP_PROTOCOL` ou `OTEL_EXPORTER_OTLP_{SINAL}_PROTOCOL` | `grpc` ou `http/protobuf` |
| `OTEL_METRIC_EXPORT_INTERVAL` | Intervalo de exportação em ms (o laboratório usa 15000) |
| `OTEL_SDK_DISABLED=true` | Desliga tudo |

O recurso informa `service.name` (`dokpod-api` ou `dokpod-bff`), `service.version` e `service.instance.id`. No Prometheus, `service.name` vira o label `job`. Os health checks não geram traces.

**Dados sensíveis**: a instrumentação redige os valores de query string (`?limit=Redacted`), o SQL dos spans é parametrizado (sem valores), e nenhum span ou métrica leva tokens, cookies ou senhas. O label `db_client_connection_pool_name` do Npgsql é a connection string **sem senha**, mas mostra host e usuário; trate as métricas como internas.

### Laboratório

O perfil `observability` de `deploy/e2e/docker-compose-dokpod.yaml` sobe um Prometheus que recebe OTLP e avalia as regras, e um Jaeger para traces. São ferramentas de teste, não fazem parte da distribuição. Defina:

```bash
export DOKPOD_E2E_OTEL_METRICS_ENDPOINT=http://prometheus:9090/api/v1/otlp/v1/metrics
export DOKPOD_E2E_OTEL_TRACES_ENDPOINT=http://jaeger:4318/v1/traces
podman-compose ... --profile observability up -d --no-build prometheus jaeger api bff
```

O Prometheus fica em `http://127.0.0.1:19090` e o Jaeger em `http://127.0.0.1:16686`.

## Métricas

| Métrica no Prometheus | Origem | Significado |
| --- | --- | --- |
| `dokpod_agents_connected{agent_engine, agent_version}` | API | Sessões de agente ativas na instância |
| `dokpod_agents_heartbeat_age_seconds` | API | Maior idade do último heartbeat entre os agentes conectados |
| `dokpod_inventory_lag_seconds` | API | Maior tempo em que a revisão relatada por um agente segue sem projeção |
| `dokpod_commands_completed_total{command_kind, command_state, command_failure_code}` | API | Comandos com resultado do agente |
| `dokpod_command_duration_seconds` (histograma) | API | Do despacho ao agente até o resultado |
| `dokpod_authorization_decisions_total{authorization_outcome, authorization_failure_code}` | API | Decisões UMA do Keycloak |
| `dokpod_authorization_duration_seconds` (histograma) | API | Duração das decisões |
| `dokpod_audit_append_attempts_total`, `dokpod_audit_append_failures_total`, `dokpod_audit_append_canceled_total`, `dokpod_audit_idempotency_conflicts_total`, `dokpod_audit_append_duration_milliseconds` | API | Gravação da auditoria (ADR 2026-0004). Gravação cancelada pelo chamador (agente que caiu, API parando) conta em `canceled`, não em `failures`: a transação é desfeita inteira e o resultado é reentregue |
| `http_server_request_duration_seconds{http_route, http_response_status_code}` | API e BFF | Latência e status por rota; no BFF, `/api/v1/{**path}` é o relay do console, e `/bff/*` são login, callback, logout e antiforgery |
| `db_client_connection_count{db_client_connection_state}`, `db_client_connection_max`, `db_client_operation_duration_seconds` | API | Pool e operações do Npgsql |
| Runtime .NET (GC, threads, memória) | API e BFF | Diagnóstico de processo |

## Traces

Uma leitura do console gera um trace único: BFF (`GET /api/v1/{**path}`) → API (rota) → Keycloak (decisão UMA) → PostgreSQL. A correlação vem do `traceparent` do HttpClient do BFF. As sessões gRPC dos agentes aparecem como um span longo por sessão.

## Alertas

As regras estão em `deploy/observability/dokpod-alerts.yml` e os testes em `dokpod-alerts.test.yml`:

```bash
cd deploy/observability
podman run --rm -v "$PWD":/w:ro,z -w /w --entrypoint promtool docker.io/prom/prometheus:v3.5.0 test rules dokpod-alerts.test.yml
```

### DokpodApiSemTelemetria

A API parou de enviar métricas. Verifique se o container está de pé e saudável (`/health/ready`) e o log de inicialização. Se a API estiver saudável, verifique o caminho OTLP até o coletor. Comprovado no laboratório: disparou 2,5 min depois de a API parar e apagou na volta.

### DokpodBffSemTelemetria

Mesmo diagnóstico, no BFF. O console fica inacessível enquanto ele estiver fora.

### DokpodKeycloakIndisponivel

Decisões UMA indeterminadas (`authorization_unavailable`): os usuários recebem 503 nas telas que dependem de autorização. Verifique o Keycloak e o `/health/ready/keycloak` da API. Esse alerta depende de tráfego; sem nenhum uso, a queda aparece só no readiness. Comprovado no laboratório: disparou 2,5 min depois de o Keycloak parar.

### DokpodPoolDoBancoEsgotando

Mais de 90% do pool do Npgsql em uso. Com 100 agentes reconectando juntos, o pool padrão (100) esgotou o `max_connections` do PostgreSQL (spec 005 T016). Compare `db_client_connection_max` com o `max_connections` do servidor e veja [o dimensionamento](../../deploy/e2e/README.md).

### DokpodAgentesDesconectando

O total de agentes caiu mais de 10% em relação ao máximo dos últimos 30 minutos. Veja `dokpod_agents_connected` por `agent_version` (rollout de agente) e os eventos `agent.disconnected` no console. Um restart da API derruba e reconecta as sessões em segundos e não deve disparar. Comprovado no laboratório: disparou 5 min depois de os agentes caírem.

### DokpodHeartbeatAtrasado

Um agente conectado está há mais de 60 s sem heartbeat (padrão 15 s; a sessão cai aos 90 s). Indica rede degradada ou agente travado. Veja o log do agente no host.

### DokpodInventarioDesatualizado

A revisão relatada por um agente segue sem projeção há mais de 2 minutos (o console mostra o host como divergente). Veja `db_client_operation_duration_seconds` e o log da API por falhas de projeção.

### DokpodComandosLentos

O p95 de despacho → resultado passou de 3 s, o limite aprovado na spec 005. Separe por `command_kind`. Pull de imagem é naturalmente mais lento; os demais tipos apontam para o engine no host ou para a rede.

### DokpodComandosFalhando

Mais de 5% dos comandos falharam em 15 minutos, sem contar `stale_target`, que é a concorrência otimista esperada. Veja a distribuição por `command_failure_code`.

### DokpodComandosIndeterminados

Há comandos com resultado `indeterminate` (o efeito pode ter ocorrido, ver spec 003 SC-003). Reconcilie o estado do container no host antes de repetir o comando.

A regra também dispara quando a série de indeterminados nasce na janela: cada combinação de tags só existe a partir do primeiro incremento, já com valor, e `increase()` não vê esse salto (spec 005, achado A3). Depois de reiniciar o Prometheus, séries antigas com valor podem disparar o alerta uma vez, por até 15 minutos.

### DokpodLeiturasLentas

O p95 das leituras do console pelo BFF passou de 500 ms, o limite aprovado. Use o Jaeger para separar o tempo do BFF, da API, do Keycloak e do PostgreSQL no trace.

### DokpodErrosDoServidor

Mais de 0,1% das chamadas do console à API terminaram em 5xx. Cruze com `DokpodKeycloakIndisponivel` e `DokpodPoolDoBancoEsgotando`. Na queda do Keycloak do laboratório, esse alerta ficou pendente, como esperado.

### DokpodAuditoriaFalhando

Falha ao gravar a auditoria; as ações auditadas são recusadas. Veja o log da API (`dokpod_append_audit_event`), as partições (`dokpod_ensure_monthly_partitions`) e o [runbook da auditoria](operacao-auditoria-postgresql.md).

Cancelamentos (agente que caiu no meio da gravação) não disparam este alerta; ficam em `dokpod_audit_append_canceled_total`. Como na regra de indeterminados, a primeira falha de uma instância também dispara, mesmo com a série nascendo já com valor.
