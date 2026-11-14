# Validação: Instalador guiado do Dokpod

Evidências das tarefas de validação da spec 033 (T034, T045, T050, T059). Origem: IA assistida; revisão humana pendente.

## T034 - Cenários 1 e 2 do quickstart (SC-001, SC-002)

### Ajuste do roteiro

O [quickstart](quickstart.md) pede a release candidata (`vX.Y.Z-rc.N`) com o `install.sh` dos assets. Ela ainda não
existe: a publicação do instalador é a T051, e nada foi publicado sem pedido explícito. Sem RC, o caminho do
`install.sh` que confere o manifesto e o cosign não instala as imagens desta branch. Por isso, a T034 valida em duas
partes:

- **`install.sh` real**, com `DOKPOD_INSTALLER_IMAGE=localhost/dokpod-installer:test` (aceita só para
  desenvolvimento): detecção do engine rootless e as recusas de engine rootful e de `httpsPort` abaixo do limite. As
  verificações iniciais rodam antes do manifesto.
- **Instalação completa** pelas etapas de `Install.psm1` (`Install.Tests.ps1` e `Resume.Tests.ps1`), com as imagens
  desta branch construídas com `--format docker`, sem a verificação de assinatura, e o primeiro acesso pela borda com
  Playwright (`frontend/tests/e2e/test_installer_first_login.py`).

Ficam para a RC (T051/T052): o fluxo guiado no TTY de ponta a ponta, a verificação de assinatura das imagens
publicadas e a medição do SC-001 contada do `install.sh` até o login.

### Evidência local (2026-11-13, host de desenvolvimento)

Host: Linux x86-64, Podman 5.7.0 rootless (pasta), Compose 5.6.1, PostgreSQL 18.4, Keycloak 26.7.0 e nginx
1.28.0 das imagens fixadas, num projeto compose próprio. Não é host descartável: o host tem a stack de laboratório no
projeto `dokpod`, que não foi tocada.

| Verificação | Resultado |
| --- | --- |
| `Install.Tests.ps1`: instalação do zero, parada sem `--confirm-migrations` (código 4) e retomada com o planHash | 5/5, 147 s, com a reexecução |
| Os 8 serviços rodando e saudáveis; pela borda, `/dokpod/` e OIDC com 200 e `/dokpod/bff/login` com 302 para o Keycloak | ok |
| `Resume.Tests.ps1`: interrupção no banco, migrations, realm, ambiente, serviços e no meio da pki, e retomada | 4/4; retomada em 78 s, sem recurso duplicado |
| Reexecução com as mesmas respostas (SC-003) | 16 s, plano com os 12 componentes em `keep`, `converged`, nada alterado |
| Playwright: primeiro login com a senha temporária, troca obrigatória, console com o host local conectado e containers | 1 passed, 2,6 s |
| Reexecução depois da troca de senha | `initial-admin-password` apagado (FR-014) |

Achados corrigidos nesta validação: o `redirect_uri` do client `dokpod-bff` era registrado sem `/dokpod` (o Keycloak
recusava o login: "Parâmetro inválido: redirect_uri"). O instalador passou a mandar `<publicUrl>/dokpod` ao
`configure-keycloak.ps1`, com teste em `Keycloak.Tests.ps1`.

### Roteiro na VM descartável

Uma VM por engine, Debian 13 ou Ubuntu 24.04 x86-64, com um usuário comum com *linger* (`loginctl enable-linger`),
`/etc/hosts` com `dokpod.lab.test` apontando para o IP da VM e nada além do engine:

- **Docker:** Docker Engine ≥ 27 em modo rootless (`dockerd-rootless-setuptool.sh install`) com o plugin Compose v2;
- **Podman:** Podman ≥ 5 rootless com `systemctl --user enable --now podman.socket` e Compose v2.

No host de desenvolvimento, levar as imagens e os arquivos (`<vm>` é o alias SSH; `docker` vira `podman` na VM Podman):

```bash
podman save --format docker-archive -m localhost/dokpod-installer:test localhost/dokpod-api:installer-test \
  localhost/dokpod-bff:installer-test localhost/dokpod-web:installer-test localhost/dokpod-agent:installer-test \
  | ssh <vm> docker load
rsync -a deploy/installer frontend/tests/e2e <vm>:dokpod-t034/
```

