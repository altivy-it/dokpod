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
