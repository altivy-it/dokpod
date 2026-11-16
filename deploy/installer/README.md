# Instalador do Dokpod

Instalador guiado e idempotente da [spec 033](../../specs/033-instalador-guiado/spec.md). Ele leva um host Linux com
Docker ou Podman **rootless** do zero ao Dokpod em execução, em três cenários:
- tudo novo;
- PostgreSQL e/ou nginx existentes;
- Keycloak existente.

Também atualiza a versão, aplica as migrations sem SDK e recria componentes com confirmação.

> **Estado**: em desenvolvimento. Esta pasta tem o esqueleto da T005. Os comandos são reconhecidos e recusados até as
> tarefas que os entregam ([tasks.md](../../specs/033-instalador-guiado/tasks.md)). Para desenvolvimento, use a stack
> de laboratório em [deploy/e2e](../e2e/README.md).

## Como vai funcionar

- `install.sh`, asset da release, detecta o engine rootless e verifica a imagem `dokpod-installer` pelo digest e pela
  assinatura. Em seguida a executa com o socket do usuário, o diretório de instalação e o destino de segredos.
- A imagem orquestra tudo pelo Docker CLI com Compose v2. Ela aplica as migrations pelo modo administrativo da imagem da
  API e configura o realm do Dokpod no Keycloak.
- O estado da instalação fica em SQLite, sem segredos. Os segredos ficam só no destino de segredos, com permissão
  restrita.

### PostgreSQL existente (`postgres.mode: existing-create`, T035)

- A credencial administrativa entra por `{ file }`, `{ env }` ou, sem `password` nas respostas, pelo prompt do
  terminal. Ela só cria o banco, a role dona e a role de runtime e não é gravada em `secretsDir`, no estado nem no
  relatório. A reexecução não a pede: confere versão e privilégios com a própria role dona.
- Não precisa de `SUPERUSER`: basta `CREATEROLE` e `CREATEDB`, como em PostgreSQL gerenciado. Durante o bootstrap, a
  credencial entra em `dokpod_owner` só pelo tempo de criar o banco e conceder o `CONNECT`, e sai em seguida.
- `postgres.host` é o endereço pelo qual os **containers** alcançam o servidor. `localhost` e `127.0.0.1` são recusados
  (código 2). Com o PostgreSQL no próprio host, use `host.containers.internal` no Podman rootless e o IP do host na rede
  no Docker rootless.
- `sslMode` vale para o `psql` do instalador e para a API. Com `verify-ca` ou `verify-full`, a CA de `caCertificate`
  entra no bundle de `SSL_CERT_FILE`; sem ela, valem as raízes do sistema.

### PostgreSQL preparado (`postgres.mode: existing-prepared`, T036)

O instalador não recebe credencial administrativa: usa o banco e as roles que você preparou e só confere. Prepare
assim (os nomes das roles e do banco são livres):

```sql
CREATE ROLE dokpod_runtime NOLOGIN;            -- o grupo que recebe os privilégios das migrations
CREATE ROLE dokpod_owner LOGIN PASSWORD '...';
CREATE ROLE dokpod_app LOGIN PASSWORD '...' IN ROLE dokpod_runtime;
CREATE DATABASE dokpod OWNER dokpod_owner;
REVOKE CONNECT ON DATABASE dokpod FROM PUBLIC;
GRANT CONNECT ON DATABASE dokpod TO dokpod_app;
```

- `postgres.owner` aplica as migrations e cria o schema `dokpod`. Se o schema já existir, ele precisa ser dessa role.
  `postgres.runtime` é a role da API. As duas senhas entram como `{ file }` ou `{ env }`.
- Com a role dona, o instalador confere a versão (`checks/server-version.sql`) e os privilégios das duas roles
  (`checks/role-privileges.sql`). Também confere que a de runtime conecta com a senha informada. Falta ou excesso
  dá código 3 e a mensagem diz o quê:
  - nenhuma das duas pode ter `SUPERUSER`, `CREATEROLE`, `CREATEDB`, `REPLICATION` nem `BYPASSRLS`;
  - as duas precisam de `LOGIN` e `CONNECT`;
  - a de runtime precisa estar em `dokpod_runtime` e não pode ter `CREATE` no banco nem no schema.
- Nada é criado nem alterado no servidor antes das migrations. As conexões gravadas em `secretsDir` nunca são
  sobrescritas: se forem diferentes das respostas, a execução para e indica `recreate secret:<nome>`.

### Nginx existente (`edge.mode: existing`, T037)

