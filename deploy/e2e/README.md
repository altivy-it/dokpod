# Stack E2E do Dokpod

Este diretório define a stack local do projeto Dokpod, consumindo a plataforma
global de identidade pela rede externa `identity-global`. O Compose cria os
serviços próprios do Dokpod e não depende do checkout do AltivyNotes.

## Serviços

- `web`: aplicação Angular estática `dokpod-web:e2e`;
- `api`: API do plano de controle `dokpod-api:e2e`, com gRPC/mTLS na porta interna `7443` e health HTTP na porta interna `8080`;
- `agent`: agente Linux opcional no perfil `agent`, com acesso explícito ao socket Docker local.

O gateway global publica o Dokpod em `https://localhost:7443/dokpod/` e encaminha
sessão, API e SignalR pelo BFF.

O `Dokpod.Bff` entra nesta stack como `dokpod-bff`, com Data Protection em
volume próprio e API como upstream interno.

## Pré-requisitos

- plataforma `altivy-identity` saudável;
- rede externa `identity-global` criada;
- `KEYCLOAK_BFF_CLIENT_SECRET` e demais valores no arquivo externo do Dokpod;
- `POSTGRES_ADMIN_USERNAME` e `POSTGRES_ADMIN_PASSWORD` no arquivo externo da plataforma `Altivy.Identity`;
- certificado PFX de laboratório para a API;
- Docker Desktop com containers Linux.

## Variáveis externas

Defina no arquivo externo `$env:APPDATA\Microsoft\UserSecrets\Dokpod\.env` ou na
sessão atual:

- `DOKPOD_E2E_API_CERTIFICATE_PATH`, obrigatório, caminho absoluto para o PFX da API;
- `DOKPOD_E2E_API_CERTIFICATE_PASSWORD`, opcional quando o PFX não tiver senha;
- `DOKPOD_E2E_AGENT_CERTIFICATE_FINGERPRINT`, opcional para o laboratório de agentes;
- `DOKPOD_E2E_AGENT_ENVIRONMENT_ID`, opcional, GUID do ambiente associado ao certificado do agente.

Com o perfil `agent`, a pasta padrão de certificados é
`$env:APPDATA\Microsoft\UserSecrets\Dokpod\certs`. Ela deve conter
`dokpod-agent.pfx` e `dokpod-dev-ca.crt`; o UUID informado deve possuir uma
identidade correspondente em `dokpod.agent_identities`.

O script recomendado também lê `POSTGRES_ADMIN_USERNAME` e
`POSTGRES_ADMIN_PASSWORD` do arquivo externo
`$env:APPDATA\Microsoft\UserSecrets\Altivy.Identity\.env`. Ele deriva somente
em memória a conexão da API com `postgres:5432` e `Search Path=dokpod`, sem
copiar ou exibir credenciais. O uso da role administrativa é uma compatibilidade
transitória exclusiva do laboratório; ambientes promovidos devem usar database
e role de runtime próprios, provisionados antes da migration.

Ao invocar o Compose diretamente, sem o script, defina também
`DOKPOD_CONTROLPLANE_CONNECTION` no processo por um mecanismo seguro.

Nunca versione certificados privados, senhas ou `.env` dentro do repositório.

## Configurar certificado da API

A API do Dokpod exige um certificado PFX para o endpoint gRPC/mTLS. Para
laboratório local, gere o PFX fora do repositório em UserSecrets. O certificado
deve possuir EKU `serverAuth` e SAN DNS `api`, pois o agente conecta ao endpoint
`https://api:7443` na rede do Compose. Não reutilize um certificado limitado a
`localhost`.

Quando a PKI dedicada já tiver produzido `dokpod-api.crt` e `dokpod-api.key`,
exporte o PFX no mesmo diretório externo:

```powershell
$DokpodSecretsDirectory = Join-Path $env:APPDATA 'Microsoft\UserSecrets\Dokpod'
$DokpodCertsDirectory = Join-Path $DokpodSecretsDirectory 'certs'
New-Item -ItemType Directory -Force $DokpodCertsDirectory | Out-Null

docker run --rm `
  -v "${DokpodCertsDirectory}:/certs" `
  lzocateli/nginx:1.28.0-bookworm-v1 `
  openssl pkcs12 -export `
    -in /certs/dokpod-api.crt `
    -inkey /certs/dokpod-api.key `
    -out /certs/dokpod-api.pfx `
    -passout pass:

$env:DOKPOD_E2E_API_CERTIFICATE_PATH = Join-Path $DokpodCertsDirectory 'dokpod-api.pfx'
$env:DOKPOD_E2E_API_CERTIFICATE_PASSWORD = ''
```

## Validar e iniciar

O caminho recomendado é o script `tools/scripts/manage-e2e-stack.ps1`, que
repassa o arquivo externo de variáveis de ambiente ao Docker Compose e cobre
subir, recriar e encerrar a stack inteira ou um serviço específico. Execute a
partir da raiz do repositório:

```powershell
./tools/scripts/manage-e2e-stack.ps1 --help