Na VM, as imagens de infraestrutura vêm do registro pelo digest de `templates/infrastructure-images.json`.

1. **Recusa da porta (código 3):** respostas do cenário 1 com `edge.httpsPort: 443` e
   `ip_unprivileged_port_start` em 1024:
   `DOKPOD_INSTALLER_IMAGE=localhost/dokpod-installer:test sh dokpod-t034/installer/install.sh plan --answers answers-443.yaml; echo $?`.
   Esperado: `3`, com as três saídas da mensagem.
2. **Recusa do engine rootful (código 3):** com o engine rootful ativo na VM (`sudo systemctl start docker`, ou
   `sudo systemctl start podman.socket`), rodar o instalador com o socket rootful:
   `sudo docker run --rm -v /var/run/docker.sock:/run/engine.sock -e DOCKER_HOST=unix:///run/engine.sock localhost/dokpod-installer:test plan --answers /a/answers.yaml`
   (com a pasta das respostas montada em `/a`). Esperado: `3`, com a orientação do modo rootless. O `install.sh` sem
   socket rootless sai com `3` antes de chamar o engine.
3. **Instalação completa e retomada:** a suíte do instalador com o engine da VM, como no host de desenvolvimento:
   `docker run --rm --user 0:0 -v $XDG_RUNTIME_DIR/<socket>:/run/engine.sock -e DOCKER_HOST=unix:///run/engine.sock`
   `-e DOKPOD_TEST_FULL_INSTALL=1 -e DOKPOD_HOST_ENGINE_SOCKET=$XDG_RUNTIME_DIR/<socket> -e DOKPOD_HOST_ENGINE=<engine>`
   `-e DOKPOD_TEST_SHARED=$HOME/dokpod-t034/shared -v $HOME/dokpod-t034/shared:$HOME/dokpod-t034/shared`
   `-e DOKPOD_TEST_*_IMAGE=localhost/dokpod-*:installer-test -e DOKPOD_INSTALLER_IMAGE_REF=localhost/dokpod-installer:test`
   `-v $HOME/dokpod-t034/installer/src:/opt/dokpod-installer/src:ro -v $HOME/dokpod-t034/installer/tests:/opt/dokpod-installer/tests:ro`
   `--entrypoint pwsh localhost/dokpod-installer:test -c "Invoke-Pester /opt/dokpod-installer/tests -Output Detailed"`.
   Esperado: tudo verde, com `Install.Tests.ps1` e `Resume.Tests.ps1` executados (não pulados).
4. **Primeiro acesso:** com uma instalação de pé (o roteiro do passo 3 sem o `AfterAll`, ou `Invoke-InstallerInstall`
   direto), o Playwright na rede da borda, com a senha temporária e a nova só por variável de ambiente:
   `docker run --rm --network <projeto>_dokpod-edge -v $HOME/dokpod-t034/e2e:/app/tests:ro -w /app --entrypoint uv`
   `-e DOKPOD_E2E_BASE_URL=https://dokpod.lab.test:<porta>/dokpod/ -e DOKPOD_E2E_USERNAME=dokpod-admin`
   `-e DOKPOD_E2E_ENVIRONMENT_ID -e DOKPOD_E2E_TEMPORARY_PASSWORD -e DOKPOD_E2E_NEW_PASSWORD`
   `docker.io/lzocateli/playwright-e2e:0.1.0 run --frozen pytest /app/tests/test_installer_first_login.py --confcutdir=/app/tests -q`.
   Esperado: `1 passed`.
5. **Tempo:** do início da instalação até o login do passo 4, sem o download das imagens. Esperado: até 20 minutos.

### Resultados na VPS (2026-11-14)

Host: VPS `hostinger-staging` (Ubuntu 26.04.1 LTS x86-64, 8 vCPU, 31 GiB), que já roda outras stacks no Podman do
usuário `brazildevops`. Para ter hosts limpos sem tocar nelas, a validação usou dois usuários descartáveis, cada um com
engine próprio: `dokpodp` (Podman 6.1.3 rootless, netavark, pasta) e `dokpodd` (Docker Engine 29.9.3 rootless, pelo
`get.docker.com/rootless`, com o perfil AppArmor do `rootlesskit` que o Ubuntu 26.04 exige). Durante a validação, o
`/etc/hosts` teve a linha `127.0.1.1 dokpod.lab.test`. No fim, usuários, engines, perfil AppArmor e a linha foram
removidos, e as stacks do `brazildevops` seguiram de pé.

