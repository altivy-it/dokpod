# Quickstart de Validação: Criação de Container

1. Hosts Docker e Podman (rootless e rootful); segredo `jwt-secret` criado no Podman (`podman secret create`) e em arquivo protegido do agente Docker; registro de laboratório da 012.
2. "Novo container" na lista: preencher conforme o board 04 (imagem, nome, Podman, host, 8081→80/tcp, 3 variáveis com uma referência a segredo, volume nomeado, 2 vCPU, 512 MiB, unless-stopped, health check) e criar.
3. Conferir no engine (`inspect`) a configuração (SC-005), na lista o novo container e no detalhe o resumo; auditoria com comando equivalente redigido.
4. Canários: valor literal sensível e segredo; varrer respostas, PostgreSQL após terminal, auditoria, eventos, logs, telemetria (SC-003).
5. Chamar `createContainer` diretamente com `privileged`, `capAdd`, bind `/`, socket do engine, `network: host`: 400; forçar no agente via teste: recusa.
6. Conflito de nome e de porta; porta < 1024 em rootless.
7. Interromper agente após `create` antes do resultado: reconciliação por label sem duplicar (SC-004).
   Interromper agente após o journal e antes do `create`: reentrega conclui sem duplicar; com parâmetros purgados ou prazo expirado, `parameters_unavailable` sem container.
7a. Health check: no Podman, container `unhealthy` é reiniciado; no Docker, `onFailure: restart` é recusado com `unsupported_option` e a ajuda mostra o texto adaptado.
7b. Remoção automática com imagem que declara `VOLUME` sem volume nomeado no destino: `anonymous_volume_with_auto_remove`.
8. Medir SC-001; loop visual desktop/mobile do drawer em todos os estados de validação.
