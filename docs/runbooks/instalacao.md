# Runbook: instalação, operação e desinstalação

Procedimento do instalador guiado do Dokpod ([spec 033](../../specs/033-instalador-guiado/spec.md)) num host Linux com
Docker Engine ou Podman em modo **rootless**. Os detalhes de cada modo estão em
[deploy/installer/README.md](../../deploy/installer/README.md). O contrato da linha de comando está em
[installer-cli.md](../../specs/033-instalador-guiado/contracts/installer-cli.md).

## Antes de começar

- Um usuário comum com o engine rootless ativo e *linger* (`loginctl enable-linger <usuário>`), para os containers
  voltarem depois do reboot.
- O nome público (`publicUrl`) resolvendo no host para o endereço que os navegadores usam.
- Uma porta HTTPS livre. No modo rootless, abaixo de `net.ipv4.ip_unprivileged_port_start` só com um nginx do host
  (`edge.mode: existing`) ou com o administrador baixando o limite. O instalador nunca altera o `sysctl`.
- `install.sh` e o `release-manifest.json` da release, ou o pacote `--offline`. O `install.sh` confere o digest e a
  assinatura de cada imagem antes de qualquer efeito (código 5 quando algo não confere).

## Os três cenários

| Cenário | Respostas | O que o instalador sobe |
| --- | --- | --- |
| Tudo novo | `postgres.mode: provision`, `keycloak.mode: provision`, `edge.mode: provision` | PostgreSQL, Keycloak, borda nginx, API, BFF, web e o agente local |
| PostgreSQL e/ou nginx existentes | `postgres.mode: existing-create` ou `existing-prepared`; `edge.mode: existing` | o mesmo, sem o banco e/ou sem a borda |
| Keycloak existente | `keycloak.mode: existing` | o mesmo, sem o Keycloak e o banco dele |

As combinações valem. O que é do usuário (`existing`) nunca é recriado nem removido.

## Arquivo de respostas

Sem `--answers`, o modo guiado pergunta tudo. Com ele, nada é perguntado, e uma resposta obrigatória faltando dá
código 2. O esquema é [answers.schema.json](../../specs/033-instalador-guiado/contracts/answers.schema.json).

Segredos nunca entram no arquivo: cada senha é `{ file: <caminho> }` ou `{ env: <variável> }`. As credenciais
administrativas do PostgreSQL e do Keycloak existentes podem ficar de fora: sem elas, o terminal pergunta, e elas são
usadas só naquela execução.

```bash
sh install.sh plan --answers respostas.yaml        # o plano, sem efeito
sh install.sh install --answers respostas.yaml --yes
```

`--yes` confirma só o plano normal. Migrations, backup, SQL sobreposto e remoção de dados têm confirmação própria
(`--confirm-migrations <planHash>`, `--confirm-backup <banco>`, `--confirm-sql-overrides <lista>` e
`--confirm-data-removal <lista>`).

## Migrations revisadas por DBA

1. `sh install.sh migrations plan` lista as pendentes e o efeito de cada uma, sem tocar no banco.
2. `sh install.sh migrations export --out pendentes.sql` gera o SQL exato, com cabeçalho de versões e hash. O DBA
   revisa e aplica com `psql -v ON_ERROR_STOP=1 -f pendentes.sql`.
3. `install` ou `upgrade` com `--manual-migrations` pausa com código 10 nessa etapa. Depois da aplicação, o mesmo
   comando retoma, e o `verify` confere o histórico e a impressão do schema (código 6 quando divergem).

Num banco que já tem dados, as migrations exigem backup antes. Com o PostgreSQL provisionado, o instalador faz o
`pg_dump` em `installDir/backups/`. Nos demais, a confirmação é nomeada, seguindo
[backup-restore-e-atualizacao.md](backup-restore-e-atualizacao.md).

## Recriação