./tools/scripts/manage-e2e-stack.ps1 -Action Config
./tools/scripts/manage-e2e-stack.ps1 -Action Up -Build
./tools/scripts/manage-e2e-stack.ps1 -Action Up -Build -ComposeProfile agent
./tools/scripts/manage-e2e-stack.ps1 -Action Recreate -Service api -NoDeps
./tools/scripts/manage-e2e-stack.ps1 -Action Down -Service agent
./tools/scripts/manage-e2e-stack.ps1 -Action Down
```

O padrão de `-EnvFile` é `$env:APPDATA\Microsoft\UserSecrets\Dokpod\.env`, e o
de `-IdentityEnvFile` é
`$env:APPDATA\Microsoft\UserSecrets\Altivy.Identity\.env`. Use os parâmetros
para apontar outros arquivos externos e `-ComposeProfile agent` para habilitar
o agente. `-DryRun` exibe o comando resultante sem executá-lo.

O build do serviço `web` executa `npm ci`, geração do cliente OpenAPI e
`ng build` dentro do Dockerfile `frontend/web/Dockerfile`, usando a imagem
`lzocateli/angular-cli`. O runtime publicado é apenas NGINX com arquivos
estáticos; Node.js e `node_modules` não fazem parte da imagem final.

Os comandos equivalentes em Docker Compose são:

```powershell
docker compose `
  --env-file "$env:APPDATA\Microsoft\UserSecrets\Dokpod\.env" `
  -f deploy/e2e/docker-compose-dokpod.yaml `
  config --quiet

docker compose `
  --env-file "$env:APPDATA\Microsoft\UserSecrets\Dokpod\.env" `
  -f deploy/e2e/docker-compose-dokpod.yaml `
  up --build --wait
```

Para incluir o agente Docker local:

```powershell
./tools/scripts/manage-e2e-stack.ps1 -Action Up -Build -ComposeProfile agent
```

O mount `/var/run/docker.sock:/run/docker.sock` concede privilégio elevado sobre
o host Docker local. Use o perfil `agent` somente em laboratório controlado.

## Incluir um agente Podman rootless

O perfil `agent-podman` (spec 008 T027) sobe um segundo agente apontando para o socket Podman do host, com identidade mTLS própria (`dokpod-agent-podman.pfx` na pasta externa de certificados e UUID em `DOKPOD_E2E_PODMAN_AGENT_ENVIRONMENT_ID`).

```bash
systemctl --user start podman.socket          # ou: podman system service --time=0 &
export DOKPOD_E2E_PODMAN_SOCKET="$XDG_RUNTIME_DIR/podman/podman.sock"
docker compose -f deploy/e2e/docker-compose-dokpod.yaml --profile agent --profile agent-podman up -d
```

O runner de CI atual não oferece Podman; a execução fica manual, com evidência registrada nas tarefas. A qualificação do adapter sem a stack completa roda com `DOKPOD_TEST_PODMAN_SOCKET` em `backend/tests/Dokpod.Agent.Podman.IntegrationTests`.

## Incluir um Docker real sem instalar Docker no host

O perfil `docker-real` (spec 023) sobe o Docker Engine oficial (`docker:28-dind`) aninhado no Podman, privilegiado só para criar cgroups e redes internas, e o agente `agent-docker` com a identidade do perfil `agent` (`dokpod-agent.pfx` e `DOKPOD_E2E_AGENT_ENVIRONMENT_ID`). O socket fica em volume nomeado compartilhado e as imagens em `docker-engine-data`.

```bash
podman-compose --env-file <.env> -f deploy/e2e/docker-compose-dokpod.yaml --profile docker-real up -d --no-build docker-engine agent-docker
podman exec -e DOCKER_HOST=unix:///var/run/docker-engine/docker.sock dokpod_docker-engine_1 docker ps -a
```

É um engine Docker real (mesma API e mesmo comportamento de containers), mas aninhado: métricas de host e o caminho de dados refletem o container do engine. Docker instalado diretamente no host continua sendo um item próprio da matriz da spec 005.

## Stack completa em Linux com Podman rootless

Validada em 2026-10-14 (Ubuntu, Podman 5.7.0, podman-compose 1.5.0) com a
plataforma de identidade, API, BFF, web e agente Podman. O caminho padrão dos
arquivos externos no Linux é `~/.microsoft/usersecrets/<Produto>/`, que o
`manage-e2e-stack.ps1` já usa quando `APPDATA` não existe.

Diferenças em relação ao Docker Desktop:

- o Podman não resolve nomes curtos de imagem sem `unqualified-search-registries`
  (por exemplo `["docker.io"]` em `registries.conf`);
- o `podman-compose` não tem `up --wait`: use `up -d --no-build` e acompanhe o
  health dos containers; construa as imagens antes com
  `podman build --format docker` (o formato OCI descarta o `HEALTHCHECK`);
- o BFF não alcança o gateway de identidade por `host-gateway`, porque a porta
  `7443` é publicada só em `127.0.0.1`. Fixe o IP do gateway na rede
  `identity-global` e informe-o em `DOKPOD_E2E_IDENTITY_GATEWAY_HOST`; informe a
  sub-rede da `identity-global` em `DOKPOD_E2E_TRUSTED_NETWORK` para o BFF aceitar
  os cabeçalhos `X-Forwarded-*` do gateway;
- o gateway da plataforma de identidade usa `resolver 127.0.0.11` (DNS embutido
  do Docker); no Podman, o DNS dos containers atende no IP de gateway da rede
  (`podman network inspect identity-global`). Esse ajuste e o IP fixo do gateway
  pertencem à plataforma de identidade e são feitos por override local dela;
- num PostgreSQL novo, o realm é criado por
  `tools/scripts/configure-keycloak.ps1 -Bootstrap -PermanentInitialUserPassword`
  com `-BaseUrl https://localhost:7443`; com CA local, aponte `SSL_CERT_FILE` para
  um bundle que a inclua.

