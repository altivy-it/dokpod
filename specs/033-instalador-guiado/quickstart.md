# Quickstart de validação: Instalador guiado do Dokpod

Cenários executáveis (1 a 9) que provam a feature de ponta a ponta. Os contratos estão em [contracts/](contracts/), e o layout e
o estado em [data-model.md](data-model.md).

## Pré-requisitos

- **Hosts**: dois hosts Linux x86-64 descartáveis (VM ou container de sistema), um com Docker Engine ≥ 27 e outro com
  Podman ≥ 5 rootless, ambos com Compose. Nada mais instalado.
- **Nome público**: um nome que resolva para o host (por exemplo, `dokpod.lab.test` no `/etc/hosts` do host e do
  navegador).
- **Release**: a candidata da release em teste (`vX.Y.Z-rc.N`), com o `install.sh` baixado dos assets e conferido pelo
  SHA-256 e pela atestação (`gh attestation verify`).
- **Cenários 2 e 3**: num terceiro container de rede isolada, PostgreSQL 17.6 ou superior, um nginx com outro site
  configurado e um Keycloak 26.x com um realm `outro`.
- **Canários (cenário 7)**: valores únicos para cada segredo informado, por exemplo `canario033-pg`.

## Cenário 1 - Instalação completa (P1, SC-001, SC-002)

1. `sh install.sh` no host Docker. Escolha "instalação completa", edição CE, CA local para tudo. Confirme o plano.
2. **Esperado**: o plano lista `create` para `dokpod-postgres`, `keycloak-postgres`, `keycloak`, `edge`, `web`, `bff`,
   `api`, `agent`, `realm`, `migrations` e `pki`. O instalador termina com código 0 e mostra a URL, o usuário
   administrador e a senha temporária uma única vez.
3. Abra a URL num navegador que confie na CA local (`secretsDir/pki/ca.crt`). **Esperado**: o login pede a troca de
   senha, e o console mostra o host local conectado com seus containers.
4. Repita no host Podman rootless. **Esperado**: mesmo resultado, e o agente local usa o socket do usuário.
5. Meça o tempo do passo 1 até o login, sem o download das imagens. **Esperado**: no máximo 20 minutos.

## Cenário 2 - Reexecução e retomada (SC-003, SC-004)

1. Registre o estado: `docker ps --format '{{.ID}} {{.Image}}'`, `ls -l --time-style=full-iso` de `installDir` e
   `secretsDir`, `admin migrations status` e o export do realm sem segredos.
2. Rode `sh install.sh --answers <installDir>/answers.yaml --yes`. **Esperado**: plano todo `keep`, código 0, relatório
   com `outcome: converged`, nenhuma diferença no estado do passo 1 e duração de até 2 minutos (SC-003).
3. Numa instalação nova, interrompa com `Ctrl+C` em cada etapa (pki, banco, migrations, realm, ambiente, serviços) e
   retome. **Esperado**: o estado final é igual ao de uma execução sem interrupção, sem banco, realm, ambiente ou
   identidade duplicados.

## Cenário 3 - PostgreSQL e nginx existentes (P2)

1. Responda `postgres.mode: existing-create` com a credencial administrativa por arquivo, e `edge.mode: existing`.
   Repita o cenário nos dois engines (Docker Engine e Podman rootless, SC-002).
2. **Esperado**:
   - nenhum container de PostgreSQL ou nginx foi criado;
   - existem `dokpod_owner` e `dokpod_runtime` sem `SUPERUSER`/`CREATEROLE`;
   - a API conecta com `dokpod_runtime`;
   - a credencial administrativa não está em `installDir` nem em `secretsDir`.
3. Inclua o `dokpod.conf` gerado no nginx existente e recarregue. **Esperado**: o console funciona, e o site anterior do
   nginx continua igual.
4. Responda `edge.upstreamHost` com um IP que não seja de loopback. **Esperado**: código 2 (FR-043).
5. Repita com `existing-prepared`, primeiro com uma role `SUPERUSER` e depois com uma sem `CONNECT`. **Esperado**: as duas
   são recusadas com código 3, e a mensagem diz o privilégio em excesso ou em falta.
6. Com `existing-prepared` sobre um banco que já tem dados, instale. **Esperado**: o instalador exige a confirmação de
   backup antes das migrations (FR-011).

## Cenário 4 - Keycloak existente (P2, SC-006)

1. Exporte o realm `outro` e o `master` (sem segredos) antes. Repita o cenário nos dois engines (SC-002).
2. Responda `keycloak.mode: existing` com `administratorUser` igual a um usuário existente.
3. **Esperado**:
   - o realm `dokpod` foi criado e o usuário indicado entra no console;
   - a reexecução lista `keep` para o realm;
   - os exports de `outro` e `master` são iguais aos de antes.