O instalador não sobe a borda do Dokpod e não toca nos arquivos do seu nginx. Ele gera `installDir/edge/dokpod.conf`
e `dokpod.locations`, valida os dois com `nginx -t` em container e lista no resumo os passos que restam:
- configurar o certificado;
- incluir `dokpod.conf` no contexto `http`;
- incluir `dokpod.locations` no `server {}` com TLS do nome público;
- testar e recarregar o nginx.

`edge.upstreamHost` diz onde web e BFF escutam (FR-043). Qualquer outro valor dá código 2.

- **`127.0.0.1` ou `::1`** (nginx no host): web, BFF e o Keycloak provisionado são publicados só nesse endereço, em
  `webPort`, `bffPort` e `keycloakPort` (padrões 18080, 18081 e 18082).
  - O encaminhamento de portas do modo rootless troca o IP do nginx pelo de um endereço da rede `dokpod-edge`, por
    onde a porta é publicada. Por isso o BFF confia no loopback e na sub-rede da `dokpod-edge`, onde só há
    containers do Dokpod, como na borda provisionada.
  - No Podman, API e BFF chegam ao Keycloak provisionado pelo nome público apontado para o host (`host-gateway`). Por
    isso o nginx precisa escutar também no endereço que os containers usam para o host, não só no IP público.
- **`{ network: <nome> }`** (nginx em container): web, BFF e o Keycloak provisionado entram nessa rede como
  `dokpod-web`, `dokpod-bff` e `dokpod-keycloak`, sem nenhuma porta no host.
  - A rede precisa existir e ter sub-rede de até /24, senão a execução para com código 3. O BFF confia só nela.
  - Com o Keycloak provisionado, dê ao container do nginx o nome público como alias nessa rede. Assim API e BFF
    alcançam a `Authority` por ele.

### Keycloak existente (`keycloak.mode: existing`, T039 e T040)

- O instalador só cria ou altera o realm do Dokpod (`keycloak.realm`, padrão `dokpod`), nunca o `master` nem outros
  realms (FR-013).
- A credencial do `master` (`keycloak.admin`) entra por `{ file }`, `{ env }` ou, sem `password`, pelo prompt do
  terminal. Ela não é gravada (FR-015).
  - Antes de alterar qualquer coisa, o instalador confere o acesso administrativo e a versão (série 26). Falta de
    acesso ou versão anterior dá código 3, sem alteração.
  - Na reexecução sem a credencial (arquivo apagado ou sem terminal), a reconciliação usa o `dokpod-provisioner` do
    realm: clients, roles e grupos. A política do realm fica como está.
- `administratorUser` entra no grupo `/dokpod/administrators` (FR-014). Se ele não existir no realm, é criado com uma
  senha temporária de troca obrigatória, entregue como a do primeiro administrador do Keycloak provisionado.
- Com `caCertificate`, a CA privada do Keycloak entra no bundle de `SSL_CERT_FILE` da API e do BFF e no container da
  reconciliação. Sem o tema `dokpod` no Keycloak, o realm fica com o tema de login atual.
- Divergências (T040): em cada reexecução, o instalador compara o realm com o contrato
  (`configure-keycloak.ps1 -Drift`, sem alterar nada) e lista cada item no plano como `realm update`, por exemplo
  `client dokpod-bff: redirectUris`. A correção só é aplicada depois da confirmação do plano (`--yes` ou `aplicar`).
  O mesmo vale para o Keycloak provisionado.

Decisões: [ADR 2026-0010](../../docs/adr/2026-0010-modo-administrativo-da-api.md) e
[ADR 2026-0011](../../docs/adr/2026-0011-instalador-da-release.md), ambos aceitos em 2026-11-08. Contrato da linha de comando:
[installer-cli.md](../../specs/033-instalador-guiado/contracts/installer-cli.md).

## Estrutura

```text
deploy/installer/
├── README.md
├── src/
│   └── Dokpod.Installer.ps1    # ponto de entrada; módulos *.psm1 entram nas tarefas da Fase 2
├── sql/state/                  # schema e consultas do estado SQLite, com manifest.json (T019)
├── templates/                  # compose.yaml, dokpod.conf, deploy.env e compose.agent.yaml (T026)
└── tests/                      # Pester 6.2.0, executado no estágio de testes da imagem
```

`install.sh` e `Dockerfile` entram na T024. Todo SQL do instalador fica em arquivos `.sql`. O SQL do banco do Dokpod
fica no catálogo de `backend/libs/Dokpod.ControlPlane.Infrastructure/Sql/`, e o do estado em `sql/state/`. Nenhum
módulo tem SQL escrito no código (FR-039).

## Ajuda

```bash
pwsh -File deploy/installer/src/Dokpod.Installer.ps1 --help
```

Até a imagem do instalador existir (T024), valide a ajuda num container com PowerShell 7, montando o repositório
somente leitura.
