# Carga e recuperação (spec 005)

Ferramentas da qualificação de release (spec 005, T013 a T016). Nada aqui faz parte do produto distribuído.

| Arquivo | Função |
| --- | --- |
| `Dokpod.LoadSim/` | Simulador de agentes: N instâncias do `AgentControlWorker` real (mesmo protocolo, mTLS, journal, inventário, eventos, métricas e comandos), cada uma com um engine em memória (`FakeEngine`) de 20 containers e reinícios aleatórios. |
| `vusers.py` | Usuários virtuais pela borda (nginx + BFF, login real no Keycloak): leituras do console com pesos, comandos periódicos e medição de latência, erros, comando aceito → concluído e convergência do inventário. |
| `resources.sh` | Amostra CPU e memória de containers a cada 10 s. |

## Preparar

1. Hosts de carga: cada agente simulado precisa de um ambiente cadastrado, um certificado de cliente emitido pela CA do ambiente, a identidade (fingerprint SHA-256) em `dokpod.agent_identities` e o recurso `urn:dokpod:environment:{id}` com as permissões por scope no Keycloak. No laboratório isso é feito por um script local (fora do repositório, porque escreve direto no banco e no Keycloak).
2. `agents.json`: `[{ "environmentId": "...", "pfx": "/load/certs/carga-001.pfx" }]`.
3. Publicar o simulador: `dotnet publish tools/loadtest/Dokpod.LoadSim -c Release -o tools/loadtest/Dokpod.LoadSim/out`.

## Executar

```bash
# Agentes (rede do stack, endpoint gRPC da API)
podman run -d --name dok005-loadsim --network dokpod_dokpod \
  -v "$PWD/tools/loadtest/Dokpod.LoadSim/out":/app:ro -v <pasta-de-carga>:/load -v <ca.crt>:/ca.crt:ro \
  --entrypoint dotnet <imagem-aspnet-10> /app/Dokpod.LoadSim.dll /load/agents.json https://api:7443 /ca.crt 20 120

# Usuários virtuais (rede do host, pela borda)
podman run --rm --network host -v "$PWD/tools/loadtest":/lt:ro -v <saida>:/out -e KC_USER -e KC_PASS \
  --entrypoint uv <imagem-playwright> run --frozen python /lt/vusers.py https://localhost:7443/dokpod 30 30 /out/nominal.json

# Recursos
tools/loadtest/resources.sh recursos.csv dokpod_api_1 dokpod_bff_1 dokpod_web_1 <postgres> dok005-loadsim
```

Argumentos do simulador: `agents.json`, endpoint, CA do servidor, containers por host (padrão 20), média em segundos entre reinícios espontâneos por host (padrão 120) e pasta de dados (padrão `/tmp/loadsim`).

## Limites aprovados (spec 005, 2026-10-17)

Leituras p95 ≤ 500 ms e p99 ≤ 1,5 s; comando aceito → concluído p95 ≤ 3 s; convergência do inventário ≤ 30 s; erros < 0,1%; reconexão de todos os agentes ≤ 60 s; API ≤ 2 vCPU e 1,5 GB; nenhuma perda, duplicidade ou acesso indevido.
