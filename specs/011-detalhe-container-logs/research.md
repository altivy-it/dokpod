# Pesquisa: Detalhe, Inspeção e Logs

## D-001 Consulta sob demanda pelo stream existente

- **Decisão**: Novas mensagens no `Connect` existente: servidor envia `ContainerQueryRequest` (id de correlação, tipo, alvo, parâmetros, prazo); agente responde com uma ou mais `ContainerQueryResponse` de até 256 KiB (`sequence`, `final`). A API monta as partes até o limite da resposta, com timeout e cancelamento.
- **Restrição verificada**: o gRPC do plano de controle limita mensagens a 1 MiB (`MaxReceiveMessageSize = 1_048_576` em `ApiHost.cs`); uma mensagem maior encerra o `Connect` inteiro. Por isso nenhuma mensagem de consulta ou de log passa de 256 KiB.
- **Limites**: até 4 consultas simultâneas por agente (`Agent:Queries:MaxConcurrentPerAgent`); excesso responde 429 `too_many_requests`.
- **Justificativa**: A arquitetura mantém um único stream por agente; consultas são pequenas e limitadas.
- **Alternativa**: Novo RPC unário iniciado pelo servidor (impossível: o agente é quem disca; exigiria conexão reversa).

## D-002 Logs ao vivo

- **Decisão**: `LogStreamControl` (start/stop, sessão, filtros, limites) do servidor; `LogChunk` (sessão, linhas, omitidas) do agente; API repassa por `IAsyncEnumerable` no hub SignalR ao navegador via BFF.
- **Prioridade no stream**: Hoje o agente usa uma fila única (`outbound` em `AgentControlWorker.cs`). Passa a ter duas filas: alta (aceite e resultado de comandos, heartbeat, inventário, eventos) e baixa (respostas de consulta e `LogChunk`); a alta é sempre drenada primeiro. Chunks de log são descartáveis sob pressão (contados como omitidos).
- **Limites**: `LogChunk` ≤ 256 KiB; 200 linhas/s e 256 KiB/s por sessão; 10 sessões por agente e 3 por usuário.
- **Revogação**: autorização reavaliada a cada 60 s pelo componente `StreamingAuthorizationMonitor`, compartilhado com a 014 (014 D-008): verifica a sessão do BFF e refaz a decisão de autorização da API com o access token atual; encerramento também no logout ou na expiração da sessão do BFF.

## D-003 Sanitização da inspeção

- **Allowlist do resumo**: `Path`, `Args` (mascarados), `Config.User`, `Config.WorkingDir`, `Config.Healthcheck`, `Config.Labels` (truncado), `HostConfig.LogConfig`, `HostConfig.RestartPolicy`, `HostConfig.PortBindings`, `HostConfig.NanoCpus`, `HostConfig.Memory`, `NetworkSettings.Networks.*.{IPAddress, NetworkID nome}`, `Mounts[].{Type, Name, Destination, Mode, RW}` (sem `Source` para bind fora de allowlist), contagem de `Config.Env`.
- **Denylist da inspeção completa**: `Config.Env`, `Config.CreateCommand` (Podman; contém a linha de comando com `-e NOME=valor`), `Mounts[].Source` de bind fora da allowlist, `HostConfig.Binds` fora da allowlist, `ExecIDs`, `GraphDriver.Data`, `Config.Secrets`/`SecretsPaths` (Podman), qualquer chave contendo `password|secret|token|key|credential|auth` (valores substituídos por `"<oculto>"`).
- **Mascaramento universal**: depois da allowlist (resumo) e da denylist (inspeção), o `SecretMasker` percorre todo valor de texto, incluindo `Path`, `Args`, `Config.Cmd`, `Config.Entrypoint`, `Config.Healthcheck.Test`, labels, `HostConfig.LogConfig.Config` (ex.: `splunk-token`, `loki-url` com `user:pass@`) e campos desconhecidos de versões novas do engine. Assim a falha é fechada para segredos com padrão conhecido mesmo quando o campo não está na denylist.
- **Limitação**: segredos sem padrão reconhecível em campos não listados podem passar; a UI avisa que o mascaramento é best-effort e o threat model registra o risco residual.

## D-004 Leitura de logs

- `GET /containers/{id}/logs?stdout=1&stderr=1&timestamps=1&tail=N&since=..` com demultiplexação do cabeçalho de 8 bytes quando sem TTY; Podman compatível igual. Filtro de texto literal (sem regex, ≤ 128 caracteres) aplicado no agente após mascaramento. Nível inferido no agente (`LogLine.level`).
- O termo `contains` é removido dos logs de acesso do nginx, do BFF e da API (redação do parâmetro de query) e nunca entra na auditoria.

## D-005 Reinícios nas últimas 24 h

- Contados a partir de `environment_events` da spec 008: `container.auto_restarted` (reinício por política) mais `command.succeeded` de comandos `restart`. `container.died` não serve: só existe para código ≠ 0 e não há evento de start na allowlist da 008.
- **Verificação obrigatória**: confirmar com Docker e Podman reais que o reinício por política emite `restart`. Se o engine emitir apenas `die` + `start`, o agente da 008 mapeia `start` após `die` sem comando pendente como `container.auto_restarted`.
- `restart_count` do engine é exibido como total desde a criação.

## D-006 Metadados de auditoria

- **Decisão**: `audit_events` hoje só tem ator, ação, ambiente, resultado, `failure_code` e `command_id`. Migration expand: `subject_id varchar(256) NULL` (container) e `details jsonb NULL`, com `CHECK` de tamanho (≤ 2 KiB) e allowlist de chaves por ação aplicada na aplicação; valores somente números, booleanos e enums, nunca texto livre (única exceção: `justification` de `container.exec.requested`, definida na 014 D-009). Nova sobrecarga de `dokpod_append_audit_event` com os dois parâmetros; a assinatura atual continua funcionando (expand-contract, rollback removendo só a sobrecarga e as colunas vazias).
- **Alternativa rejeitada**: tabela de auditoria de consultas separada (duplica retenção e partições).