4. Altere à mão um redirect URI do client `dokpod-bff` e reexecute. **Esperado**: o plano mostra a divergência e a
   corrige após confirmação.

## Cenário 5 - Migrations sob controle do usuário (FR-036 a FR-038, SC-007, SC-010)

1. Instale a versão N. Depois rode `sh install.sh migrations plan --version N+1`. **Esperado**: lista das pendentes com
   os objetos afetados, sem efeito no banco.
2. `sh install.sh migrations export --version N+1 --out /tmp/n1.sql`. Confira o cabeçalho
   ([formato](contracts/migration-script.md)) e aplique com `psql -v ON_ERROR_STOP=1 -f /tmp/n1.sql` num **clone** do
   banco.
3. No banco original, `sh install.sh upgrade --version N+1` com `--manual-migrations`. **Esperado**: pausa com código 10.
   Aplique o arquivo, retome, e o `verify` dá `ok`; os serviços sobem na N+1.
4. Compare o schema do clone com o do original (`pg_dump --schema-only`). **Esperado**: iguais.
5. Rode `upgrade` sem confirmar o backup. **Esperado**: código 4, nada alterado.
6. Tente `upgrade --version N`. **Esperado**: recusa, com referência ao rollback.

## Cenário 6 - Recriação (P3, SC-008)

1. `sh install.sh recreate pki:edge`. **Esperado**: só o certificado da borda e o container `edge` mudam.
2. `sh install.sh recreate --all` e recuse a segunda confirmação. **Esperado**: banco, `dokpod-api-keys`,
   `dokpod-bff-keys` e identidades intactos; contagem de linhas de `dokpod.audit_events` e `dokpod.agent_identities`
   igual à de antes.
3. `sh install.sh recreate pki:ca`. **Esperado**: transição com as raízes antiga e nova, os agentes reemitidos e
   conectados, e no fim só a raiz nova.

## Cenário 7 - Segredos (SC-005)

Depois dos cenários 1 a 6, procure cada canário em:
- saída do instalador e `reports/*.json`;
- `installDir` (exceto `secretsDir`);
- `docker inspect` de todos os containers;
- `ps -ef` capturado durante a execução;
- logs dos containers.

**Esperado**: nenhuma ocorrência.

## Cenário 8 - Host adicional e assinatura (P3, SC-009, FR-005)

1. `sh install.sh host add --name host-b --engine podman`. Copie `packages/<id>.tar` para o host Podman e siga o
   `README.txt`. **Esperado**: o host aparece conectado em até 5 minutos.
2. `sh install.sh host revoke --environment <id> --reason teste`. **Esperado**: o agente perde a sessão, e a auditoria
   registra a revogação.
3. Altere um byte de uma imagem no diretório `--offline` e instale. **Esperado**: código 5 antes de qualquer alteração.

## Cenário 9 - Catálogo SQL sobreposto (FR-039 a FR-041, SC-011)

1. `sh install.sh sql export --version N+1 --out /tmp/sql-n1`. **Esperado**: a árvore `Migrations/Script/` e
   `Sql/Installation/` com `manifest.json` (finalidade, executor, parâmetros e hash de cada arquivo).
2. Altere um arquivo de uma migration pendente (por exemplo, um índice com outro `fillfactor`).
3. `sh install.sh upgrade --version N+1 --sql-dir /tmp/sql-n1 --yes`. **Esperado**: código 4, porque `--yes` não
   confirma sobreposição. O plano lista o arquivo com o hash original e o novo.
4. Repita com `--confirm-sql-overrides <caminho>`. **Esperado**:
   - a migration aplicada com o arquivo alterado;
   - o desvio em `sqlOverrides` do estado, no relatório e na linha `-- override:` do SQL exportado;
   - `acceptedSchemaFingerprint` gravado no estado;
   - o `verify` dá `ok`, com a impressão aceita.
5. Num clone, aplique à mão um arquivo alterado sem passar pelo `--sql-dir`. Repita para cada caso: coluna, índice,
   restrição, função e gatilho. Rode o `verify` sem `--script`. **Esperado**: código 6 em todos, com `schemaDiff`
   apontando o objeto (FR-042, SC-011).
6. Coloque no `--sql-dir` um arquivo sem correspondente na release, ou um `manifest.json` de outra versão.
   **Esperado**: recusa antes de qualquer alteração.
7. Altere no `--sql-dir` o arquivo de uma migration já aplicada e reexecute. **Esperado**: o plano avisa que a alteração
   não tem efeito nesse banco, e ela só entra em `sqlOverrides` com confirmação nomeada.
8. Varredura do SC-011: os testes `NoInlineSqlTests` e `NoInlineSql.Tests.ps1` passam, e nenhum literal SQL aparece em
   `backend/apps/Dokpod.ControlPlane.Api/Admin/` nem em `deploy/installer/src/`.
