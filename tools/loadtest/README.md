# Carga e recuperação (spec 005)

Ferramentas da qualificação de release (spec 005, T013 a T016). Nada aqui faz parte do produto distribuído.

| Arquivo | Função |
| --- | --- |
| `Dokpod.LoadSim/` | Simulador de agentes: N instâncias do `AgentControlWorker` real (mesmo protocolo, mTLS, journal, inventário, eventos, métricas e comandos), cada uma com um engine em memória (`FakeEngine`) de 20 containers e reinícios aleatórios. |
| `vusers.py` | Usuários virtuais pela borda (nginx + BFF, login real no Keycloak): leituras do console com pesos, comandos periódicos e medição de latência, erros, comando aceito → concluído e convergência do inventário. |
| `resources.sh` | Amostra CPU e memória de containers a cada 10 s. |
| `provision-load-hosts.sh` | Emite pela CA de laboratório os certificados de cliente dos ambientes `Carga NNN`, grava os fingerprints em `dokpod.agent_identities` e gera o `agents.json`. |
| `retire-load-hosts.sh` | Aposenta os ambientes `Carga NNN` ao fim da campanha: revoga as identidades, desabilita os ambientes e remove do Keycloak os recursos e as permissions `carga-*`. Não apaga os ambientes, porque a auditoria é append-only e os comandos referenciam o ambiente. Sem `--apply`, só mostra o estado. Para outra campanha, reabilite os ambientes e refaça a autorização (passo 1 de Preparar). |

## Preparar

1. Hosts de carga: cada agente simulado precisa de um ambiente `Carga NNN` cadastrado e do recurso `urn:dokpod:environment:{id}` com as permissões por scope no Keycloak (`provision-environment-authorization.ps1`).
2. Certificados, identidades e `agents.json` (`[{ "environmentId": "...", "pfx": "/load/certs/carga-001.pfx" }]`): `tools/loadtest/provision-load-hosts.sh 100 ~/.microsoft/usersecrets/Dokpod/load`. A pasta guarda chaves privadas e fica fora do repositório. Para a carga nominal, use os 56 primeiros hosts do arquivo.
3. Publicar o simulador: `dotnet publish tools/loadtest/Dokpod.LoadSim -c Release -o tools/loadtest/Dokpod.LoadSim/out`. Com mais de uma origem NuGet configurada no usuário, o gerenciamento central de pacotes falha (NU1507): passe `-p:RestoreConfigFile=<nuget.config só com nuget.org>`.

No Podman rootless, rode o simulador e os usuários virtuais com `--userns keep-id --user $(id -u):$(id -g)`, para que leiam os arquivos privados da pasta de carga.

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