| Verificação | Docker 29.9.3 rootless | Podman 6.1.3 rootless |
| --- | --- | --- |
| `install.sh plan` com `httpsPort: 443` (`ip_unprivileged_port_start` 1024) | código 3, com as três saídas | código 3, com as três saídas |
| `install.sh plan` com DNS e porta corretos | pré-requisitos ok; código 5 por falta do manifesto (sem RC, esperado) | idem |
| Aviso de IP de origem | aviso: driver de portas `builtin` troca o IP do cliente | ok (pasta) |
| Engine rootful | não executado: a VPS não tem Docker rootful e não foi instalado um | código 3: "O podman está em modo rootful… (D-015)" |
| `Install.Tests.ps1` | 5/5 | 5/5 |
| `Resume.Tests.ps1` | 4/4; retomada em 40 s, reexecução em 22 s | 4/4; retomada em 84 s, reexecução em 22 s |
| Reexecução (SC-003) | plano todo `keep`, `converged`, nada alterado | idem |
| Primeiro acesso com Playwright | 1 passed (4,2 s) | 1 passed (4,5 s) |
| Tempo até o login (SC-001), com as imagens já baixadas | 121 s (instalação em 115 s) | 150 s (instalação em 146 s) |

Achados corrigidos na VPS:

- **Podman 6 e `docker run`:** pela API de compatibilidade do Podman 6.1.3, o `docker run` do CLI 29.9.2 sem
  `--pids-limit` cria o container com `pids.max` 1, e o container avulso do `psql` não criava nem o `tar` ("sh: 1:
  Cannot fork"). Os containers avulsos do instalador (psql, modo administrativo, Keycloak, cosign e o `id` da imagem)
  passaram a usar `--pids-limit 512` (`Get-ContainerLimitArguments`). No Podman 5.7 não acontecia.
- **Host limpo:** o dono dos segredos montados era ajustado antes do `compose up` com `image inspect`, que falhava com a
  imagem ainda não baixada. `Resolve-ServiceUserId` passou a baixá-la pelo digest antes.
- **DNS no Docker:** a verificação do nome público resolvia dentro do container do instalador; o Docker não copia o
  `/etc/hosts` do host, e o caminho do quickstart (nome no `/etc/hosts`) era recusado. O `install.sh` passou a resolver
  o nome das respostas no host (`getent ahosts`) e a repassá-lo em `DOKPOD_HOST_PUBLIC_NAME` e
  `DOKPOD_HOST_PUBLIC_ADDRESSES`, que a verificação usa quando o nome confere.
- **Testes:** no Podman 6, o `compose down` da limpeza pode deixar um container parado e as redes do projeto; como a
  rede da borda tem sub-rede fixa, a execução seguinte falhava. A limpeza dos testes remove o que sobrar pelo rótulo do
  projeto. O instalador não usa `compose down` hoje; o `recreate --all` (T043) deve tratar isso.
- **Artefato do teste, não do produto:** o `podman run` copia o `/etc/hosts` do host para o container; com a linha
  temporária, o Playwright ia para 127.0.1.1. Os containers criados pela API de compatibilidade (os da instalação) não
  herdam a linha. O Playwright roda com `--no-hosts` no Podman.

Observação fora do escopo: no Docker rootless, o console mostra CPU e memória como "indisponível" (o agente não publica
amostras de recursos nesse engine), enquanto no Podman os cartões vêm preenchidos. Fica para triagem pelo Bug Fixing.

Pendente da RC (T051/T052): o fluxo guiado no TTY pelo `install.sh` publicado, a verificação de assinatura das imagens e
a medição do SC-001 contada do `install.sh`. A recusa de Docker rootful também fica para um host com Docker rootful.

## T038, T041, T045, T053 e T059 - Cenários 3, 4, 6, 7 e 9 (SC-002, SC-005, SC-006, SC-008, SC-011)

Execução em 2026-11-14, com as suítes de `deploy/installer/tests` contra engine real, as imagens desta branch construídas
com `--format docker` e uma instalação por vez (a sub-rede fixa da `dokpod-edge` não permite duas ao mesmo tempo no
mesmo engine).

- **Podman**: host de desenvolvimento, Podman 5.7.0 rootless com pasta.
- **Docker**: VPS `hostinger-staging`, Docker Engine 29.9.3 rootless num usuário descartável (`dokpodd`, com o perfil
  AppArmor do `rootlesskit`). Usuário, engine e perfil foram removidos no fim, e as stacks do `brazildevops` não foram
  tocadas.

| Cenário | Verificação | Podman 5.7 rootless | Docker 29.9 rootless |
| --- | --- | --- | --- |
| 3 | PostgreSQL existente `existing-create` com TLS `verify-full` de CA privada e administrador sem `SUPERUSER` (`Install.Tests.ps1`) | 4/4 | 4/4 (containers chegam ao host pelo IP dele) |
| 3 | PostgreSQL preparado (`existing-prepared`), sem nenhuma role nova no servidor | 2/2 | 2/2 |
| 3 | Nginx existente em loopback: console, OIDC, login com `redirect_uri` público e site anterior do nginx igual | 3/3 | NOT RUN: com Keycloak provisionado, o nome público precisa resolver por DNS para um IP alcançável pelos containers; no Docker não há `host-gateway` equivalente ao do Podman (limitação documentada no README) |
| 3 | Combinações de PostgreSQL e nginx existentes no plano (`Plan.Tests.ps1`) | ok | ok (sem engine) |
| 4 | Keycloak existente com CA privada: login do BFF pelo Keycloak do usuário, `master` e `outro` iguais, credencial em nenhum arquivo, divergência no plano recusada sem `--yes` e corrigida com ele | 3/3 | 3/3 |
| 6 | Recriação por item (`pki:edge`, `service:web`, `secret:<client do BFF>`, `pki:agent:<id>`), recusa de componente do usuário, `--all` sem e com a remoção de dados, troca da CA | 8/8 | 8/8 |
| 6 | SC-008: contagens de `dokpod.audit_events` e `dokpod.agent_identities` iguais depois do `--all` sem a segunda confirmação; lista de dados inexata recusada com código 2 | ok | ok |
| 7 | Canário (`SecretCanary.Tests.ps1`): nenhum segredo na saída, nos relatórios, em `installDir`, no `docker inspect` e nos processos amostrados durante a execução e nos logs; containers avulsos com `--log-driver none` | 4/4 | 4/4 |
| 8 (parcial) | Host remoto (`host add` e `host revoke`): pacote 0600 cifrado aberto pelo README, só os seis arquivos e nenhum outro segredo, agente do pacote conectado em menos de 5 minutos, sessão derrubada e revogação na auditoria | 2/2 | NOT RUN (o teste usa o `host-gateway` do Podman para o agente chegar ao gRPC publicado) |
| 9 | Catálogo sobreposto e FR-042 (`Migrations.Tests.ps1`): hashes no plano, recusa sem confirmação nomeada, aplicação, impressão aceita, cópia alterada voltando ao plano; coluna, índice, restrição, função e gatilho sem registro dão código 6 no `verify` | 9/9 | NOT RUN (sem diferença de engine: o SQL roda no PostgreSQL) |

Achados corrigidos nesta rodada:
- **Recriação total com dados removidos**: `compose down` apagava e recriava as redes do projeto, e o DNS interno do
  Podman deixava de responder na rede recriada. Agora só os containers saem (`compose rm --stop --force`).
- **`host add`**: no PowerShell, `$name` e o parâmetro `$Name` são a mesma variável, e o ambiente ficava gravado no
  estado com o nome do arquivo da frase.
- **Troca da CA**: um `foreach` direto sobre o resultado do estado recebia o array inteiro como um item.
- **`host revoke`**: o modo administrativo só grava a revogação no banco, e a sessão ativa do agente vive na memória
  da API. O `host revoke` agora reinicia a API, e o agente revogado não volta.
- **Testes**: a limpeza por rótulo com o nome do projeto vazio casava com todo container compose do host e removeu
  stacks locais alheias (incidente de 2026-11-14). Toda limpeza passa agora por `TestCleanup.ps1`, que só aceita
  `dokpod-tNNN`.
