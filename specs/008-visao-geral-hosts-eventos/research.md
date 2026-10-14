# Pesquisa: Visão Geral, Hosts, Eventos e Inventário Enriquecido

## D-001 Origem dos eventos

- **Decisão**: O agente assina o stream `/events` do engine (Docker e API compatível do Podman), filtrado por tipo (`container`, `image`, `volume`), além de manter o polling atual como reconciliação.
- **Justificativa**: Latência ≤ 5 s (SC-002) e explicação das mudanças; polling sozinho perde `oom` e `health_status`.
- **Alternativas**: Inferir eventos por diferença entre snapshots (perde causa e OOM).

## D-002 Allowlist e mapeamento de eventos

| Engine (`Type`/`Action`) | Tipo Dokpod | Severidade |
| --- | --- | --- |
| container `die` (exitCode ≠ 0), exceto quando precedido por `oom` do mesmo container na janela de agregação | `container.died` | erro |
| container `oom` | `container.oom_killed` | erro |
| container `health_status: unhealthy` | `container.unhealthy` | aviso |
| container `health_status: healthy` após unhealthy | `container.recovered` | sucesso |
| container `restart` (política) | `container.auto_restarted` | aviso |
| container `create` / `destroy` | `container.created` / `container.removed` | informativo / neutro |
| image `pull` / `delete` | `image.pulled` / `image.removed` | sucesso / neutro |
| volume `destroy` | `volume.removed` | neutro |
| plano de controle | `engine.version_outdated`, `agent.connected`, `agent.disconnected`, `command.succeeded`, `command.failed`, `command.indeterminate`, `events.dropped` | aviso / informativo / aviso / sucesso / erro / aviso / aviso |

Atributos do engine são reduzidos a uma allowlist (`name`, `image`, `exitCode`). Labels e variáveis nunca são copiados.

**Verificação**: confirmar com Docker e Podman reais que o reinício por política emite `restart`; se emitir apenas `die` + `start`, o agente mapeia `start` após `die` sem comando pendente como `container.auto_restarted`. A spec 011 conta reinícios em 24 h a partir deste evento (011 D-005).

## D-003 Limite e agregação

- Janela de agregação de 10 s por (tipo, sujeito); `die` que chega depois de `oom` do mesmo container na janela incrementa o `container.oom_killed` em vez de gerar `container.died`.
- Limite de 120 eventos/min por host; o agente informa o excedente em `EngineEventBatch.dropped_count`, e o plano de controle grava um evento `events.dropped` com a quantidade (no máximo um por lote).

## D-004 Persistência de eventos

- **Decisão**: Tabela `environment_events` particionada por mês (como `agent_commands`), com criação antecipada pela função existente `dokpod_ensure_monthly_partitions`; retenção padrão 30 dias (decisão humana pendente para o valor final); índice `(environment_id, occurred_at desc)` e `(severity, occurred_at)`.
- **Retenção**: não existe rotina de retenção no repositório (a migration `202609220002_ScheduleTemporalPartitions` só cria partições). Criar a função `dokpod_apply_temporal_retention(p_table regclass, p_column text, p_retention interval)` (genérica, reutilizada pela 009 para `host_resource_samples`): remove (`DETACH` + `DROP`) partições cujo limite superior é anterior ao corte e apaga em lotes de até 5.000 linhas as linhas vencidas da partição que contém o corte, com `lock_timeout` curto e sem bloquear inserções. Agendada pelo mesmo mecanismo da criação de partições.
- **Alternativa rejeitada**: partição diária (eliminaria o apagamento em lotes, mas multiplica partições e diverge do padrão mensal já usado).
- **Alternativa**: Somente memória (perde histórico em restart; rejeitada).

## D-005 Descritor e conexão

- Descritor: uma linha por ambiente, atualizada no `Hello` e quando muda.
- Conexão: linha por ambiente com `connected` e `session_started_at` gravados na abertura e no encerramento da sessão; `last_seen_at` persistido no máximo a cada 60 s, somente para exibição (evita escrita por heartbeat, conforme a arquitetura).
- Situação: "Desconectado" usa o estado da sessão (`connected`) e o último heartbeat mantido em memória pela instância que detém a sessão, nunca `last_seen_at`. Na inicialização e a cada 60 s, conexões marcadas como conectadas sem sessão ativa correspondente no `IAgentSessionStore` (hoje `InMemoryAgentSessionStore`, por instância) passam a desconectadas, com evento `agent.disconnected`.
- Desatualização: o heartbeat já transporta `inventory_revision`; o host fica "Desatualizado" quando essa revisão diverge da projetada por mais de 2 minutos sem reconciliação concluída. Ausência de deltas em host estável não é desatualização.

## D-006 Versão homologada

- `EngineSupport:Docker:MinimumVersion` e `EngineSupport:Podman:MinimumVersion` nas opções da API; comparação SemVer tolerante (`27.3.1`, `5.2.3`, sufixos ignorados). Mudança de configuração reavalia na próxima consulta, sem migração.

## D-007 Podman

- **Decisão**: Adapter `PodmanCompatEngineClient` usando a API compatível com Docker exposta pelo socket do Podman (`/v{api}/...`), com detecção por `/version` (`Components[].Name == "Podman Engine"`). Funções exclusivas do Libpod (pods) ficam para adapter Libpod futuro; por isso pods não aparecem nesta spec nem como atributo nem como evento.
- **Risco**: Diferenças de eventos e `health_status` entre versões; testes reais obrigatórios (6.6 do 006).

## D-008 Mapeamento de situação

- Implementado no domínio (`ContainerDisplayStatus`) e exposto pela API como `displayStatus` além de `state` bruto, para que lista, detalhe, resumo e eventos usem a mesma regra.

## D-009 Negociação de capabilities (N/N-1 nos dois sentidos)

- **Evidência**: `SessionEstablished` não devolve capabilities, e `AgentControlService` encerra a sessão com `Unimplemented` (`agent_payload_not_supported`) ao receber payload desconhecido. Um agente N enviando `engine_events` a um servidor N-1 derrubaria a sessão.
- **Decisão**: Adicionar `repeated Capability accepted_capabilities = 6` em `SessionEstablished`. O servidor lista as capabilities do `Hello` que ele aceita processar. O agente só envia mensagens novas (008-014) cujas capabilities aparecem nessa lista; lista ausente significa servidor N-1. No sentido contrário, o servidor só envia mensagens novas (`ControlPlaneMessage` 14-16) a agentes que anunciaram a capability no `Hello`.
- **Testes**: agente N contra servidor N-1 (sessão mantida, nenhum payload novo) e agente N-1 contra servidor N.
- **Alcance**: regra de todas as specs 008-014; registrada em [specs/README.md](../README.md#registro-de-alocação-de-contratos).

## D-010 Lista de hosts e alteração de estágio

- **Decisão**: Não criar `listHosts`. Estender `listEnvironments` (que já tem `cursor`/`limit`) com filtros opcionais `stage`, `engine`, `situation` e `q`; sem filtros, o comportamento atual é preservado.
- **Decisão**: `PUT …/stage` sem `If-Match`, porque `environments` não tem revisão nem ETag. A operação é idempotente (mesmo estágio = sem mudança e sem auditoria) e a auditoria registra o estágio anterior e o novo.