Sequência usada:

1. plataforma de identidade saudável, com o schema `identity` criado;
2. `dotnet ef database update` no schema `dokpod` (a sessão é fixada em UTC pela
   aplicação; o servidor pode estar em outro fuso);
3. `configure-keycloak.ps1 -Bootstrap` e
   `provision-environment-authorization.ps1` para cada ambiente e scope;
4. cadastro dos ambientes por `POST /dokpod/api/v1/environments`, autenticado no
   BFF;
5. identidade técnica do agente em `dokpod.agent_identities` (fingerprint SHA-256
   do certificado de cliente, laboratório sintético);
6. `systemctl --user start podman.socket` e
   `podman-compose --env-file <.env> -f deploy/e2e/docker-compose-dokpod.yaml --profile agent-podman up -d --no-build`.

A suíte autenticada roda com o Playwright em rede do host:

```bash
podman run --rm --network host \
  -v "$PWD/frontend/tests/e2e":/app/tests:ro -v "$PWD/artifacts/e2e/real":/app/reports \
  -w /app --entrypoint uv \
  -e DOKPOD_E2E_BASE_URL=https://localhost:7443/dokpod/ \
  -e DOKPOD_E2E_USERNAME -e DOKPOD_E2E_PASSWORD -e DOKPOD_E2E_ENVIRONMENT_ID \
  -e DOKPOD_E2E_MUTATION_CONTAINER -e DOKPOD_E2E_DENIED_ENVIRONMENT_ID \
  -e DOKPOD_E2E_REPORT_DIRECTORY=/app/reports \
  docker.io/lzocateli/playwright-e2e:0.1.0 run --frozen pytest /app/tests/test_authenticated_smoke.py \
  --confcutdir=/app/tests -p no:cacheprovider -q
```

`DOKPOD_E2E_MUTATION_CONTAINER` deve ser um container sintético **parado**: o
teste o inicia, reinicia, para e remove.

## Validar o agente Windows self-contained

O agente também pode ser publicado como `win-x64` e executado fora de
container em uma máquina Windows com Docker Desktop. O adapter usa o named pipe
`docker_engine` e o endpoint gRPC publicado pela stack:

```powershell
docker run --rm -v "${PWD}:/workspace" -w /workspace `
  lzocateli/dotnet-sdk:10.0.402-noble-v1 `
  dotnet publish backend/apps/Dokpod.Agent/Dokpod.Agent.csproj `
  --configuration Release --runtime win-x64 --self-contained true `
  --output artifacts/agent/windows-e2e

$env:DOKPOD_AGENT_CONTROL_PLANE_ENDPOINT = 'https://127.0.0.1:17443'
$env:DOKPOD_AGENT_ENVIRONMENT_ID = '<uuid-do-ambiente-provisionado>'
$env:DOKPOD_AGENT_DOCKER_SOCKET = 'docker_engine'
$env:DOKPOD_AGENT_CLIENT_CERTIFICATE_PATH = Join-Path $env:APPDATA 'Microsoft\UserSecrets\Dokpod\certs\dokpod-agent.pfx'
$env:DOKPOD_AGENT_SERVER_CA_CERTIFICATE_PATH = Join-Path $env:APPDATA 'Microsoft\UserSecrets\Dokpod\certs\dokpod-dev-ca.crt'
$env:DOKPOD_AGENT_DATA_DIRECTORY = Join-Path $env:APPDATA 'Microsoft\UserSecrets\Dokpod\agent-windows-e2e'
& ./artifacts/agent/windows-e2e/Dokpod.Agent.exe
```

Esta execução em console é uma prova técnica. Instalação como Windows Service,
ACL dedicada, atualização e rollback continuam gates de P-07.

## Encerrar

```powershell
./tools/scripts/manage-e2e-stack.ps1 -Action Down
```

Para remover também os volumes nomeados da stack, use `-RemoveVolumes`. A
operação é destrutiva e descarta as chaves de Data Protection do BFF.
