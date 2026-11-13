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

### Resultados na VM

| Verificação | Docker rootless | Podman rootless |
| --- | --- | --- |
| Recusa de `httpsPort` 443 (código 3) | pendente | pendente |
| Recusa do engine rootful (código 3) | pendente | pendente |
| `Install.Tests.ps1` e `Resume.Tests.ps1` | pendente | pendente |
| Primeiro acesso com Playwright | pendente | pendente |
| Tempo até o login (SC-001) | pendente | pendente |
