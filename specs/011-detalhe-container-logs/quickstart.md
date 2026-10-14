# Quickstart de Validação: Detalhe, Inspeção e Logs

1. Container sintético `log-canary` com variáveis `DOKPOD_CANARY_ENV=canary-env-<uuid>`, label `traefik.basicauth=canary-user:canary-label-<uuid>`, health check `curl -u canary:canary-hc-<uuid>`, driver de log com opção `loki-url=https://u:canary-url-<uuid>@...` (quando disponível) e logs periódicos contendo `password=canary-pass-<uuid>`, `Bearer eyJ...canary` e níveis INFO/WARN/ERROR/DEBUG. No Podman, criar com `-e` para cobrir `Config.CreateCommand`.
2. Abrir `/containers/{env}/{id}?aba=logs`: últimas 500 linhas em ≤ 2 s, níveis coloridos com texto, canários mascarados.
3. Ativar "Ao vivo": novas linhas ≤ 2 s; sair da aba e confirmar `stream_ended` na auditoria e no agente.
4. Filtrar por texto e nível; baixar e conferir limite.
5. Aba Resumo e Inspecionar: valores de variáveis, label, health check, opção de log e `CreateCommand` mascarados ou ausentes; `redactedPaths` listados.
5a. Gerar resposta de logs no tamanho máximo e 10 sessões ao vivo na taxa máxima enquanto um comando de reinício é enviado: agente continua conectado e o comando não atrasa mais de 2 s (SC-006). Uma 4ª sessão do mesmo usuário e uma 5ª consulta simultânea recebem 429.
5b. Buscar por um canário: conferir que o termo não aparece nos logs de acesso do nginx, BFF e API nem na auditoria.
6. Varrer PostgreSQL, logs de API/BFF/agente e telemetria pelos canários: zero ocorrências (SC-003/SC-004).
7. Revogar `container:logs` durante sessão ao vivo: encerramento em ≤ 60 s (reavaliação) e imediatamente em nova requisição. Fazer logout com sessão ao vivo: encerramento em ≤ 5 s.
7a. Reiniciar o container por política (processo que sai com erro) e por comando: "reinícios nas últimas 24 h" conta ambos em Docker e Podman.
8. Desconectar agente: abas mostram indisponibilidade, sem cache.
9. Repetir em Podman; loop visual desktop/mobile de todas as abas.
