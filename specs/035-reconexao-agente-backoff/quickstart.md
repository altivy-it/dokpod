# Guia de Validação: Reconexão do agente com espera crescente e aleatória

## 1. Testes da política (SC-002, SC-003, FR-001 a FR-005, FR-009)

Na raiz do repositório, com a mesma imagem do job Backend do CI:

```bash
podman run --rm -v "$PWD:/src:Z" -w /src lzocateli/dotnet-sdk:10.0.403-noble-v1 \
  dotnet test backend/tests/Dokpod.Agent.Application.Tests --filter "FullyQualifiedName~AgentReconnectBackoff"
```

**Esperado**: todos os testes passam, incluindo:

- sequência de limites 1, 2, 4, 8, 16, 30, 30… e espera sempre em `[limite/2, limite]`;
- recomeço só após uma sessão ≥ 60 s;
- pior caso ≤ 45 tentativas em 10 min de queda;
- simulação de 100 agentes que caem juntos, com a API de volta em 35 s: todos reconectados em ≤ 60 s após a volta e nenhuma janela de 1 s com mais de 20% das tentativas.

## 2. Regressão da sessão real (FR-008)

```bash
podman run --rm -v "$PWD:/src:Z" -w /src lzocateli/dotnet-sdk:10.0.403-noble-v1 \
  dotnet test backend/tests/Dokpod.ControlPlane.Api.Tests --filter "FullyQualifiedName~TransportBoundaryTests"
```

**Esperado**: os testes de sessão existentes passam sem mudança de protocolo. O CI completo roda a suíte com PostgreSQL.

## 3. Agente real e restart da API (SC-001, SC-004, FR-006)

Com uma instalação do Dokpod em execução, com o agente local na versão nova (stack do `deploy/e2e` ou instalação pelo instalador da 033):

1. Pare a API por cerca de 35 s e suba de novo (por exemplo, `docker stop dokpod-api-1; sleep 35; docker start dokpod-api-1`).
2. Acompanhe o log do agente (`docker logs -f dokpod-agent-1`): um aviso por tentativa, com número da tentativa, status e espera crescente; nenhum certificado ou segredo.
3. Meça o tempo entre a API voltar a ficar healthy e a linha `Agent control session established`.

**Esperado**: reconexão em ≤ 60 s após a API voltar; o host volta a `connected` no console pela borda nginx; nenhum alerta "Agente desconectado" com a regra padrão de 2 minutos.