| Comando | Efeito |
| --- | --- |
| `recreate pki:edge`, `recreate pki:api` | Certificado novo e só o serviço que o usa recriado. Com nginx existente, troque o certificado nele. |
| `recreate pki:agent:<id>` | Certificado novo do agente local: a identidade antiga é revogada e a nova, registrada. |
| `recreate secret:<nome>` | Os regeneráveis: o client secret do BFF e as conexões das roles do PostgreSQL (não no `existing-prepared`). |
| `recreate config`, `recreate service:<nome>` | Os arquivos de `installDir` regravados ou só um container recriado. |
| `recreate pki:ca` | Troca da CA local pelo [runbook](troca-da-ca-dos-agentes.md): CA nova, as duas raízes na confiança, API, borda e agente local reemitidos, e a raiz antiga só sai depois da reconexão. Com hosts remotos, a troca fica em andamento até cada um receber o pacote novo e o `recreate pki:ca` rodar de novo. |
| `recreate --all` | Recria todos os serviços e **preserva** banco, login, key rings e identidades. |

Os dados só saem com a segunda confirmação, que nomeia exatamente os itens: `dokpod-postgres` e `keycloak-postgres`
(quando provisionados), `dokpod-api-keys`, `dokpod-bff-keys` e `agent-identities`. No terminal, ela é digitada
depois da confirmação normal. Sem terminal, vem em `--confirm-data-removal` com a lista exata. Uma lista diferente dá
código 2, sem efeito. Com os dados removidos, as migrations pedem de novo o `planHash`, e os hosts remotos precisam de
pacote novo.

## Hosts remotos

Exige `agentGrpcPort` nas respostas: é a porta gRPC com mTLS dos agentes, a única publicada além da borda.

```bash
sh install.sh host add --name host-b --engine podman     # docker, podman ou windows
```

- O pacote `installDir/packages/<environmentId>.tar` (`0600`) tem o `README.txt`, o `agent.tar.enc` e o SHA-256 dele.
- A frase da cifra aparece uma vez no terminal, ou fica em `secretsDir/packages/<id>.passphrase` sem terminal.
  Entregue-a por outro canal e apague o arquivo.
- No host de destino:
  1. `sha256sum -c agent.tar.enc.sha256`;
  2. `openssl enc -d -aes-256-cbc -pbkdf2 -iter 600000 -md sha256 -in agent.tar.enc -out agent.tar`;
  3. extraia e siga o `README.txt` de dentro. No Docker e no Podman é um `docker compose up`, e no Windows,
     [qualificacao-windows.md](qualificacao-windows.md).

O host aparece conectado em até 5 minutos. `status` avisa sobre um pacote gerado há mais de 7 dias cujo host não
conectou. Para tirar um host:

```bash
sh install.sh host revoke --environment <id> --reason "host desativado"
```

A sessão do agente cai, e a revogação fica na auditoria.

## Atualização

```bash
sh install.sh upgrade --version X.Y.Z --answers respostas.yaml
```

- Só para versão maior. Para voltar, siga [deploy-e-rollback.md](deploy-e-rollback.md): as migrations são forward-only.
- As imagens são verificadas, as migrations novas seguem o fluxo acima, e o plano de controle sobe antes do agente.
- No fim, o rótulo `org.opencontainers.image.version` de cada serviço do Dokpod precisa ser a versão pedida.

## Certificados

`sh install.sh certificates check` lista a validade da CA, da borda, da API e de cada agente. Perto de
`renewWarningDays` (padrão 30) ou vencido, sugere a ação:
- `recreate pki:<item>` com a CA local;
- `host add` para um host remoto;
- só o aviso para um certificado da organização.

O mesmo aviso sai em cada reexecução do `install`.

## Desinstalação e o que é preservado

O instalador não tem comando de desinstalação: os dados só saem por decisão explícita. Para remover a instalação:

1. Faça o backup do banco e do Keycloak ([backup-restore-e-atualizacao.md](backup-restore-e-atualizacao.md)).
2. `docker compose -f <installDir>/compose.yaml -p dokpod down`. Isso para e remove os containers e as redes, e mantém
   os volumes.
3. Só se quiser apagar os dados: `docker volume rm dokpod_dokpod-postgres-data dokpod_keycloak-postgres-data
   dokpod_dokpod-api-keys dokpod_dokpod-bff-keys dokpod_dokpod-agent-data`.
4. Apague `installDir` e `secretsDir`. O `secretsDir` tem a chave da CA, as senhas e as conexões.

O PostgreSQL, o Keycloak e o nginx do usuário não são tocados. Do Keycloak existente, remova o realm do Dokpod pelo
console dele, se quiser. Do PostgreSQL existente, o banco e as roles do Dokpod.
